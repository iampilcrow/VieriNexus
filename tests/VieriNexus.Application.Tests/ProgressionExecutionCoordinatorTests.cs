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
        Assert.Contains("Current activity complete", coordinator.State.Goal.StatusDetail);
    }

    [Fact]
    public void PauseStopsCurrentDutyAndResumesOnlyFromAFreshPlan()
    {
        FakeDutyProvider provider = new() { IsStopped = false };
        ProgressionExecutionCoordinator coordinator = new(new MemoryStore(), new ResourceLeaseManager(), provider);
        coordinator.Start(Draft(targetLevel: 95), Plan());
        coordinator.Update(World(level: 90, inDuty: true));

        ProgressionActionResult paused = coordinator.PauseNow();
        Assert.True(paused.Success);
        Assert.Equal(1, provider.StopCalls);

        provider.IsStopped = true;
        coordinator.Update(World(level: 90, inDuty: false));

        Assert.Equal(GoalStatus.Paused, coordinator.State!.Goal.Status);
        Assert.Null(coordinator.State.ActiveTaskId);
        Assert.Single(provider.StartedTerritories);
        Assert.Contains("will not replay", coordinator.State.Goal.StatusDetail);
    }

    [Fact]
    public void LastRunSkipsConfiguredMaintenanceAfterVerifiedDuty()
    {
        FakeDutyProvider duty = new();
        FakeMaintenanceProvider maintenance = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, maintenanceProvider: maintenance);
        coordinator.Start(Draft(targetLevel: 95), Plan());
        Assert.True(coordinator.StopAfterCurrentDuty().Success);

        duty.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(200);
        duty.IsStopped = true;
        coordinator.Update(World(level: 91, inDuty: false));

        Assert.Equal(GoalStatus.Paused, coordinator.State!.Goal.Status);
        Assert.Equal(0, maintenance.StartCalls);
        Assert.Single(duty.StartedTerritories);
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
    public void CompletedDutyRunsConfiguredMaintenanceBeforePlanningAnotherDuty()
    {
        FakeDutyProvider duty = new();
        FakeMaintenanceProvider maintenance = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(),
            new ResourceLeaseManager(),
            duty,
            maintenanceProvider: maintenance);
        coordinator.Start(Draft(targetLevel: 95), Plan());

        duty.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(200);
        duty.IsStopped = true;
        coordinator.Update(World(level: 91, inDuty: false));

        Assert.Single(duty.StartedTerritories);
        Assert.Equal(1, maintenance.StartCalls);
        Assert.Equal("vieri.inventory.run-between-duty-maintenance/v1", coordinator.State!.ActiveTask!.Kind.Value);

        maintenance.Complete();
        coordinator.Update(World(level: 91, inDuty: false));

        Assert.Equal(2, duty.StartedTerritories.Count);
        Assert.Equal(NexusTaskStatus.Succeeded, coordinator.State.Tasks[1].Status);
        Assert.Equal("vieri.duties.run-one/v1", coordinator.State.ActiveTask!.Kind.Value);
    }

    [Fact]
    public void StopDuringBetweenDutyMaintenanceUsesMaintenanceStopOnly()
    {
        FakeDutyProvider duty = new();
        FakeMaintenanceProvider maintenance = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(),
            new ResourceLeaseManager(),
            duty,
            maintenanceProvider: maintenance);
        coordinator.Start(Draft(targetLevel: 95), Plan());
        duty.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(200);
        duty.IsStopped = true;
        coordinator.Update(World(level: 91, inDuty: false));

        ProgressionActionResult stopped = coordinator.StopNow();
        maintenance.IsBusy = false;
        coordinator.Update(World(level: 91, inDuty: false));

        Assert.True(stopped.Success);
        Assert.Equal(1, maintenance.StopCalls);
        Assert.Equal(0, duty.StopCalls);
        Assert.Equal(GoalStatus.Cancelled, coordinator.State!.Goal.Status);
        Assert.Equal(2, coordinator.State.Tasks.Count);
    }

    [Fact]
    public void VerifiedMaintenanceReturnsThroughGearReadinessBeforeNextDuty()
    {
        FakeDutyProvider duty = new();
        FakeGearProvider gear = new();
        FakeMaintenanceProvider maintenance = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(),
            new ResourceLeaseManager(),
            duty,
            gear,
            maintenanceProvider: maintenance);
        ProgressionWorldObservation world = World(90, false) with { ItemLevel = 640, Gil = 1_500_000 };
        coordinator.Start(Draft(targetLevel: 95) with { CurrentItemLevel = 640, CurrentGil = 1_500_000 }, Plan());
        gear.IsBusy = true;
        coordinator.Update(world);
        gear.IsBusy = false;
        coordinator.Update(world);
        Assert.Single(duty.StartedTerritories);

        duty.IsStopped = false;
        coordinator.Update(world with { IsInDuty = true });
        coordinator.ObserveDutyCompletion(200);
        duty.IsStopped = true;
        ProgressionWorldObservation returned = world with { Level = 91 };
        coordinator.Update(returned);
        maintenance.Complete();
        coordinator.Update(returned);

        Assert.Single(duty.StartedTerritories);
        Assert.Equal(2, gear.StartedGilFloors.Count);
        Assert.Equal("vieri.gear.ensure-readiness/v1", coordinator.State!.ActiveTask!.Kind.Value);
    }

    [Fact]
    public void FailedMaintenanceCannotBeCountedAsCompleteOrStartAnotherDuty()
    {
        FakeDutyProvider duty = new();
        FakeMaintenanceProvider maintenance = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, maintenanceProvider: maintenance);
        coordinator.Start(Draft(targetLevel: 95), Plan());

        duty.IsStopped = false;
        coordinator.Update(World(level: 90, inDuty: true));
        coordinator.ObserveDutyCompletion(200);
        duty.IsStopped = true;
        coordinator.Update(World(level: 91, inDuty: false));
        maintenance.Fail();
        coordinator.Update(World(level: 91, inDuty: false));

        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.Null(coordinator.State.ActiveTask);
        Assert.Equal("maintenance-not-completed", coordinator.State.Tasks[1].Failure!.Code);
        Assert.Single(duty.StartedTerritories);
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

    [Fact]
    public void ExactClassJobRoleQuestRunsBeforeDutyAndMustComplete()
    {
        FakeDutyProvider duty = new();
        FakeQuestProvider quest = new();
        ResourceLeaseManager leases = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), leases, duty, gearProvider: null, questProvider: quest);

        ProgressionActionResult result = coordinator.Start(Draft(), Plan());

        Assert.True(result.Success);
        Assert.Equal(["500"], quest.StartedQuestIds);
        Assert.Empty(duty.StartedTerritories);
        Assert.Equal("vieri.quest.run-one/v1", coordinator.State!.ActiveTask!.Kind.Value);
        Assert.Contains(ResourceKind.Navigation, coordinator.State.ActiveTask.RequiredResources);
        Assert.Contains(ResourceKind.Combat, coordinator.State.ActiveTask.RequiredResources);

        quest.IsRunning = true;
        quest.CurrentQuestId = "500";
        coordinator.Update(World(90, false));
        Assert.Equal(NexusTaskStatus.Running, coordinator.State.ActiveTask!.Status);

        quest.IsComplete = true;
        quest.IsRunning = false;
        quest.CurrentQuestId = null;
        coordinator.Update(World(90, false));

        Assert.Equal(NexusTaskStatus.Succeeded, coordinator.State!.Tasks[0].Status);
        Assert.Equal([200u], duty.StartedTerritories);
        Assert.Equal("vieri.duties.run-one/v1", coordinator.State.ActiveTask!.Kind.Value);
    }

    [Fact]
    public void QuestOnlyGoalCanStartWithoutDuties()
    {
        FakeDutyProvider duty = new();
        FakeQuestProvider quest = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, gearProvider: null, questProvider: quest);

        ProgressionActionResult result = coordinator.Start(
            Draft() with { AllowDuties = false, AllowJobQuests = true }, Plan());

        Assert.True(result.Success);
        Assert.Equal(["500"], quest.StartedQuestIds);
        Assert.Empty(duty.StartedTerritories);
    }

    [Fact]
    public void MainScenarioIsSelectedBeforeOtherUnacceptedQuestKinds()
    {
        FakeQuestProvider quest = new()
        {
            Candidates =
            [
                new("500", "A Test of the Job", 80, false),
                new("600", "The Next Main Scenario Quest", 80, false, ProgressionQuestKind.MainScenario),
                new("700", "An Ordinary Side Quest", 80, false, ProgressionQuestKind.GeneralSideQuest),
            ],
        };
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), new FakeDutyProvider(),
            gearProvider: null, questProvider: quest);

        ProgressionActionResult result = coordinator.Start(
            Draft() with { AllowDuties = false, AllowHuntingLog = false, AllowMainScenario = true }, Plan());

        Assert.True(result.Success);
        Assert.Equal(["600"], quest.StartedQuestIds);
        ProgressionQuestTaskPayload payload = System.Text.Json.JsonSerializer.Deserialize<ProgressionQuestTaskPayload>(
            coordinator.State!.ActiveTask!.PayloadJson)!;
        Assert.Equal(ProgressionQuestKind.MainScenario, payload.Kind);
    }

    [Fact]
    public void GeneralSideQuestOnlyGoalStartsAndPersistsItsExactKind()
    {
        FakeDutyProvider duty = new();
        FakeQuestProvider quest = new()
        {
            Candidates =
            [
                new("700", "An Ordinary Side Quest", 80, false, ProgressionQuestKind.GeneralSideQuest),
            ],
        };
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, gearProvider: null, questProvider: quest);

        ProgressionActionResult result = coordinator.Start(
            Draft() with { AllowDuties = false, AllowJobQuests = false, AllowHuntingLog = false, AllowSideQuests = true },
            Plan());

        Assert.True(result.Success);
        Assert.Equal(["700"], quest.StartedQuestIds);
        ProgressionQuestTaskPayload payload = System.Text.Json.JsonSerializer.Deserialize<ProgressionQuestTaskPayload>(
            coordinator.State!.ActiveTask!.PayloadJson)!;
        Assert.Equal(ProgressionQuestKind.GeneralSideQuest, payload.Kind);
        Assert.Empty(duty.StartedTerritories);
    }

    [Fact]
    public void MissingQuestPathIsSkippedAndNextSupportedQuestStartsAutomatically()
    {
        FakeQuestProvider quest = new()
        {
            Candidates =
            [
                new("700", "Unsupported Side Quest", 80, false, ProgressionQuestKind.GeneralSideQuest),
                new("701", "Supported Side Quest", 81, false, ProgressionQuestKind.GeneralSideQuest),
            ],
        };
        quest.UnsupportedQuestIds.Add("700");
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), new FakeDutyProvider(),
            gearProvider: null, questProvider: quest);

        ProgressionActionResult result = coordinator.Start(
            Draft() with { AllowDuties = false, AllowJobQuests = false, AllowHuntingLog = false, AllowSideQuests = true },
            Plan());

        Assert.True(result.Success);
        Assert.Equal(["700", "701"], quest.StartedQuestIds);
        Assert.Equal(NexusTaskStatus.Cancelled, coordinator.State!.Tasks[0].Status);
        Assert.Equal("quest-path-unsupported", coordinator.State.Tasks[0].Failure!.Code);
        Assert.Equal("701", System.Text.Json.JsonSerializer.Deserialize<ProgressionQuestTaskPayload>(
            coordinator.State.ActiveTask!.PayloadJson)!.QuestId);
    }

    [Fact]
    public void StopDuringQuestUsesQuestStopAndNotDutyStop()
    {
        FakeDutyProvider duty = new();
        FakeQuestProvider quest = new() { IsRunning = true, CurrentQuestId = "500" };
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), duty, gearProvider: null, questProvider: quest);
        coordinator.Start(Draft(), Plan());
        coordinator.Update(World(90, false));

        ProgressionActionResult result = coordinator.StopNow();

        Assert.True(result.Success);
        Assert.Equal(1, quest.StopCalls);
        Assert.Equal(0, duty.StopCalls);
    }

    [Fact]
    public void ProviderQuestMismatchStopsAndBlocksOwnership()
    {
        FakeQuestProvider quest = new() { IsRunning = true, CurrentQuestId = "999" };
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(), new ResourceLeaseManager(), new FakeDutyProvider(),
            gearProvider: null, questProvider: quest);
        coordinator.Start(Draft(), Plan());

        coordinator.Update(World(90, false));

        Assert.Equal(GoalStatus.Blocked, coordinator.State!.Goal.Status);
        Assert.Equal("quest-provider-mismatch", coordinator.State.ActiveTask!.Failure!.Code);
        Assert.Equal(1, quest.StopCalls);
    }

    [Fact]
    public void HuntingLogTargetRunsAsExactOwnedTaskAndRequiresVerifiedCredit()
    {
        FakeHuntingProvider hunt = new();
        ResourceLeaseManager leases = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(),
            leases,
            new FakeDutyProvider(),
            gearProvider: null,
            questProvider: null,
            huntingProvider: hunt);

        ProgressionActionResult started = coordinator.Start(
            Draft() with
            {
                AllowDuties = false,
                AllowJobQuests = false,
                AllowSideQuests = false,
                AllowHuntingLog = true,
            },
            Plan());

        Assert.True(started.Success);
        Assert.Equal("vieri.hunting-log.complete-target/v1", coordinator.State!.ActiveTask!.Kind.Value);
        Assert.Contains(ResourceKind.Teleport, coordinator.State.ActiveTask.RequiredResources);
        Assert.Contains(ResourceKind.Rotation, coordinator.State.ActiveTask.RequiredResources);
        Assert.Equal(1, hunt.StartCalls);

        coordinator.Update(World(90, false));
        Assert.Equal(NexusTaskStatus.Running, coordinator.State.ActiveTask!.Status);

        hunt.Killed = hunt.Required;
        hunt.IsComplete = true;
        hunt.IsBusy = false;
        coordinator.Update(World(92, false));

        Assert.Equal(NexusTaskStatus.Succeeded, coordinator.State!.Tasks.Single().Status);
        Assert.Equal(GoalStatus.Satisfied, coordinator.State.Goal.Status);
        Assert.Empty(leases.Snapshot());
    }

    [Fact]
    public void DutyOnlyGrandCompanyTargetOwnsDutyQueueAndNotNavigation()
    {
        FakeHuntingProvider hunt = new() { DutyOnly = true };
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(),
            new ResourceLeaseManager(),
            new FakeDutyProvider(),
            gearProvider: null,
            questProvider: null,
            huntingProvider: hunt);

        ProgressionActionResult started = coordinator.Start(
            Draft() with
            {
                AllowDuties = false,
                AllowJobQuests = false,
                AllowSideQuests = false,
                AllowHuntingLog = true,
            },
            Plan());

        Assert.True(started.Success);
        Assert.Contains(ResourceKind.DutyQueue, coordinator.State!.ActiveTask!.RequiredResources);
        Assert.Contains(ResourceKind.UiInteraction, coordinator.State.ActiveTask.RequiredResources);
        Assert.DoesNotContain(ResourceKind.Navigation, coordinator.State.ActiveTask.RequiredResources);
    }

    [Fact]
    public void StopDuringHuntingLogUsesHuntStopAndCancelsAfterInactivity()
    {
        FakeDutyProvider duty = new();
        FakeHuntingProvider hunt = new();
        ProgressionExecutionCoordinator coordinator = new(
            new MemoryStore(),
            new ResourceLeaseManager(),
            duty,
            gearProvider: null,
            questProvider: null,
            huntingProvider: hunt);
        coordinator.Start(
            Draft() with
            {
                AllowDuties = false,
                AllowJobQuests = false,
                AllowSideQuests = false,
                AllowHuntingLog = true,
            },
            Plan());
        coordinator.Update(World(90, false));

        ProgressionActionResult stopped = coordinator.StopNow();
        coordinator.Update(World(90, false));

        Assert.True(stopped.Success);
        Assert.Equal(1, hunt.StopCalls);
        Assert.Equal(0, duty.StopCalls);
        Assert.Equal(GoalStatus.Cancelled, coordinator.State!.Goal.Status);
        Assert.Null(coordinator.State.ActiveTask);
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

    private sealed class FakeMaintenanceProvider : IProgressionMaintenanceProvider
    {
        public ProviderId Id { get; } = new("provider.maintenance-test/v1");
        public bool Available { get; set; } = true;
        public bool HasConfiguredOperations { get; set; } = true;
        public bool IsBusy { get; set; }
        public long StartedSequence { get; private set; }
        public long CompletedSequence { get; private set; }
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }

        public ProgressionMaintenanceProviderObservation ObserveMaintenance() => new(
            Available,
            HasConfiguredOperations,
            Available ? IsBusy : null,
            StartedSequence,
            CompletedSequence,
            IsBusy ? "running maintenance" : "maintenance inactive");

        public bool TryStartConfiguredMaintenance(out string message)
        {
            StartCalls++;
            if (!Available || !HasConfiguredOperations)
            {
                message = "maintenance unavailable";
                return false;
            }
            StartedSequence++;
            IsBusy = true;
            message = "started maintenance";
            return true;
        }

        public bool TryStopMaintenance(out string message)
        {
            StopCalls++;
            message = Available ? "maintenance stop requested" : "maintenance unavailable";
            return Available;
        }

        public void Complete()
        {
            IsBusy = false;
            CompletedSequence = StartedSequence;
        }

        public void Fail() => IsBusy = false;
    }

    private sealed class FakeQuestProvider : IProgressionQuestProvider
    {
        public ProviderId Id { get; } = new("provider.quest-test/v1");
        public bool Available { get; set; } = true;
        public bool IsRunning { get; set; }
        public bool IsComplete { get; set; }
        public string? CurrentQuestId { get; set; }
        public List<string> StartedQuestIds { get; } = [];
        public IReadOnlyList<ProgressionQuestCandidate> Candidates { get; init; } =
            [new("500", "A Test of the Job", 90, false)];
        public HashSet<string> UnsupportedQuestIds { get; } = [];
        private HashSet<string> RejectedQuestIds { get; } = [];
        public int StopCalls { get; private set; }

        public IReadOnlyList<ProgressionQuestCandidate> EligibleQuests(
            uint classJobId,
            int currentLevel,
            bool includeMainScenario,
            bool includeClassJobRole,
            bool includeGeneralSideQuests) => IsComplete
            ? []
            : Candidates
                .Where(candidate => !RejectedQuestIds.Contains(candidate.QuestId))
                .Where(candidate => candidate.Kind switch
                {
                    ProgressionQuestKind.MainScenario => includeMainScenario,
                    ProgressionQuestKind.ClassJobRole => includeClassJobRole,
                    _ => includeGeneralSideQuests,
                })
                .ToArray();

        public ProgressionQuestProviderObservation ObserveQuest(string questId) => new(
            Available,
            Available ? IsRunning : null,
            CurrentQuestId,
            Available ? IsComplete : null,
            Available ? "test quest" : "missing quest");

        public ProgressionQuestStartResult TryStartQuest(ProgressionQuestCandidate quest)
        {
            if (!Available)
                return new(false, false, "missing quest");
            StartedQuestIds.Add(quest.QuestId);
            if (UnsupportedQuestIds.Contains(quest.QuestId))
            {
                RejectedQuestIds.Add(quest.QuestId);
                return new(false, true, "unsupported quest");
            }
            return new(true, false, "started quest");
        }

        public bool TryStopQuest(out string message)
        {
            StopCalls++;
            message = Available ? "quest stop requested" : "missing quest";
            return Available;
        }
    }

    private sealed class FakeHuntingProvider : IProgressionHuntingProvider
    {
        private static readonly ProgressionHuntingTargetCandidate Candidate = new(
            1,
            "Lancer Hunting Log",
            0,
            0,
            0,
            123,
            "Little Ladybug",
            0,
            3,
            [new HuntingLogLocation(134, 13, 0, 20f, 20f)]);

        public ProviderId Id { get; } = new("provider.hunting-test/v1");
        public bool Available { get; set; } = true;
        public bool IsBusy { get; set; }
        public bool IsComplete { get; set; }
        public bool HasFailed { get; set; }
        public int Killed { get; set; }
        public int Required => Candidate.Required;
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public bool DutyOnly { get; init; }

        public IReadOnlyList<ProgressionHuntingTargetCandidate> EligibleTargets(uint classJobId, int currentLevel) =>
            IsComplete
                ? []
                : [Candidate with
                {
                    Killed = Killed,
                    Locations = DutyOnly
                        ? [new HuntingLogLocation(0, 0, 1245, 0f, 0f)]
                        : Candidate.Locations,
                }];

        public ProgressionHuntingProviderObservation ObserveHunt(ProgressionHuntingTargetCandidate target) => new(
            Available,
            Available ? IsBusy : null,
            IsComplete,
            HasFailed,
            Killed,
            Required,
            IsBusy ? "hunting" : "idle");

        public bool TryStartHunt(ProgressionHuntingTargetCandidate target, out string message)
        {
            StartCalls++;
            IsBusy = Available;
            message = Available ? "hunt started" : "hunt unavailable";
            return Available;
        }

        public bool TryStopHunt(out string message)
        {
            StopCalls++;
            IsBusy = false;
            message = Available ? "hunt stopped" : "hunt unavailable";
            return Available;
        }
    }
}
