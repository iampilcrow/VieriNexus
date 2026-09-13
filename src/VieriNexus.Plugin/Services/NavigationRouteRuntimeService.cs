using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class NavigationRouteRuntimeService(
    NavigationRouteExecutionCoordinator execution,
    NavigationSuiteTravelCoordinator suiteTravel,
    NavigationRoutePreviewService preview,
    NavigationLivePathService livePath,
    NavigationRouteRecordingService recording,
    NavigationLibraryService library,
    NavigationRecoveryService recovery)
{
    internal NavigationRouteExecutionStatus Status => execution.Status.IsActive
        ? execution.Status
        : suiteTravel.Status.State != NavigationRouteExecutionState.Idle
            ? suiteTravel.Status
            : execution.Status;

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
        NavigationRoutePlanKind kind)
    {
        NavigationRoutePlan plan = Plan(route, kind);
        NavigationRecoveryStatus recoveryStatus = recovery.PrepareForExplicitStart(Environment.TickCount64);
        if (recoveryStatus.IsRequired)
            return new NavigationRouteExecutionStatus(
                NavigationRouteExecutionState.Blocked,
                plan.RouteId,
                plan.RouteName,
                false,
                false,
                recoveryStatus.Code,
                recoveryStatus.Message);
        if (recording.Status.IsRecording)
            return new NavigationRouteExecutionStatus(
                NavigationRouteExecutionState.Blocked,
                plan.RouteId,
                plan.RouteName,
                false,
                false,
                "route-recording-active",
                "Stop timed recording before starting route movement.");
        if (execution.Status.IsActive || suiteTravel.Status.IsActive)
            return new NavigationRouteExecutionStatus(
                NavigationRouteExecutionState.Blocked,
                plan.RouteId,
                plan.RouteName,
                false,
                false,
                "route-already-running",
                "Stop the current Nexus route before starting another one.");
        suiteTravel.ResetInactive();
        return NavigationRouteDispatchPolicy.Select(plan, suiteTravel.CanDispatch) switch
        {
            NavigationRouteDispatchKind.SuiteTravel => suiteTravel.Start(route, plan, DateTimeOffset.UtcNow),
            NavigationRouteDispatchKind.Local => execution.Start(plan, DateTimeOffset.UtcNow),
            _ => new NavigationRouteExecutionStatus(
                NavigationRouteExecutionState.Blocked,
                plan.RouteId,
                plan.RouteName,
                false,
                false,
                plan.Code,
                plan.Message),
        };
    }

    internal NavigationRouteExecutionStatus Stop() => suiteTravel.Status.IsActive
        ? suiteTravel.Stop()
        : execution.Stop(DateTimeOffset.UtcNow);

    internal bool CanDispatchCrossZone => suiteTravel.CanDispatch;

    internal NavigationRouteRecordingStatus StartRecording(NavigationRouteSnapshot route, long now) =>
        recording.Start(route, now);

    internal NavigationRouteRecordingStatus StopRecording(string message = "Recording stopped.") =>
        recording.Stop(message);

    internal bool IsRecording(Guid routeId) => recording.IsRecording(routeId);

    internal void Update(long now)
    {
        execution.Update(DateTimeOffset.UtcNow);
        suiteTravel.Update(DateTimeOffset.UtcNow);
        recording.Update(now);
        if (recording.Status is { IsRecording: true, RouteId: { } routeId } &&
            preview.RouteId == routeId &&
            library.Current is { } snapshot &&
            snapshot.Routes.FirstOrDefault(route => route.Id == routeId) is { } route)
        {
            preview.Set(Plan(route, NavigationRoutePlanKind.Review), snapshot.ShowPointNumbers);
        }
    }

    internal void DrawPreview()
    {
        preview.Draw();
        if (library.Current is not { } snapshot)
            return;
        livePath.Draw(
            snapshot.ShowLiveNavigationPath,
            snapshot.ShowPointNumbers,
            execution.Status.IsActive,
            suiteTravel.IsVisualizationAuthorized);
    }

    internal void Shutdown()
    {
        recording.Stop("Recording stopped because Nexus is unloading.");
        suiteTravel.Shutdown();
    }

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
