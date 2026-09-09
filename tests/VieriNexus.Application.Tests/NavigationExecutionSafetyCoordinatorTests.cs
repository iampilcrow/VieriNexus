using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class NavigationExecutionSafetyCoordinatorTests
{
    [Fact]
    public void EmptyJournalBecomesReadyWithoutCallingProvider()
    {
        var setup = Setup();

        NavigationExecutionSafetyStatus status = setup.Safety.Update(DateTimeOffset.UtcNow);

        Assert.Equal(NavigationExecutionSafetyState.Ready, status.State);
        Assert.True(status.IsReadyForActivation);
        Assert.Equal(0, setup.Provider.StopRequests);
    }

    [Fact]
    public void ReloadedRunningIntentIsStoppedAndNeverReplayed()
    {
        var intent = Intent(NavigationExecutionIntentState.Running);
        var setup = Setup(intent);

        NavigationExecutionSafetyStatus status = setup.Safety.Update(DateTimeOffset.UtcNow);

        Assert.Equal(NavigationExecutionSafetyState.AwaitingExplicitResume, status.State);
        Assert.False(status.IsReadyForActivation);
        Assert.Equal(1, setup.Provider.StopRequests);
        Assert.Equal(NavigationExecutionIntentState.AwaitingExplicitResume, setup.Store.Current!.State);
        Assert.Contains(setup.Store.Writes, item => item.State == NavigationExecutionIntentState.StopPending);
    }

    [Fact]
    public void ReloadRecoveryRetriesUntilInactiveIsConfirmed()
    {
        var setup = Setup(Intent(NavigationExecutionIntentState.Running));
        setup.Provider.MovementActive = true;

        NavigationExecutionSafetyStatus moving = setup.Safety.Update(DateTimeOffset.UtcNow);
        setup.Provider.MovementActive = false;
        NavigationExecutionSafetyStatus stopped = setup.Safety.Update(DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(NavigationExecutionSafetyState.RecoveringReload, moving.State);
        Assert.False(moving.IsReadyForActivation);
        Assert.Equal(NavigationExecutionSafetyState.AwaitingExplicitResume, stopped.State);
        Assert.Equal(2, setup.Provider.StopRequests);
    }

    [Fact]
    public void UnavailableProviderKeepsReloadRecoveryFailClosed()
    {
        var setup = Setup(Intent(NavigationExecutionIntentState.StopPending));
        setup.Provider.Available = false;

        NavigationExecutionSafetyStatus status = setup.Safety.Update(DateTimeOffset.UtcNow);

        Assert.Equal(NavigationExecutionSafetyState.RecoveringReload, status.State);
        Assert.False(status.IsReadyForActivation);
        Assert.Equal(0, setup.Provider.StopRequests);
        Assert.Equal(NavigationExecutionIntentState.StopPending, setup.Store.Current!.State);
    }

    [Fact]
    public void InvalidJournalBlocksReadiness()
    {
        var setup = Setup();
        setup.Store.LoadException = new InvalidDataException("bad journal");

        NavigationExecutionSafetyStatus status = setup.Safety.Update(DateTimeOffset.UtcNow);

        Assert.Equal(NavigationExecutionSafetyState.Faulted, status.State);
        Assert.False(status.IsReadyForActivation);
        Assert.Equal("reload-intent-invalid", status.Code);
    }

    [Fact]
    public void ExecutionCannotStartWhenDurableIntentWriteFails()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setup.Safety.Update(now);
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));
        setup.Store.SaveException = new IOException("disk unavailable");

        Assert.Throws<InvalidOperationException>(() =>
            setup.Safety.BeginExecution(Guid.NewGuid(), lease!, now));

        Assert.Equal(NavigationExecutionSafetyState.Faulted, setup.Safety.Status.State);
        Assert.False(setup.Safety.IsReadyForActivation);
        Assert.Empty(setup.Leases.Snapshot());
        Assert.Equal(0, setup.Provider.StopRequests);
    }

    [Fact]
    public void ExecutionRejectsALeaseWithoutNavigationAndMovementOwnership()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setup.Safety.Update(now);
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Targeting], TimeSpan.FromMinutes(1), out var lease, out _));

        Assert.Throws<InvalidOperationException>(() =>
            setup.Safety.BeginExecution(Guid.NewGuid(), lease!, now));

        Assert.Null(setup.Store.Current);
        Assert.Equal(0, setup.Provider.StopRequests);
        lease!.Dispose();
    }

    [Fact]
    public void ExpiredTrackedNavigationLeaseLatchesStopAndExplicitResume()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));
        setup.Safety.Update(now);
        setup.Safety.BeginExecution(Guid.NewGuid(), lease!, now);

        NavigationExecutionSafetyStatus status = setup.Safety.Update(now.AddMinutes(2));

        Assert.Equal(NavigationExecutionSafetyState.AwaitingExplicitResume, status.State);
        Assert.False(status.IsReadyForActivation);
        Assert.Equal(1, setup.Provider.StopRequests);
        Assert.Equal(NavigationExecutionIntentState.AwaitingExplicitResume, setup.Store.Current!.State);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void InterruptedIntentRequiresExplicitAcknowledgementBeforeAnotherStart()
    {
        var setup = Setup(Intent(NavigationExecutionIntentState.Running));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setup.Safety.Update(now);
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var blockedLease, out _));

        Assert.Throws<InvalidOperationException>(() =>
            setup.Safety.BeginExecution(Guid.NewGuid(), blockedLease!, now.AddSeconds(1)));
        blockedLease!.Dispose();

        Assert.True(setup.Safety.AcknowledgeInterruptedIntent(now.AddSeconds(2)));
        Assert.True(setup.Safety.IsReadyForActivation);
        Assert.Equal(NavigationExecutionIntentState.Completed, setup.Store.Current!.State);
    }

    [Fact]
    public void ExpiredLeaseWithUnconfirmedStopRetriesThroughTrackedCoordinator()
    {
        var setup = Setup();
        setup.Provider.MovementActive = true;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));
        setup.Safety.Update(now);
        setup.Safety.BeginExecution(Guid.NewGuid(), lease!, now);

        NavigationExecutionSafetyStatus expired = setup.Safety.Update(now.AddMinutes(2));
        setup.Provider.MovementActive = false;
        NavigationExecutionSafetyStatus stopped = setup.Safety.Update(now.AddMinutes(2).AddSeconds(1));

        Assert.Equal(NavigationExecutionSafetyState.StoppingExpiredLease, expired.State);
        Assert.False(expired.IsReadyForActivation);
        Assert.Equal(NavigationExecutionSafetyState.AwaitingExplicitResume, stopped.State);
        Assert.Equal(2, setup.Provider.StopRequests);
    }

    [Fact]
    public void ShutdownStopsTrackedExecutionAndPersistsNoReplayCheckpoint()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));
        setup.Safety.Update(now);
        setup.Safety.BeginExecution(Guid.NewGuid(), lease!, now);

        NavigationExecutionSafetyStatus status = setup.Safety.Shutdown(now.AddSeconds(1));

        Assert.Equal(NavigationExecutionSafetyState.AwaitingExplicitResume, status.State);
        Assert.Equal(NavigationExecutionIntentState.AwaitingExplicitResume, setup.Store.Current!.State);
        Assert.Equal(1, setup.Provider.StopRequests);
    }

    private static SetupState Setup(NavigationExecutionIntent? intent = null)
    {
        var leases = new ResourceLeaseManager();
        var provider = new FakeProvider();
        var stop = new NavigationStopCoordinator(provider, TimeSpan.FromMinutes(1));
        var store = new FakeStore { Current = intent };
        var safety = new NavigationExecutionSafetyCoordinator(leases, stop, provider, store);
        return new SetupState(leases, provider, store, safety);
    }

    private static NavigationExecutionIntent Intent(NavigationExecutionIntentState state) => new(
        NavigationExecutionIntent.CurrentSchemaVersion,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        state,
        DateTimeOffset.UtcNow);

    private static LeaseOwner Owner() => new(
        GoalId.New(), TaskId.New(), AttemptId.New(), 0, "execution-safety-test");

    private sealed record SetupState(
        ResourceLeaseManager Leases,
        FakeProvider Provider,
        FakeStore Store,
        NavigationExecutionSafetyCoordinator Safety);

    private sealed class FakeStore : INavigationExecutionIntentStore
    {
        internal NavigationExecutionIntent? Current { get; set; }
        internal Exception? LoadException { get; set; }
        internal Exception? SaveException { get; set; }
        internal List<NavigationExecutionIntent> Writes { get; } = [];

        public NavigationExecutionIntent? Load()
        {
            if (LoadException is not null)
                throw LoadException;
            return Current;
        }

        public void Save(NavigationExecutionIntent intent)
        {
            if (SaveException is not null)
                throw SaveException;
            Current = intent;
            Writes.Add(intent);
        }
    }

    private sealed class FakeProvider : INavigationStopProvider
    {
        internal bool Available { get; set; } = true;
        internal bool? MovementActive { get; set; } = false;
        internal int StopRequests { get; private set; }

        public bool IsAvailable => Available;

        public void RequestStop() => StopRequests++;

        public bool? IsMovementActive() => MovementActive;
    }
}
