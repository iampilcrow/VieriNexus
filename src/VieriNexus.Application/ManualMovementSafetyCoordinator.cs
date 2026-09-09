namespace VieriNexus.Application;

public enum ManualMovementSafetyState
{
    Disabled,
    Monitoring,
    ManualControlDetected,
    ManualControlQuietPeriod,
    StoppingForManualControl,
    AwaitingExplicitResume,
}

public sealed record ManualMovementSafetyResult(
    ManualMovementSafetyState State,
    bool IsExecutionBlocked,
    bool CanStartExecution,
    bool RequiresExplicitResume,
    long? LastManualInputAt,
    NavigationStopResult? StopResult,
    string Code,
    string Message);

/// <summary>
/// Gives physical player movement priority over Nexus navigation. A manual takeover during a
/// tracked execution latches until Stop is verified and a future caller explicitly acknowledges
/// resume after the configured quiet period. This class never resumes execution on its own.
/// </summary>
public sealed class ManualMovementSafetyCoordinator(NavigationStopCoordinator stop)
{
    private readonly object sync = new();
    private long? lastManualInputAt;
    private long? lastObservedAt;
    private bool manualYieldLatched;

    public bool IsYieldLatched
    {
        get
        {
            lock (sync)
                return manualYieldLatched;
        }
    }

    public ManualMovementSafetyResult Update(
        long now,
        bool protectionEnabled,
        bool manualMovementInputActive,
        TimeSpan quietPeriod)
    {
        if (quietPeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(quietPeriod));

        lock (sync)
        {
            if (lastObservedAt is { } previous && now < previous)
                throw new ArgumentOutOfRangeException(nameof(now), "Observation time must be monotonic.");
            lastObservedAt = now;

            if (manualMovementInputActive)
                lastManualInputAt = now;

            if (!protectionEnabled)
            {
                if (stop.HasTrackedExecution)
                {
                    manualYieldLatched = true;
                    return StopTrackedExecution(
                        "manual-protection-disabled",
                        "Manual movement protection was disabled while navigation was tracked; Stop is required.");
                }

                return Result(
                    ManualMovementSafetyState.Disabled,
                    blocked: true,
                    canStart: false,
                    requiresResume: manualYieldLatched,
                    stopResult: null,
                    "manual-protection-disabled",
                    "Manual movement protection must be enabled before navigation can start.");
            }

            if (manualMovementInputActive && stop.HasTrackedExecution)
                manualYieldLatched = true;

            if (manualYieldLatched)
            {
                if (stop.HasTrackedExecution)
                {
                    return StopTrackedExecution(
                        "manual-yield-stopping",
                        "Player movement took priority; Nexus is stopping navigation and retaining ownership until Stop is confirmed.");
                }

                return Result(
                    ManualMovementSafetyState.AwaitingExplicitResume,
                    blocked: true,
                    canStart: false,
                    requiresResume: true,
                    stopResult: null,
                    "manual-yield-awaiting-resume",
                    "Navigation yielded to the player and remains paused until an explicit resume decision.");
            }

            if (manualMovementInputActive)
            {
                return Result(
                    ManualMovementSafetyState.ManualControlDetected,
                    blocked: true,
                    canStart: false,
                    requiresResume: false,
                    stopResult: null,
                    "manual-control-active",
                    "Player movement input is active; Nexus navigation cannot start.");
            }

            if (!QuietPeriodElapsed(now, quietPeriod))
            {
                return Result(
                    ManualMovementSafetyState.ManualControlQuietPeriod,
                    blocked: true,
                    canStart: false,
                    requiresResume: false,
                    stopResult: null,
                    "manual-control-quiet-period",
                    "Nexus is waiting for the manual-control quiet period before navigation can start.");
            }

            return Result(
                ManualMovementSafetyState.Monitoring,
                blocked: false,
                canStart: true,
                requiresResume: false,
                stopResult: null,
                "manual-control-monitoring",
                "Manual movement protection is monitoring player input.");
        }
    }

    public bool TryAcknowledgeResume(long now, TimeSpan quietPeriod)
    {
        if (quietPeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(quietPeriod));

        lock (sync)
        {
            if (!manualYieldLatched || stop.HasTrackedExecution || !QuietPeriodElapsed(now, quietPeriod))
                return false;

            manualYieldLatched = false;
            return true;
        }
    }

    private ManualMovementSafetyResult StopTrackedExecution(string code, string message)
    {
        NavigationStopResult stopResult = stop.Stop();
        ManualMovementSafetyState state = stop.HasTrackedExecution
            ? ManualMovementSafetyState.StoppingForManualControl
            : ManualMovementSafetyState.AwaitingExplicitResume;
        return Result(
            state,
            blocked: true,
            canStart: false,
            requiresResume: true,
            stopResult,
            code,
            stopResult.IsStopConfirmed ? $"{message} Stop is confirmed." : $"{message} Stop is not yet confirmed.");
    }

    private bool QuietPeriodElapsed(long now, TimeSpan quietPeriod) =>
        lastManualInputAt is not { } inputAt || now - inputAt >= quietPeriod.TotalMilliseconds;

    private ManualMovementSafetyResult Result(
        ManualMovementSafetyState state,
        bool blocked,
        bool canStart,
        bool requiresResume,
        NavigationStopResult? stopResult,
        string code,
        string message) => new(
            state,
            blocked,
            canStart,
            requiresResume,
            lastManualInputAt,
            stopResult,
            code,
            message);
}
