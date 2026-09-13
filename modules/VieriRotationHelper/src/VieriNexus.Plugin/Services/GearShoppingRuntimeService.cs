using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed class GearShoppingRuntimeService
{
    private readonly ManualGearShoppingCoordinator coordinator;

    internal GearShoppingRuntimeService(ResourceLeaseManager leases, ProgressionProviderService provider)
    {
        coordinator = new ManualGearShoppingCoordinator(leases, provider);
    }

    internal ManualGearShoppingStatus Status => coordinator.Status;

    internal ProgressionActionResult Start(GearShoppingApproval approval) =>
        coordinator.Start(approval, DateTimeOffset.UtcNow);

    internal ProgressionActionResult Stop() => coordinator.Stop(DateTimeOffset.UtcNow);

    internal void Update() => coordinator.Update(DateTimeOffset.UtcNow);

    internal void Shutdown() => coordinator.Shutdown(DateTimeOffset.UtcNow);
}
