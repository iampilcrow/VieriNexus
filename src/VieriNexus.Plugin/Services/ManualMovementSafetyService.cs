using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class ManualMovementSafetyService(
    Configuration configuration,
    WorldStateStore world,
    GameManualMovementInputSource input,
    ManualMovementSafetyCoordinator coordinator)
{
    private static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromMilliseconds(1500);
    private bool observationHealthy;

    internal ManualMovementSafetyResult Current { get; private set; } = new(
        ManualMovementSafetyState.Disabled,
        IsExecutionBlocked: true,
        CanStartExecution: false,
        RequiresExplicitResume: false,
        LastManualInputAt: null,
        StopResult: null,
        "manual-control-unavailable",
        "Manual movement protection is waiting for a ready character.");

    internal bool IsReadyForActivation => observationHealthy && Settings().Enabled;

    internal TimeSpan QuietPeriod => Settings().QuietPeriod;

    internal void Update(long now)
    {
        (bool enabled, TimeSpan quietPeriod) = Settings();
        bool movementInput = false;
        observationHealthy = false;
        if (InputAvailable)
        {
            try
            {
                movementInput = input.IsMovementInputActive();
                observationHealthy = true;
            }
            catch
            {
                enabled = false;
            }
        }
        else
        {
            enabled = false;
        }

        Current = coordinator.Update(now, enabled, movementInput, quietPeriod);
    }

    private bool InputAvailable
    {
        get
        {
            try
            {
                return input.IsAvailable;
            }
            catch
            {
                return false;
            }
        }
    }

    private (bool Enabled, TimeSpan QuietPeriod) Settings()
    {
        CharacterKey key = world.Current.Character.Value?.Key ?? default;
        if (!key.IsKnown)
            return (false, DefaultQuietPeriod);

        if (!configuration.Characters.TryGetValue(key.ToString(), out CharacterConfiguration? character))
            return (true, DefaultQuietPeriod);

        int milliseconds = Math.Clamp(character.ManualControlQuietPeriodMs, 250, 10_000);
        return (character.PauseOnManualMovement, TimeSpan.FromMilliseconds(milliseconds));
    }
}
