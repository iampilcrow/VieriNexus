using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed class NavigationRouteRecordingService(
    NavigationLibraryService library,
    NavigationRouteRecordingCoordinator coordinator)
{
    internal NavigationRouteRecordingStatus Status => coordinator.Status;

    internal bool IsRecording(Guid routeId) =>
        coordinator.Status is { IsRecording: true, RouteId: { } active } && active == routeId;

    internal NavigationRouteRecordingStatus Start(NavigationRouteSnapshot route, long now)
    {
        if (!library.HasWorkingLibrary)
            return coordinator.Stop("Create the Nexus working library before recording.").Status;
        if (Plugin.ObjectTable.LocalPlayer is not { } player)
            return coordinator.Stop("Recording could not start because the character position is unavailable.").Status;

        NavigationLibrarySnapshot snapshot = library.Current!;
        NavigationRouteRecordingDecision decision = coordinator.Start(
            route.Id,
            route.TerritoryId,
            Plugin.ClientState.TerritoryType,
            new(player.Position.X, player.Position.Y, player.Position.Z),
            route.Points.LastOrDefault(),
            snapshot.RecordingIntervalSeconds,
            snapshot.MinimumPointDistance,
            now);
        PersistCapture(route.Id, decision);
        return coordinator.Status;
    }

    internal NavigationRouteRecordingStatus Stop(string message = "Recording stopped.") =>
        coordinator.Stop(message).Status;

    internal void Update(long now)
    {
        NavigationRouteRecordingStatus status = coordinator.Status;
        if (!status.IsRecording || status.RouteId is not { } routeId)
            return;

        NavigationLibrarySnapshot? snapshot = library.HasWorkingLibrary ? library.Current : null;
        NavigationRouteSnapshot? route = snapshot?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (snapshot is null || route is null)
        {
            coordinator.Stop("Recording stopped because the route is no longer available.");
            return;
        }
        if (Plugin.ObjectTable.LocalPlayer is not { } player)
        {
            coordinator.Stop("Recording stopped because the character position became unavailable.");
            return;
        }

        NavigationRouteRecordingDecision decision = coordinator.Update(
            route.Id,
            route.TerritoryId,
            Plugin.ClientState.TerritoryType,
            new(player.Position.X, player.Position.Y, player.Position.Z),
            route.Points.LastOrDefault(),
            snapshot.RecordingIntervalSeconds,
            snapshot.MinimumPointDistance,
            now);
        PersistCapture(route.Id, decision);
    }

    private void PersistCapture(Guid routeId, NavigationRouteRecordingDecision decision)
    {
        if (decision.Action != NavigationRouteRecordingAction.Capture || decision.Point is not { } point)
            return;

        NavigationLibraryWriteResult result = library.AddPoint(routeId, point);
        if (!result.Success)
            coordinator.Stop($"Recording stopped because the point could not be saved: {result.Message}");
    }
}
