using Dalamud.Interface;
using Dalamud.Plugin;
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
    string? Version)
{
    internal bool IsReady => Health == DependencyHealth.Healthy;
}

internal sealed record PluginPresence(
    bool IsInstalled,
    bool IsLoaded,
    string? Version,
    string? DisplayName = null);

internal sealed class DependencyService(IDalamudPluginInterface pluginInterface)
{
    internal IReadOnlyList<DependencyStatus> Snapshot()
    {
        var installed = pluginInterface.InstalledPlugins;
        return NexusDependencyCatalog.All.Select(definition =>
        {
            var plugin = installed.FirstOrDefault(candidate => definition.InternalNames.Any(name =>
                string.Equals(candidate.InternalName, name, StringComparison.OrdinalIgnoreCase)));
            var health = plugin is null
                ? DependencyHealth.Missing
                : plugin.IsLoaded ? DependencyHealth.Healthy : DependencyHealth.Disabled;
            return new DependencyStatus(definition, health, plugin?.Version?.ToString());
        }).ToArray();
    }

    internal bool RequiredReady => Snapshot().Where(x => x.Definition.Required).All(x => x.IsReady);

    internal PluginPresence FindPlugin(string internalName)
    {
        return FindPlugins(internalName).FirstOrDefault()
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
}
