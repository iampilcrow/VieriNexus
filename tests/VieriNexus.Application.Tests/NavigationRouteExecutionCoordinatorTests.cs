using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRouteExecutionCoordinatorTests
{
    [Fact]
    public void StartArmsSafetyBeforeProviderMovementAndCompletionReleasesOwnership()
    {
        SetupState setup = Setup();
        NavigationRoutePlan plan = Plan();

        NavigationRouteExecutionStatus started = setup.Execution.Start(plan, DateTimeOffset.UtcNow);

        Assert.Equal(NavigationRouteExecutionState.Running, started.State);
        Assert.True(setup.Provider.SafetyWasArmedAtStart);
        Assert.Equal(NavigationExecutionIntentState.Running, setup.Store.Current!.State);
        Assert.Single(setup.Leases.Snapshot());

        setup.Provider.MovementActive = false;
        NavigationRouteExecutionStatus completed = setup.Execution.Update(DateTimeOffset.UtcNow.AddSeconds(2));

        Assert.Equal(NavigationRouteExecutionState.Completed, completed.State);
        Assert.Equal(NavigationExecutionIntentState.Completed, setup.Store.Current!.State);
        Assert.Empty(setup.Leases.Snapshot());
        Assert.Equal(0, setup.Provider.StopRequests);
    }

    [Fact]
    public void ManualMovementGateBlocksBeforeAuthorityOrProviderMutation()
    {
        SetupState setup = Setup(canStart: false);

        NavigationRouteExecutionStatus result = setup.Execution.Start(Plan(), DateTimeOffset.UtcNow);

        Assert.Equal(NavigationRouteExecutionState.Blocked, result.State);
        Assert.Equal("character-execution-blocked", result.Code);
        Assert.Equal(0, setup.Provider.StartRequests);
        Assert.Null(setup.Store.Current);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void ProviderStartFailureUsesVerifiedStopAndNeverLeavesRunningIntent()
    {
        SetupState setup = Setup();
        setup.Provider.ThrowOnStart = true;

        NavigationRouteExecutionStatus result = setup.Execution.Start(Plan(), DateTimeOffset.UtcNow);

        Assert.Equal(NavigationRouteExecutionState.Failed, result.State);
        Assert.Equal(1, setup.Provider.StopRequests);
        Assert.Equal(NavigationExecutionIntentState.AwaitingExplicitResume, setup.Store.Current!.State);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void ExplicitStopCreatesNoReplayCheckpoint()
    {
        SetupState setup = Setup();
        setup.Execution.Start(Plan(), DateTimeOffset.UtcNow);

        NavigationRouteExecutionStatus stopped = setup.Execution.Stop(DateTimeOffset.UtcNow);

        Assert.Equal(NavigationRouteExecutionState.AwaitingAcknowledgement, stopped.State);
        Assert.Equal(NavigationExecutionIntentState.AwaitingExplicitResume, setup.Store.Current!.State);
        Assert.Equal(1, setup.Provider.StopRequests);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void AReplacementProviderPathIsNeverStoppedByNexus()
    {
        SetupState setup = Setup();
        setup.Execution.Start(Plan(), DateTimeOffset.UtcNow);
        setup.Provider.DestinationOwned = false;

        NavigationRouteExecutionStatus result = setup.Execution.Update(DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(NavigationRouteExecutionState.Blocked, result.State);
        Assert.Equal("execution-superseded", result.Code);
        Assert.Equal(0, setup.Provider.StopRequests);
        Assert.True(setup.Provider.MovementActive);
        Assert.Equal(NavigationExecutionIntentState.Superseded, setup.Store.Current!.State);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void InitialIdleObservationGetsGraceBeforeCompletion()
    {
        SetupState setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setup.Execution.Start(Plan(), now);
        setup.Provider.MovementActive = false;

        NavigationRouteExecutionStatus result = setup.Execution.Update(now.AddMilliseconds(250));

        Assert.Equal(NavigationRouteExecutionState.Running, result.State);
        Assert.Single(setup.Leases.Snapshot());
    }

    private static SetupState Setup(bool canStart = true)
    {
        var leases = new ResourceLeaseManager();
        var store = new FakeStore();
        var provider = new FakeProvider();
        var stop = new NavigationStopCoordinator(provider, TimeSpan.FromMinutes(1));
        var safety = new NavigationExecutionSafetyCoordinator(leases, stop, provider, store);
        safety.Update(DateTimeOffset.UtcNow);
        var authority = new NavigationAuthorityCoordinator(
            leases, stop, safety, () => new NavigationAuthorityPrerequisites(
                true, false, true, true, true, true));
        authority.Update();
        authority.Approve();
        provider.SafetyArmed = () => safety.TrackedLeaseId is not null;
        var execution = new NavigationRouteExecutionCoordinator(
            authority, safety, provider, () => canStart, () => 100);
        return new(leases, store, provider, safety, authority, execution);
    }

    private static NavigationRoutePlan Plan()
    {
        NavigationRouteSnapshot route = new(
            Guid.NewGuid(), "Test", 100, [new(1, 2, 3), new(4, 5, 6)],
            string.Empty, string.Empty, true, false, 0.75f, 3f,
            0, 0, string.Empty, false, DateTime.UtcNow);
        return NavigationRoutePlanner.Build(route, NavigationRoutePlanKind.Playback, 100);
    }

    private sealed record SetupState(
        ResourceLeaseManager Leases,
        FakeStore Store,
        FakeProvider Provider,
        NavigationExecutionSafetyCoordinator Safety,
        NavigationAuthorityCoordinator Authority,
        NavigationRouteExecutionCoordinator Execution);

    private sealed class FakeStore : INavigationExecutionIntentStore
    {
        internal NavigationExecutionIntent? Current { get; private set; }
        public NavigationExecutionIntent? Load() => Current;
        public void Save(NavigationExecutionIntent intent) => Current = intent;
    }

    private sealed class FakeProvider : INavigationStopProvider, INavigationMovementProvider
    {
        internal Func<bool> SafetyArmed { get; set; } = () => false;
        internal bool SafetyWasArmedAtStart { get; private set; }
        internal bool ThrowOnStart { get; set; }
        internal int StartRequests { get; private set; }
        internal int StopRequests { get; private set; }
        internal bool? MovementActive { get; set; }
        internal bool? DestinationOwned { get; set; } = true;
        public bool IsAvailable { get; set; } = true;
        public bool IsReady { get; set; } = true;

        public void Start(IReadOnlyList<NavigationRoutePoint> points, bool useFlight, float tolerance)
        {
            StartRequests++;
            SafetyWasArmedAtStart = SafetyArmed();
            if (ThrowOnStart)
                throw new InvalidOperationException("start failed");
            MovementActive = true;
        }

        public void RequestStop()
        {
            StopRequests++;
            MovementActive = false;
        }

        public bool? IsMovementActive() => MovementActive;
        public bool? IsDestinationOwned(NavigationRoutePoint expectedDestination) => DestinationOwned;
    }
}
