using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationAuthoredLegPolicyTests
{
    [Fact]
    public void MeshAssistedSinglePointUsesPathfindingAndFinalTolerance()
    {
        var request = Request(useMesh: true, tolerance: .75f, lastPointTolerance: 3f);

        NavigationAuthoredLeg leg = NavigationAuthoredLegPolicy.Create(request, 0, flightSupported: true);

        Assert.True(leg.RequiresPathfinding);
        Assert.Equal(request.Points[0], leg.Destination);
        Assert.Equal(3f, leg.Tolerance);
    }

    [Fact]
    public void MultiPointRouteUsesOrdinaryToleranceUntilFinalLeg()
    {
        var request = Request(
            useMesh: true,
            tolerance: .75f,
            lastPointTolerance: 3f,
            points: [new(10, 0, 10), new(20, 0, 20)]);

        Assert.Equal(.75f, NavigationAuthoredLegPolicy.Create(request, 0, flightSupported: true).Tolerance);
        Assert.Equal(3f, NavigationAuthoredLegPolicy.Create(request, 1, flightSupported: true).Tolerance);
    }

    [Fact]
    public void NonMeshRoutePreservesExplicitDirectMovement()
    {
        var request = Request(useMesh: false, tolerance: .75f, lastPointTolerance: 3f);

        Assert.False(NavigationAuthoredLegPolicy.Create(request, 0, flightSupported: true).RequiresPathfinding);
    }

    [Fact]
    public void FlightPermissionIsSuppressedWhenTerritoryHasNoFlightVolume()
    {
        var request = Request(useMesh: true, tolerance: .75f, lastPointTolerance: 3f, useFlight: true);

        NavigationAuthoredLeg leg = NavigationAuthoredLegPolicy.Create(request, 0, flightSupported: false);

        Assert.False(leg.UseFlight);
    }

    [Fact]
    public void MissingFlightPathRetriesOnGroundOnlyOnce()
    {
        Assert.True(NavigationAuthoredLegPolicy.ShouldRetryPathOnGround(attemptedFlight: true, pathFound: false));
        Assert.False(NavigationAuthoredLegPolicy.ShouldRetryPathOnGround(attemptedFlight: false, pathFound: false));
        Assert.False(NavigationAuthoredLegPolicy.ShouldRetryPathOnGround(attemptedFlight: true, pathFound: true));
    }

    private static NavigationSuiteRouteRequest.PlaybackRequest Request(
        bool useMesh,
        float tolerance,
        float lastPointTolerance,
        bool useFlight = false,
        params NavigationRoutePoint[] points) => new(
            129,
            points.Length == 0 ? [new NavigationRoutePoint(10, 0, 10)] : points,
            useMesh,
            useFlight,
            tolerance,
            lastPointTolerance,
            false,
            0,
            null);
}
