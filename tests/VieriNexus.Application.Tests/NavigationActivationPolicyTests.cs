using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationActivationPolicyTests
{
    [Fact]
    public void LoadedSourceAndMissingSafetyInfrastructureKeepExecutionBlocked()
    {
        NavigationActivationAssessment assessment = NavigationActivationPolicy.Evaluate(Inputs() with
        {
            SourcePluginInstalled = true,
            SourcePluginLoaded = true,
            StopAvailable = false,
            ManualOverrideAvailable = false,
            ReloadReconciliationAvailable = false,
        });

        Assert.Equal(NavigationActivationState.SourceAuthoritative, assessment.State);
        Assert.False(assessment.CanActivate);
        Assert.True(assessment.IsSourcePluginInstalled);
        Assert.True(assessment.IsSourcePluginLoaded);
        Assert.True(assessment.IsSourcePluginAuthoritative);
        Assert.False(assessment.IsStopAvailable);
        Assert.False(assessment.IsManualOverrideAvailable);
        Assert.Contains(assessment.Blockers, blocker => blocker.Code == "source-plugin-loaded");
        Assert.Contains(assessment.Blockers, blocker => blocker.Code == "stop-required");
        Assert.Contains(assessment.Blockers, blocker => blocker.Code == "manual-override-required");
        Assert.Contains(assessment.Blockers, blocker => blocker.Code == "reload-reconciliation-required");
    }

    [Fact]
    public void CompletePrerequisitesStillRequireExplicitApproval()
    {
        NavigationActivationAssessment waiting = NavigationActivationPolicy.Evaluate(Inputs());
        NavigationActivationAssessment approved = NavigationActivationPolicy.Evaluate(Inputs() with
        {
            ExplicitActivationApproved = true,
        });

        Assert.Equal(NavigationActivationState.AwaitingExplicitApproval, waiting.State);
        Assert.False(waiting.CanActivate);
        Assert.True(waiting.IsStopAvailable);
        Assert.True(waiting.IsManualOverrideAvailable);
        Assert.Equal(NavigationActivationState.ReadyForActivation, approved.State);
        Assert.True(approved.CanActivate);
    }

    [Fact]
    public void ActiveExecutionWithALoadedSourceIsAnUnsafeConflict()
    {
        NavigationActivationAssessment assessment = NavigationActivationPolicy.Evaluate(Inputs() with
        {
            SourcePluginInstalled = true,
            SourcePluginLoaded = true,
            ExplicitActivationApproved = true,
            NexusExecutionEnabled = true,
        });

        Assert.Equal(NavigationActivationState.UnsafeConflict, assessment.State);
        Assert.False(assessment.CanActivate);
        Assert.False(assessment.IsSourcePluginAuthoritative);
    }

    [Fact]
    public void NavigationOrMovementLeaseConflictIsReportedBeforeActivation()
    {
        NavigationActivationAssessment assessment = NavigationActivationPolicy.Evaluate(Inputs() with
        {
            HasNavigationOrMovementLeaseConflict = true,
            ExplicitActivationApproved = true,
        });

        Assert.Equal(NavigationActivationState.ResourceConflict, assessment.State);
        Assert.Contains(assessment.Blockers, blocker => blocker.Code == "resource-conflict");
        Assert.False(assessment.CanActivate);
    }

    private static NavigationActivationInputs Inputs() => new(
        HasVerifiedStagedLibrary: true,
        SourcePluginInstalled: false,
        SourcePluginLoaded: false,
        RequiredDependenciesReady: true,
        ResourceOwnershipConnected: true,
        HasNavigationOrMovementLeaseConflict: false,
        StopAvailable: true,
        ManualOverrideAvailable: true,
        ReloadReconciliationAvailable: true,
        ExplicitActivationApproved: false,
        NexusExecutionEnabled: false);
}
