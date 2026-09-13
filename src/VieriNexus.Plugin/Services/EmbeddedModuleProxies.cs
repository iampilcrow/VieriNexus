using System.Reflection;
using Dalamud.Configuration;
using Dalamud.Interface;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace VieriNexus.Services;

internal sealed class EmbeddedModuleConfigStore(string configFilePath)
{
    private object? configuration;

    internal object? Load(Type type)
    {
        if (configuration is not null)
            return configuration;
        if (!File.Exists(configFilePath))
            return null;
        configuration = JsonConvert.DeserializeObject(File.ReadAllText(configFilePath), type);
        return configuration;
    }

    internal void Save(object value)
    {
        configuration = value;
        Directory.CreateDirectory(Path.GetDirectoryName(configFilePath)!);
        string temporary = configFilePath + ".new";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(value, Formatting.Indented));
        File.Move(temporary, configFilePath, true);
    }
}

public class EmbeddedUiBuilderProxy : DispatchProxy
{
    private IUiBuilder real = null!;
    private readonly List<Action> openConfigHandlers = [];
    private readonly List<Action> openMainHandlers = [];

    internal void Configure(IUiBuilder value) => real = value;
    internal void OpenConfiguration()
    {
        foreach (Action handler in openConfigHandlers.ToArray())
            handler();
        foreach (Action handler in openMainHandlers.ToArray())
            if (!openConfigHandlers.Contains(handler))
                handler();
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            throw new MissingMethodException();
        args ??= [];
        if (targetMethod.Name is "add_OpenConfigUi" or "remove_OpenConfigUi" or
            "add_OpenMainUi" or "remove_OpenMainUi")
        {
            List<Action> handlers = targetMethod.Name.Contains("Config", StringComparison.Ordinal)
                ? openConfigHandlers
                : openMainHandlers;
            Action handler = (Action)args[0]!;
            if (targetMethod.Name.StartsWith("add_", StringComparison.Ordinal))
                handlers.Add(handler);
            else
                handlers.Remove(handler);
            return null;
        }
        try
        {
            return targetMethod.Invoke(real, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}

public class EmbeddedPluginInterfaceProxy : DispatchProxy
{
    private IDalamudPluginInterface real = null!;
    private EmbeddedModuleConfigStore configStore = null!;
    private Dictionary<Type, object> services = null!;
    private Type? configurationType;
    private string internalName = string.Empty;
    private FileInfo assemblyLocation = null!;
    private DirectoryInfo configDirectory = null!;
    private IUiBuilder uiBuilder = null!;

    internal void Configure(
        IDalamudPluginInterface realInterface,
        EmbeddedModuleConfigStore store,
        IReadOnlyDictionary<Type, object> availableServices,
        Type? configType,
        string name,
        string assemblyPath,
        string configPath,
        IUiBuilder moduleUiBuilder)
    {
        real = realInterface;
        configStore = store;
        services = new Dictionary<Type, object>(availableServices);
        configurationType = configType;
        internalName = name;
        assemblyLocation = new FileInfo(assemblyPath);
        configDirectory = new DirectoryInfo(configPath);
        uiBuilder = moduleUiBuilder;
        services[typeof(IDalamudPluginInterface)] = (IDalamudPluginInterface)(object)this;
        services[typeof(IUiBuilder)] = uiBuilder;
    }

    internal bool InjectServices(object target)
    {
        bool injected = false;
        foreach (PropertyInfo property in target.GetType().GetProperties(
                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (property.SetMethod is null || !HasPluginServiceAttribute(property) || !TryService(property.PropertyType, out object? service))
                continue;
            property.SetValue(property.SetMethod.IsStatic ? null : target, service);
            injected = true;
        }
        foreach (FieldInfo field in target.GetType().GetFields(
                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!HasPluginServiceAttribute(field) || !TryService(field.FieldType, out object? service))
                continue;
            field.SetValue(field.IsStatic ? null : target, service);
            injected = true;
        }
        return injected;
    }

    internal bool TryService(Type requested, out object? service)
    {
        if (services.TryGetValue(requested, out service))
            return true;
        service = services.Values.FirstOrDefault(requested.IsInstanceOfType);
        return service is not null;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            throw new MissingMethodException();
        args ??= [];
        switch (targetMethod.Name)
        {
            case "get_InternalName": return internalName;
            case "get_AssemblyLocation": return assemblyLocation;
            case "get_ConfigDirectory": return configDirectory;
            case "get_ConfigFile": return new FileInfo(Path.Combine(configDirectory.FullName, "config.json"));
            case "get_UiBuilder": return uiBuilder;
            case "GetPluginConfigDirectory": return configDirectory.FullName;
            case "GetPluginLocDirectory": return assemblyLocation.DirectoryName!;
            case "GetPlugin": return real.GetPlugin(typeof(Plugin).Assembly);
            case "GetPluginConfig": return configurationType is null ? null : configStore.Load(configurationType);
            case "SavePluginConfig":
                if (args[0] is IPluginConfiguration value)
                    configStore.Save(value);
                return null;
            case "Inject":
            {
                bool result = (bool)(targetMethod.Invoke(real, args) ?? false);
                if (args[0] is object injectTarget)
                    InjectServices(injectTarget);
                return result;
            }
        }

        if (targetMethod.Name == "Create" && targetMethod.IsGenericMethod)
        {
            object created = targetMethod.Invoke(real, args)
                             ?? throw new InvalidOperationException("Dalamud could not create the embedded service object.");
            InjectServices(created);
            return created;
        }

        try
        {
            return targetMethod.Invoke(real, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static bool HasPluginServiceAttribute(MemberInfo member) => member.CustomAttributes.Any(attribute =>
        attribute.AttributeType.FullName == "Dalamud.IoC.PluginServiceAttribute");
}
