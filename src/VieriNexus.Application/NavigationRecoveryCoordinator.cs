namespace VieriNexus.Application;

public sealed record NavigationRecoveryStatus(
    bool IsRequired,
    bool CanAcknowledge,
    bool ExecutionIntentPending,
    bool ManualYieldPending,
    string Code,
    string Message);

/// <summary>
/// Combines the two independent no-replay latches. Acknowledgement can only clear
/// already-stopped intent after ownership is released and the manual-input quiet period
/// has elapsed. It has no resume or movement operation.
/// </summary>
public sealed class NavigationRecoveryCoordinator(
    NavigationExecutionSafetyCoordinator executionSafety,
    ManualMovementSafetyCoordinator manualMovement,
    NavigationStopCoordinator stop)
{
    private readonly object sync = new();

    public NavigationRecoveryStatus Observe(long now, TimeSpan quietPeriod)
    {
        if (quietPeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(quietPeriod));

        lock (sync)
        {
            bool executionPending = executionSafety.CanAcknowledgeInterruptedIntent;
            bool manualPending = manualMovement.IsYieldLatched;
            if (!executionPending && !manualPending)
            {
                return new(
                    false,
                    false,
                    false,
                    false,
                    "no-recovery-checkpoint",
                    "No stopped navigation intent is waiting for acknowledgement.");
            }

            if (stop.HasTrackedExecution)
            {
                return new(
                    true,
                    false,
                    executionPending,
                    manualPending,
                    "recovery-stop-unconfirmed",
                    "Stop is not yet confirmed. The checkpoint cannot be cleared while ownership is retained.");
            }

            if (manualPending && !manualMovement.CanAcknowledgeResume(now, quietPeriod))
            {
                return new(
                    true,
                    false,
                    executionPending,
                    true,
                    "recovery-manual-quiet-period",
                    "Player control remains protected until the manual-movement quiet period has elapsed.");
            }

            return new(
                true,
                true,
                executionPending,
                manualPending,
                "recovery-ready-for-acknowledgement",
                "Movement is confirmed inactive. Acknowledge the stopped intent without resuming or replaying it.");
        }
    }

    public NavigationRecoveryStatus Acknowledge(
        long now,
        DateTimeOffset utcNow,
        TimeSpan quietPeriod)
    {
        lock (sync)
        {
            NavigationRecoveryStatus before = Observe(now, quietPeriod);
            if (!before.CanAcknowledge)
                return before;

            bool executionCleared = !before.ExecutionIntentPending ||
                                    executionSafety.AcknowledgeInterruptedIntent(utcNow);
            bool manualCleared = !before.ManualYieldPending ||
                                 manualMovement.TryAcknowledgeResume(now, quietPeriod);
            if (!executionCleared || !manualCleared)
            {
                return new(
                    true,
                    false,
                    !executionCleared,
                    !manualCleared,
                    "recovery-acknowledgement-failed",
                    "The stopped checkpoint could not be cleared safely; navigation remains blocked.");
            }

            return new(
                false,
                false,
                false,
                false,
                "recovery-acknowledged-no-replay",
                "Stopped navigation intent was acknowledged. Nothing resumed or replayed.");
        }
    }
}
