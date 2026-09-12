using Dalamud.Plugin;

namespace VieriNexus.Services;

internal sealed record LegacySource(
    string Id,
    string DisplayName,
    string Destination,
    bool Found,
    IReadOnlyList<string> ExistingPaths,
    bool ContainsProtectedValues = false);

internal sealed class LegacyConfigurationInventory
{
    private static readonly (string Id, string Name, string Destination, string[] Candidates)[] Sources =
    [
        ("autoduty", "VieriAutoDuty", "Duties, Gear, Inventory", ["AutoDuty.json", "AutoDuty", "VieriAutoDuty.json", "VieriAutoDuty"]),
        ("automarket", "VieriAutoMarket", "Market", ["VieriAutoMarket.json", "VieriAutoMarket"]),
        ("avarice", "VieriAvarice", "Positional Guidance", ["VieriAvarice.json", "VieriAvarice"]),
        ("codex", "VieriCodex", "Progression and Questing", ["VieriCodex.json", "VieriCodex"]),
        ("deck", "VieriDeck", "Plugins", ["VieriDeck.json", "VieriDeck"]),
        ("delvui", "VieriDelvUI", "Custom UI", ["VieriDelvUI.json", "VieriDelvUI"]),
        ("link", "VieriLink", "Communications", ["VieriLink.json", "VieriLink"]),
        ("rotation", "VieriRotationHelper", "Rotation Engine", ["VieriRotationHelper.json", "VieriRotationHelper"]),
        ("navplotter", "VieriNavPlotter", "Routes and Navigation", ["VieriNavPlotter.json", "VieriNavPlotter"]),
    ];

    private readonly string configRoot;

    internal LegacyConfigurationInventory(IDalamudPluginInterface pluginInterface)
    {
        configRoot = Directory.GetParent(pluginInterface.GetPluginConfigDirectory())?.FullName
                     ?? pluginInterface.GetPluginConfigDirectory();
    }

    internal IReadOnlyList<LegacySource> Scan() => Sources.Select(source =>
    {
        var paths = source.Candidates
            .Select(candidate => Path.Combine(configRoot, candidate))
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToArray();
        return new LegacySource(source.Id, source.Name, source.Destination, paths.Length > 0, paths,
            source.Id.Equals("link", StringComparison.OrdinalIgnoreCase));
    }).ToArray();
}
