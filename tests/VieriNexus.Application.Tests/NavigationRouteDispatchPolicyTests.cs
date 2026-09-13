using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRouteDispatchPolicyTests
{
    [Fact]
    public void SuiteTravelOwnsSameTerritoryRestartWhenAvailable()
    {
        NavigationRoutePlan plan = Plan(currentTerritory: 100);

        Assert.Equal(
            NavigationRouteDispatchKind.SuiteTravel,
            NavigationRouteDispatchPolicy.Select(plan, suiteTravelAvailable: true));
    }

    [Fact]
    public void LocalProviderIsFallbackForSameTerritory()
    {
        NavigationRoutePlan plan = Plan(currentTerritory: 100);

        Assert.Equal(
            NavigationRouteDispatchKind.Local,
            NavigationRouteDispatchPolicy.Select(plan, suiteTravelAvailable: false));
    }

    [Fact]
    public void CrossTerritoryWithoutSuiteTravelIsUnavailable()
    {
        NavigationRoutePlan plan = Plan(currentTerritory: 200);

        Assert.Equal(
            NavigationRouteDispatchKind.Unavailable,
            NavigationRouteDispatchPolicy.Select(plan, suiteTravelAvailable: false));
    }

    private static NavigationRoutePlan Plan(uint currentTerritory)
    {
        NavigationRouteSnapshot route = new(
            Guid.NewGuid(), "Route", 100, [new(1, 2, 3)],
            string.Empty, string.Empty, true, true, 0.75f, 3f,
            0, 0, string.Empty, false, DateTime.UtcNow);
        return NavigationRoutePlanner.Build(route, NavigationRoutePlanKind.Playback, currentTerritory);
    }
}
