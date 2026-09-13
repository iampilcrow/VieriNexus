namespace VieriNexus.Application;

public readonly record struct SuiteRouteDispatchResult(bool Started, string Message);

public enum SuiteRouteProviderState
{
    Idle,
    Running,
    Completed,
    Failed,
}

public readonly record struct SuiteRouteProviderObservation(
    SuiteRouteProviderState State,
    string Code,
    string Message,
    bool VisualizationActive)
{
    public bool IsActive => State == SuiteRouteProviderState.Running;
}

public interface INavigationSuiteTravelProvider
{
    bool IsAvailable { get; }

    SuiteRouteDispatchResult Dispatch(string requestJson);

    bool Stop();

    SuiteRouteProviderObservation Observe(DateTimeOffset now);
}

/// <summary>
/// Tracks only route travel explicitly dispatched by this Nexus process. Other suite-provider and
/// vnavmesh activity is never adopted, stopped, or visualized.
/// </summary>
public sealed class NavigationSuiteTravelCoordinator(
    INavigationSuiteTravelProvider provider,
    Func<bool> authorityActive,
    Func<bool> startAllowed)
{
    private static readonly TimeSpan StartObservationGrace = TimeSpan.FromSeconds(2);
    private NavigationRouteExecutionStatus status = Idle();
    private DateTimeOffset startedAt;
    private bool observedActive;
    private bool visualizationActive;

    public NavigationRouteExecutionStatus Status => status;

    public bool CanDispatch => provider.IsAvailable;

    public bool IsVisualizationAuthorized =>
        status.IsActive && visualizationActive;

    public NavigationRouteExecutionStatus Start(
        NavigationRouteSnapshot route,
        NavigationRoutePlan plan,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(plan);
        if (status.IsActive)
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "suite-route-already-running", "Stop the current suite route before starting another one.");
        if (!plan.IsValid || plan.Kind == NavigationRoutePlanKind.Review)
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false, plan.Code, plan.Message);
        if (!authorityActive())
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "navigation-authority-required", "Nexus navigation is not ready while VieriNavPlotter or another movement owner is active.");
        if (!startAllowed())
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "character-execution-blocked", "Character automation is disabled for this character.");
        if (!provider.IsAvailable)
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "suite-route-provider-unavailable", "Nexus route travel requires vnavmesh to be loaded.");

        string request;
        try
        {
            request = NavigationSuiteRouteRequest.Create(route, plan.Kind);
        }
        catch (ArgumentException ex)
        {
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "suite-route-request-invalid", ex.Message);
        }

        SuiteRouteDispatchResult dispatch = provider.Dispatch(request);
        if (!dispatch.Started)
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "suite-route-not-started", dispatch.Message);

        startedAt = now;
        observedActive = false;
        visualizationActive = true;
        return Set(NavigationRouteExecutionState.Running, plan, true, true,
            "suite-route-running", dispatch.Message);
    }

    public NavigationRouteExecutionStatus Update(DateTimeOffset now)
    {
        if (!status.IsActive)
            return status;
        if (!provider.IsAvailable)
        {
            visualizationActive = false;
            return Set(NavigationRouteExecutionState.Failed, status, false, false,
                "suite-route-provider-lost", "vnavmesh became unavailable; Nexus will not replay the route.");
        }

        SuiteRouteProviderObservation observation;
        try
        {
            observation = provider.Observe(now);
        }
        catch
        {
            observation = new SuiteRouteProviderObservation(
                SuiteRouteProviderState.Failed,
                "suite-route-provider-observation-failed",
                "Nexus could not observe the route provider; the trip was stopped and will not replay.",
                false);
        }
        visualizationActive = observation.VisualizationActive;
        if (observation.IsActive)
        {
            observedActive = true;
            return Set(NavigationRouteExecutionState.Running, status, true, true,
                observation.Code, observation.Message);
        }
        if (observation.State == SuiteRouteProviderState.Failed)
        {
            visualizationActive = false;
            return Set(NavigationRouteExecutionState.Failed, status, false, false,
                observation.Code, observation.Message);
        }
        if (!observedActive && now - startedAt < StartObservationGrace)
            return status;

        return Set(NavigationRouteExecutionState.Completed, status, false, false,
            observation.Code, string.IsNullOrWhiteSpace(observation.Message)
                ? $"Completed {status.RouteName}; Nexus route travel is no longer active."
                : observation.Message);
    }

    public NavigationRouteExecutionStatus Stop()
    {
        if (!status.IsActive)
            return status;
        bool stopped = provider.Stop();
        visualizationActive = false;
        return Set(stopped ? NavigationRouteExecutionState.Completed : NavigationRouteExecutionState.Failed,
            status, false, false,
            stopped ? "suite-route-stopped" : "suite-route-stop-unconfirmed",
            stopped
                ? "Suite route travel stopped. It will not resume or replay automatically."
                : "Suite route travel could not be confirmed stopped. The route remains blocked until its provider reports inactive.");
    }

    public void Shutdown()
    {
        if (status.IsActive)
            Stop();
    }

    public void ResetInactive()
    {
        if (!status.IsActive)
        {
            status = Idle();
            visualizationActive = false;
        }
    }

    private NavigationRouteExecutionStatus Set(
        NavigationRouteExecutionState state,
        NavigationRoutePlan plan,
        bool active,
        bool canStop,
        string code,
        string message) => status = new(
            state, plan.RouteId, plan.RouteName, active, canStop, code, message);

    private NavigationRouteExecutionStatus Set(
        NavigationRouteExecutionState state,
        NavigationRouteExecutionStatus prior,
        bool active,
        bool canStop,
        string code,
        string message) => status = prior with
        {
            State = state,
            IsActive = active,
            CanStop = canStop,
            Code = code,
            Message = message,
        };

    private static NavigationRouteExecutionStatus Idle() => new(
        NavigationRouteExecutionState.Idle, null, null, false, false,
        "suite-route-idle", "No Nexus suite route is running.");
}
