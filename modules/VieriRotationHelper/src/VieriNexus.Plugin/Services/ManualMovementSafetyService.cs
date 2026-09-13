using VieriNexus.Application;
namespace VieriNexus.Services;

/// <summary>
/// Nexus routes use explicit Stop only. This compatibility service keeps the existing
/// diagnostics/activation seam stable without observing or acting on movement input.
/// </summary>
internal sealed class ManualMovementSafetyService
{
    internal ManualMovementSafetyResult Current { get; private set; } = new(
        ManualMovementSafetyState.Monitoring,
        IsExecutionBlocked: false,
        CanStartExecution: true,
        RequiresExplicitResume: false,
        LastManualInputAt: null,
        StopResult: null,
        "explicit-stop-only",
        "Manual movement does not stop Nexus routes; use the route Stop button.");

    internal bool IsReadyForActivation => true;

    internal TimeSpan QuietPeriod => TimeSpan.Zero;

    internal void Update(long now) { }
}
