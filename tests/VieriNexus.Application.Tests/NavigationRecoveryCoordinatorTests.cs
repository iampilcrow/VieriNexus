using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRecoveryCoordinatorTests
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(500);

    [Fact]
    public void EmptyStateHasNothingToAcknowledge()
    {
        var setup = Setup();

        NavigationRecoveryStatus status = setup.Recovery.Observe(1_000, QuietPeriod);

        Assert.False(status.IsRequired);
        Assert.False(status.CanAcknowledge);
    }

    [Fact]
    public void CombinedManualAndExecutionCheckpointWaitsForQuietPeriodThenClearsWithoutReplay()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Begin(setup, now);
        ManualMovementSafetyResult manual = setup.Manual.Update(
            1_000, true, true, QuietPeriod);
        setup.Safety.Update(now.AddSeconds(1));

        NavigationRecoveryStatus waiting = setup.Recovery.Observe(1_200, QuietPeriod);
        NavigationRecoveryStatus ready = setup.Recovery.Observe(1_600, QuietPeriod);
        NavigationRecoveryStatus acknowledged = setup.Recovery.Acknowledge(
            1_600, now.AddSeconds(2), QuietPeriod);

        Assert.True(manual.RequiresExplicitResume);
        Assert.True(waiting.IsRequired);
        Assert.False(waiting.CanAcknowledge);
        Assert.True(ready.CanAcknowledge);
        Assert.False(acknowledged.IsRequired);
        Assert.Equal("recovery-acknowledged-no-replay", acknowledged.Code);
        Assert.False(setup.Manual.IsYieldLatched);
        Assert.True(setup.Safety.IsReadyForActivation);
        Assert.Equal(NavigationExecutionIntentState.Completed, setup.Store.Current!.State);
        Assert.Equal(1, setup.Provider.StopRequests);
    }

    [Fact]
    public void UnconfirmedStopCannotBeAcknowledged()
    {
        var setup = Setup();
        setup.Provider.MovementActive = true;
        Begin(setup, DateTimeOffset.UtcNow);
        setup.Manual.Update(1_000, true, true, QuietPeriod);

        NavigationRecoveryStatus status = setup.Recovery.Observe(2_000, QuietPeriod);
        NavigationRecoveryStatus attempted = setup.Recovery.Acknowledge(
            2_000, DateTimeOffset.UtcNow, QuietPeriod);

        Assert.True(status.IsRequired);
        Assert.False(status.CanAcknowledge);
        Assert.Equal("recovery-stop-unconfirmed", status.Code);
        Assert.Equal(status, attempted);
        Assert.True(setup.Stop.HasTrackedExecution);
    }

    [Fact]
    public void ExecutionOnlyCheckpointCanBeAcknowledged()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Begin(setup, now);
        setup.Safety.Shutdown(now.AddSeconds(1));

        NavigationRecoveryStatus ready = setup.Recovery.Observe(1_000, QuietPeriod);
        NavigationRecoveryStatus acknowledged = setup.Recovery.Acknowledge(
            1_000, now.AddSeconds(2), QuietPeriod);

        Assert.True(ready.CanAcknowledge);
        Assert.False(acknowledged.IsRequired);
        Assert.Equal(NavigationExecutionIntentState.Completed, setup.Store.Current!.State);
    }

    private static SetupState Setup()
    {
        var leases = new ResourceLeaseManager();
        var provider = new FakeProvider();
        var stop = new NavigationStopCoordinator(provider, TimeSpan.FromMinutes(1));
        var store = new FakeStore();
        var safety = new NavigationExecutionSafetyCoordinator(leases, stop, provider, store);
        safety.Update(DateTimeOffset.UtcNow);
        var manual = new ManualMovementSafetyCoordinator(stop);
        var recovery = new NavigationRecoveryCoordinator(safety, manual, stop);
        return new(leases, provider, stop, store, safety, manual, recovery);
    }

    private static void Begin(SetupState setup, DateTimeOffset now)
    {
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));
        setup.Safety.BeginExecution(Guid.NewGuid(), lease!, now);
    }

    private static LeaseOwner Owner() => new(
        GoalId.New(), TaskId.New(), AttemptId.New(), 0, "recovery-test");

    private sealed record SetupState(
        ResourceLeaseManager Leases,
        FakeProvider Provider,
        NavigationStopCoordinator Stop,
        FakeStore Store,
        NavigationExecutionSafetyCoordinator Safety,
        ManualMovementSafetyCoordinator Manual,
        NavigationRecoveryCoordinator Recovery);

    private sealed class FakeStore : INavigationExecutionIntentStore
    {
        internal NavigationExecutionIntent? Current { get; private set; }

        public NavigationExecutionIntent? Load() => Current;

        public void Save(NavigationExecutionIntent intent) => Current = intent;
    }

    private sealed class FakeProvider : INavigationStopProvider
    {
        internal int StopRequests { get; private set; }
        internal bool? MovementActive { get; set; } = false;

        public bool IsAvailable => true;

        public void RequestStop() => StopRequests++;

        public bool? IsMovementActive() => MovementActive;
    }
}
