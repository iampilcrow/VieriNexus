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

internal sealed record PluginPresence(bool IsInstalled, bool IsLoaded, string? Version);

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
        var plugin = pluginInterface.InstalledPlugins.FirstOrDefault(candidate =>
            string.Equals(candidate.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
        return new(plugin is not null, plugin?.IsLoaded == true, plugin?.Version?.ToString());
    }

    internal void OpenInstaller(DependencyStatus dependency)
    {
        var kind = dependency.Health == DependencyHealth.Missing
            ? PluginInstallerOpenKind.AllPlugins
            : PluginInstallerOpenKind.InstalledPlugins;
        pluginInterface.OpenPluginInstallerTo(kind,
            dependency.Definition.InstallerSearch ?? dependency.Definition.DisplayName);
    }
}
