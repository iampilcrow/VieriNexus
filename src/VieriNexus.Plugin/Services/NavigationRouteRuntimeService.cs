using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class NavigationRouteRuntimeService(
    NavigationRouteExecutionCoordinator execution,
    NavigationRoutePreviewService preview,
    NavigationRouteRecordingService recording,
    NavigationLibraryService library)
{
    internal NavigationRouteExecutionStatus Status => execution.Status;

    internal NavigationRouteRecordingStatus RecordingStatus => recording.Status;

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

    internal NavigationRouteRecordingStatus StartRecording(NavigationRouteSnapshot route, long now) =>
        recording.Start(route, now);

    internal NavigationRouteRecordingStatus StopRecording(string message = "Recording stopped.") =>
        recording.Stop(message);

    internal bool IsRecording(Guid routeId) => recording.IsRecording(routeId);

    internal void Update(long now)
    {
        execution.Update(DateTimeOffset.UtcNow);
        recording.Update(now);
        if (recording.Status is { IsRecording: true, RouteId: { } routeId } &&
            preview.RouteId == routeId &&
            library.Current is { } snapshot &&
            snapshot.Routes.FirstOrDefault(route => route.Id == routeId) is { } route)
        {
            preview.Set(Plan(route, NavigationRoutePlanKind.Review), snapshot.ShowPointNumbers);
        }
    }

    internal void DrawPreview() => preview.Draw();

    internal void ClearPreview(Guid routeId)
    {
        if (preview.RouteId == routeId)
            preview.Set(null);
    }

    internal void ClearPreview() => preview.Set(null);

    internal void RefreshPreview(Guid routeId)
    {
        if (preview.RouteId != routeId || library.Current is not { } snapshot ||
            snapshot.Routes.FirstOrDefault(route => route.Id == routeId) is not { } route)
            return;
        preview.Set(Plan(route, NavigationRoutePlanKind.Review), snapshot.ShowPointNumbers);
    }
}
