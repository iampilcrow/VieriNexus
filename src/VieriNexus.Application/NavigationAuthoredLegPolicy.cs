namespace VieriNexus.Application;

public sealed record NavigationAuthoredLeg(
    NavigationRoutePoint Destination,
    bool RequiresPathfinding,
    bool UseFlight,
    float Tolerance);

public enum NavigationArrivalAction
{
    WaitForTerritory,
    WaitForMesh,
    StartLocalPath,
    Fail,
}

public enum NavigationFlightPreparationAction
{
    UseGroundPath,
    Mount,
    TakeOff,
    UseFlightPath,
}

/// <summary>
/// Preserves the predecessor's outdoor-route behavior without allowing a missing mount, locked
/// flight, or failed takeoff to strand an owned trip at its arrival Aetheryte.
/// </summary>
public static class NavigationFlightPreparationPolicy
{
    public static NavigationFlightPreparationAction Decide(
        bool flightRequested,
        bool flightUnlocked,
        bool mounted,
        bool inFlight,
        bool preparationTimedOut)
    {
        if (!flightRequested || !flightUnlocked || preparationTimedOut)
            return NavigationFlightPreparationAction.UseGroundPath;
        if (!mounted)
            return NavigationFlightPreparationAction.Mount;
        return inFlight
            ? NavigationFlightPreparationAction.UseFlightPath
            : NavigationFlightPreparationAction.TakeOff;
    }
}

/// <summary>
/// Decides when a completed cross-zone transfer may hand control to local navigation. Reaching
/// the exact destination territory is authoritative; a provider's lingering busy flag must not
/// strand the player at the arrival Aetheryte after the transfer has already completed.
/// </summary>
public static class NavigationArrivalPolicy
{
    public static NavigationArrivalAction Decide(
        bool destinationTerritoryLoaded,
        bool navigationReady,
        bool phaseTimedOut)
    {
        if (destinationTerritoryLoaded)
            return navigationReady
                ? NavigationArrivalAction.StartLocalPath
                : NavigationArrivalAction.WaitForMesh;

        return phaseTimedOut
            ? NavigationArrivalAction.Fail
            : NavigationArrivalAction.WaitForTerritory;
    }
}

/// <summary>
/// Converts each authored route point into one movement leg. Mesh-assisted routes must calculate
/// a navigable path to every authored point; passing a single destination directly to Path.MoveTo
/// would produce an unsafe straight line through geometry.
/// </summary>
public static class NavigationAuthoredLegPolicy
{
    public const float MinimumVendorFlightDistance = 100f;

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

    public static bool ShouldUseFlightForLeg(bool requestedFlight, bool vendorApproach, float directDistance) =>
        requestedFlight && (!vendorApproach || directDistance >= MinimumVendorFlightDistance);
}
