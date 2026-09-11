using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ProgressionExecutionCoordinatorTests
{
    private static readonly CharacterKey Character = new(0x1234, 74);

    [Fact]
    public void GearReadinessRunsAsOwnedVerifiedTaskBeforeDutySelection()
    {
        MemoryStore store = new();
        FakeDutyProvider duty = new();
        FakeGearProvider gear = new();
        ResourceLeaseManager leases = new();
        ProgressionExecutionCoordinator coordinator = new(store, leases, duty, gear);

        ProgressionActionResult result = coordinator.Start(
            Draft() with { CurrentItemLevel = 640, CurrentGil = 1_500_000 }, Plan());

        Assert.True(result.Success);
        Assert.Equal([1_000_000], gear.StartedGilFloors);
        Assert.Empty(duty.StartedTerritories);
        Assert.Equal("vieri.gear.ensure-readiness/v1", coordinator.State!.ActiveTask!.Kind.Value);
        Assert.True(leases.Snapshot().Single().Resources.IsSupersetOf(
            [ResourceKind.Teleport, ResourceKind.Navigation, ResourceKind.Movement,
                ResourceKind.UiInteraction, ResourceKind.InventoryMutation]));

        gear.IsBusy = true;
        coordinator.Update(World(level: 90, inDuty: false) with { ItemLevel = 640, Gil = 1_500_000 });
        gear.IsBusy = false;
        gear.ItemsPurchased = 2;
        coordinator.Update(World(level: 90, inDuty: false) with { ItemLevel = 650, Gil = 1_420_000 });

        Assert.Equal([200u], duty.StartedTerritories);
        Assert.Equal(2, coordinator.State.Tasks.Count);
        Assert.Equal(NexusTaskStatus.Succeeded, coordinator.State.Tasks[0].Status);
        Assert.Contains("item level 640 → 650", coordinator.State.Tasks[0].StatusDetail);
        Assert.Equal(NexusTaskStatus.Acquiring, coordinator.State.ActiveTask!.Status);
    }

    [Fact]
    public void GearVerificationBlocksDutyWhenProtectedGilFloorIsViolated()
    {
        FakeDutyProvider duty = new();
        FakeGearProvider gear = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, gear);
        coordinator.Start(Draft() with { CurrentItemLevel = 640, CurrentGil = 1_500_000 }, Plan());

        gear.IsBusy = true;
        coordinator.Update(World(90, false) with { ItemLevel = 640, Gil = 1_500_000 });
        gear.IsBusy = false;
        coordinator.Update(World(90, false) with { ItemLevel = 650, Gil = 999_999 });

        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.Equal("gear-gil-floor-violated", coordinator.State.Tasks.Single().Failure!.Code);
        Assert.Empty(duty.StartedTerritories);
    }

    [Fact]
    public void GearProviderFailureCannotBeMisreportedAsACompletedTransaction()
    {
        FakeDutyProvider duty = new();
        FakeGearProvider gear = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, gear);
        coordinator.Start(Draft() with { CurrentItemLevel = 640, CurrentGil = 1_500_000 }, Plan());

        gear.IsBusy = true;
        coordinator.Update(World(90, false) with { ItemLevel = 640, Gil = 1_500_000 });
        gear.EndWithoutCompletion();
        coordinator.Update(World(90, false) with { ItemLevel = 640, Gil = 1_500_000 });

        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.Equal("gear-transaction-not-completed", coordinator.State.Tasks.Single().Failure!.Code);
        Assert.Empty(duty.StartedTerritories);
    }

    [Fact]
    public void StopDuringGearReadinessUsesGearStopAndNeverCallsDutyStop()
    {
        FakeDutyProvider duty = new();
        FakeGearProvider gear = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, gear);
        coordinator.Start(Draft() with { CurrentItemLevel = 640, CurrentGil = 1_500_000 }, Plan());
        gear.IsBusy = true;
        coordinator.Update(World(90, false) with { ItemLevel = 640, Gil = 1_500_000 });

        ProgressionActionResult stopped = coordinator.StopNow();

        Assert.True(stopped.Success);
        Assert.Equal(1, gear.StopCalls);
        Assert.Equal(0, duty.StopCalls);
        gear.IsBusy = false;
        coordinator.Update(World(90, false) with { ItemLevel = 640, Gil = 1_500_000 });
        Assert.Equal(GoalStatus.Cancelled, coordinator.State!.Goal.Status);
    }

    [Fact]
    public void StartsHighestEligibleDutyAsOneBoundedTaskWithFullOwnership()
    {
        MemoryStore store = new();
        FakeDutyProvider provider = new();
        ResourceLeaseManager leases = new();
        ProgressionExecutionCoordinator coordinator = new(store, leases, provider);

        ProgressionActionResult result = coordinator.Start(Draft(), Plan());

        Assert.True(result.Success);
        Assert.Equal([200u], provider.StartedTerritories);
        Assert.Equal(NexusTaskStatus.Acquiring, coordinator.State!.ActiveTask!.Status);
        Assert.Contains(ResourceKind.DutyQueue, coordinator.State.ActiveTask.RequiredResources);
        Assert.Contains(ResourceKind.InventoryMutation, coordinator.State.ActiveTask.RequiredResources);
        Assert.Contains(ResourceKind.Teleport, coordinator.State.ActiveTask.RequiredResources);
        Assert.True(leases.Snapshot().Single().Resources.IsSupersetOf(
            [ResourceKind.DutyQueue, ResourceKind.Teleport, ResourceKind.Navigation, ResourceKind.Movement,
                ResourceKind.UiInteraction, ResourceKind.InventoryMutation, ResourceKind.Targeting,
                ResourceKind.Combat, ResourceKind.Rotation]));
        Assert.NotNull(store.State);
    }

    [Fact]
    public void VerifiesDutyEntryAndReturnBeforeSatisfyingGoal()
    {
        FakeDutyProvider provider = new();
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(targetLevel: 92), Plan());

        provider.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: false));
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(200);
        provider.IsStopped = true;
        coordinator.Update(World(level: 92, inDuty: false));

        Assert.Equal(GoalStatus.Satisfied, coordinator.State!.Goal.Status);
        Assert.Equal(NexusTaskStatus.Succeeded, coordinator.State.Tasks.Single().Status);
        Assert.Null(coordinator.State.ActiveTaskId);
    }

    [Fact]
    public void LastRunPausesAfterVerifiedDutyInsteadOfSchedulingAnother()
    {
        FakeDutyProvider provider = new();
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(targetLevel: 95), Plan());
        Assert.True(coordinator.StopAfterCurrentDuty().Success);

        provider.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(200);
        provider.IsStopped = true;
        coordinator.Update(World(level: 91, inDuty: false));

        Assert.Equal(GoalStatus.Paused, coordinator.State!.Goal.Status);
        Assert.Single(provider.StartedTerritories);
        Assert.Contains("Last Run complete", coordinator.State.Goal.StatusDetail);
    }

    [Fact]
    public void CompletedDutyReplansAndStartsExactlyOneNextRun()
    {
        FakeDutyProvider provider = new();
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(targetLevel: 95), Plan());

        provider.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(200);
        provider.IsStopped = true;
        coordinator.Update(World(level: 91, inDuty: false));

        Assert.Equal(GoalStatus.Active, coordinator.State!.Goal.Status);
        Assert.Equal(2, provider.StartedTerritories.Count);
        Assert.Equal(2, coordinator.State.Tasks.Count);
        Assert.Equal(NexusTaskStatus.Succeeded, coordinator.State.Tasks[0].Status);
        Assert.Equal(NexusTaskStatus.Acquiring, coordinator.State.Tasks[1].Status);
        Assert.Equal(2, coordinator.State.Goal.PlanRevision);
    }

    [Fact]
    public void StopCancelsOnlyAfterProviderConfirmsInactive()
    {
        FakeDutyProvider provider = new() { IsStopped = false };
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(), Plan());
        coordinator.Update(World(level: 90, inDuty: false));

        ProgressionActionResult result = coordinator.StopNow();

        Assert.True(result.Success);
        Assert.Equal(1, provider.StopCalls);
        Assert.Equal(NexusTaskStatus.Cancelling, coordinator.State!.ActiveTask!.Status);
        provider.IsStopped = true;
        coordinator.Update(World(level: 90, inDuty: false));
        Assert.Equal(GoalStatus.Cancelled, coordinator.State.Goal.Status);
        Assert.Equal(NexusTaskStatus.Cancelled, coordinator.State.Tasks.Single().Status);
    }

    [Fact]
    public void ProviderExitWithoutDutyCompletionPausesWithoutCountingTheRun()
    {
        FakeDutyProvider provider = new();
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(targetLevel: 95), Plan());

        provider.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        provider.IsStopped = true;
        coordinator.Update(World(level: 90, inDuty: false));

        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.Equal(NexusTaskStatus.Failed, coordinator.State.Tasks.Single().Status);
        Assert.Equal("provider-ended-without-completion", coordinator.State.Tasks.Single().Failure!.Code);
        Assert.Single(provider.StartedTerritories);
    }

    [Fact]
    public void CompletionForDifferentDutyIsIgnored()
    {
        FakeDutyProvider provider = new();
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(targetLevel: 95), Plan());

        provider.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(999);
        provider.IsStopped = true;
        coordinator.Update(World(level: 90, inDuty: false));

        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.False(coordinator.State.DutyCompletionObserved);
    }

    [Fact]
    public void StableRunningObservationDoesNotRewriteDurableStateEveryFrame()
    {
        MemoryStore store = new();
        FakeDutyProvider provider = new();
        ProgressionExecutionCoordinator coordinator = new(store, new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(), Plan());
        provider.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: false));
        int savesAfterRunningCheckpoint = store.SaveCalls;

        coordinator.Update(World(level: 90, inDuty: false));
        coordinator.Update(World(level: 90, inDuty: false));

        Assert.Equal(savesAfterRunningCheckpoint, store.SaveCalls);
    }

    [Fact]
    public void ChangingJobsStopsAndBlocksTheOwnedDuty()
    {
        FakeDutyProvider provider = new() { IsStopped = false };
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(), Plan());

        coordinator.Update(new ProgressionWorldObservation(Character, 25, 90, true, false));

        Assert.Equal(1, provider.StopCalls);
        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.Equal(NexusTaskStatus.NeedsReconciliation, coordinator.State.ActiveTask!.Status);
        Assert.Equal("job-changed", coordinator.State.ActiveTask.Failure!.Code);

        provider.IsStopped = true;
        coordinator.Update(new ProgressionWorldObservation(Character, 25, 90, true, false));
        Assert.Null(coordinator.State.ActiveTaskId);
        Assert.Equal(NexusTaskStatus.Failed, coordinator.State.Tasks.Single().Status);
    }

    [Fact]
    public void ProviderLossRetainsOwnershipUntilInactivityIsConfirmed()
    {
        FakeDutyProvider provider = new() { IsStopped = false };
        ResourceLeaseManager leases = new();
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), leases, provider);
        coordinator.Start(Draft(), Plan());
        coordinator.Update(World(level: 90, inDuty: false));

        provider.Available = false;
        coordinator.Update(World(level: 90, inDuty: false));

        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.NotNull(coordinator.State.ActiveTaskId);
        Assert.NotEmpty(leases.Snapshot());

        provider.Available = true;
        provider.IsStopped = true;
        coordinator.Update(World(level: 90, inDuty: false));
        Assert.Null(coordinator.State.ActiveTaskId);
        Assert.Empty(leases.Snapshot());
        Assert.Equal("provider-unavailable", coordinator.State.Tasks.Single().Failure!.Code);
    }

    [Fact]
    public void ReloadStopsAndPausesWithoutReplayingTask()
    {
        MemoryStore store = new();
        FakeDutyProvider firstProvider = new() { IsStopped = false };
        ProgressionExecutionCoordinator first = new(store, new ResourceLeaseManager(), firstProvider);
        first.Start(Draft(), Plan());
        first.Update(World(level: 90, inDuty: true));

        FakeDutyProvider restoredProvider = new() { IsStopped = false };
        ProgressionExecutionCoordinator restored = new(store, new ResourceLeaseManager(), restoredProvider);
        restored.Update(World(level: 90, inDuty: true));
        Assert.Equal(1, restoredProvider.StopCalls);
        Assert.Empty(restoredProvider.StartedTerritories);

        restoredProvider.IsStopped = true;
        restored.Update(World(level: 90, inDuty: false));
        Assert.Equal(GoalStatus.Paused, restored.State!.Goal.Status);
        Assert.Null(restored.State.ActiveTaskId);
        Assert.Empty(restoredProvider.StartedTerritories);
    }

    [Fact]
    public void ShutdownCheckpointReconcilesOnNextLoadWithoutGettingStuck()
    {
        MemoryStore store = new();
        FakeDutyProvider firstProvider = new() { IsStopped = false };
        ProgressionExecutionCoordinator first = new(store, new ResourceLeaseManager(), firstProvider);
        first.Start(Draft(), Plan());
        first.Update(World(level: 90, inDuty: true));
        first.Shutdown();
        Assert.Equal(NexusTaskStatus.NeedsReconciliation, store.State!.ActiveTask!.Status);

        FakeDutyProvider restoredProvider = new() { IsStopped = true };
        ProgressionExecutionCoordinator restored = new(store, new ResourceLeaseManager(), restoredProvider);
        restored.Update(World(level: 90, inDuty: false));

        Assert.Equal(GoalStatus.Paused, restored.State!.Goal.Status);
        Assert.Null(restored.State.ActiveTaskId);
        Assert.Empty(restoredProvider.StartedTerritories);
    }

    [Fact]
    public void ResourceConflictBlocksGoalWithoutCallingProvider()
    {
        ResourceLeaseManager leases = new();
        leases.TryAcquire(
            new LeaseOwner(GoalId.New(), TaskId.New(), AttemptId.New(), 1, "existing navigation"),
            [ResourceKind.Navigation],
            TimeSpan.FromMinutes(1),
            out ResourceLeaseHandle? blocker,
            out _);
        using (blocker)
        {
            FakeDutyProvider provider = new();
            ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), leases, provider);
            ProgressionActionResult result = coordinator.Start(Draft(), Plan());

            Assert.False(result.Success);
            Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
            Assert.Empty(provider.StartedTerritories);
            Assert.Equal("resource-conflict", coordinator.State.Tasks.Single().Failure!.Code);
        }
    }

    [Fact]
    public void FileStoreRoundTripsAndPreservesPreviousDocument()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"VieriNexus-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "progression.v1.json");
        try
        {
            MemoryStore memory = new();
            ProgressionExecutionCoordinator coordinator = new(memory, new ResourceLeaseManager(), new FakeDutyProvider());
            coordinator.Start(Draft(), Plan());
            ProgressionGoalState first = coordinator.State!;
            FileProgressionGoalStore store = new(path);
            store.Save(first);
            store.Save(first with { StopAfterCurrentDuty = true });

            ProgressionGoalState loaded = store.Load()!;
            Assert.True(loaded.StopAfterCurrentDuty);
            Assert.Equal(first.Goal.Id, loaded.Goal.Id);
            Assert.True(File.Exists(path + ".previous"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static ReachJobLevelGoalDraft Draft(int targetLevel = 92) => new(
        Character,
        41,
        90,
        targetLevel,
        true,
        true,
        true,
        true,
        1_000_000);

    private static ReachJobLevelPlan Plan() => new(
        true,
        false,
        true,
        true,
        "ready",
        "ready",
        [],
        []);

    private static ProgressionWorldObservation World(int level, bool inDuty) =>
        new(Character, 41, level, true, inDuty);

    private sealed class MemoryStore : IProgressionGoalStore
    {
        public ProgressionGoalState? State { get; private set; }
        public int SaveCalls { get; private set; }

        public ProgressionGoalState? Load() => State;

        public void Save(ProgressionGoalState state)
        {
            State = state;
            SaveCalls++;
        }
    }

    private sealed class FakeDutyProvider : IProgressionDutyProvider
    {
        public ProviderId Id { get; } = new("provider.test/v1");
        public bool Available { get; set; } = true;
        public bool IsStopped { get; set; } = true;
        public List<uint> StartedTerritories { get; } = [];
        public int StopCalls { get; private set; }

        public IReadOnlyList<ProgressionDutyCandidate> EligibleDuties(int currentLevel) =>
        [
            new(100, 10, "Lower Duty", 80, 500),
            new(200, 20, "Highest Duty", 90, 650),
        ];

        public ProgressionDutyProviderObservation Observe() =>
            new(Available, Available ? IsStopped : null, Available ? "test" : "missing");

        public bool TryStart(uint territoryId, out string message)
        {
            if (!Available)
            {
                message = "missing";
                return false;
            }

            StartedTerritories.Add(territoryId);
            message = "started";
            return true;
        }

        public bool TryStop(out string message)
        {
            StopCalls++;
            message = Available ? "stop requested" : "missing";
            return Available;
        }
    }

    private sealed class FakeGearProvider : IProgressionGearProvider
    {
        private bool isBusy;
        public ProviderId Id { get; } = new("provider.gear-test/v1");
        public bool Available { get; set; } = true;
        public bool IsBusy
        {
            get => isBusy;
            set
            {
                if (isBusy && !value)
                    CompletedSequence++;
                isBusy = value;
            }
        }
        public int ItemsPurchased { get; set; }
        public int StopCalls { get; private set; }
        public List<int> StartedGilFloors { get; } = [];
        public long StartedSequence { get; private set; }
        public long CompletedSequence { get; private set; }

        public ProgressionGearProviderObservation ObserveGearReadiness() => new(
            Available,
            Available ? IsBusy : null,
            StartedSequence,
            CompletedSequence,
            640,
            650,
            ItemsPurchased,
            Available ? "test gear" : "missing gear");

        public bool TryStartGearReadiness(int minimumGilReserve, out string message)
        {
            if (!Available)
            {
                message = "missing gear";
                return false;
            }
            StartedGilFloors.Add(minimumGilReserve);
            StartedSequence++;
            message = "started gear";
            return true;
        }

        public bool TryStopGearReadiness(out string message)
        {
            StopCalls++;
            message = Available ? "gear stop requested" : "missing gear";
            return Available;
        }

        public void EndWithoutCompletion() => isBusy = false;
    }
}
