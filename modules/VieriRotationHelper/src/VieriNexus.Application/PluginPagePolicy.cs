namespace VieriNexus.Application;

public sealed record PluginPageGroups(
    IReadOnlyList<string> Favorites,
    IReadOnlyList<string> AllOtherPlugins);

public sealed record PluginCommandPanelState(bool IsOpen, string SelectedPluginId);

public static class PluginPagePolicy
{
    public static PluginPageGroups Group(
        IEnumerable<string> visiblePluginIds,
        IReadOnlyCollection<string> favoritePluginIds)
    {
        HashSet<string> favorites = favoritePluginIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] visible = visiblePluginIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(
            visible.Where(favorites.Contains).ToArray(),
            visible.Where(id => !favorites.Contains(id)).ToArray());
    }

    public static PluginCommandPanelState ToggleCommands(
        bool panelOpen,
        string selectedPluginId,
        string clickedPluginId)
    {
        bool closeCurrent = panelOpen &&
                            selectedPluginId.Equals(clickedPluginId, StringComparison.OrdinalIgnoreCase);
        return new(!closeCurrent, clickedPluginId);
    }
}
