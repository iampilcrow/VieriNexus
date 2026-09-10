using VieriNexus.Domain;

namespace VieriNexus.Application;

public interface INavigationMovementProvider
{
    bool IsAvailable { get; }

    bool IsReady { get; }

    void Start(
        IReadOnlyList<NavigationRoutePoint> points,
        bool useFlight,
        float tolerance);

    bool? IsMovementActive();

    bool? IsDestinationOwned(NavigationRoutePoint expectedDestination);
}

public enum NavigationRouteExecutionState
{
    Idle,
    Running,
    Completed,
    Stopping,
    AwaitingAcknowledgement,
    Blocked,
    Failed,
}

public sealed record NavigationRouteExecutionStatus(
    NavigationRouteExecutionState State,
    Guid? RouteId,
    string? RouteName,
    bool IsActive,
    bool CanStop,
    string Code,
    string Message);

/// <summary>
/// The sole provider-neutral route playback coordinator. It obtains session authority and
/// atomic Navigation/Movement ownership before invoking movement, heartbeats ownership while
/// active, and routes every exit through verified Stop/no-replay safety.
/// </summary>
public sealed class NavigationRouteExecutionCoordinator
{
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StartObservationGrace = TimeSpan.FromSeconds(1);
    private readonly object sync = new();
    private readonly NavigationAuthorityCoordinator authority;
    private readonly NavigationExecutionSafetyCoordinator safety;
    private readonly INavigationMovementProvider provider;
    private readonly Func<bool> executionAllowed;
    private readonly Func<uint> currentTerritory;
    private ResourceLeaseHandle? lease;
    private NavigationRoutePlan? activePlan;
    private DateTimeOffset startedAt;
    private bool observedMovementActive;
    private NavigationRouteExecutionStatus status = Idle();

    public NavigationRouteExecutionCoordinator(
        NavigationAuthorityCoordinator authority,
        NavigationExecutionSafetyCoordinator safety,
        INavigationMovementProvider provider,
        Func<bool> executionAllowed,
        Func<uint> currentTerritory)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(safety);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(executionAllowed);
        ArgumentNullException.ThrowIfNull(currentTerritory);
        this.authority = authority;
        this.safety = safety;
        this.provider = provider;
        this.executionAllowed = executionAllowed;
        this.currentTerritory = currentTerritory;
    }

    public NavigationRouteExecutionStatus Status
    {
        get
        {
            lock (sync)
                return status;
        }
    }

    public NavigationRouteExecutionStatus Start(NavigationRoutePlan plan, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (sync)
        {
            if (lease is not null || status.IsActive)
                return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                    "route-already-running", "Stop the current Nexus route before starting another one.");
            if (!plan.IsValid || !plan.IsExecutable || plan.Kind == NavigationRoutePlanKind.Review)
                return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                    plan.Code, plan.Message);
            if (currentTerritory() != plan.TerritoryId)
                return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                    "route-territory-changed", $"Travel to territory {plan.TerritoryId} before starting this route.");
            if (!executionAllowed())
                return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                    "character-execution-blocked", "Character automation or manual-movement safety currently blocks route execution.");
            if (!ProviderAvailable() || !ProviderReady())
                return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                    "movement-provider-not-ready", "vnavmesh is not ready for route playback.");

            NavigationExecutionStartResult start = authority.TryBeginExecution(
                plan.RouteId,
                new LeaseOwner(GoalId.New(), TaskId.New(), AttemptId.New(), 0,
                    $"Nexus route: {plan.RouteName}"),
                LeaseLifetime,
                now);
            if (!start.Success || start.Lease is null)
                return Set(NavigationRouteExecutionState.Blocked, plan, false, false,
                    start.Code, start.Message);

            lease = start.Lease;
            activePlan = plan;
            startedAt = now;
            observedMovementActive = false;
            try
            {
                provider.Start(plan.Points, plan.UseFlight, plan.Tolerance);
                return Set(NavigationRouteExecutionState.Running, plan, true, true,
                    "route-running", plan.Kind == NavigationRoutePlanKind.TravelToStart
                        ? $"Traveling to the first point of {plan.RouteName}."
                        : $"Playing {plan.RouteName} through all {plan.Points.Count} points.");
            }
            catch (Exception ex)
            {
                safety.InterruptExecution(now, "route-start-failed-stopped",
                    "Route start failed; movement was stopped and will not replay.");
                lease = null;
                activePlan = null;
                return Set(NavigationRouteExecutionState.Failed, plan, false, false,
                    "route-provider-start-failed", $"vnavmesh could not start the route: {ex.Message}");
            }
        }
    }

    public NavigationRouteExecutionStatus Update(DateTimeOffset now)
    {
        lock (sync)
        {
            if (lease is null || activePlan is null)
            {
                if (safety.CanAcknowledgeInterruptedIntent && status.State is
                    NavigationRouteExecutionState.Running or NavigationRouteExecutionState.Stopping)
                {
                    status = Set(NavigationRouteExecutionState.AwaitingAcknowledgement,
                        activePlan, false, false, "route-stopped-awaiting-acknowledgement",
                        "Route movement is stopped and will not resume until the checkpoint is acknowledged.");
                }
                return status;
            }

            if (!lease.Heartbeat(LeaseLifetime))
            {
                lease = null;
                NavigationRoutePlan interrupted = activePlan;
                activePlan = null;
                return Set(NavigationRouteExecutionState.Stopping, interrupted, false, false,
                    "route-lease-lost", "Route ownership heartbeat was lost; verified Stop is taking over.");
            }

            if (currentTerritory() != activePlan.TerritoryId || !ProviderAvailable())
                return Interrupt(now, "route-runtime-prerequisite-lost",
                    "Territory or provider state changed; Nexus stopped the route without replay.");
            bool? movement = ProviderMovementActive();
            if (movement is true)
            {
                observedMovementActive = true;
                bool? owned = ProviderDestinationOwned(activePlan.Points[^1]);
                if (owned is false)
                {
                    NavigationRoutePlan superseded = activePlan;
                    NavigationExecutionSafetyStatus released = safety.ReleaseSupersededExecution(now);
                    lease = null;
                    activePlan = null;
                    return Set(NavigationRouteExecutionState.Blocked, superseded, false, false,
                        released.Code,
                        "Another plugin replaced the Nexus path. Nexus yielded without stopping that plugin's movement.");
                }
                return status;
            }
            if (movement is null || (!observedMovementActive && now - startedAt < StartObservationGrace))
                return status;

            NavigationRoutePlan completed = activePlan;
            NavigationExecutionSafetyStatus completion = safety.CompleteExecution(now);
            if (completion.State != NavigationExecutionSafetyState.Ready)
                return Set(NavigationRouteExecutionState.Stopping, completed, true, true,
                    completion.Code, completion.Message);

            lease = null;
            activePlan = null;
            return Set(NavigationRouteExecutionState.Completed, completed, false, false,
                "route-completed", $"Completed {completed.RouteName}; movement is inactive and ownership was released.");
        }
    }

    public NavigationRouteExecutionStatus Stop(DateTimeOffset now)
    {
        lock (sync)
        {
            if (lease is null || activePlan is null)
                return status;

            NavigationRoutePlan interrupted = activePlan;
            NavigationExecutionSafetyStatus stopped = safety.StopByUser(now);
            bool retained = safety.TrackedLeaseId is not null;
            if (!retained)
            {
                lease = null;
                activePlan = null;
            }
            return Set(
                stopped.State == NavigationExecutionSafetyState.Ready
                    ? NavigationRouteExecutionState.Completed
                    : stopped.State == NavigationExecutionSafetyState.AwaitingExplicitResume
                        ? NavigationRouteExecutionState.AwaitingAcknowledgement
                        : NavigationRouteExecutionState.Stopping,
                interrupted,
                retained,
                retained,
                stopped.Code,
                stopped.Message);
        }
    }

    private NavigationRouteExecutionStatus Interrupt(DateTimeOffset now, string code, string message)
    {
        lock (sync)
        {
            if (lease is null || activePlan is null)
                return status;

            NavigationRoutePlan interrupted = activePlan;
            NavigationExecutionSafetyStatus stopped = safety.InterruptExecution(now, code, message);
            bool retained = safety.TrackedLeaseId is not null;
            if (!retained)
            {
                lease = null;
                activePlan = null;
            }
            return Set(
                stopped.State == NavigationExecutionSafetyState.AwaitingExplicitResume
                    ? NavigationRouteExecutionState.AwaitingAcknowledgement
                    : NavigationRouteExecutionState.Stopping,
                interrupted,
                retained,
                retained,
                stopped.Code,
                stopped.Message);
        }
    }

    private bool ProviderAvailable()
    {
        try { return provider.IsAvailable; }
        catch { return false; }
    }

    private bool ProviderReady()
    {
        try { return provider.IsReady; }
        catch { return false; }
    }

    private bool? ProviderMovementActive()
    {
        try { return provider.IsMovementActive(); }
        catch { return null; }
    }

    private bool? ProviderDestinationOwned(NavigationRoutePoint destination)
    {
        try { return provider.IsDestinationOwned(destination); }
        catch { return null; }
    }

    private NavigationRouteExecutionStatus Set(
        NavigationRouteExecutionState state,
        NavigationRoutePlan? plan,
        bool active,
        bool canStop,
        string code,
        string message) => status = new(
            state,
            plan?.RouteId,
            plan?.RouteName,
            active,
            canStop,
            code,
            message);

    private static NavigationRouteExecutionStatus Idle() => new(
        NavigationRouteExecutionState.Idle, null, null, false, false,
        "route-idle", "No Nexus route is running.");
}
