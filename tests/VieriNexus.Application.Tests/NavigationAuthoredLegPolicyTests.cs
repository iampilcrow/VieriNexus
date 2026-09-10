using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationAuthoredLegPolicyTests
{
    [Fact]
    public void MeshAssistedSinglePointUsesPathfindingAndFinalTolerance()
    {
        var request = Request(useMesh: true, tolerance: .75f, lastPointTolerance: 3f);

        NavigationAuthoredLeg leg = NavigationAuthoredLegPolicy.Create(request, 0);

        Assert.True(leg.RequiresPathfinding);
        Assert.Equal(request.Points[0], leg.Destination);
        Assert.Equal(3f, leg.Tolerance);
    }

    [Fact]
    public void MultiPointRouteUsesOrdinaryToleranceUntilFinalLeg()
    {
        var request = Request(useMesh: true, tolerance: .75f, lastPointTolerance: 3f,
            new(10, 0, 10), new(20, 0, 20));

        Assert.Equal(.75f, NavigationAuthoredLegPolicy.Create(request, 0).Tolerance);
        Assert.Equal(3f, NavigationAuthoredLegPolicy.Create(request, 1).Tolerance);
    }

    [Fact]
    public void NonMeshRoutePreservesExplicitDirectMovement()
    {
        var request = Request(useMesh: false, tolerance: .75f, lastPointTolerance: 3f);

        Assert.False(NavigationAuthoredLegPolicy.Create(request, 0).RequiresPathfinding);
    }

    private static NavigationSuiteRouteRequest.PlaybackRequest Request(
        bool useMesh,
        float tolerance,
        float lastPointTolerance,
        params NavigationRoutePoint[] points) => new(
            129,
            points.Length == 0 ? [new NavigationRoutePoint(10, 0, 10)] : points,
            useMesh,
            false,
            tolerance,
            lastPointTolerance,
            false,
            0,
            null);
}
