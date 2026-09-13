using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class NavigationAuthorityCoordinatorTests
{
    [Fact]
    public void LoadedSourceBlocksExplicitApprovalWithoutChangingIt()
    {
        var setup = Setup();
        setup.Prerequisites.Value = Ready() with { SourcePluginLoaded = true };

        NavigationAuthorityStatus status = setup.Authority.Update();
        NavigationAuthorityStatus approval = setup.Authority.Approve();

        Assert.Equal(NavigationAuthorityState.Blocked, status.State);
        Assert.False(status.CanApprove);
        Assert.False(approval.IsActive);
        Assert.Contains("Disable it", approval.Message);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void ReadyPrerequisitesAutomaticallyActivateWithoutMovement()
    {
        var setup = Setup();

        NavigationAuthorityStatus ready = setup.Authority.Update();
        NavigationAuthorityStatus active = setup.Authority.Approve();

        Assert.Equal(NavigationAuthorityState.ActiveWithoutExecution, ready.State);
        Assert.False(ready.CanApprove);
        Assert.Equal(NavigationAuthorityState.ActiveWithoutExecution, active.State);
        Assert.True(active.IsActive);
        Assert.Equal(0, setup.Provider.StopRequests);
        Assert.Null(setup.ExecutionSafety.TrackedLeaseId);
        Assert.Empty(setup.Leases.Snapshot());

        var nextSession = new NavigationAuthorityCoordinator(
            setup.Leases,
            setup.Stop,
            setup.ExecutionSafety,
            () => setup.Prerequisites.Value);
        Assert.False(nextSession.IsActive);
    }

    [Fact]
    public void ExistingNavigationConflictBlocksAtomicApprovalProbe()
    {
        var setup = Setup();
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Movement], TimeSpan.FromMinutes(1), out var conflict, out _));

        NavigationAuthorityStatus status = setup.Authority.Approve();

        Assert.Equal(NavigationAuthorityState.Blocked, status.State);
        Assert.Equal("activation-resource-conflict", status.Code);
        Assert.False(status.IsActive);
        conflict!.Dispose();
    }

    [Fact]
    public void SourceLoadingAfterApprovalRevokesAuthority()
    {
        var setup = Setup();
        setup.Authority.Update();
        Assert.True(setup.Authority.Approve().IsActive);
        setup.Prerequisites.Value = Ready() with { SourcePluginLoaded = true };

        NavigationAuthorityStatus status = setup.Authority.Update();

        Assert.Equal(NavigationAuthorityState.Revoked, status.State);
        Assert.False(status.IsActive);
        Assert.Equal(0, setup.Provider.StopRequests);
    }

    [Fact]
    public void ReturnToStagingIsExplicitAndDoesNotTouchSourcePlugin()
    {
        var setup = Setup();
        setup.Authority.Update();
        setup.Authority.Approve();

        NavigationAuthorityStatus status = setup.Authority.ReturnToStaging(DateTimeOffset.UtcNow);

        Assert.Equal(NavigationAuthorityState.StagingOnly, status.State);
        Assert.False(status.IsActive);
        Assert.Contains("No source plugin was enabled or changed", status.Message);
        Assert.Equal(0, setup.Provider.StopRequests);
    }

    [Fact]
    public void FutureExecutionStartAtomicallyAcquiresResourcesAndArmsNoReplaySafety()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setup.Authority.Update();
        setup.Authority.Approve();

        NavigationExecutionStartResult result = setup.Authority.TryBeginExecution(
            Guid.NewGuid(), Owner(), TimeSpan.FromMinutes(1), now);
        NavigationAuthorityStatus afterStart = setup.Authority.Update();

        Assert.True(result.Success);
        Assert.NotNull(result.Lease);
        Assert.NotNull(setup.ExecutionSafety.TrackedLeaseId);
        Assert.Equal(NavigationExecutionIntentState.Running, setup.Store.Current!.State);
        Assert.Contains(ResourceKind.Navigation, Assert.Single(setup.Leases.Snapshot()).Resources);
        Assert.Contains(ResourceKind.Movement, Assert.Single(setup.Leases.Snapshot()).Resources);
        Assert.True(afterStart.IsActive);
        Assert.Equal(0, setup.Provider.StopRequests);
    }

    [Fact]
    public void SourceConflictDuringTrackedExecutionStopsAndRevokes()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setup.Authority.Update();
        setup.Authority.Approve();
        Assert.True(setup.Authority.TryBeginExecution(
            Guid.NewGuid(), Owner(), TimeSpan.FromMinutes(1), now).Success);
        setup.Prerequisites.Value = Ready() with { SourcePluginLoaded = true };

        NavigationAuthorityStatus status = setup.Authority.Update();

        Assert.Equal(NavigationAuthorityState.Revoked, status.State);
        Assert.False(status.IsActive);
        Assert.Equal(1, setup.Provider.StopRequests);
        Assert.Equal(NavigationExecutionIntentState.AwaitingExplicitResume, setup.Store.Current!.State);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void ExecutionStartAutomaticallyActivatesWhenPrerequisitesAreReady()
    {
        var setup = Setup();

        NavigationExecutionStartResult result = setup.Authority.TryBeginExecution(
            Guid.NewGuid(), Owner(), TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);

        Assert.True(result.Success);
        Assert.Equal("execution-safety-armed", result.Code);
        Assert.NotNull(setup.Store.Current);
        result.Lease!.Dispose();
    }

    [Fact]
    public void SourceIsRecheckedAtTheExecutionBoundary()
    {
        var setup = Setup();
        setup.Authority.Update();
        setup.Authority.Approve();
        setup.Prerequisites.Value = Ready() with { SourcePluginLoaded = true };

        NavigationExecutionStartResult result = setup.Authority.TryBeginExecution(
            Guid.NewGuid(), Owner(), TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);

        Assert.False(result.Success);
        Assert.Equal("authority-prerequisite-lost", result.Code);
        Assert.False(setup.Authority.IsActive);
        Assert.Null(setup.Store.Current);
        Assert.Empty(setup.Leases.Snapshot());
    }

    [Fact]
    public void UnconfirmedStopAfterAConflictRemainsLatchedAndBlocked()
    {
        var setup = Setup();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setup.Authority.Update();
        setup.Authority.Approve();
        Assert.True(setup.Authority.TryBeginExecution(
            Guid.NewGuid(), Owner(), TimeSpan.FromMinutes(1), now).Success);
        setup.Provider.MovementActive = true;
        setup.Prerequisites.Value = Ready() with { SourcePluginLoaded = true };

        NavigationAuthorityStatus status = setup.Authority.Update();

        Assert.Equal(NavigationAuthorityState.Revoked, status.State);
        Assert.False(status.IsActive);
        Assert.Contains("Stop remains latched", status.Message);
        Assert.Equal(NavigationExecutionIntentState.StopPending, setup.Store.Current!.State);
        Assert.NotEmpty(setup.Leases.Snapshot());
    }

    [Fact]
    public void ResourceRaceAtExecutionBoundaryFailsWithoutDisarmingAuthority()
    {
        var setup = Setup();
        setup.Authority.Update();
        setup.Authority.Approve();
        Assert.True(setup.Leases.TryAcquire(
            Owner(), [ResourceKind.Movement], TimeSpan.FromMinutes(1), out var conflict, out _));

        NavigationExecutionStartResult result = setup.Authority.TryBeginExecution(
            Guid.NewGuid(), Owner(), TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);

        Assert.False(result.Success);
        Assert.Equal("execution-resource-conflict", result.Code);
        Assert.True(setup.Authority.IsActive);
        Assert.Null(setup.Store.Current);
        conflict!.Dispose();
    }

    private static SetupState Setup()
    {
        var leases = new ResourceLeaseManager();
        var provider = new FakeProvider();
        var stop = new NavigationStopCoordinator(provider, TimeSpan.FromMinutes(1));
        var store = new FakeStore();
        var safety = new NavigationExecutionSafetyCoordinator(leases, stop, provider, store);
        safety.Update(DateTimeOffset.UtcNow);
        var prerequisites = new PrerequisiteHolder { Value = Ready() };
        var authority = new NavigationAuthorityCoordinator(
            leases, stop, safety, () => prerequisites.Value);
        return new(leases, provider, stop, store, safety, prerequisites, authority);
    }

    private static NavigationAuthorityPrerequisites Ready() => new(
        HasVerifiedStagedLibrary: true,
        SourcePluginLoaded: false,
        RequiredDependenciesReady: true,
        StopAvailable: true,
        ManualMovementYieldingAvailable: true,
        ReloadReconciliationAvailable: true);

    private static LeaseOwner Owner() => new(
        GoalId.New(), TaskId.New(), AttemptId.New(), 0, "authority-test");

    private sealed record SetupState(
        ResourceLeaseManager Leases,
        FakeProvider Provider,
        NavigationStopCoordinator Stop,
        FakeStore Store,
        NavigationExecutionSafetyCoordinator ExecutionSafety,
        PrerequisiteHolder Prerequisites,
        NavigationAuthorityCoordinator Authority);

    private sealed class PrerequisiteHolder
    {
        internal NavigationAuthorityPrerequisites Value { get; set; } = Ready();
    }

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
