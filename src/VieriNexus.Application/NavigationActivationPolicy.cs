using VieriNexus.Domain;

namespace VieriNexus.Application;

public enum NavigationActivationState
{
    StagingRequired,
    SourceAuthoritative,
    DependenciesRequired,
    ResourceConflict,
    SafetyInfrastructureIncomplete,
    AwaitingExplicitApproval,
    ReadyForActivation,
    Active,
    UnsafeConflict,
}

public sealed record NavigationActivationInputs(
    bool HasVerifiedStagedLibrary,
    bool SourcePluginInstalled,
    bool SourcePluginLoaded,
    bool RequiredDependenciesReady,
    bool ResourceOwnershipConnected,
    bool HasNavigationOrMovementLeaseConflict,
    bool StopAvailable,
    bool ManualOverrideAvailable,
    bool ReloadReconciliationAvailable,
    bool ExplicitActivationApproved,
    bool NexusExecutionEnabled);

public sealed record NavigationActivationBlocker(string Code, string Message);

public sealed record NavigationActivationAssessment(
    NavigationActivationState State,
    bool CanActivate,
    bool IsSourcePluginInstalled,
    bool IsSourcePluginLoaded,
    bool IsSourcePluginAuthoritative,
    bool IsStopAvailable,
    IReadOnlyList<NavigationActivationBlocker> Blockers);

public static class NavigationActivationPolicy
{
    public static NavigationActivationAssessment Evaluate(NavigationActivationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        List<NavigationActivationBlocker> blockers = [];

        AddIf(!inputs.HasVerifiedStagedLibrary, "staged-library-required",
            "Import and verify a staged route library first.", blockers);
        AddIf(inputs.SourcePluginLoaded, "source-plugin-loaded",
            "VieriNavPlotter is loaded and remains the navigation owner.", blockers);
        AddIf(!inputs.RequiredDependenciesReady, "dependencies-required",
            "Required navigation providers are not all ready.", blockers);
        AddIf(!inputs.ResourceOwnershipConnected, "ownership-unavailable",
            "Navigation and Movement ownership is not connected.", blockers);
        AddIf(inputs.HasNavigationOrMovementLeaseConflict, "resource-conflict",
            "Navigation or Movement is already owned by another task.", blockers);
        AddIf(!inputs.StopAvailable, "stop-required",
            "A verified Stop path must exist before route execution can activate.", blockers);
        AddIf(!inputs.ManualOverrideAvailable, "manual-override-required",
            "Manual movement detection and safe yielding must be implemented first.", blockers);
        AddIf(!inputs.ReloadReconciliationAvailable, "reload-reconciliation-required",
            "Reload reconciliation must recover intent without replaying unsafe movement.", blockers);
        AddIf(!inputs.ExplicitActivationApproved, "explicit-approval-required",
            "Activation requires a separate explicit user decision after safety validation.", blockers);

        bool prerequisitesSatisfied = blockers.Count == 0;
        NavigationActivationState state = ResolveState(inputs, blockers, prerequisitesSatisfied);
        bool sourceAuthoritative = inputs.SourcePluginLoaded && !inputs.NexusExecutionEnabled;
        return new(
            state,
            !inputs.NexusExecutionEnabled && prerequisitesSatisfied,
            inputs.SourcePluginInstalled,
            inputs.SourcePluginLoaded,
            sourceAuthoritative,
            inputs.StopAvailable,
            blockers);
    }

    private static NavigationActivationState ResolveState(
        NavigationActivationInputs inputs,
        IReadOnlyList<NavigationActivationBlocker> blockers,
        bool prerequisitesSatisfied)
    {
        if (inputs.NexusExecutionEnabled)
            return blockers.Count > 0
                ? NavigationActivationState.UnsafeConflict
                : NavigationActivationState.Active;
        if (!inputs.HasVerifiedStagedLibrary)
            return NavigationActivationState.StagingRequired;
        if (inputs.SourcePluginLoaded)
            return NavigationActivationState.SourceAuthoritative;
        if (!inputs.RequiredDependenciesReady)
            return NavigationActivationState.DependenciesRequired;
        if (inputs.HasNavigationOrMovementLeaseConflict)
            return NavigationActivationState.ResourceConflict;
        if (!inputs.ResourceOwnershipConnected || !inputs.StopAvailable ||
            !inputs.ManualOverrideAvailable || !inputs.ReloadReconciliationAvailable)
            return NavigationActivationState.SafetyInfrastructureIncomplete;
        if (!inputs.ExplicitActivationApproved)
            return NavigationActivationState.AwaitingExplicitApproval;
        return prerequisitesSatisfied
            ? NavigationActivationState.ReadyForActivation
            : NavigationActivationState.SafetyInfrastructureIncomplete;
    }

    private static void AddIf(
        bool condition,
        string code,
        string message,
        ICollection<NavigationActivationBlocker> blockers)
    {
        if (condition)
            blockers.Add(new(code, message));
    }
}
