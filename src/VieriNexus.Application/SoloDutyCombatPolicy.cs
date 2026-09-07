namespace VieriNexus.Application;

/// <summary>
/// Defines the provider-neutral combat handoff used when progression enters a solo duty.
/// The rotation provider chooses action targets; the encounter provider retains movement ownership.
/// </summary>
public static class SoloDutyCombatPolicy
{
    public static TimeSpan TargetFallbackDelay => TimeSpan.FromSeconds(2);
    public static TimeSpan TargetFallbackPulseDuration => TimeSpan.FromSeconds(1.5);
    public static TimeSpan TargetFallbackRetryDelay => TimeSpan.FromSeconds(2);
    public static bool RequiresFreshRotationAutomationHandoff => true;
    public static bool AllowsHardTargetMutation => false;
    public static string PrimaryRotationTargetingMode => "SelectedTarget";
    public static string FallbackRotationTargetingMode => "NearestHostile";
    public static string MovementOwner => "EncounterProvider";

    public static bool ShouldEnableFallback(bool hasUsableHostileTarget, TimeSpan missingFor, bool retryAllowed) =>
        !hasUsableHostileTarget && retryAllowed && missingFor >= TargetFallbackDelay;

    public static bool ShouldDisableFallback(bool hasUsableHostileTarget, TimeSpan enabledFor) =>
        hasUsableHostileTarget || enabledFor >= TargetFallbackPulseDuration;
}
