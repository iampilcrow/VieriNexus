using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace VieriNexus.Services;

internal enum EmbeddedModuleHealth
{
    Preparing,
    WaitingForPredecessor,
    Disabled,
    Running,
    Failed,
}

internal sealed record EmbeddedModuleStatus(
    string Id,
    string Page,
    string DisplayName,
    string Description,
    EmbeddedModuleHealth Health,
    string Message,
    bool HasLegacySettings,
    bool SettingsPrepared,
    bool ContainsProtectedSettings,
    bool CanOpenSettings);

internal sealed record EmbeddedModuleDefinition(
    string Id,
    string Page,
    string DisplayName,
    string Description,
    string ArchiveName,
    string AssemblyName,
    string PluginType,
    string? ConfigurationType,
    string LegacyInternalName,
    bool ContainsProtectedSettings = false);

internal sealed class EmbeddedModuleManager : IDisposable
{
    private static readonly EmbeddedModuleDefinition[] Definitions =
    [
        new("rotation", "Combat", "Nexus Rotation", "VieriRotationHelper suggestions, hotkeys, and the cleaned-up switch overlay follow the stock Wrath Combo engine live.",
            "rotation.zip", "VieriRotationHelper.dll", "VieriRotationHelper.Plugin", "VieriRotationHelper.Configuration", "VieriRotationHelper"),
        new("avarice", "Combat", "Nexus Positionals", "VieriAvarice positional forecast, profiles, encounter feedback, and native overlay behavior.",
            "avarice.zip", "VieriAvarice.dll", "Avarice.Avarice", "Avarice.Configuration.Config", "VieriAvarice"),
        new("delvui", "Custom UI", "Nexus HUD", "The complete customized VieriDelvUI HUD, profiles, highlighting, markers, party roles, and ready checks.",
            "delvui.zip", "VieriDelvUI.dll", "DelvUI.Plugin", null, "VieriDelvUI"),
        new("automarket", "Market", "Nexus Market", "VieriAutoMarket owned-retainer matching, guarded repricing, pacing, confirmation, and verification.",
            "automarket.zip", "VieriAutoMarket.dll", "VieriAutoMarket.Plugin", "VieriAutoMarket.Configuration", "VieriAutoMarket"),
        new("link", "Communications", "Nexus Link", "The encrypted Discord status, notification, recovery, and authorized Nexus command gateway.",
            "link.zip", "VieriLink.dll", "VieriLink.Plugin", "VieriLink.Configuration", "VieriLink", true),
    ];

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly LegacyConfigurationInventory inventory;
    private readonly Configuration configuration;
    private readonly IReadOnlyDictionary<Type, object> services;
    private readonly string packageRoot;
    private readonly string dataRoot;
    private readonly Dictionary<string, LoadedModule> loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> failures = new(StringComparer.OrdinalIgnoreCase);
    private long nextUpdate;

    internal EmbeddedModuleManager(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        LegacyConfigurationInventory inventory,
        Configuration configuration,
        IReadOnlyDictionary<Type, object> services)
    {
        this.pluginInterface = pluginInterface;
        this.log = log;
        this.inventory = inventory;
        this.configuration = configuration;
        this.services = services;
        packageRoot = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "EmbeddedModules");
        dataRoot = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "NexusData", "EmbeddedModules");
    }

    internal IReadOnlyList<EmbeddedModuleStatus> Statuses => Definitions.Select(Status).ToArray();

    internal void Update(long now)
    {
        if (now < nextUpdate)
            return;
        nextUpdate = now + 2000;
        foreach (EmbeddedModuleDefinition definition in Definitions)
            Reconcile(definition);
    }

    internal void SetEnabled(string id, bool enabled)
    {
        configuration.SetEmbeddedModuleEnabled(id, enabled);
        configuration.Save();
        failures.Remove(id);
        if (!enabled)
            Unload(id);
        nextUpdate = 0;
    }

    internal void Retry(string id)
    {
        failures.Remove(id);
        nextUpdate = 0;
    }

    internal bool OpenSettings(string id)
    {
        if (!loaded.TryGetValue(id, out LoadedModule? module))
            return false;
        module.UiProxy.OpenConfiguration();
        return true;
    }

    public void Dispose()
    {
        foreach (string id in loaded.Keys.ToArray())
            Unload(id);
    }

    private EmbeddedModuleStatus Status(EmbeddedModuleDefinition definition)
    {
        LegacySource source = inventory.Find(definition.Id);
        bool prepared = File.Exists(MarkerPath(definition));
        bool predecessorLoaded = IsPredecessorLoaded(definition);
        if (!configuration.IsEmbeddedModuleEnabled(definition.Id))
            return Build(EmbeddedModuleHealth.Disabled, "Disabled in Nexus.", false);
        if (loaded.ContainsKey(definition.Id))
            return Build(EmbeddedModuleHealth.Running, "Running inside VieriNexus; the separate predecessor is no longer needed.", true);
        if (predecessorLoaded)
            return Build(EmbeddedModuleHealth.WaitingForPredecessor,
                $"Settings are protected. Disable {definition.LegacyInternalName}; Nexus will take over automatically without a reload.", false);
        if (failures.TryGetValue(definition.Id, out string? failure))
            return Build(EmbeddedModuleHealth.Failed, failure, false);
        return Build(EmbeddedModuleHealth.Preparing, "Preparing the isolated Nexus-owned runtime.", false);

        EmbeddedModuleStatus Build(EmbeddedModuleHealth health, string message, bool canOpen) => new(
            definition.Id, definition.Page, definition.DisplayName, definition.Description, health, message,
            source.Found, prepared, definition.ContainsProtectedSettings, canOpen);
    }

    private void Reconcile(EmbeddedModuleDefinition definition)
    {
        if (!configuration.IsEmbeddedModuleEnabled(definition.Id) || IsPredecessorLoaded(definition))
        {
            Unload(definition.Id);
            if (IsPredecessorLoaded(definition))
                Prepare(definition);
            return;
        }
        if (loaded.ContainsKey(definition.Id) || failures.ContainsKey(definition.Id))
            return;
        try
        {
            Prepare(definition);
            Load(definition);
        }
        catch (Exception ex)
        {
            string message = RootMessage(ex);
            failures[definition.Id] = $"Stopped safely: {message}";
            log.Error(ex, "Nexus embedded module {Module} failed to start", definition.DisplayName);
        }
    }

    private void Prepare(EmbeddedModuleDefinition definition)
    {
        Directory.CreateDirectory(ModuleConfigDirectory(definition));
        if (File.Exists(MarkerPath(definition)))
            return;

        LegacySource source = inventory.Find(definition.Id);
        string backupDirectory = Path.Combine(dataRoot, "Backups", definition.Id,
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (source.Found)
            Directory.CreateDirectory(backupDirectory);
        foreach (string path in source.ExistingPaths)
        {
            if (File.Exists(path))
            {
                if (definition.ContainsProtectedSettings)
                    VerifyProtectedLinkConfiguration(path);
                File.Copy(path, Path.Combine(backupDirectory, Path.GetFileName(path)), false);
                string target = Path.Combine(ModuleConfigDirectory(definition), "config.json");
                if (!File.Exists(target))
                    File.Copy(path, target, false);
            }
            else if (Directory.Exists(path))
            {
                string backup = Path.Combine(backupDirectory, Path.GetFileName(path));
                CopyDirectory(path, backup, overwrite: false);
                CopyDirectory(path, ModuleConfigDirectory(definition), overwrite: false);
            }
        }

        string marker = JsonSerializer.Serialize(new
        {
            definition.Id,
            PreparedAtUtc = DateTimeOffset.UtcNow,
            SourceFound = source.Found,
            SourceFiles = source.ExistingPaths.Count,
            ProtectedRoundTripVerified = definition.ContainsProtectedSettings && source.Found,
        });
        WriteAtomic(MarkerPath(definition), marker);
    }

    private void Load(EmbeddedModuleDefinition definition)
    {
        string archive = Path.Combine(packageRoot, definition.ArchiveName);
        if (!File.Exists(archive))
            throw new FileNotFoundException("The embedded runtime archive is missing from the Nexus package.", archive);
        string runtimeDirectory = ExtractRuntime(definition, archive);
        string assemblyPath = Path.Combine(runtimeDirectory, definition.AssemblyName);
        var context = new EmbeddedModuleLoadContext(assemblyPath);
        try
        {
            Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
            Type pluginType = assembly.GetType(definition.PluginType, throwOnError: true)!;
            Type? configurationType = definition.ConfigurationType is null
                ? null
                : assembly.GetType(definition.ConfigurationType, throwOnError: true);

            IUiBuilder uiBuilder = DispatchProxy.Create<IUiBuilder, EmbeddedUiBuilderProxy>();
            var uiProxy = (EmbeddedUiBuilderProxy)(object)uiBuilder;
            uiProxy.Configure(pluginInterface.UiBuilder);
            IDalamudPluginInterface pi = DispatchProxy.Create<IDalamudPluginInterface, EmbeddedPluginInterfaceProxy>();
            var piProxy = (EmbeddedPluginInterfaceProxy)(object)pi;
            string configDirectory = ModuleConfigDirectory(definition);
            piProxy.Configure(pluginInterface,
                new EmbeddedModuleConfigStore(Path.Combine(configDirectory, "config.json")),
                services, configurationType, definition.LegacyInternalName, assemblyPath, configDirectory, uiBuilder);

            InjectStaticServices(pluginType, piProxy);
            object plugin = CreatePlugin(pluginType, piProxy, pi);
            loaded[definition.Id] = new LoadedModule(plugin, context, uiProxy);
            log.Information("Nexus embedded module {Module} is running", definition.DisplayName);
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    private object CreatePlugin(Type pluginType, EmbeddedPluginInterfaceProxy piProxy, IDalamudPluginInterface pi)
    {
        foreach (ConstructorInfo constructor in pluginType.GetConstructors().OrderByDescending(item => item.GetParameters().Length))
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            object?[] values = new object?[parameters.Length];
            bool valid = true;
            for (int index = 0; index < parameters.Length; index++)
            {
                Type requested = parameters[index].ParameterType;
                if (requested == typeof(IDalamudPluginInterface))
                    values[index] = pi;
                else if (piProxy.TryService(requested, out object? value))
                    values[index] = value;
                else if (parameters[index].HasDefaultValue)
                    values[index] = parameters[index].DefaultValue;
                else
                    valid = false;
            }
            if (valid)
                return constructor.Invoke(values);
        }
        throw new InvalidOperationException($"No supported constructor was found for {pluginType.FullName}.");
    }

    private static void InjectStaticServices(Type pluginType, EmbeddedPluginInterfaceProxy proxy)
    {
        object uninitialized = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(pluginType);
        proxy.InjectServices(uninitialized);
    }

    private string ExtractRuntime(EmbeddedModuleDefinition definition, string archive)
    {
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)))[..16];
        string destination = Path.Combine(dataRoot, "Runtime", definition.Id, hash);
        string ready = Path.Combine(destination, ".ready");
        if (File.Exists(ready))
            return destination;
        Directory.CreateDirectory(destination);
        string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using ZipArchive zip = ZipFile.OpenRead(archive);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("An embedded runtime entry escaped its isolated directory.");
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
        File.WriteAllText(ready, hash);
        return destination;
    }

    private bool IsPredecessorLoaded(EmbeddedModuleDefinition definition) => pluginInterface.InstalledPlugins.Any(plugin =>
        plugin.InternalName.Equals(definition.LegacyInternalName, StringComparison.OrdinalIgnoreCase) && plugin.IsLoaded);

    private void Unload(string id)
    {
        if (!loaded.Remove(id, out LoadedModule? module))
            return;
        try
        {
            if (module.Plugin is IDisposable disposable)
                disposable.Dispose();
            else
                module.Plugin.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public)?.Invoke(module.Plugin, null);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Nexus embedded module {Module} did not dispose cleanly", id);
        }
        module.Context.Unload();
    }

    private string ModuleConfigDirectory(EmbeddedModuleDefinition definition) => Path.Combine(dataRoot, "Config", definition.Id);
    private string MarkerPath(EmbeddedModuleDefinition definition) => Path.Combine(ModuleConfigDirectory(definition), ".prepared.json");

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(file));
            if (overwrite || !File.Exists(target))
                File.Copy(file, target, overwrite);
        }
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), overwrite);
    }

    private static void VerifyProtectedLinkConfiguration(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("EncryptedBotToken", out JsonElement value) ||
            value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            return;
        byte[] entropy = Encoding.UTF8.GetBytes("VieriLink.Discord.BotToken.v1");
        byte[] protectedBytes = Convert.FromBase64String(value.GetString()!);
        byte[] plain = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
        try
        {
            byte[] roundTrip = ProtectedData.Protect(plain, entropy, DataProtectionScope.CurrentUser);
            byte[] verified = ProtectedData.Unprotect(roundTrip, entropy, DataProtectionScope.CurrentUser);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(plain, verified))
                    throw new CryptographicException("The protected VieriLink token did not pass the same-account round-trip check.");
            }
            finally { CryptographicOperations.ZeroMemory(verified); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".new";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, true);
    }

    private static string RootMessage(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: not null } || exception.InnerException is not null)
            exception = exception.InnerException!;
        return exception.Message;
    }

    private sealed record LoadedModule(object Plugin, AssemblyLoadContext Context, EmbeddedUiBuilderProxy UiProxy);
}
