using System.Collections;
using System.Reflection;
using System.Text.Json;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal enum DependencyHealth
{
    Missing,
    Disabled,
    Healthy,
}

internal sealed record DependencyStatus(
    DependencyDescriptor Definition,
    DependencyHealth Health,
    string? Version,
    string? ConflictingPackage = null,
    bool CleanupRestartRequired = false)
{
    internal bool IsReady => Health == DependencyHealth.Healthy && !HasPackageConflict && !CleanupRestartRequired;
    internal bool HasPackageConflict => !string.IsNullOrWhiteSpace(ConflictingPackage);
}

internal sealed record PluginPresence(
    bool IsInstalled,
    bool IsLoaded,
    string? Version,
    string? DisplayName = null);

internal sealed class DependencyService(
    IDalamudPluginInterface pluginInterface,
    IPluginLog log)
{
    private readonly object actionLock = new();
    private readonly string? installedPluginRoot = ResolveInstalledPluginRoot(pluginInterface);
    private string? activeActionId;

    internal string? ActionMessage { get; private set; }
    internal bool ActionSucceeded { get; private set; }

    internal IReadOnlyList<DependencyStatus> Snapshot()
    {
        var installed = pluginInterface.InstalledPlugins;
        return NexusDependencyCatalog.All.Select(definition =>
        {
            var plugin = installed
                .Where(candidate => DependencyPackageIdentityPolicy.MatchesInstalled(
                    definition,
                    candidate.InternalName,
                    candidate.Name))
                .OrderByDescending(candidate => candidate.IsLoaded)
                .ThenByDescending(candidate => candidate.Version)
                .FirstOrDefault();
            string? conflictingPackage = definition.Id == "autoduty"
                ? installed.FirstOrDefault(candidate =>
                    DependencyPackageIdentityPolicy.IsBlockingLegacyCollision(definition, candidate.Name))?.Name
                : null;
            bool cleanupRestartRequired = definition.Id == "autoduty" && HasPendingVieriAutoDutyCleanup();
            var health = plugin is null
                ? DependencyHealth.Missing
                : plugin.IsLoaded ? DependencyHealth.Healthy : DependencyHealth.Disabled;
            return new DependencyStatus(
                definition,
                health,
                plugin?.Version?.ToString(),
                conflictingPackage,
                cleanupRestartRequired);
        }).ToArray();
    }

    internal bool RequiredReady => Snapshot().Where(x => x.Definition.Required).All(x => x.IsReady);

    internal PluginPresence FindPlugin(string internalName)
    {
        return FindPlugins(internalName)
                   .OrderByDescending(plugin => plugin.IsLoaded)
                   .ThenByDescending(plugin => plugin.Version)
                   .FirstOrDefault()
            ?? new PluginPresence(false, false, null);
    }

    internal IReadOnlyList<PluginPresence> FindPlugins(string internalName) =>
        pluginInterface.InstalledPlugins
            .Where(candidate => string.Equals(
                candidate.InternalName,
                internalName,
                StringComparison.OrdinalIgnoreCase))
            .Select(plugin => new PluginPresence(
                true,
                plugin.IsLoaded,
                plugin.Version?.ToString(),
                plugin.Name))
            .ToArray();

    internal void OpenInstaller(DependencyStatus dependency)
    {
        var kind = dependency.Health == DependencyHealth.Missing
            ? PluginInstallerOpenKind.AllPlugins
            : PluginInstallerOpenKind.InstalledPlugins;
        pluginInterface.OpenPluginInstallerTo(kind,
            dependency.Definition.InstallerSearch ?? dependency.Definition.DisplayName);
    }

    internal void OpenConflictingPackage(DependencyStatus dependency) =>
        pluginInterface.OpenPluginInstallerTo(
            PluginInstallerOpenKind.InstalledPlugins,
            dependency.ConflictingPackage ?? dependency.Definition.DisplayName);

    internal bool OpenSettings(DependencyDescriptor definition, out string message)
    {
        IExposedPlugin? plugin = pluginInterface.InstalledPlugins
            .Where(candidate => DependencyPackageIdentityPolicy.MatchesInstalled(
                definition,
                candidate.InternalName,
                candidate.Name))
            .OrderByDescending(candidate => candidate.IsLoaded)
            .FirstOrDefault();
        if (plugin is not { IsLoaded: true })
        {
            message = $"{definition.DisplayName} must be installed and enabled before its settings can open.";
            return false;
        }

        try
        {
            if (plugin.HasConfigUi)
                plugin.OpenConfigUi();
            else if (plugin.HasMainUi)
                plugin.OpenMainUi();
            else
            {
                message = $"{definition.DisplayName} does not expose a settings window.";
                return false;
            }
            message = $"Opened {definition.DisplayName} settings.";
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not open settings for {Dependency}", definition.Id);
            message = $"Nexus could not open {definition.DisplayName} settings. Open it from Dalamud Plugins.";
            return false;
        }
    }

    internal bool OpenMain(DependencyDescriptor definition, out string message)
    {
        IExposedPlugin? plugin = pluginInterface.InstalledPlugins
            .Where(candidate => DependencyPackageIdentityPolicy.MatchesInstalled(
                definition,
                candidate.InternalName,
                candidate.Name))
            .OrderByDescending(candidate => candidate.IsLoaded)
            .FirstOrDefault();
        if (plugin is not { IsLoaded: true })
        {
            message = $"{definition.DisplayName} must be installed and enabled before it can open.";
            return false;
        }

        try
        {
            if (plugin.HasMainUi)
                plugin.OpenMainUi();
            else if (plugin.HasConfigUi)
                plugin.OpenConfigUi();
            else
            {
                message = $"{definition.DisplayName} does not expose a window.";
                return false;
            }
            message = $"Opened {definition.DisplayName}.";
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not open main window for {Dependency}", definition.Id);
            message = $"Nexus could not open {definition.DisplayName}. Open it from Dalamud Plugins.";
            return false;
        }
    }

    internal void OpenPluginInstaller() =>
        pluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.AllPlugins);

    internal void OpenRepositorySettings() =>
        pluginInterface.OpenDalamudSettingsTo(SettingsOpenKind.Experimental, "Custom Plugin Repositories");

    internal bool IsWorking(string dependencyId)
    {
        lock (actionLock)
            return string.Equals(activeActionId, dependencyId, StringComparison.Ordinal);
    }

    internal void InstallOrEnable(DependencyStatus dependency)
    {
        if (dependency.CleanupRestartRequired && !dependency.HasPackageConflict)
        {
            ActionSucceeded = false;
            ActionMessage =
                "Restart FFXIV before installing stock AutoDuty. Dalamud still has the removed VieriAutoDuty files queued for startup cleanup and would delete a stock installation made during this session.";
            return;
        }

        if (dependency.HasPackageConflict && dependency.Health == DependencyHealth.Missing)
        {
            ActionSucceeded = false;
            ActionMessage =
                "Uninstall VieriAutoDuty first. It shares AutoDuty's internal package identity, so leaving it installed causes Dalamud to remove stock AutoDuty on restart.";
            OpenConflictingPackage(dependency);
            return;
        }

        lock (actionLock)
        {
            if (activeActionId is not null)
                return;
            activeActionId = dependency.Definition.Id;
            ActionMessage = dependency.Health == DependencyHealth.Missing
                ? $"Installing {dependency.Definition.DisplayName}..."
                : $"Enabling {dependency.Definition.DisplayName}...";
            ActionSucceeded = false;
        }

        _ = CompleteSetupActionAsync(dependency);
    }

    private async Task CompleteSetupActionAsync(DependencyStatus dependency)
    {
        try
        {
            if (dependency.Health == DependencyHealth.Missing)
                await InstallAsync(dependency.Definition);
            else if (dependency.Health == DependencyHealth.Disabled)
                await EnableAsync(dependency.Definition);
            else
                OpenInstaller(dependency);

            ActionSucceeded = true;
            ActionMessage = dependency.Health switch
            {
                DependencyHealth.Missing => $"{dependency.Definition.DisplayName} was installed and enabled.",
                DependencyHealth.Disabled => $"{dependency.Definition.DisplayName} was enabled.",
                _ => $"Opened {dependency.Definition.DisplayName} in Dalamud Plugins.",
            };
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not complete dependency setup for {Dependency}", dependency.Definition.Id);
            ActionSucceeded = false;
            ActionMessage = $"Dalamud could not complete {dependency.Definition.DisplayName} automatically. Use Open in Dalamud on its card to finish there.";
        }
        finally
        {
            lock (actionLock)
                activeActionId = null;
        }
    }

    private async Task InstallAsync(DependencyDescriptor definition)
    {
        object pluginManager = GetDalamudService("Dalamud.Plugin.Internal.PluginManager");
        object? manifest = FindAvailableManifest(pluginManager, definition);
        if (manifest is null && !string.IsNullOrWhiteSpace(definition.RepositoryUrl))
        {
            await AddOrEnableRepositoryAsync(definition.RepositoryUrl, pluginManager);
            manifest = FindAvailableManifest(pluginManager, definition);
        }

        if (manifest is null)
            throw new InvalidOperationException($"{definition.DisplayName} is not present in the current Dalamud plugin catalog.");

        MethodInfo install = pluginManager.GetType().GetMethod(
                                 "InstallPluginAsync",
                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             ?? throw new MissingMethodException(pluginManager.GetType().FullName, "InstallPluginAsync");
        object? result = install.Invoke(pluginManager, [manifest, false, PluginLoadReason.Installer]);
        if (result is Task task)
            await task;
        else
            throw new InvalidOperationException("Dalamud did not return an installation task.");
    }

    private bool HasPendingVieriAutoDutyCleanup()
    {
        if (installedPluginRoot is null)
            return false;

        string autoDutyRoot = Path.Combine(installedPluginRoot, "AutoDuty");
        if (!Directory.Exists(autoDutyRoot))
            return false;

        try
        {
            foreach (string versionDirectory in Directory.EnumerateDirectories(autoDutyRoot))
            {
                string manifestPath = Path.Combine(versionDirectory, "AutoDuty.json");
                if (!File.Exists(manifestPath))
                    continue;
                using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                if (manifest.RootElement.TryGetProperty("Name", out JsonElement name) &&
                    name.ValueKind == JsonValueKind.String &&
                    string.Equals(name.GetString(), "VieriAutoDuty", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "Could not inspect the pending AutoDuty cleanup folder");
        }

        return false;
    }

    private static string? ResolveInstalledPluginRoot(IDalamudPluginInterface pluginInterface)
    {
        string? assemblyDirectory = pluginInterface.AssemblyLocation.DirectoryName;
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
            return null;
        DirectoryInfo? pluginDirectory = Directory.GetParent(assemblyDirectory);
        DirectoryInfo? installedRoot = pluginDirectory?.Parent;
        return installedRoot is not null &&
               string.Equals(installedRoot.Name, "installedPlugins", StringComparison.OrdinalIgnoreCase)
            ? installedRoot.FullName
            : null;
    }

    private async Task EnableAsync(DependencyDescriptor definition)
    {
        object pluginManager = GetDalamudService("Dalamud.Plugin.Internal.PluginManager");
        object localPlugin = EnumerateProperty(pluginManager, "InstalledPlugins")
                                 .Where(plugin => DependencyPackageIdentityPolicy.MatchesInstalled(
                                     definition,
                                     ReadString(plugin, "InternalName"),
                                     ReadString(plugin, "Name")))
                                 .OrderByDescending(plugin => ReadBool(plugin, "IsLoaded"))
                                 .FirstOrDefault()
                             ?? throw new InvalidOperationException($"{definition.DisplayName} is not installed.");

        Guid workingPluginId = ReadProperty<Guid>(localPlugin, "EffectiveWorkingPluginId");
        string internalName = ReadString(localPlugin, "InternalName")
                              ?? throw new InvalidOperationException("The installed plugin has no internal name.");
        object profileManager = GetDalamudService("Dalamud.Plugin.Internal.Profiles.ProfileManager");
        if (ReadBool(localPlugin, "IsWantedByAnyProfile"))
        {
            MethodInfo load = localPlugin.GetType().GetMethod(
                                  "LoadAsync",
                                  BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? throw new MissingMethodException(localPlugin.GetType().FullName, "LoadAsync");
            await RequireTask(load.Invoke(localPlugin,
                [PluginLoadReason.Installer, false, CancellationToken.None]), "plugin activation");
            return;
        }

        object[] activeDeclaringProfiles = EnumerateProperty(profileManager, "Profiles")
            .Where(profile => ReadBool(profile, "IsEnabled") && WantsPlugin(profile, workingPluginId) is not null)
            .ToArray();
        if (activeDeclaringProfiles.Length == 0)
            activeDeclaringProfiles = [ReadProperty<object>(profileManager, "DefaultProfile")];

        foreach (object profile in activeDeclaringProfiles)
        {
            MethodInfo addOrUpdate = profile.GetType().GetMethod(
                                         "AddOrUpdateAsync",
                                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                     ?? throw new MissingMethodException(profile.GetType().FullName, "AddOrUpdateAsync");
            await RequireTask(addOrUpdate.Invoke(profile, [workingPluginId, internalName, true, true]),
                "plugin activation");
        }
    }

    private async Task AddOrEnableRepositoryAsync(string repositoryUrl, object pluginManager)
    {
        object config = GetDalamudService("Dalamud.Configuration.Internal.DalamudConfiguration");
        PropertyInfo listProperty = config.GetType().GetProperty(
                                        "ThirdRepoList",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? throw new MissingMemberException(config.GetType().FullName, "ThirdRepoList");
        if (listProperty.GetValue(config) is not IList repositories)
            throw new InvalidOperationException("Dalamud's custom repository list is unavailable.");

        object? existing = repositories.Cast<object>().FirstOrDefault(repo =>
            string.Equals(ReadString(repo, "Url"), repositoryUrl, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            Type settingsType = typeof(IDalamudPluginInterface).Assembly.GetType(
                                    "Dalamud.Configuration.ThirdPartyRepoSettings",
                                    throwOnError: true)!
                                ?? throw new TypeLoadException("Dalamud repository settings are unavailable.");
            existing = Activator.CreateInstance(settingsType)
                       ?? throw new InvalidOperationException("Dalamud could not create repository settings.");
            WriteProperty(existing, "Url", repositoryUrl);
            WriteProperty(existing, "IsEnabled", true);
            repositories.Add(existing);
        }
        else
        {
            WriteProperty(existing, "IsEnabled", true);
        }

        config.GetType().GetMethod("QueueSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(config, null);
        MethodInfo refresh = pluginManager.GetType().GetMethod(
                                 "SetPluginReposFromConfigAsync",
                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             ?? throw new MissingMethodException(pluginManager.GetType().FullName, "SetPluginReposFromConfigAsync");
        if (refresh.Invoke(pluginManager, [true]) is Task refreshTask)
            await refreshTask;
        else
            throw new InvalidOperationException("Dalamud did not return a repository refresh task.");
    }

    private object? FindAvailableManifest(object pluginManager, DependencyDescriptor definition) =>
        EnumerateProperty(pluginManager, "AvailablePlugins").FirstOrDefault(manifest =>
            DependencyPackageIdentityPolicy.MatchesAvailable(
                definition,
                ReadString(manifest, "InternalName"),
                ReadString(manifest, "Name")));

    private object GetDalamudService(string fullName)
    {
        Assembly dalamud = typeof(IDalamudPluginInterface).Assembly;
        Type serviceType = dalamud.GetType(fullName, throwOnError: true)
                           ?? throw new TypeLoadException($"Dalamud service {fullName} is unavailable.");
        MethodInfo getService = pluginInterface.GetType().GetMethod(
                                    "GetService",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                    null,
                                    [typeof(Type)],
                                    null)
                                ?? throw new MissingMethodException(pluginInterface.GetType().FullName, "GetService");
        return getService.Invoke(pluginInterface, [serviceType])
               ?? throw new InvalidOperationException($"Dalamud service {fullName} is unavailable.");
    }

    private static IEnumerable<object> EnumerateProperty(object owner, string propertyName) =>
        ReadProperty<object>(owner, propertyName) is IEnumerable items
            ? items.Cast<object>()
            : [];

    private static string? ReadString(object owner, string propertyName) =>
        owner.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(owner) as string;

    private static bool ReadBool(object owner, string propertyName) =>
        owner.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(owner) is true;

    private static bool? WantsPlugin(object profile, Guid workingPluginId) =>
        profile.GetType().GetMethod(
                "WantsPlugin",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(profile, [workingPluginId]) as bool?;

    private static async Task RequireTask(object? result, string operation)
    {
        if (result is Task task)
        {
            await task;
            return;
        }

        throw new InvalidOperationException($"Dalamud did not return a {operation} task.");
    }

    private static T ReadProperty<T>(object owner, string propertyName) =>
        owner.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(owner) is T value
            ? value
            : throw new MissingMemberException(owner.GetType().FullName, propertyName);

    private static void WriteProperty(object owner, string propertyName, object value)
    {
        PropertyInfo property = owner.GetType().GetProperty(
                                    propertyName,
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? throw new MissingMemberException(owner.GetType().FullName, propertyName);
        property.SetValue(owner, value);
    }

}
