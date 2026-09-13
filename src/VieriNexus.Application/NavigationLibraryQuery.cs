namespace VieriNexus.Application;

public static class NavigationLibraryQuery
{
    public static IReadOnlyList<NavigationRouteSnapshot> Filter(
        NavigationLibrarySnapshot snapshot,
        string? searchText)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string query = searchText?.Trim() ?? string.Empty;

        return snapshot.Routes
            .Where(route => query.Length == 0 || Matches(route, query))
            .OrderBy(route => route.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(route => route.Id)
            .ToArray();
    }

    public static NavigationRouteSnapshot? Find(
        NavigationLibrarySnapshot snapshot,
        string nameOrId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(nameOrId))
            return null;

        string value = nameOrId.Trim();
        if (Guid.TryParse(value, out Guid id))
            return snapshot.Routes.FirstOrDefault(route => route.Id == id);

        return snapshot.Routes.FirstOrDefault(route =>
            string.Equals(route.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Matches(NavigationRouteSnapshot route, string query) =>
        route.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        route.Tags.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        route.Notes.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        route.TargetLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        route.TerritoryId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
}
