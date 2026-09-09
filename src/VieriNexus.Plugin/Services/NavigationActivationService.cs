using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class NavigationActivationService(
    DependencyService dependencies,
    NavigationMigrationService migration,
    ResourceLeaseManager leases,
    NavigationStopCoordinator stop)
{
    internal NavigationActivationAssessment Assess()
    {
        PluginPresence source = dependencies.FindPlugin("VieriNavPlotter");
        bool leaseConflict = leases.Snapshot().Any(lease =>
            lease.Resources.Contains(ResourceKind.Navigation) ||
            lease.Resources.Contains(ResourceKind.Movement));

        return NavigationActivationPolicy.Evaluate(new NavigationActivationInputs(
            migration.StagedSnapshot is not null,
            source.IsInstalled,
            source.IsLoaded,
            dependencies.RequiredReady,
            ResourceOwnershipConnected: true,
            HasNavigationOrMovementLeaseConflict: leaseConflict,
            StopAvailable: stop.IsProviderAvailable,
            ManualOverrideAvailable: false,
            ReloadReconciliationAvailable: false,
            ExplicitActivationApproved: false,
            NexusExecutionEnabled: false));
    }
}
