namespace VieriNexus.Application;

public enum NavigationRoutePlanKind
{
    Review,
    TravelToStart,
    Playback,
}

public sealed record NavigationRoutePlan(
    NavigationRoutePlanKind Kind,
    Guid RouteId,
    string RouteName,
    uint TerritoryId,
    IReadOnlyList<NavigationRoutePoint> Points,
    bool UseMesh,
    bool UseFlight,
    float Tolerance,
    float LastPointTolerance,
    float TotalDistance,
    bool IsValid,
    bool IsExecutable,
    string Code,
    string Message);

/// <summary>
/// Produces an immutable, provider-neutral route plan. Planning never acquires authority,
/// changes a provider, or moves the character.
/// </summary>
public static class NavigationRoutePlanner
{
    public static NavigationRoutePlan Build(
        NavigationRouteSnapshot route,
        NavigationRoutePlanKind kind,
        uint currentTerritoryId)
    {
        ArgumentNullException.ThrowIfNull(route);

        string? invalid = Validate(route, kind);
        IReadOnlyList<NavigationRoutePoint> points = kind == NavigationRoutePlanKind.TravelToStart &&
                                                     route.Points.Count > 0
            ? [route.Points[0]]
            : route.Points.ToArray();
        float distance = RouteDistance(points);
        if (invalid is not null)
        {
            return new(kind, route.Id, route.Name, route.TerritoryId, points,
                route.UseMesh, route.UseFlight, route.Tolerance, route.LastPointTolerance,
                distance, false, false, "route-plan-invalid", invalid);
        }

        bool review = kind == NavigationRoutePlanKind.Review;
        bool sameTerritory = currentTerritoryId != 0 && route.TerritoryId == currentTerritoryId;
        string message = review
            ? sameTerritory
                ? $"Ready to preview {route.Name} in the current territory."
                : $"{route.Name} can be reviewed here, but its world preview is visible only in territory {route.TerritoryId}."
            : sameTerritory
                ? kind == NavigationRoutePlanKind.TravelToStart
                    ? $"Ready to travel to the first point of {route.Name}."
                    : $"Ready to play all {points.Count} points of {route.Name} in order."
                : $"This route requires territory {route.TerritoryId}; cross-zone execution needs the suite travel provider.";

        return new(kind, route.Id, route.Name, route.TerritoryId, points,
            route.UseMesh, route.UseFlight, route.Tolerance, route.LastPointTolerance,
            distance, true, !review && sameTerritory, review ? "route-preview-ready" :
                sameTerritory ? "route-execution-ready" : "route-territory-mismatch", message);
    }

    private static string? Validate(NavigationRouteSnapshot route, NavigationRoutePlanKind kind)
    {
        if (route.Id == Guid.Empty)
            return "The route has no stable ID.";
        if (route.TerritoryId == 0)
            return "Choose a territory before planning this route.";
        if (route.Points.Count == 0)
            return "Add at least one route point first.";
        if (route.Points.Any(point =>
                !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)))
            return "Every route point must contain finite coordinates.";
        if (!float.IsFinite(route.Tolerance) || route.Tolerance is < 0.1f or > 20f)
            return "Route tolerance must be between 0.1 and 20 yalms.";
        if (!float.IsFinite(route.LastPointTolerance) || route.LastPointTolerance is < 0.1f or > 30f)
            return "Final-point tolerance must be between 0.1 and 30 yalms.";
        return null;
    }

    private static float RouteDistance(IReadOnlyList<NavigationRoutePoint> points)
    {
        double total = 0;
        for (int index = 1; index < points.Count; index++)
        {
            NavigationRoutePoint previous = points[index - 1];
            NavigationRoutePoint current = points[index];
            double x = current.X - previous.X;
            double y = current.Y - previous.Y;
            double z = current.Z - previous.Z;
            total += Math.Sqrt((x * x) + (y * y) + (z * z));
        }
        return (float)total;
    }
}
