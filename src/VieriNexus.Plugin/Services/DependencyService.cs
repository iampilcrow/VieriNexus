using Dalamud.Interface;
using Dalamud.Plugin;

namespace VieriNexus.Services;

internal enum DependencyHealth
{
    Missing,
    Disabled,
    Healthy,
}

internal sealed record DependencyDefinition(
    string Id,
    string DisplayName,
    string Capability,
    string Description,
    bool Required,
    string[] InternalNames,
    string? RepositoryUrl = null);

internal sealed record DependencyStatus(
    DependencyDefinition Definition,
    DependencyHealth Health,
    string? Version)
{
    internal bool IsReady => Health == DependencyHealth.Healthy;
}

internal sealed class DependencyService(IDalamudPluginInterface pluginInterface)
{
    private static readonly DependencyDefinition[] Definitions =
    [
        new("questionable", "Questionable", "Progression", "Quest execution and supported quest routes. The current VieriCodex migration provider also satisfies this requirement.", true, ["Questionable", "VieriCodex"]),
        new("bossmod", "Boss Mod", "Duties and Combat", "Encounter intelligence, movement, and duty support.", true, ["BossMod"]),
        new("vnavmesh", "vnavmesh", "Navigation", "Navigation meshes and safe world movement.", true, ["vnavmesh"], "https://puni.sh/api/repository/veyn"),
        new("lifestream", "Lifestream", "Travel", "Aetheryte, world, and local travel services.", true, ["Lifestream"], "https://love.puni.sh/ment.json"),
        new("marketbuddy", "Marketbuddy", "Market", "Applies configured retainer listing price changes.", true, ["Marketbuddy"], "https://love.puni.sh/ment.json"),
        new("allagan-market", "Allagan Market", "Market", "Market ownership, pricing, and undercut intelligence.", true, ["AllaganMarket"]),
    ];

    internal IReadOnlyList<DependencyStatus> Snapshot()
    {
        var installed = pluginInterface.InstalledPlugins;
        return Definitions.Select(definition =>
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

    internal void OpenInstaller(DependencyStatus dependency)
    {
        var kind = dependency.Health == DependencyHealth.Missing
            ? PluginInstallerOpenKind.AllPlugins
            : PluginInstallerOpenKind.InstalledPlugins;
        pluginInterface.OpenPluginInstallerTo(kind, dependency.Definition.DisplayName);
    }
}
