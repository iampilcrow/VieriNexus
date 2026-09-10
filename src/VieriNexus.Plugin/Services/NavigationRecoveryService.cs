using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed class NavigationRecoveryService(
    NavigationRecoveryCoordinator coordinator,
    ManualMovementSafetyService manualMovement)
{
    internal NavigationRecoveryStatus Current { get; private set; } = new(
        false,
        false,
        false,
        false,
        "no-recovery-checkpoint",
        "No stopped navigation intent is waiting for acknowledgement.");

    internal NavigationRecoveryStatus Update(long now)
    {
        Current = coordinator.Observe(now, manualMovement.QuietPeriod);
        return Current;
    }

    internal NavigationRecoveryStatus Acknowledge(long now)
    {
        Current = coordinator.Acknowledge(
            now,
            DateTimeOffset.UtcNow,
            manualMovement.QuietPeriod);
        return Current;
    }

    internal NavigationRecoveryStatus PrepareForExplicitStart(long now)
    {
        NavigationRecoveryStatus observed = Update(now);
        return observed.CanAcknowledge ? Acknowledge(now) : observed;
    }
}
