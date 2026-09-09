using VieriNexus.Domain;

namespace VieriNexus.Application;

public enum NavigationExecutionSafetyState
{
    NotObserved,
    Ready,
    RecoveringReload,
    StoppingExpiredLease,
    AwaitingExplicitResume,
    Faulted,
}

public sealed record NavigationExecutionSafetyStatus(
    NavigationExecutionSafetyState State,
    bool IsReadyForActivation,
    string Code,
    string Message);

/// <summary>
/// Connects durable execution intent, reload recovery, and active lease expiry. A reload
/// or missed heartbeat can only issue Stop and move to an explicit-resume checkpoint;
/// this coordinator has no operation that replays a route.
/// </summary>
public sealed class NavigationExecutionSafetyCoordinator
{
    private readonly object sync = new();
    private readonly ResourceLeaseManager leases;
    private readonly NavigationStopCoordinator stop;
    private readonly INavigationStopProvider provider;
    private readonly INavigationExecutionIntentStore store;
    private NavigationExecutionIntent? intent;
    private bool loaded;
    private bool currentProcessExecution;
    private bool faulted;
    private NavigationExecutionSafetyStatus status = new(
        NavigationExecutionSafetyState.NotObserved,
        false,
        "reload-not-observed",
        "Reload reconciliation and the lease watchdog have not run yet.");

    public NavigationExecutionSafetyCoordinator(
        ResourceLeaseManager leases,
        NavigationStopCoordinator stop,
        INavigationStopProvider provider,
        INavigationExecutionIntentStore store)
    {
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(store);
        this.leases = leases;
        this.stop = stop;
        this.provider = provider;
        this.store = store;
    }

    public NavigationExecutionSafetyStatus Status
    {
        get
        {
            lock (sync)
                return status;
        }
    }

    public bool IsReadyForActivation => Status.IsReadyForActivation;

    public bool CanAcknowledgeInterruptedIntent =>
        Status.State == NavigationExecutionSafetyState.AwaitingExplicitResume;

    public Guid? TrackedLeaseId
    {
        get
        {
            lock (sync)
                return currentProcessExecution ? intent?.LeaseId : null;
        }
    }

    public NavigationExecutionSafetyStatus Update(DateTimeOffset now)
    {
        lock (sync)
        {
            if (!loaded && !TryLoad())
                return status;
            if (faulted)
            {
                leases.SweepExpired(now);
                if (stop.HasTrackedExecution)
                    stop.Stop();
                return status;
            }

            IReadOnlyList<ResourceLeaseSnapshot> expired = leases.SweepExpired(now);
            ResourceLeaseSnapshot? expiredNavigation = expired.FirstOrDefault(IsTrackedNavigationLease);
            if (expiredNavigation is not null)
                return StopExpiredLease(expiredNavigation, now);

            if (currentProcessExecution && intent is not null && !stop.HasTrackedExecution)
            {
                currentProcessExecution = false;
                if (!TrySaveState(NavigationExecutionIntentState.AwaitingExplicitResume, now))
                    return status;
                status = AwaitingResume("execution-stopped-externally",
                    "Tracked navigation stopped outside the execution reconciler; automatic restart is blocked until explicit acknowledgement.");
                return status;
            }

            if (!currentProcessExecution && intent?.State is
                NavigationExecutionIntentState.Running or NavigationExecutionIntentState.StopPending)
            {
                return ReconcileReload(now);
            }

            status = intent?.State == NavigationExecutionIntentState.AwaitingExplicitResume
                ? AwaitingResume("reload-safe-checkpoint",
                    "Persisted navigation intent is stopped and awaits an explicit user decision; it will not replay automatically.")
                : Ready();
            return status;
        }
    }

    /// <summary>
    /// Future executors must call this after acquiring Navigation/Movement and before
    /// asking a provider to move. The durable record is written before the lease is tracked.
    /// </summary>
    public void BeginExecution(Guid routeId, ResourceLeaseHandle navigationLease, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(navigationLease);
        if (routeId == Guid.Empty)
            throw new ArgumentException("A route ID is required.", nameof(routeId));

        lock (sync)
        {
            if (!loaded || !status.IsReadyForActivation)
                throw new InvalidOperationException("Reload reconciliation and the lease watchdog must be ready first.");
            if (currentProcessExecution || stop.HasTrackedExecution)
                throw new InvalidOperationException("A navigation execution is already tracked.");
            if (!leases.LeaseOwns(
                    navigationLease.LeaseId,
                    ResourceKind.Navigation,
                    ResourceKind.Movement))
            {
                throw new InvalidOperationException(
                    "The execution lease must actively own both Navigation and Movement.");
            }

            var next = new NavigationExecutionIntent(
                NavigationExecutionIntent.CurrentSchemaVersion,
                Guid.NewGuid(),
                routeId,
                navigationLease.LeaseId,
                NavigationExecutionIntentState.Running,
                now);
            try
            {
                store.Save(next);
            }
            catch (Exception ex)
            {
                navigationLease.Dispose();
                faulted = true;
                status = Fault("execution-intent-write-failed",
                    $"Navigation execution was blocked because its intent could not be saved: {ex.Message}");
                throw new InvalidOperationException(status.Message, ex);
            }
            try
            {
                stop.TrackExecution(navigationLease);
                intent = next;
                currentProcessExecution = true;
                status = Ready("execution-tracked",
                    "Navigation intent is durable and its ownership lease is actively watched.");
            }
            catch
            {
                navigationLease.Dispose();
                intent = next with
                {
                    State = NavigationExecutionIntentState.AwaitingExplicitResume,
                    UpdatedAtUtc = now,
                };
                store.Save(intent);
                currentProcessExecution = false;
                status = AwaitingResume("execution-track-failed",
                    "Execution did not start and requires an explicit user decision.");
                throw;
            }
        }
    }

    public NavigationExecutionSafetyStatus Shutdown(DateTimeOffset now)
    {
        lock (sync)
        {
            if (!loaded && !TryLoad())
                return status;
            if (!currentProcessExecution || intent is null || !stop.HasTrackedExecution)
                return status;

            if (!TrySaveState(NavigationExecutionIntentState.StopPending, now))
            {
                stop.Stop();
                return status;
            }

            NavigationStopResult result = stop.Stop();
            if (result.IsStopConfirmed)
            {
                TrySaveState(NavigationExecutionIntentState.AwaitingExplicitResume, now);
                currentProcessExecution = false;
                status = AwaitingResume("shutdown-stopped",
                    "Plugin shutdown stopped navigation; the saved intent will not replay after reload.");
            }
            else
            {
                currentProcessExecution = false;
                status = Recovering("shutdown-stop-unconfirmed",
                    "Plugin shutdown requested Stop, but inactivity is not yet confirmed; reload recovery remains armed.");
            }

            return status;
        }
    }

    /// <summary>
    /// Completes the no-replay checkpoint. This is intentionally not exposed by the
    /// current UI or IPC; a future activation flow must bind it to an explicit user action.
    /// </summary>
    public bool AcknowledgeInterruptedIntent(DateTimeOffset now)
    {
        lock (sync)
        {
            if (faulted || intent?.State != NavigationExecutionIntentState.AwaitingExplicitResume)
                return false;
            if (!TrySaveState(NavigationExecutionIntentState.Completed, now))
                return false;

            status = Ready("reload-intent-acknowledged",
                "The interrupted navigation intent was explicitly acknowledged; no movement was replayed.");
            return true;
        }
    }

    private bool TryLoad()
    {
        try
        {
            intent = store.Load();
            loaded = true;
            currentProcessExecution = false;
            return true;
        }
        catch (Exception ex)
        {
            faulted = true;
            status = Fault("reload-intent-invalid",
                $"Saved navigation intent could not be verified: {ex.Message}");
            return false;
        }
    }

    private NavigationExecutionSafetyStatus ReconcileReload(DateTimeOffset now)
    {
        if (intent!.State == NavigationExecutionIntentState.Running &&
            !TrySaveState(NavigationExecutionIntentState.StopPending, now))
        {
            return status;
        }

        if (stop.HasTrackedExecution)
        {
            NavigationStopResult trackedResult = stop.Stop();
            if (!trackedResult.IsStopConfirmed)
            {
                status = Recovering(trackedResult.Code,
                    "Saved navigation intent will not replay; tracked movement Stop is still unconfirmed.");
                return status;
            }

            if (!TrySaveState(NavigationExecutionIntentState.AwaitingExplicitResume, now))
                return status;

            status = AwaitingResume("reload-stopped",
                "Reload reconciliation confirmed movement inactive; saved intent awaits an explicit decision and was not replayed.");
            return status;
        }

        if (!ProviderAvailable())
        {
            status = Recovering("reload-stop-provider-unavailable",
                "Saved navigation intent will not replay; waiting for the Stop provider before reconciliation can finish.");
            return status;
        }

        try
        {
            provider.RequestStop();
        }
        catch
        {
            status = Recovering("reload-stop-request-failed",
                "Saved navigation intent will not replay; the Stop request must be retried.");
            return status;
        }

        bool? active;
        try
        {
            active = provider.IsMovementActive();
        }
        catch
        {
            active = null;
        }

        if (active is not false)
        {
            status = Recovering(
                active is true ? "reload-still-moving" : "reload-stop-unconfirmed",
                active is true
                    ? "Saved navigation intent will not replay; movement remains active after Stop."
                    : "Saved navigation intent will not replay; inactive movement has not been confirmed.");
            return status;
        }

        if (!TrySaveState(NavigationExecutionIntentState.AwaitingExplicitResume, now))
            return status;

        status = AwaitingResume("reload-stopped",
            "Reload reconciliation confirmed movement inactive; saved intent awaits an explicit decision and was not replayed.");
        return status;
    }

    private NavigationExecutionSafetyStatus StopExpiredLease(ResourceLeaseSnapshot expired, DateTimeOffset now)
    {
        if (intent is null || intent.LeaseId != expired.LeaseId || !stop.HasTrackedExecution)
        {
            status = Fault("expired-navigation-ownership-unmatched",
                "A Navigation/Movement lease expired without a matching tracked execution; execution remains blocked.");
            return status;
        }

        if (!TrySaveState(NavigationExecutionIntentState.StopPending, now))
        {
            stop.Stop();
            return status;
        }

        NavigationStopResult result = stop.Stop();
        currentProcessExecution = false;
        if (!result.IsStopConfirmed)
        {
            status = new NavigationExecutionSafetyStatus(
                NavigationExecutionSafetyState.StoppingExpiredLease,
                false,
                result.Code,
                "The navigation lease heartbeat expired. Stop is latched and will be retried until movement is confirmed inactive.");
            return status;
        }

        if (!TrySaveState(NavigationExecutionIntentState.AwaitingExplicitResume, now))
            return status;

        status = AwaitingResume("expired-lease-stopped",
            "The navigation lease heartbeat expired; Stop was confirmed and automatic resume is blocked.");
        return status;
    }

    private bool IsTrackedNavigationLease(ResourceLeaseSnapshot lease) =>
        intent is not null &&
        lease.LeaseId == intent.LeaseId &&
        (lease.Resources.Contains(ResourceKind.Navigation) || lease.Resources.Contains(ResourceKind.Movement));

    private bool TrySaveState(NavigationExecutionIntentState state, DateTimeOffset now)
    {
        try
        {
            intent = intent! with { State = state, UpdatedAtUtc = now };
            store.Save(intent);
            return true;
        }
        catch (Exception ex)
        {
            faulted = true;
            status = Fault("reload-intent-write-failed",
                $"Navigation intent could not be saved safely: {ex.Message}");
            return false;
        }
    }

    private bool ProviderAvailable()
    {
        try
        {
            return provider.IsAvailable;
        }
        catch
        {
            return false;
        }
    }

    private static NavigationExecutionSafetyStatus Ready(
        string code = "reload-watchdog-ready",
        string message = "Reload reconciliation and the active lease watchdog are connected; stale movement intent is never replayed.") =>
        new(NavigationExecutionSafetyState.Ready, true, code, message);

    private static NavigationExecutionSafetyStatus Recovering(string code, string message) =>
        new(NavigationExecutionSafetyState.RecoveringReload, false, code, message);

    private static NavigationExecutionSafetyStatus AwaitingResume(string code, string message) =>
        new(NavigationExecutionSafetyState.AwaitingExplicitResume, false, code, message);

    private static NavigationExecutionSafetyStatus Fault(string code, string message) =>
        new(NavigationExecutionSafetyState.Faulted, false, code, message);
}
