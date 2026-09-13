using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Linq.Expressions;

namespace VieriRotationHelper;

internal sealed class WrathLiveProvider(IDalamudPluginInterface pluginInterface)
{
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Func<nint, uint, uint>? originalAdjustment;
    private object? currentHook;
    private long nextRefresh;
    private bool previouslyLoaded;
    private string? optionsJson;
    internal bool IsLoaded => pluginInterface.InstalledPlugins.Any(plugin =>
        plugin.InternalName.Equals("WrathCombo", StringComparison.OrdinalIgnoreCase) && plugin.IsLoaded);

    internal unsafe uint GetAdjusted(uint anchorAction)
    {
        var manager = ActionManager.Instance();
        return manager == null ? anchorAction : manager->GetAdjustedActionId(anchorAction);
    }

    internal unsafe uint GetNativeAdjusted(uint action)
    {
        Refresh();
        var manager = ActionManager.Instance();
        if (manager == null) return action;
        if (IsLoaded)
        {
            // Wrath's own manager field can be zero before its first hook call.
            if (originalAdjustment == null)
                throw new InvalidOperationException("Could not obtain Wrath's native action resolver; suggestions paused rather than guessing.");
            return originalAdjustment((nint)manager, action);
        }
        return manager->GetAdjustedActionId(action);
    }

    internal string? GetOptions(Configuration configuration)
    {
        Refresh();
        if (optionsJson != null)
            configuration.WrathOptionsSnapshot = optionsJson;
        return optionsJson ?? configuration.WrathOptionsSnapshot;
    }

    private void Refresh()
    {
        var loaded = IsLoaded;
        if (loaded != previouslyLoaded)
        {
            originalAdjustment = null;
            currentHook = null;
            nextRefresh = 0;
            previouslyLoaded = loaded;
        }
        if (Environment.TickCount64 < nextRefresh) return;
        nextRefresh = Environment.TickCount64 + 500;
        try
        {
            if (loaded)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().LastOrDefault(a => a.GetName().Name == "WrathCombo");
                var service = assembly?.GetType("WrathCombo.Services.Service");
                var config = service?.GetProperty("Configuration", StaticMembers)?.GetValue(null);
                var replacer = service?.GetProperty("ActionReplacer", StaticMembers)?.GetValue(null);
                var hook = replacer?.GetType().GetField("getActionHook", InstanceMembers)?.GetValue(replacer);
                if (hook != null && !ReferenceEquals(hook, currentHook))
                {
                    var original = hook.GetType().GetProperty("Original", InstanceMembers)?.GetValue(hook) as Delegate;
                    if (original != null)
                    {
                        var manager = Expression.Parameter(typeof(nint));
                        var id = Expression.Parameter(typeof(uint));
                        originalAdjustment = Expression.Lambda<Func<nint, uint, uint>>(
                            Expression.Invoke(Expression.Constant(original), manager, id), manager, id).Compile();
                        currentHook = hook;
                    }
                }
                if (config != null)
                    optionsJson = JsonConvert.SerializeObject(config);
            }
            else
            {
                var path = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "..", "WrathCombo.json");
                if (File.Exists(path))
                {
                    var candidate = File.ReadAllText(path);
                    _ = JObject.Parse(candidate); // retain the old snapshot during a partial file write
                    optionsJson = candidate;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Could not refresh optional Wrath integration.");
        }
    }
}
