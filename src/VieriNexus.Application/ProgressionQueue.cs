namespace VieriNexus.Application;

public enum ProgressionQueueMethod
{
    Automatic,
    HuntingLog,
    SideQuests,
    Duties,
}

public enum ProgressionQueueFallbackPolicy
{
    PauseQueue,
    StopQueue,
    SkipStep,
    Automatic,
    SideQuests,
    Duties,
}

public enum ProgressionQueueStepStatus
{
    Waiting,
    Current,
    SwitchingJob,
    Running,
    Paused,
    Completed,
    AlreadySatisfied,
    Disabled,
    Blocked,
    Skipped,
}

public enum ProgressionQueueRuntimeState
{
    Stopped,
    PreparingStep,
    WaitingForSafeState,
    SwitchingJob,
    StartingGoal,
    RunningGoal,
    Paused,
    Completed,
}

[Serializable]
public sealed class ProgressionQueueStepConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public uint ClassJobId { get; set; }
    public int TargetLevel { get; set; } = 100;
    public ProgressionQueueMethod Method { get; set; }
    public ProgressionQueueFallbackPolicy FallbackPolicy { get; set; }
    public ProgressionQueueStepStatus Status { get; set; } = ProgressionQueueStepStatus.Waiting;
    public string StatusDetail { get; set; } = string.Empty;
}

[Serializable]
public sealed class ProgressionQueueSettingsConfiguration
{
    public bool SkipTargetsAlreadyReached { get; set; } = true;
    public bool AutomaticallySwitchJobs { get; set; } = true;
    public bool AutomaticallyAdvance { get; set; } = true;
    public bool ResumeAfterRestart { get; set; } = true;
    public bool AutomaticUsesHuntingLog { get; set; } = true;
    public bool AutomaticUsesSideQuests { get; set; } = true;
    public bool AutomaticUsesDuties { get; set; } = true;
    public ProgressionQueueFallbackPolicy OnStepFailure { get; set; }
}

[Serializable]
public sealed class ProgressionQueueConfiguration
{
    public List<ProgressionQueueStepConfiguration> Steps { get; set; } = [];
    public ProgressionQueueSettingsConfiguration Settings { get; set; } = new();
    public int CurrentIndex { get; set; } = -1;
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }
    public ProgressionQueueRuntimeState State { get; set; }
    public Guid? ActiveGoalId { get; set; }
    public ProgressionQueueMethod? EffectiveMethod { get; set; }
    public string StatusDetail { get; set; } = "Build an ordered queue, then start it.";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record ProgressionQueueMethodSelection(
    bool AllowMainScenario,
    bool AllowClassJobRole,
    bool AllowHuntingLog,
    bool AllowSideQuests,
    bool AllowDuties);

public static class ProgressionQueuePolicy
{
    public static ProgressionQueueConfiguration Import(CodexMigrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ProgressionQueueConfiguration queue = new()
        {
            Steps = snapshot.QueueSteps.Select(step => new ProgressionQueueStepConfiguration
            {
                Id = step.Id == Guid.Empty ? Guid.NewGuid() : step.Id,
                Enabled = step.Enabled,
                ClassJobId = step.ClassJobId,
                TargetLevel = step.TargetLevel,
                Method = (ProgressionQueueMethod)Math.Clamp(step.Method, 0, 3),
                FallbackPolicy = (ProgressionQueueFallbackPolicy)Math.Clamp(step.FallbackPolicy, 0, 5),
                Status = step.Enabled
                    ? ProgressionQueueStepStatus.Waiting
                    : ProgressionQueueStepStatus.Disabled,
            }).ToList(),
            Settings = new ProgressionQueueSettingsConfiguration
            {
                SkipTargetsAlreadyReached = snapshot.QueueSettings.SkipTargetsAlreadyReached,
                AutomaticallySwitchJobs = snapshot.QueueSettings.AutomaticallySwitchJobs,
                AutomaticallyAdvance = snapshot.QueueSettings.AutomaticallyAdvance,
                ResumeAfterRestart = snapshot.QueueSettings.ResumeAfterRestart,
                AutomaticUsesHuntingLog = snapshot.QueueSettings.AutomaticUsesHuntingLog,
                AutomaticUsesSideQuests = snapshot.QueueSettings.AutomaticUsesSideQuests,
                AutomaticUsesDuties = snapshot.QueueSettings.AutomaticUsesDungeonGrind,
                OnStepFailure = (ProgressionQueueFallbackPolicy)Math.Clamp(snapshot.QueueSettings.OnStepFailure, 0, 2),
            },
            StatusDetail = snapshot.QueueSteps.Count == 0
                ? "No VieriCodex queue steps were saved. Add jobs in Nexus when ready."
                : $"Imported {snapshot.QueueSteps.Count} queue step(s) from VieriCodex. Nothing was started.",
        };
        Normalize(queue);
        return queue;
    }

    public static void Normalize(ProgressionQueueConfiguration queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        queue.Steps ??= [];
        queue.Settings ??= new ProgressionQueueSettingsConfiguration();
        if (queue.Steps.Count > 100)
            queue.Steps = queue.Steps.Take(100).ToList();

        HashSet<Guid> ids = [];
        foreach (ProgressionQueueStepConfiguration step in queue.Steps)
        {
            if (step.Id == Guid.Empty || !ids.Add(step.Id))
            {
                do step.Id = Guid.NewGuid(); while (!ids.Add(step.Id));
            }
            step.ClassJobId = Math.Min(step.ClassJobId, 43);
            step.TargetLevel = Math.Clamp(step.TargetLevel, 1, ReachJobLevelPlanner.MaximumSupportedLevel);
            if (!step.Enabled)
                step.Status = ProgressionQueueStepStatus.Disabled;
        }

        if (queue.CurrentIndex < -1 || queue.CurrentIndex >= queue.Steps.Count)
            queue.CurrentIndex = -1;
        if (!queue.IsRunning && queue.State is ProgressionQueueRuntimeState.SwitchingJob or
            ProgressionQueueRuntimeState.StartingGoal or ProgressionQueueRuntimeState.RunningGoal)
        {
            queue.IsPaused = true;
            queue.State = ProgressionQueueRuntimeState.Paused;
            queue.StatusDetail = "The queue was interrupted. Resume revalidates the step without replaying prior provider work.";
        }
    }

    public static int FindNextEnabledIndex(
        IReadOnlyList<ProgressionQueueStepConfiguration> steps,
        int startIndex)
    {
        ArgumentNullException.ThrowIfNull(steps);
        for (int index = Math.Max(0, startIndex); index < steps.Count; index++)
        {
            ProgressionQueueStepConfiguration step = steps[index];
            if (step.Enabled && step.Status is not (ProgressionQueueStepStatus.Completed or
                    ProgressionQueueStepStatus.AlreadySatisfied or ProgressionQueueStepStatus.Skipped))
                return index;
        }
        return -1;
    }

    public static ProgressionQueueMethodSelection SelectMethods(
        ProgressionQueueMethod method,
        ProgressionQueueSettingsConfiguration settings,
        bool baseMainScenario,
        bool baseClassJobRole) => method switch
    {
        ProgressionQueueMethod.Automatic => new(
            baseMainScenario,
            baseClassJobRole,
            settings.AutomaticUsesHuntingLog,
            settings.AutomaticUsesSideQuests,
            settings.AutomaticUsesDuties),
        ProgressionQueueMethod.HuntingLog => new(false, false, true, false, false),
        ProgressionQueueMethod.SideQuests => new(false, false, false, true, false),
        ProgressionQueueMethod.Duties => new(false, false, false, false, true),
        _ => throw new ArgumentOutOfRangeException(nameof(method)),
    };

    public static ProgressionQueueFallbackPolicy EffectiveFailurePolicy(
        ProgressionQueueStepConfiguration step,
        ProgressionQueueSettingsConfiguration settings) =>
        step.FallbackPolicy is ProgressionQueueFallbackPolicy.Automatic or
            ProgressionQueueFallbackPolicy.SideQuests or ProgressionQueueFallbackPolicy.Duties
            ? settings.OnStepFailure
            : step.FallbackPolicy;
}
