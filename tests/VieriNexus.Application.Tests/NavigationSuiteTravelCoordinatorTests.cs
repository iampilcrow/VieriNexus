using System.Text.Json;
using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationSuiteTravelCoordinatorTests
{
    [Fact]
    public void DispatchRequiresExplicitAuthority()
    {
        var provider = new FakeProvider();
        var coordinator = new NavigationSuiteTravelCoordinator(provider, () => false, () => true);
        NavigationRouteSnapshot route = Route();

        NavigationRouteExecutionStatus result = coordinator.Start(
            route, Plan(route), DateTimeOffset.UtcNow);

        Assert.Equal("navigation-authority-required", result.Code);
        Assert.Equal(0, provider.DispatchCount);
    }

    [Fact]
    public void StartedRouteAloneAuthorizesVisualizationAndCompletesAfterProviderStops()
    {
        var provider = new FakeProvider();
        var coordinator = new NavigationSuiteTravelCoordinator(provider, () => true, () => true);
        NavigationRouteSnapshot route = Route();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        NavigationRouteExecutionStatus started = coordinator.Start(route, Plan(route), now);

        Assert.True(started.IsActive);
        Assert.True(coordinator.IsVisualizationAuthorized);
        Assert.Equal(1, provider.DispatchCount);
        using JsonDocument request = JsonDocument.Parse(provider.LastRequest!);
        Assert.Equal(200u, request.RootElement.GetProperty("TerritoryId").GetUInt32());

        provider.RouteActive = false;
        NavigationRouteExecutionStatus completed = coordinator.Update(now.AddSeconds(3));

        Assert.Equal(NavigationRouteExecutionState.Completed, completed.State);
        Assert.False(completed.IsActive);
        Assert.False(coordinator.IsVisualizationAuthorized);
    }

    [Fact]
    public void ExistingExternalProviderActivityIsNeverAdoptedOrStopped()
    {
        var provider = new FakeProvider { RouteActive = true };
        var coordinator = new NavigationSuiteTravelCoordinator(provider, () => true, () => true);

        Assert.False(coordinator.IsVisualizationAuthorized);
        coordinator.Shutdown();
        Assert.Equal(0, provider.StopCount);
    }

    [Fact]
    public void ExplicitStopAffectsOnlyTrackedDispatch()
    {
        var provider = new FakeProvider();
        var coordinator = new NavigationSuiteTravelCoordinator(provider, () => true, () => true);
        NavigationRouteSnapshot route = Route();
        coordinator.Start(route, Plan(route), DateTimeOffset.UtcNow);

        NavigationRouteExecutionStatus result = coordinator.Stop();

        Assert.Equal("suite-route-stopped", result.Code);
        Assert.Equal(1, provider.StopCount);
        Assert.False(result.IsActive);
    }

    [Fact]
    public void StartPermissionChangesDoNotStopAnActiveSuiteTrip()
    {
        var provider = new FakeProvider();
        bool executionAllowed = true;
        var coordinator = new NavigationSuiteTravelCoordinator(
            provider, () => true, () => executionAllowed);
        NavigationRouteSnapshot route = Route();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        coordinator.Start(route, Plan(route), now);

        executionAllowed = false;
        NavigationRouteExecutionStatus result = coordinator.Update(now.AddMilliseconds(50));

        Assert.Equal("suite-route-running", result.Code);
        Assert.Equal(0, provider.StopCount);
        Assert.True(result.IsActive);
    }

    [Fact]
    public void UnconfirmedStopDoesNotDirectTheUserToProviderUi()
    {
        var provider = new FakeProvider { StopResult = false };
        var coordinator = new NavigationSuiteTravelCoordinator(provider, () => true, () => true);
        NavigationRouteSnapshot route = Route();
        coordinator.Start(route, Plan(route), DateTimeOffset.UtcNow);

        NavigationRouteExecutionStatus result = coordinator.Stop();

        Assert.Equal("suite-route-stop-unconfirmed", result.Code);
        Assert.DoesNotContain("VieriAutoDuty Stop", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider reports inactive", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static NavigationRouteSnapshot Route() => new(
        Guid.NewGuid(), "Cross-zone test", 200, [new(1, 2, 3), new(4, 5, 6)],
        string.Empty, string.Empty, true, true, 0.75f, 3f,
        0, 0, string.Empty, false, DateTime.UnixEpoch);

    private static NavigationRoutePlan Plan(NavigationRouteSnapshot route) =>
        NavigationRoutePlanner.Build(route, NavigationRoutePlanKind.Playback, 100);

    private sealed class FakeProvider : INavigationSuiteTravelProvider
    {
        public bool IsAvailable { get; set; } = true;
        public bool RouteActive { get; set; } = true;
        public int DispatchCount { get; private set; }
        public int StopCount { get; private set; }
        public string? LastRequest { get; private set; }
        public bool StopResult { get; set; } = true;

        public SuiteRouteDispatchResult Dispatch(string requestJson)
        {
            DispatchCount++;
            LastRequest = requestJson;
            RouteActive = true;
            return new(true, "Started route playback through VieriAutoDuty (2 points).");
        }

        public bool Stop()
        {
            StopCount++;
            RouteActive = false;
            return StopResult;
        }

        public bool IsRouteVisualizationActive() => RouteActive;
    }
}
