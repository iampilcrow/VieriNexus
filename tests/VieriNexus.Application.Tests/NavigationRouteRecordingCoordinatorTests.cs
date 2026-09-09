using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRouteRecordingCoordinatorTests
{
    private static readonly Guid RouteId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
    private static readonly NavigationRoutePoint Origin = new(0, 0, 0);

    [Fact]
    public void StartCapturesCurrentPositionWhenItIsNotAlreadyRepresented()
    {
        var coordinator = new NavigationRouteRecordingCoordinator();

        NavigationRouteRecordingDecision result = coordinator.Start(
            RouteId, 100, 100, new(5, 0, 0), Origin, 1f, 0.75f, 10_000);

        Assert.Equal(NavigationRouteRecordingAction.Capture, result.Action);
        Assert.Equal(new NavigationRoutePoint(5, 0, 0), result.Point);
        Assert.True(result.Status.IsRecording);
        Assert.Equal(1, result.Status.CapturedPointCount);
    }

    [Fact]
    public void StartDoesNotDuplicateNearbyLastPoint()
    {
        var coordinator = new NavigationRouteRecordingCoordinator();

        NavigationRouteRecordingDecision result = coordinator.Start(
            RouteId, 100, 100, new(0.5f, 0, 0), Origin, 1f, 0.75f, 10_000);

        Assert.Equal(NavigationRouteRecordingAction.None, result.Action);
        Assert.True(result.Status.IsRecording);
        Assert.Equal(0, result.Status.CapturedPointCount);
    }

    [Fact]
    public void UpdateWaitsForIntervalAndMinimumSpacing()
    {
        var coordinator = new NavigationRouteRecordingCoordinator();
        coordinator.Start(RouteId, 100, 100, Origin, Origin, 1f, 0.75f, 10_000);

        NavigationRouteRecordingDecision early = coordinator.Update(
            RouteId, 100, 100, new(4, 0, 0), Origin, 1f, 0.75f, 10_999);
        NavigationRouteRecordingDecision near = coordinator.Update(
            RouteId, 100, 100, new(0.5f, 0, 0), Origin, 1f, 0.75f, 11_000);
        NavigationRouteRecordingDecision capture = coordinator.Update(
            RouteId, 100, 100, new(2, 0, 0), Origin, 1f, 0.75f, 12_000);

        Assert.Equal(NavigationRouteRecordingAction.None, early.Action);
        Assert.Equal(NavigationRouteRecordingAction.None, near.Action);
        Assert.Equal(NavigationRouteRecordingAction.Capture, capture.Action);
        Assert.Equal(new NavigationRoutePoint(2, 0, 0), capture.Point);
    }

    [Fact]
    public void TerritoryChangeStopsAndCannotCaptureAgain()
    {
        var coordinator = new NavigationRouteRecordingCoordinator();
        coordinator.Start(RouteId, 100, 100, Origin, null, 1f, 0.75f, 10_000);

        NavigationRouteRecordingDecision changed = coordinator.Update(
            RouteId, 100, 101, new(2, 0, 0), Origin, 1f, 0.75f, 11_000);
        NavigationRouteRecordingDecision later = coordinator.Update(
            RouteId, 100, 100, new(4, 0, 0), Origin, 1f, 0.75f, 12_000);

        Assert.Equal(NavigationRouteRecordingAction.Stop, changed.Action);
        Assert.False(changed.Status.IsRecording);
        Assert.Equal("recording-territory-changed", changed.Status.Code);
        Assert.Equal(NavigationRouteRecordingAction.None, later.Action);
    }

    [Fact]
    public void WrongTerritoryCannotStartRecording()
    {
        var coordinator = new NavigationRouteRecordingCoordinator();

        NavigationRouteRecordingDecision result = coordinator.Start(
            RouteId, 100, 101, Origin, null, 1f, 0.75f, 10_000);

        Assert.Equal(NavigationRouteRecordingAction.None, result.Action);
        Assert.False(result.Status.IsRecording);
        Assert.Equal("recording-territory-mismatch", result.Status.Code);
    }
}
