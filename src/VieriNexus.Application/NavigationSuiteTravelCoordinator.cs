namespace VieriNexus.Application;

public readonly record struct SuiteRouteDispatchResult(bool Started, string Message);

public interface INavigationSuiteTravelProvider
{
    bool IsAvailable { get; }

    SuiteRouteDispatchResult Dispatch(string requestJson);

    bool Stop();

    bool IsRouteVisualizationActive();
}

/// <summary>
/// Tracks only route travel explicitly dispatched by this Nexus process. Other suite-provider and
/// vnavmesh activity is never adopted, stopped, or visualized.
/// </summary>
public sealed class NavigationSuiteTravelCoordinator(
    INavigationSuiteTravelProvider provider,
    Func<bool> authorityActive,
    Func<bool> executionAllowed)
{
    private static readonly TimeSpan StartObservationGrace = TimeSpan.FromSeconds(2);
    private NavigationRouteExecutionStatus status = Idle();
    private DateTimeOffset startedAt;
    private bool observedActive;

    public NavigationRouteExecutionStatus Status => status;

    public bool CanDispatch => provider.IsAvailable;

    public bool IsVisualizationAuthorized =>
        status.IsActive && provider.IsRouteVisualizationActive();

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
        if (!executionAllowed())
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "character-execution-blocked", "Character automation or manual-movement safety currently blocks route execution.");
        if (!provider.IsAvailable)
            return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                "suite-route-provider-unavailable", "Cross-zone route travel requires VieriAutoDuty to be loaded.");

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
        return Set(NavigationRouteExecutionState.Running, plan, true, true,
            "suite-route-running", dispatch.Message);
    }

    public NavigationRouteExecutionStatus Update(DateTimeOffset now)
    {
        if (!status.IsActive)
            return status;
        if (!executionAllowed())
        {
            bool stopped = provider.Stop();
            return Set(stopped ? NavigationRouteExecutionState.Completed : NavigationRouteExecutionState.Failed,
                status, false, false,
                stopped ? "suite-route-safety-stopped" : "suite-route-safety-stop-unconfirmed",
                stopped
                    ? "Player control took priority and stopped the complete Nexus route trip."
                    : "Nexus could not confirm the suite route stopped; use VieriAutoDuty Stop before starting other movement.");
        }
        if (!provider.IsAvailable)
            return Set(NavigationRouteExecutionState.Failed, status, false, false,
                "suite-route-provider-lost", "VieriAutoDuty became unavailable; Nexus will not replay the route.");

        bool active = provider.IsRouteVisualizationActive();
        if (active)
        {
            observedActive = true;
            return status;
        }
        if (!observedActive && now - startedAt < StartObservationGrace)
            return status;

        return Set(NavigationRouteExecutionState.Completed, status, false, false,
            "suite-route-completed", $"Completed {status.RouteName}; suite travel is no longer active.");
    }

    public NavigationRouteExecutionStatus Stop()
    {
        if (!status.IsActive)
            return status;
        bool stopped = provider.Stop();
        return Set(stopped ? NavigationRouteExecutionState.Completed : NavigationRouteExecutionState.Failed,
            status, false, false,
            stopped ? "suite-route-stopped" : "suite-route-stop-unconfirmed",
            stopped
                ? "Suite route travel stopped. It will not resume or replay automatically."
                : "Suite route travel could not be confirmed stopped; use VieriAutoDuty Stop before starting other movement.");
    }

    public void Shutdown()
    {
        if (status.IsActive)
            Stop();
    }

    public void ResetInactive()
    {
        if (!status.IsActive)
            status = Idle();
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
