namespace VieriNexus.Application;

public enum NavigationRouteDispatchKind
{
    Unavailable,
    Local,
    SuiteTravel,
}

/// <summary>
/// Keeps route playback consistent across territory changes. When suite travel is available it
/// owns the complete trip, including same-territory restarts; raw local vnavmesh is only fallback.
/// </summary>
public static class NavigationRouteDispatchPolicy
{
    public static NavigationRouteDispatchKind Select(NavigationRoutePlan plan, bool suiteTravelAvailable)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid || plan.Kind == NavigationRoutePlanKind.Review)
            return NavigationRouteDispatchKind.Unavailable;
        if (suiteTravelAvailable)
            return NavigationRouteDispatchKind.SuiteTravel;
        return plan.IsExecutable
            ? NavigationRouteDispatchKind.Local
            : NavigationRouteDispatchKind.Unavailable;
    }
}
