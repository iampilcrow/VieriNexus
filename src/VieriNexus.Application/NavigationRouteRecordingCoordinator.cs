using System.Numerics;

namespace VieriNexus.Application;

public enum NavigationRouteRecordingAction
{
    None,
    Capture,
    Stop,
}

public sealed record NavigationRouteRecordingStatus(
    bool IsRecording,
    Guid? RouteId,
    int CapturedPointCount,
    string Code,
    string Message);

public sealed record NavigationRouteRecordingDecision(
    NavigationRouteRecordingAction Action,
    NavigationRoutePoint? Point,
    NavigationRouteRecordingStatus Status);

/// <summary>
/// Provider-neutral timed route capture. The coordinator decides when a point may be
/// captured; persistence and live-position observation remain in the plugin layer.
/// </summary>
public sealed class NavigationRouteRecordingCoordinator
{
    private Guid? routeId;
    private long nextCaptureAt;
    private int capturedPointCount;
    private NavigationRouteRecordingStatus status = new(
        false, null, 0, "recording-idle", "Route recording is idle.");

    public NavigationRouteRecordingStatus Status => status;

    public NavigationRouteRecordingDecision Start(
        Guid requestedRouteId,
        uint routeTerritoryId,
        uint currentTerritoryId,
        NavigationRoutePoint currentPosition,
        NavigationRoutePoint? lastPoint,
        float intervalSeconds,
        float minimumPointDistance,
        long now)
    {
        if (requestedRouteId == Guid.Empty)
            return Decision(NavigationRouteRecordingAction.None, null, false, null,
                "recording-route-invalid", "The selected route has no stable ID.");
        if (routeTerritoryId == 0 || routeTerritoryId != currentTerritoryId)
            return Decision(NavigationRouteRecordingAction.None, null, false, null,
                "recording-territory-mismatch", "Recording can start only in the route's territory.");
        if (!Finite(currentPosition))
            return Decision(NavigationRouteRecordingAction.None, null, false, null,
                "recording-position-unavailable", "The current character position is unavailable.");

        routeId = requestedRouteId;
        capturedPointCount = 0;
        nextCaptureAt = SaturatingAdd(now, IntervalMilliseconds(intervalSeconds));
        bool capture = lastPoint is null || Distance(lastPoint, currentPosition) >= ClampSpacing(minimumPointDistance);
        if (capture)
            capturedPointCount = 1;
        return Decision(capture ? NavigationRouteRecordingAction.Capture : NavigationRouteRecordingAction.None,
            capture ? currentPosition : null, true, routeId, "recording-started",
            capture
                ? "Recording started and captured the current position."
                : "Recording started; the current position is already represented by the last point.");
    }

    public NavigationRouteRecordingDecision Update(
        Guid availableRouteId,
        uint routeTerritoryId,
        uint currentTerritoryId,
        NavigationRoutePoint currentPosition,
        NavigationRoutePoint? lastPoint,
        float intervalSeconds,
        float minimumPointDistance,
        long now)
    {
        if (routeId is null)
            return new(NavigationRouteRecordingAction.None, null, status);
        if (availableRouteId != routeId.Value)
            return Stop("recording-route-missing", "Recording stopped because the route is no longer available.");
        if (routeTerritoryId == 0 || routeTerritoryId != currentTerritoryId)
            return Stop("recording-territory-changed", "Recording stopped because the territory changed.");
        if (!Finite(currentPosition))
            return Stop("recording-position-unavailable", "Recording stopped because the character position became unavailable.");
        if (now < nextCaptureAt)
            return new(NavigationRouteRecordingAction.None, null, status);

        nextCaptureAt = SaturatingAdd(now, IntervalMilliseconds(intervalSeconds));
        if (lastPoint is not null && Distance(lastPoint, currentPosition) < ClampSpacing(minimumPointDistance))
        {
            status = status with
            {
                Code = "recording-spacing-filtered",
                Message = $"Recording is active; {capturedPointCount} point(s) captured this session.",
            };
            return new(NavigationRouteRecordingAction.None, null, status);
        }

        capturedPointCount++;
        status = status with
        {
            CapturedPointCount = capturedPointCount,
            Code = "recording-point-ready",
            Message = $"Recording is active; {capturedPointCount} point(s) captured this session.",
        };
        return new(NavigationRouteRecordingAction.Capture, currentPosition, status);
    }

    public NavigationRouteRecordingDecision Stop(string message = "Recording stopped.") =>
        Stop("recording-stopped", message);

    private NavigationRouteRecordingDecision Stop(string code, string message)
    {
        Guid? stoppedRoute = routeId;
        routeId = null;
        nextCaptureAt = 0;
        status = new(false, stoppedRoute, capturedPointCount, code, message);
        return new(NavigationRouteRecordingAction.Stop, null, status);
    }

    private NavigationRouteRecordingDecision Decision(
        NavigationRouteRecordingAction action,
        NavigationRoutePoint? point,
        bool isRecording,
        Guid? activeRouteId,
        string code,
        string message)
    {
        status = new(isRecording, activeRouteId, capturedPointCount, code, message);
        return new(action, point, status);
    }

    private static long IntervalMilliseconds(float seconds) =>
        Math.Max(200L, (long)(Math.Clamp(seconds, 0.2f, 5f) * 1000f));

    private static float ClampSpacing(float distance) => Math.Clamp(distance, 0.1f, 10f);

    private static long SaturatingAdd(long value, long addition) =>
        value > long.MaxValue - addition ? long.MaxValue : value + addition;

    private static bool Finite(NavigationRoutePoint point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    private static float Distance(NavigationRoutePoint left, NavigationRoutePoint right) =>
        Vector3.Distance(new(left.X, left.Y, left.Z), new(right.X, right.Y, right.Z));
}
