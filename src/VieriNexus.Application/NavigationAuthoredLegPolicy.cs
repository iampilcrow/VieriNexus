namespace VieriNexus.Application;

public sealed record NavigationAuthoredLeg(
    NavigationRoutePoint Destination,
    bool RequiresPathfinding,
    bool UseFlight,
    float Tolerance);

/// <summary>
/// Converts each authored route point into one movement leg. Mesh-assisted routes must calculate
/// a navigable path to every authored point; passing a single destination directly to Path.MoveTo
/// would produce an unsafe straight line through geometry.
/// </summary>
public static class NavigationAuthoredLegPolicy
{
    public static NavigationAuthoredLeg Create(
        NavigationSuiteRouteRequest.PlaybackRequest request,
        int pointIndex,
        bool flightSupported)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (pointIndex < 0 || pointIndex >= request.Points.Count)
            throw new ArgumentOutOfRangeException(nameof(pointIndex));

        return new NavigationAuthoredLeg(
            request.Points[pointIndex],
            request.UseMesh,
            request.UseFlight && flightSupported,
            pointIndex == request.Points.Count - 1
                ? request.LastPointTolerance
                : request.Tolerance);
    }

    public static bool ShouldRetryPathOnGround(bool attemptedFlight, bool pathFound) =>
        attemptedFlight && !pathFound;
}
