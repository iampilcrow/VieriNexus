using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRoutePlannerTests
{
    [Fact]
    public void PlaybackPlanPreservesOrderedPointsAndDistance()
    {
        NavigationRouteSnapshot route = Route([
            new(0, 0, 0),
            new(3, 0, 4),
            new(3, 12, 4),
        ]);

        NavigationRoutePlan plan = NavigationRoutePlanner.Build(
            route, NavigationRoutePlanKind.Playback, route.TerritoryId);

        Assert.True(plan.IsValid);
        Assert.True(plan.IsExecutable);
        Assert.Equal(route.Points, plan.Points);
        Assert.Equal(17f, plan.TotalDistance);
    }

    [Fact]
    public void TravelToStartPlansOnlyFirstPoint()
    {
        NavigationRouteSnapshot route = Route([new(1, 2, 3), new(4, 5, 6)]);

        NavigationRoutePlan plan = NavigationRoutePlanner.Build(
            route, NavigationRoutePlanKind.TravelToStart, route.TerritoryId);

        Assert.True(plan.IsExecutable);
        Assert.Equal(NavigationRoutePlanKind.TravelToStart, plan.Kind);
        Assert.Equal(route.Points[0], Assert.Single(plan.Points));
    }

    [Fact]
    public void ReviewCanBePreparedOutsideRouteTerritoryButExecutionCannot()
    {
        NavigationRouteSnapshot route = Route([new(1, 2, 3), new(4, 5, 6)]);

        NavigationRoutePlan review = NavigationRoutePlanner.Build(
            route, NavigationRoutePlanKind.Review, 999);
        NavigationRoutePlan playback = NavigationRoutePlanner.Build(
            route, NavigationRoutePlanKind.Playback, 999);

        Assert.True(review.IsValid);
        Assert.False(review.IsExecutable);
        Assert.True(playback.IsValid);
        Assert.False(playback.IsExecutable);
        Assert.Equal("route-territory-mismatch", playback.Code);
    }

    [Fact]
    public void PlaybackAllowsOnePointDestinationRoute()
    {
        NavigationRoutePlan plan = NavigationRoutePlanner.Build(
            Route([new(1, 2, 3)]), NavigationRoutePlanKind.Playback, 100);

        Assert.True(plan.IsValid);
        Assert.True(plan.IsExecutable);
        Assert.Single(plan.Points);
    }

    private static NavigationRouteSnapshot Route(IReadOnlyList<NavigationRoutePoint> points) => new(
        Guid.NewGuid(), "Test route", 100, points, string.Empty, string.Empty,
        true, false, 0.75f, 3f, 0, 0, string.Empty, false, DateTime.UtcNow);
}
