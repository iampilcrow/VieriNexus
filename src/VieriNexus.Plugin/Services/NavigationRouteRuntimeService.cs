using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class NavigationRouteRuntimeService(
    NavigationRouteExecutionCoordinator execution,
    NavigationRoutePreviewService preview)
{
    internal NavigationRouteExecutionStatus Status => execution.Status;

    internal NavigationRoutePlan Plan(NavigationRouteSnapshot route, NavigationRoutePlanKind kind) =>
        NavigationRoutePlanner.Build(route, kind, Plugin.ClientState.TerritoryType);

    internal bool IsPreviewing(Guid routeId) => preview.RouteId == routeId;

    internal NavigationRoutePlan TogglePreview(NavigationRouteSnapshot route, bool showPointNumbers)
    {
        NavigationRoutePlan plan = Plan(route, NavigationRoutePlanKind.Review);
        preview.Set(preview.RouteId == route.Id ? null : plan, showPointNumbers);
        return plan;
    }

    internal NavigationRouteExecutionStatus Start(
        NavigationRouteSnapshot route,
        NavigationRoutePlanKind kind) => execution.Start(Plan(route, kind), DateTimeOffset.UtcNow);

    internal NavigationRouteExecutionStatus Stop() => execution.Stop(DateTimeOffset.UtcNow);

    internal void Update() => execution.Update(DateTimeOffset.UtcNow);

    internal void DrawPreview() => preview.Draw();

    internal void ClearPreview(Guid routeId)
    {
        if (preview.RouteId == routeId)
            preview.Set(null);
    }
}
