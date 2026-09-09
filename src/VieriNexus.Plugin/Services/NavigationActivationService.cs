using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class NavigationActivationService(
    DependencyService dependencies,
    NavigationMigrationService migration,
    ResourceLeaseManager leases,
    NavigationStopCoordinator stop,
    ManualMovementSafetyService manualMovement,
    NavigationExecutionSafetyCoordinator executionSafety,
    NavigationAuthorityCoordinator authority)
{
    internal NavigationAuthorityStatus AuthorityStatus => authority.Status;

    internal NavigationAuthorityStatus ApproveAuthority() => authority.Approve();

    internal NavigationAuthorityStatus ReturnAuthorityToStaging() =>
        authority.ReturnToStaging(DateTimeOffset.UtcNow);

    internal NavigationActivationAssessment Assess()
    {
        PluginPresence source = dependencies.FindPlugin("VieriNavPlotter");
        Guid? trackedLeaseId = executionSafety.TrackedLeaseId;
        bool leaseConflict = leases.Snapshot().Any(lease =>
            lease.LeaseId != trackedLeaseId &&
            (lease.Resources.Contains(ResourceKind.Navigation) ||
             lease.Resources.Contains(ResourceKind.Movement)));

        return NavigationActivationPolicy.Evaluate(new NavigationActivationInputs(
            migration.StagedSnapshot is not null,
            source.IsInstalled,
            source.IsLoaded,
            dependencies.RequiredReady,
            ResourceOwnershipConnected: true,
            HasNavigationOrMovementLeaseConflict: leaseConflict,
            StopAvailable: stop.IsProviderAvailable,
            ManualOverrideAvailable: manualMovement.IsReadyForActivation,
            ReloadReconciliationAvailable: executionSafety.IsReadyForActivation,
            ExplicitActivationApproved: authority.IsActive,
            NexusExecutionEnabled: trackedLeaseId is not null));
    }
}
