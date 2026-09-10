using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using VieriNexus.Application;
using System.Numerics;

namespace VieriNexus.Services;

internal sealed class VnavmeshNavigationStopProvider : INavigationStopProvider, INavigationMovementProvider
{
    private readonly DependencyService dependencies;
    private readonly ICallGateSubscriber<object> stop;
    private readonly ICallGateSubscriber<bool> isRunning;
    private readonly ICallGateSubscriber<bool> isReady;
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> moveTo;
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>> pathfind;
    private readonly ICallGateSubscriber<float, object> setTolerance;
    private readonly ICallGateSubscriber<List<Vector3>> listWaypoints;
    private CancellationTokenSource? pathfindCancellation;
    private Task<List<Vector3>>? pathfindTask;
    private NavigationRoutePoint? pathfindDestination;
    private bool pathfindUseFlight;

    internal VnavmeshNavigationStopProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies)
    {
        this.dependencies = dependencies;
        stop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        isRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        isReady = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        moveTo = pluginInterface.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
        pathfind = pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>>(
            "vnavmesh.Nav.PathfindCancelable");
        setTolerance = pluginInterface.GetIpcSubscriber<float, object>("vnavmesh.Path.SetTolerance");
        listWaypoints = pluginInterface.GetIpcSubscriber<List<Vector3>>("vnavmesh.Path.ListWaypoints");
    }

    public bool IsAvailable => dependencies.FindPlugin("vnavmesh").IsLoaded;

    public bool IsReady => IsAvailable && isReady.InvokeFunc();

    public void Start(
        IReadOnlyList<NavigationRoutePoint> points,
        bool useFlight,
        float tolerance)
    {
        if (points.Count == 0)
            throw new ArgumentException("At least one route point is required.", nameof(points));
        if (points.Any(point =>
                !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)))
            throw new ArgumentException("Every route point must be finite.", nameof(points));
        setTolerance.InvokeAction(Math.Clamp(tolerance, 0.1f, 20f));
        moveTo.InvokeAction(points.Select(point => new Vector3(point.X, point.Y, point.Z)).ToList(), useFlight);
    }

    internal void StartPathfinding(
        NavigationRoutePoint destination,
        bool useFlight,
        float tolerance)
    {
        if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y) || !float.IsFinite(destination.Z))
            throw new ArgumentException("The route destination must be finite.", nameof(destination));
        if (pathfindTask is not null)
            throw new InvalidOperationException("A Nexus vnavmesh path calculation is already running.");
        if (Plugin.ObjectTable.LocalPlayer is not { } player)
            throw new InvalidOperationException("FFXIV is not logged into a character.");

        setTolerance.InvokeAction(Math.Clamp(tolerance, 0.1f, 20f));
        pathfindCancellation = new CancellationTokenSource();
        pathfindDestination = destination;
        pathfindUseFlight = useFlight;
        pathfindTask = pathfind.InvokeFunc(
            player.Position,
            new Vector3(destination.X, destination.Y, destination.Z),
            useFlight,
            pathfindCancellation.Token);
    }

    public void RequestStop()
    {
        CancelPendingPathfind();
        stop.InvokeAction();
    }

    public bool? IsMovementActive()
    {
        PumpPendingPathfind();
        return pathfindTask is not null || isRunning.InvokeFunc();
    }

    public bool? IsDestinationOwned(NavigationRoutePoint expectedDestination)
    {
        PumpPendingPathfind();
        if (pathfindTask is not null)
            return pathfindDestination is { } pending && Distance(pending, expectedDestination) <= 1.5f;
        if (!isRunning.InvokeFunc())
            return null;
        List<Vector3> points = listWaypoints.InvokeFunc();
        if (points.Count == 0)
            return null;
        Vector3 expected = new(expectedDestination.X, expectedDestination.Y, expectedDestination.Z);
        return Vector3.Distance(points[^1], expected) <= 1.5f;
    }

    internal IReadOnlyList<NavigationRoutePoint> GetActiveWaypoints()
    {
        if (!IsAvailable)
            return [];
        try
        {
            if (!isRunning.InvokeFunc())
                return [];
            return listWaypoints.InvokeFunc()
                .Where(point => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z))
                .Select(point => new NavigationRoutePoint(point.X, point.Y, point.Z))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private void PumpPendingPathfind()
    {
        if (pathfindTask is not { IsCompleted: true } completed)
            return;

        CancellationTokenSource? cancellation = pathfindCancellation;
        pathfindTask = null;
        pathfindCancellation = null;
        pathfindDestination = null;
        try
        {
            List<Vector3> points = completed.GetAwaiter().GetResult();
            if (points.Count == 0)
                throw new InvalidOperationException("vnavmesh could not find a navigable path to the route point.");
            moveTo.InvokeAction(points, pathfindUseFlight);
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private void CancelPendingPathfind()
    {
        CancellationTokenSource? cancellation = pathfindCancellation;
        pathfindTask = null;
        pathfindCancellation = null;
        pathfindDestination = null;
        if (cancellation is null)
            return;
        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static float Distance(NavigationRoutePoint left, NavigationRoutePoint right) =>
        Vector3.Distance(
            new Vector3(left.X, left.Y, left.Z),
            new Vector3(right.X, right.Y, right.Z));
}
