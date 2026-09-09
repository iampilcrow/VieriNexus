namespace VieriNexus.Application;

public enum NavigationStopState
{
    AlreadyStopped,
    ProviderUnavailable,
    StopRequestFailed,
    ConfirmationUnavailable,
    StillMoving,
    Stopped,
    OwnershipLost,
}

public sealed record NavigationStopResult(
    NavigationStopState State,
    bool IsStopConfirmed,
    bool ProviderStopRequested,
    bool? ProviderReportsMovementActive,
    bool NavigationLeaseRetained,
    bool NavigationLeaseReleased,
    string Code,
    string Message);

public interface INavigationStopProvider
{
    bool IsAvailable { get; }

    void RequestStop();

    bool? IsMovementActive();
}

/// <summary>
/// Owns the safety-critical end of a tracked navigation execution. A successful provider
/// Stop call is not enough to release ownership: the provider must also report that movement
/// is inactive. Unavailable, failed, unknown, and still-moving outcomes retain the lease.
/// </summary>
public sealed class NavigationStopCoordinator
{
    private readonly object sync = new();
    private readonly INavigationStopProvider provider;
    private readonly TimeSpan retainedLeaseLifetime;
    private ResourceLeaseHandle? activeLease;

    public NavigationStopCoordinator(
        INavigationStopProvider provider,
        TimeSpan retainedLeaseLifetime)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (retainedLeaseLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retainedLeaseLifetime));

        this.provider = provider;
        this.retainedLeaseLifetime = retainedLeaseLifetime;
    }

    public bool HasTrackedExecution
    {
        get
        {
            lock (sync)
                return activeLease is not null;
        }
    }

    public bool IsProviderAvailable
    {
        get
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
    }

    public void TrackExecution(ResourceLeaseHandle navigationLease)
    {
        ArgumentNullException.ThrowIfNull(navigationLease);
        lock (sync)
        {
            if (activeLease is not null)
                throw new InvalidOperationException("A navigation execution is already being tracked.");

            activeLease = navigationLease;
        }
    }

    public NavigationStopResult Stop()
    {
        lock (sync)
        {
            if (activeLease is null)
            {
                return Result(
                    NavigationStopState.AlreadyStopped,
                    confirmed: true,
                    requested: false,
                    movementActive: null,
                    retained: false,
                    released: false,
                    "already-stopped",
                    "No Nexus navigation execution is active.");
            }

            if (!IsProviderAvailable)
            {
                return RetainOrReportOwnershipLoss(
                    NavigationStopState.ProviderUnavailable,
                    requested: false,
                    movementActive: null,
                    "stop-provider-unavailable",
                    "The navigation provider is unavailable; ownership remains held until Stop can be confirmed.");
            }

            try
            {
                provider.RequestStop();
            }
            catch
            {
                return RetainOrReportOwnershipLoss(
                    NavigationStopState.StopRequestFailed,
                    requested: true,
                    movementActive: null,
                    "stop-request-failed",
                    "The navigation provider did not accept Stop; ownership remains held.");
            }

            bool? movementActive;
            try
            {
                movementActive = provider.IsMovementActive();
            }
            catch
            {
                movementActive = null;
            }

            if (movementActive is false)
            {
                activeLease.Dispose();
                activeLease = null;
                return Result(
                    NavigationStopState.Stopped,
                    confirmed: true,
                    requested: true,
                    movementActive: false,
                    retained: false,
                    released: true,
                    "stopped-confirmed",
                    "The provider confirmed movement is inactive and navigation ownership was released.");
            }

            return movementActive is true
                ? RetainOrReportOwnershipLoss(
                    NavigationStopState.StillMoving,
                    requested: true,
                    movementActive: true,
                    "stop-still-moving",
                    "Stop was requested, but movement is still active; ownership remains held.")
                : RetainOrReportOwnershipLoss(
                    NavigationStopState.ConfirmationUnavailable,
                    requested: true,
                    movementActive: null,
                    "stop-unconfirmed",
                    "Stop was requested, but inactive movement could not be confirmed; ownership remains held.");
        }
    }

    private NavigationStopResult RetainOrReportOwnershipLoss(
        NavigationStopState state,
        bool requested,
        bool? movementActive,
        string code,
        string message)
    {
        bool retained = activeLease!.Heartbeat(retainedLeaseLifetime);
        return retained
            ? Result(state, false, requested, movementActive, true, false, code, message)
            : Result(
                NavigationStopState.OwnershipLost,
                confirmed: false,
                requested,
                movementActive,
                retained: false,
                released: false,
                "stop-ownership-lost",
                "Stop is unconfirmed and the navigation lease could not be retained. Execution must remain blocked.");
    }

    private static NavigationStopResult Result(
        NavigationStopState state,
        bool confirmed,
        bool requested,
        bool? movementActive,
        bool retained,
        bool released,
        string code,
        string message) => new(
            state,
            confirmed,
            requested,
            movementActive,
            retained,
            released,
            code,
            message);
}
