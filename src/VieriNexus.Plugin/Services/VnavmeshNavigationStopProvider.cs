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
    private readonly ICallGateSubscriber<float, object> setTolerance;
    private readonly ICallGateSubscriber<List<Vector3>> listWaypoints;

    internal VnavmeshNavigationStopProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies)
    {
        this.dependencies = dependencies;
        stop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        isRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        isReady = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        moveTo = pluginInterface.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
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

    public void RequestStop() => stop.InvokeAction();

    public bool? IsMovementActive() => isRunning.InvokeFunc();

    public bool? IsDestinationOwned(NavigationRoutePoint expectedDestination)
    {
        if (!isRunning.InvokeFunc())
            return null;
        List<Vector3> points = listWaypoints.InvokeFunc();
        if (points.Count == 0)
            return null;
        Vector3 expected = new(expectedDestination.X, expectedDestination.Y, expectedDestination.Z);
        return Vector3.Distance(points[^1], expected) <= 1.5f;
    }
}
