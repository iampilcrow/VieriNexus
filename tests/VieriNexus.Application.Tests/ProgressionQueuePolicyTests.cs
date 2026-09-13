using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class ProgressionQueuePolicyTests
{
    [Fact]
    public void FindsNextIncompleteEnabledStepInOrder()
    {
        ProgressionQueueStepConfiguration[] steps =
        [
            new() { Enabled = false },
            new() { Status = ProgressionQueueStepStatus.Completed },
            new() { ClassJobId = 31 },
            new() { ClassJobId = 35 },
        ];

        Assert.Equal(2, ProgressionQueuePolicy.FindNextEnabledIndex(steps, 0));
        Assert.Equal(3, ProgressionQueuePolicy.FindNextEnabledIndex(steps, 3));
        Assert.Equal(-1, ProgressionQueuePolicy.FindNextEnabledIndex(steps, 4));
    }

    [Fact]
    public void AutomaticMethodUsesImportedSettingsAndBaseQuestChoices()
    {
        ProgressionQueueSettingsConfiguration settings = new()
        {
            AutomaticUsesHuntingLog = true,
            AutomaticUsesSideQuests = false,
            AutomaticUsesDuties = true,
        };

        ProgressionQueueMethodSelection result = ProgressionQueuePolicy.SelectMethods(
            ProgressionQueueMethod.Automatic, settings, true, true);

        Assert.True(result.AllowMainScenario);
        Assert.True(result.AllowClassJobRole);
        Assert.True(result.AllowHuntingLog);
        Assert.False(result.AllowSideQuests);
        Assert.True(result.AllowDuties);
    }

    [Theory]
    [InlineData(ProgressionQueueMethod.HuntingLog, true, false, false)]
    [InlineData(ProgressionQueueMethod.SideQuests, false, true, false)]
    [InlineData(ProgressionQueueMethod.Duties, false, false, true)]
    public void ExplicitMethodSelectsOnlyThatLevelingLane(
        ProgressionQueueMethod method,
        bool hunting,
        bool sideQuests,
        bool duties)
    {
        ProgressionQueueMethodSelection result = ProgressionQueuePolicy.SelectMethods(
            method, new ProgressionQueueSettingsConfiguration(), true, true);

        Assert.False(result.AllowMainScenario);
        Assert.False(result.AllowClassJobRole);
        Assert.Equal(hunting, result.AllowHuntingLog);
        Assert.Equal(sideQuests, result.AllowSideQuests);
        Assert.Equal(duties, result.AllowDuties);
    }

    [Fact]
    public void NormalizeRepairsUnsafePersistedStateAndDuplicateIds()
    {
        Guid duplicate = Guid.NewGuid();
        ProgressionQueueConfiguration queue = new()
        {
            IsRunning = false,
            State = ProgressionQueueRuntimeState.RunningGoal,
            CurrentIndex = 9,
            Steps =
            [
                new() { Id = duplicate, ClassJobId = 999, TargetLevel = 999 },
                new() { Id = duplicate, Enabled = false },
            ],
        };

        ProgressionQueuePolicy.Normalize(queue);

        Assert.Equal(-1, queue.CurrentIndex);
        Assert.True(queue.IsPaused);
        Assert.Equal(ProgressionQueueRuntimeState.Paused, queue.State);
        Assert.Equal(43u, queue.Steps[0].ClassJobId);
        Assert.Equal(100, queue.Steps[0].TargetLevel);
        Assert.NotEqual(queue.Steps[0].Id, queue.Steps[1].Id);
        Assert.Equal(ProgressionQueueStepStatus.Disabled, queue.Steps[1].Status);
    }

    [Fact]
    public void ImportsEveryCodexStepAndQueueSettingWithoutStartingWork()
    {
        CodexMigrationSnapshot snapshot = new(
            1, 2, true, true, true, true, true, true, true, true, true, true, true, true, 90,
            [
                new CodexQueueStepSnapshot(Guid.NewGuid(), true, 31, 90, 0, 4),
                new CodexQueueStepSnapshot(Guid.NewGuid(), false, 35, 100, 3, 2),
            ],
            new CodexQueueSettingsSnapshot(true, true, false, true, true, false, true, 1));

        ProgressionQueueConfiguration queue = ProgressionQueuePolicy.Import(snapshot);

        Assert.Equal(2, queue.Steps.Count);
        Assert.Equal(31u, queue.Steps[0].ClassJobId);
        Assert.Equal(ProgressionQueueFallbackPolicy.SideQuests, queue.Steps[0].FallbackPolicy);
        Assert.Equal(ProgressionQueueStepStatus.Disabled, queue.Steps[1].Status);
        Assert.False(queue.Settings.AutomaticallyAdvance);
        Assert.False(queue.Settings.AutomaticUsesSideQuests);
        Assert.Equal(ProgressionQueueFallbackPolicy.StopQueue, queue.Settings.OnStepFailure);
        Assert.False(queue.IsRunning);
        Assert.Null(queue.ActiveGoalId);
    }
}
