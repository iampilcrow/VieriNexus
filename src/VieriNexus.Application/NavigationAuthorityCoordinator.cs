using VieriNexus.Domain;

namespace VieriNexus.Application;

public sealed record NavigationAuthorityPrerequisites(
    bool HasVerifiedStagedLibrary,
    bool SourcePluginLoaded,
    bool RequiredDependenciesReady,
    bool StopAvailable,
    bool ManualMovementYieldingAvailable,
    bool ReloadReconciliationAvailable);

public enum NavigationAuthorityState
{
    StagingOnly,
    ReadyForExplicitApproval,
    ActiveWithoutExecution,
    Blocked,
    Revoked,
}

public sealed record NavigationAuthorityStatus(
    NavigationAuthorityState State,
    bool IsActive,
    bool CanApprove,
    string Code,
    string Message);

public sealed record NavigationExecutionStartResult(
    bool Success,
    string Code,
    string Message,
    ResourceLeaseHandle? Lease);

/// <summary>
/// Makes Nexus the route authority whenever its working library and providers are ready and
/// VieriNavPlotter is not loaded. It never disables the predecessor and never persists a claim.
/// An execution can begin only through the same lock that re-checks source authority,
/// atomically acquires Navigation/Movement, and arms the durable no-replay safety journal.
/// </summary>
public sealed class NavigationAuthorityCoordinator
{
    private readonly object sync = new();
    private readonly ResourceLeaseManager leases;
    private readonly NavigationStopCoordinator stop;
    private readonly NavigationExecutionSafetyCoordinator executionSafety;
    private readonly Func<NavigationAuthorityPrerequisites> observe;
    private bool active;
    private NavigationAuthorityStatus status = Staging("activation-not-reviewed",
        "Nexus navigation is waiting for its route library and providers.");

    public NavigationAuthorityCoordinator(
        ResourceLeaseManager leases,
        NavigationStopCoordinator stop,
        NavigationExecutionSafetyCoordinator executionSafety,
        Func<NavigationAuthorityPrerequisites> observe)
    {
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(executionSafety);
        ArgumentNullException.ThrowIfNull(observe);
        this.leases = leases;
        this.stop = stop;
        this.executionSafety = executionSafety;
        this.observe = observe;
    }

    public NavigationAuthorityStatus Status
    {
        get
        {
            lock (sync)
                return status;
        }
    }

    public bool IsActive => Status.IsActive;

    public NavigationAuthorityStatus Update()
    {
        lock (sync)
        {
            NavigationAuthorityPrerequisites inputs = ObserveSafely();
            string? blocker = Blocker(inputs);
            bool resourceConflict = HasNavigationConflict();

            if (active && (blocker is not null || resourceConflict))
                return Revoke(blocker ?? "Navigation or Movement ownership was acquired by another task.");

            if (active)
            {
                status = new(
                    NavigationAuthorityState.ActiveWithoutExecution,
                    true,
                    false,
                    "authority-active",
                    "Nexus owns navigation for this session. Guarded route controls are available; no route starts automatically.");
                return status;
            }

            if (blocker is not null)
            {
                status = Blocked(blocker);
                return status;
            }
            if (resourceConflict)
            {
                status = Blocked("Navigation or Movement is already owned by another task.", "activation-resource-conflict");
                return status;
            }

            active = true;
            status = new(
                NavigationAuthorityState.ActiveWithoutExecution,
                true,
                false,
                "authority-auto-active",
                "Nexus routes are ready. VieriNavPlotter is not loaded, so no manual ownership approval is needed.");
            return status;
        }
    }

    public NavigationAuthorityStatus Approve()
    {
        lock (sync)
        {
            return Update();
        }
    }

    public NavigationAuthorityStatus ReturnToStaging(DateTimeOffset now)
    {
        lock (sync)
        {
            NavigationExecutionSafetyStatus safety = executionSafety.Shutdown(now);
            active = false;
            if (safety.State is NavigationExecutionSafetyState.RecoveringReload or
                NavigationExecutionSafetyState.StoppingExpiredLease or
                NavigationExecutionSafetyState.Faulted)
            {
                status = new(
                    NavigationAuthorityState.Revoked,
                    false,
                    false,
                    safety.Code,
                    "Nexus authority was revoked, but Stop is not yet confirmed; execution remains blocked.");
                return status;
            }

            status = Staging("authority-released",
                "Nexus navigation ownership was released for shutdown. No source plugin was enabled or changed.");
            return status;
        }
    }

    /// <summary>
    /// Future route execution must enter through this gate. On success the returned lease
    /// is already journaled and tracked by verified Stop, but no provider call has occurred.
    /// </summary>
    public NavigationExecutionStartResult TryBeginExecution(
        Guid routeId,
        LeaseOwner owner,
        TimeSpan leaseLifetime,
        DateTimeOffset now)
    {
        if (routeId == Guid.Empty)
            throw new ArgumentException("A route ID is required.", nameof(routeId));
        if (leaseLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseLifetime));

        lock (sync)
        {
            if (!active)
            {
                NavigationAuthorityStatus automatic = Update();
                if (!automatic.IsActive)
                    return FailedStart("authority-not-active", automatic.Message);
            }

            NavigationAuthorityPrerequisites inputs = ObserveSafely();
            string? blocker = Blocker(inputs);
            if (blocker is not null)
            {
                Revoke(blocker);
                return FailedStart("authority-prerequisite-lost", blocker);
            }

            if (!leases.TryAcquire(
                    owner,
                    [ResourceKind.Navigation],
                    leaseLifetime,
                    out ResourceLeaseHandle? lease,
                    out ResourceLeaseSnapshot? conflict))
            {
                return FailedStart(
                    "execution-resource-conflict",
                    conflict is null
                        ? "Navigation and Movement ownership could not be acquired."
                        : $"Navigation or Movement is owned by task {conflict.Owner.TaskId}.");
            }

            try
            {
                executionSafety.BeginExecution(routeId, lease!, now);
                return new(
                    true,
                    "execution-safety-armed",
                    "Navigation and Movement are atomically owned and no-replay safety is armed; no provider movement has started.",
                    lease);
            }
            catch (Exception ex)
            {
                lease!.Dispose();
                active = false;
                status = new(
                    NavigationAuthorityState.Revoked,
                    false,
                    false,
                    "execution-safety-arm-failed",
                    "Nexus authority was revoked because execution safety could not be armed.");
                return FailedStart("execution-safety-arm-failed", ex.Message);
            }
        }
    }

    private NavigationAuthorityStatus Revoke(string reason)
    {
        active = false;
        NavigationExecutionSafetyStatus? result = stop.HasTrackedExecution
            ? executionSafety.Shutdown(DateTimeOffset.UtcNow)
            : null;
        bool stopUnconfirmed = result?.State is
            NavigationExecutionSafetyState.RecoveringReload or
            NavigationExecutionSafetyState.StoppingExpiredLease or
            NavigationExecutionSafetyState.Faulted;
        status = new(
            NavigationAuthorityState.Revoked,
            false,
            false,
            stopUnconfirmed ? result!.Code : "authority-revoked",
            stopUnconfirmed
                ? $"{reason} Nexus authority was revoked and Stop remains latched until inactivity is confirmed."
                : $"{reason} Nexus authority was revoked without starting or replaying movement.");
        return status;
    }

    private NavigationAuthorityPrerequisites ObserveSafely()
    {
        try
        {
            return observe();
        }
        catch
        {
            return new(false, true, false, false, false, false);
        }
    }

    private bool HasNavigationConflict()
    {
        Guid? trackedLeaseId = executionSafety.TrackedLeaseId;
        return leases.Snapshot().Any(lease =>
            lease.LeaseId != trackedLeaseId &&
            (lease.Resources.Contains(ResourceKind.Navigation) ||
             lease.Resources.Contains(ResourceKind.Movement)));
    }

    private static string? Blocker(NavigationAuthorityPrerequisites inputs)
    {
        if (!inputs.HasVerifiedStagedLibrary)
            return "A verified staged route library is required.";
        if (inputs.SourcePluginLoaded)
            return "VieriNavPlotter is loaded and remains the navigation owner. Disable it to use Nexus routes.";
        if (!inputs.RequiredDependenciesReady)
            return "Required navigation providers are not ready.";
        if (!inputs.StopAvailable)
            return "Verified Stop is unavailable.";
        if (!inputs.ManualMovementYieldingAvailable)
            return "Manual movement yielding is unavailable or disabled.";
        if (!inputs.ReloadReconciliationAvailable)
            return "Reload recovery and the lease watchdog are not ready.";
        return null;
    }

    private static NavigationAuthorityStatus Staging(string code, string message) =>
        new(NavigationAuthorityState.StagingOnly, false, false, code, message);

    private static NavigationAuthorityStatus Blocked(
        string message,
        string code = "authority-prerequisite-blocked") =>
        new(NavigationAuthorityState.Blocked, false, false, code, message);

    private static NavigationExecutionStartResult FailedStart(string code, string message) =>
        new(false, code, message, null);
}
