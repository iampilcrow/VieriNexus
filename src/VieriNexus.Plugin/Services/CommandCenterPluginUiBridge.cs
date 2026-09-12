using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace VieriNexus.Services;

/// <summary>
/// Closes only a target plugin's visible main/configuration window. Persistent overlays,
/// progress views, HUDs, and status controls are deliberately excluded.
/// </summary>
internal sealed class CommandCenterPluginUiBridge(IPluginLog log)
{
    internal bool TryClose(CommandCenterEntry plugin)
    {
        try
        {
            IWindow[] windows = FindWindows(plugin)
                .Where(window => window.IsOpen)
                .Distinct(ReferenceComparer<IWindow>.Instance)
                .ToArray();
            if (windows.Length == 0)
                return false;
            IWindow[] settings = windows.Where(IsSettingsWindow).ToArray();
            IWindow[] named = windows.Where(window => IsMainWindow(plugin, window)).ToArray();
            IWindow[] candidates = settings.Length > 0
                ? settings
                : named.Length > 0
                    ? named
                    : windows.Where(window => !LooksLikePersistentControl(window)).Take(1).ToArray();
            if (candidates.Length == 0)
                return false;
            foreach (IWindow window in candidates)
                window.IsOpen = false;
            return true;
        }
        catch (Exception exception)
        {
            log.Debug(exception, "Nexus could not inspect {PluginName}'s windows for a close operation.", plugin.Name);
            return false;
        }
    }

    private static IEnumerable<IWindow> FindWindows(CommandCenterEntry plugin)
    {
        Assembly? owner = plugin.OwnerAssembly ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            Normalize(assembly.GetName().Name ?? string.Empty) == Normalize(plugin.AssemblyName) ||
            Normalize(assembly.GetName().Name ?? string.Empty) == Normalize(plugin.Id));
        if (owner is null)
            yield break;
        AssemblyLoadContext? context = AssemblyLoadContext.GetLoadContext(owner);
        Assembly[] assemblies = context?.Assemblies.ToArray() ?? [owner];
        HashSet<object> seen = new(ReferenceComparer<object>.Instance);

        if (plugin.CommandTarget is not null)
            foreach (IWindow window in WindowsFromValue(plugin.CommandTarget, context, seen, 0))
                yield return window;

        foreach (Assembly assembly in assemblies.Where(assembly => assembly == owner ||
                     assembly.GetName().Name?.Equals("ECommons", StringComparison.OrdinalIgnoreCase) == true))
        {
            foreach (Type type in SafeTypes(assembly))
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!MayContainWindows(field.FieldType, field.Name, context))
                        continue;
                    object? value;
                    try { value = field.GetValue(null); }
                    catch { continue; }
                    foreach (IWindow window in WindowsFromValue(value, context, seen, 0))
                        yield return window;
                }
            }
        }
    }

    private static IEnumerable<IWindow> WindowsFromValue(object? value, AssemblyLoadContext? context, HashSet<object> seen, int depth)
    {
        if (value is null || depth > 3 || !seen.Add(value))
            yield break;
        if (value is IWindow window)
        {
            yield return window;
            yield break;
        }
        if (value is IWindowSystem system)
        {
            foreach (IWindow item in system.Windows)
                yield return item;
            yield break;
        }
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string or Delegate)
            yield break;
        if (context is not null && AssemblyLoadContext.GetLoadContext(type.Assembly) != context)
            yield break;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!MayContainWindows(field.FieldType, field.Name, context))
                continue;
            object? child;
            try { child = field.GetValue(value); }
            catch { continue; }
            foreach (IWindow item in WindowsFromValue(child, context, seen, depth + 1))
                yield return item;
        }
    }

    private static bool MayContainWindows(Type type, string fieldName, AssemblyLoadContext? context)
    {
        if (typeof(IWindow).IsAssignableFrom(type) || typeof(IWindowSystem).IsAssignableFrom(type))
            return true;
        string normalized = Normalize(fieldName);
        if (normalized.Contains("window") || normalized.Contains("gui") || normalized.Contains("ui") ||
            normalized is "p" or "instance" or "plugin" || normalized.Contains("this"))
            return context is null || AssemblyLoadContext.GetLoadContext(type.Assembly) == context;
        return false;
    }

    private static bool IsMainWindow(CommandCenterEntry plugin, IWindow window)
    {
        string name = Normalize(window.WindowName.Split("###", StringSplitOptions.None)[0]);
        if (name.Length == 0 || LooksLikePersistentControl(window))
            return false;
        string pluginName = Normalize(plugin.Name);
        string pluginId = Normalize(plugin.Id);
        return name.Contains(pluginName, StringComparison.Ordinal) ||
               name.Contains(pluginId, StringComparison.Ordinal) ||
               name.Contains("config", StringComparison.Ordinal) ||
               name.Contains("setting", StringComparison.Ordinal) ||
               name.Contains("main", StringComparison.Ordinal);
    }

    private static bool IsSettingsWindow(IWindow window)
    {
        if (LooksLikePersistentControl(window))
            return false;
        string identity = Normalize(window.WindowName + window.GetType().Name);
        return identity.Contains("config", StringComparison.Ordinal) ||
               identity.Contains("setting", StringComparison.Ordinal) ||
               identity.Contains("preference", StringComparison.Ordinal);
    }

    private static bool LooksLikePersistentControl(IWindow window)
    {
        string identity = Normalize(window.WindowName + window.GetType().Name);
        return identity.Contains("overlay", StringComparison.Ordinal) ||
               identity.Contains("progress", StringComparison.Ordinal) ||
               identity.Contains("hud", StringComparison.Ordinal) ||
               identity.Contains("searchhelper", StringComparison.Ordinal) ||
               identity.Contains("rotationwindow", StringComparison.Ordinal) ||
               identity.Contains("statuswindow", StringComparison.Ordinal) ||
               identity.Contains("switchwindow", StringComparison.Ordinal);
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException exception) { return exception.Types.OfType<Type>(); }
        catch { return []; }
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
