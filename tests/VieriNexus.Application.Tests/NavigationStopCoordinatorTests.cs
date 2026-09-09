using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class NavigationStopCoordinatorTests
{
    [Fact]
    public void StopIsIdempotentWhenNexusHasNoTrackedExecution()
    {
        var provider = new FakeProvider();
        var coordinator = Coordinator(provider);

        NavigationStopResult first = coordinator.Stop();
        NavigationStopResult second = coordinator.Stop();

        Assert.Equal(NavigationStopState.AlreadyStopped, first.State);
        Assert.Equal(NavigationStopState.AlreadyStopped, second.State);
        Assert.True(first.IsStopConfirmed);
        Assert.Equal(0, provider.StopRequests);
    }

    [Fact]
    public void UnavailableProviderRetainsNavigationOwnership()
    {
        var leases = new ResourceLeaseManager();
        ResourceLeaseHandle lease = AcquireNavigation(leases);
        var coordinator = Coordinator(new FakeProvider { Available = false });
        coordinator.TrackExecution(lease);

        NavigationStopResult result = coordinator.Stop();

        Assert.Equal(NavigationStopState.ProviderUnavailable, result.State);
        Assert.True(result.NavigationLeaseRetained);
        Assert.False(result.IsStopConfirmed);
        AssertNavigationIsBlocked(leases);
    }

    [Fact]
    public void FailedStopRequestRetainsNavigationOwnership()
    {
        var leases = new ResourceLeaseManager();
        ResourceLeaseHandle lease = AcquireNavigation(leases);
        var provider = new FakeProvider { ThrowOnStop = true };
        var coordinator = Coordinator(provider);
        coordinator.TrackExecution(lease);

        NavigationStopResult result = coordinator.Stop();

        Assert.Equal(NavigationStopState.StopRequestFailed, result.State);
        Assert.True(result.ProviderStopRequested);
        Assert.True(result.NavigationLeaseRetained);
        AssertNavigationIsBlocked(leases);
    }

    [Fact]
    public void UnknownConfirmationRetainsNavigationOwnership()
    {
        var leases = new ResourceLeaseManager();
        ResourceLeaseHandle lease = AcquireNavigation(leases);
        var provider = new FakeProvider { MovementActive = null };
        var coordinator = Coordinator(provider);
        coordinator.TrackExecution(lease);

        NavigationStopResult result = coordinator.Stop();

        Assert.Equal(NavigationStopState.ConfirmationUnavailable, result.State);
        Assert.Null(result.ProviderReportsMovementActive);
        Assert.True(result.NavigationLeaseRetained);
        AssertNavigationIsBlocked(leases);
    }

    [Fact]
    public void StillMovingRetainsLeaseUntilARepeatedStopConfirmsInactive()
    {
        var leases = new ResourceLeaseManager();
        ResourceLeaseHandle lease = AcquireNavigation(leases);
        var provider = new FakeProvider { MovementActive = true };
        var coordinator = Coordinator(provider);
        coordinator.TrackExecution(lease);

        NavigationStopResult moving = coordinator.Stop();
        provider.MovementActive = false;
        NavigationStopResult stopped = coordinator.Stop();

        Assert.Equal(NavigationStopState.StillMoving, moving.State);
        Assert.True(moving.NavigationLeaseRetained);
        Assert.Equal(NavigationStopState.Stopped, stopped.State);
        Assert.True(stopped.IsStopConfirmed);
        Assert.True(stopped.NavigationLeaseReleased);
        Assert.False(coordinator.HasTrackedExecution);
        Assert.Equal(2, provider.StopRequests);
        Assert.True(leases.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var next, out _));
        next!.Dispose();
    }

    [Fact]
    public void ASecondExecutionCannotReplaceTheTrackedLease()
    {
        var leases = new ResourceLeaseManager();
        ResourceLeaseHandle first = AcquireNavigation(leases);
        Assert.True(leases.TryAcquire(Owner(), [ResourceKind.Targeting], TimeSpan.FromMinutes(1), out var second, out _));
        var coordinator = Coordinator(new FakeProvider());
        coordinator.TrackExecution(first);

        Assert.Throws<InvalidOperationException>(() => coordinator.TrackExecution(second!));

        second!.Dispose();
    }

    private static NavigationStopCoordinator Coordinator(FakeProvider provider) =>
        new(provider, TimeSpan.FromMinutes(1));

    private static ResourceLeaseHandle AcquireNavigation(ResourceLeaseManager leases)
    {
        Assert.True(leases.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));
        return lease!;
    }

    private static void AssertNavigationIsBlocked(ResourceLeaseManager leases) =>
        Assert.False(leases.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out _, out _));

    private static LeaseOwner Owner() => new(
        GoalId.New(),
        TaskId.New(),
        AttemptId.New(),
        0,
        "stop-test");

    private sealed class FakeProvider : INavigationStopProvider
    {
        internal bool Available { get; set; } = true;
        internal bool ThrowOnStop { get; set; }
        internal bool? MovementActive { get; set; } = false;
        internal int StopRequests { get; private set; }

        public bool IsAvailable => Available;

        public void RequestStop()
        {
            StopRequests++;
            if (ThrowOnStop)
                throw new InvalidOperationException("provider failure");
        }

        public bool? IsMovementActive() => MovementActive;
    }
}
