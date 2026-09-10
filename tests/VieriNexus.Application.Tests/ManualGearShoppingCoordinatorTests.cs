using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ManualGearShoppingCoordinatorTests
{
    [Fact]
    public void HoldsCompleteResourceBundleUntilProviderCompletes()
    {
        ResourceLeaseManager leases = new();
        FakeProvider provider = new();
        ManualGearShoppingCoordinator coordinator = new(leases, provider);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.True(coordinator.Start(Approval(), now).Success);
        Assert.True(coordinator.Status.IsActive);
        Assert.Single(leases.Snapshot());
        Assert.Equal(5, Assert.Single(leases.Snapshot()).Resources.Count);

        provider.Busy = true;
        coordinator.Update(now.AddSeconds(1));
        Assert.Equal(ManualGearShoppingState.Running, coordinator.Status.State);

        provider.Busy = false;
        coordinator.Update(now.AddSeconds(2));
        Assert.Equal(ManualGearShoppingState.Completed, coordinator.Status.State);
        Assert.Empty(leases.Snapshot());
    }

    [Fact]
    public void ResourceConflictPreventsProviderStart()
    {
        ResourceLeaseManager leases = new();
        Assert.True(leases.TryAcquire(new LeaseOwner(GoalId.New(), TaskId.New(), AttemptId.New(), 1, "other"), [ResourceKind.Navigation],
            TimeSpan.FromMinutes(1), out _, out _));
        FakeProvider provider = new();
        ManualGearShoppingCoordinator coordinator = new(leases, provider);

        ProgressionActionResult result = coordinator.Start(Approval(), DateTimeOffset.UtcNow);

        Assert.False(result.Success);
        Assert.Equal(0, provider.StartCalls);
    }

    [Fact]
    public void StopWaitsForConfirmedInactivityThenReleasesResources()
    {
        ResourceLeaseManager leases = new();
        FakeProvider provider = new() { Busy = false, StopLeavesBusy = true };
        ManualGearShoppingCoordinator coordinator = new(leases, provider);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        coordinator.Start(Approval(), now);
        provider.Busy = true;
        coordinator.Update(now.AddSeconds(1));

        Assert.True(coordinator.Stop(now.AddSeconds(2)).Success);
        Assert.Equal(ManualGearShoppingState.Stopping, coordinator.Status.State);
        Assert.NotEmpty(leases.Snapshot());

        provider.Busy = false;
        coordinator.Update(now.AddSeconds(3));
        Assert.Equal(ManualGearShoppingState.Idle, coordinator.Status.State);
        Assert.Empty(leases.Snapshot());
    }

    [Fact]
    public void ProviderLossRetainsResourcesUntilInactivityCanBeConfirmed()
    {
        ResourceLeaseManager leases = new();
        FakeProvider provider = new();
        ManualGearShoppingCoordinator coordinator = new(leases, provider);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        coordinator.Start(Approval(), now);
        provider.Busy = true;
        coordinator.Update(now.AddSeconds(1));

        provider.Available = false;
        coordinator.Update(now.AddSeconds(2));

        Assert.Equal(ManualGearShoppingState.Stopping, coordinator.Status.State);
        Assert.NotEmpty(leases.Snapshot());

        provider.Available = true;
        provider.Busy = false;
        coordinator.Update(now.AddSeconds(3));
        Assert.Equal(ManualGearShoppingState.Idle, coordinator.Status.State);
        Assert.Empty(leases.Snapshot());
    }

    private static GearShoppingApproval Approval() => new(
        1, 10, 20, 1_000_000, [new(3, 100, 5_000, 1)]);

    private sealed class FakeProvider : IManualGearShoppingProvider
    {
        public ProviderId Id { get; } = new("fake");
        public bool Available { get; set; } = true;
        public bool Busy { get; set; }
        public bool StopLeavesBusy { get; set; }
        public int StartCalls { get; private set; }

        public ProgressionGearProviderObservation Observe() =>
            new(Available, Busy, 0, 0, 0, 0, 0, Available ? "Provider state." : "Unavailable.");

        public bool TryStart(GearShoppingApproval approval, out string message)
        {
            StartCalls++;
            Busy = true;
            message = "Started.";
            return true;
        }

        public bool TryStop(out string message)
        {
            if (!StopLeavesBusy)
                Busy = false;
            message = "Stop requested.";
            return true;
        }
    }
}
