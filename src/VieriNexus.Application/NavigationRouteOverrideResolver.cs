namespace VieriNexus.Application;

public sealed record NavigationRouteOverrideResolution(
    bool Success,
    string Code,
    string Message,
    NavigationRouteSnapshot? Route = null);

public sealed record NavigationRouteOverrideUpdate(
    bool Success,
    string Code,
    string Message,
    NavigationLibrarySnapshot? Library = null);

public static class NavigationRouteOverrideResolver
{
    public static NavigationRouteOverrideUpdate SetExclusive(
        NavigationLibrarySnapshot? library,
        Guid routeId,
        bool enabled,
        DateTime updatedAtUtc)
    {
        NavigationRouteSnapshot? route = library?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (library is null || route is null)
            return UpdateFailed("route-not-found", "The selected Nexus route no longer exists.");
        if (route.BindingKind != 1 || route.TargetDataId == 0)
            return UpdateFailed("route-binding-required", "Assign a valid gear-vendor target before enabling an override.");
        if (enabled && route.Points.Count == 0)
            return UpdateFailed("route-points-required", "Add at least one verified movement point before enabling an override.");

        NavigationRouteSnapshot[] routes = library.Routes.Select(item =>
        {
            bool sameTarget = item.Id != route.Id && item.BindingKind == 1 &&
                              item.TerritoryId == route.TerritoryId &&
                              item.TargetDataId == route.TargetDataId;
            if (item.Id == route.Id)
                return item with { OverrideEnabled = enabled, UpdatedAtUtc = updatedAtUtc };
            return enabled && sameTarget && item.OverrideEnabled
                ? item with { OverrideEnabled = false, UpdatedAtUtc = updatedAtUtc }
                : item;
        }).ToArray();

        return new NavigationRouteOverrideUpdate(
            true,
            enabled ? "route-override-enabled" : "route-override-disabled",
            enabled
                ? $"Enabled {route.Name} as the sole gear-vendor override for target {route.TargetDataId}."
                : $"Disabled the gear-vendor override for {route.Name}.",
            library with { Routes = routes, SelectedRouteId = route.Id });
    }

    public static NavigationRouteOverrideResolution ResolveGearVendor(
        NavigationLibrarySnapshot? library,
        uint territoryId,
        uint targetDataId)
    {
        if (library is null)
            return Failed("route-library-unavailable", "The Nexus working route library is unavailable.");
        if (territoryId == 0 || targetDataId == 0)
            return Failed("route-target-invalid", "A territory and gear-vendor target are required.");

        NavigationRouteSnapshot[] matches = library.Routes.Where(route =>
            route.OverrideEnabled &&
            route.BindingKind == 1 &&
            route.TerritoryId == territoryId &&
            route.TargetDataId == targetDataId).ToArray();
        if (matches.Length == 0)
            return Failed("route-override-not-found", "No enabled Nexus gear-vendor override matches this target.");
        if (matches.Length > 1)
            return Failed("route-override-ambiguous", "More than one enabled Nexus gear-vendor override matches this target.");

        NavigationRouteSnapshot route = matches[0];
        NavigationRoutePlan plan = NavigationRoutePlanner.Build(route, NavigationRoutePlanKind.TravelToStart,
            route.TerritoryId);
        if (!plan.IsValid)
            return Failed("route-override-invalid", plan.Message);
        return new(true, "route-override-resolved", $"Resolved {route.Name}.", route);
    }

    private static NavigationRouteOverrideResolution Failed(string code, string message) =>
        new(false, code, message);

    private static NavigationRouteOverrideUpdate UpdateFailed(string code, string message) =>
        new(false, code, message);
}
