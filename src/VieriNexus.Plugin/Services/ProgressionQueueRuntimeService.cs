using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class ProgressionQueueRuntimeService
{
    private static readonly TimeSpan JobSwitchTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);
    private readonly Configuration configuration;
    private readonly ProgressionRuntimeService progression;
    private readonly FastJobSwitchService jobSwitcher;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly Action save;
    private string characterKey = string.Empty;
    private DateTimeOffset nextActionAtUtc;
    private DateTimeOffset switchDeadlineUtc;
    private uint requestedClassJobId;
    private bool sessionReconciled;

    internal ProgressionQueueRuntimeService(
        Configuration configuration,
        ProgressionRuntimeService progression,
        FastJobSwitchService jobSwitcher,
        IClientState clientState,
        IObjectTable objectTable,
        ICondition condition,
        Action save)
    {
        this.configuration = configuration;
        this.progression = progression;
        this.jobSwitcher = jobSwitcher;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.save = save;
    }

    internal bool IsFastJobSwitcherInstalled => jobSwitcher.IsInstalled;
    internal bool IsFastJobSwitcherLoaded => jobSwitcher.IsLoaded;
    internal IReadOnlyList<NexusClassJob> CombatJobs => jobSwitcher.CombatJobs();
    internal int Level(uint classJobId) => jobSwitcher.Level(classJobId);
    internal string JobLabel(uint classJobId) => jobSwitcher.Label(classJobId);

    internal ProgressionQueueConfiguration? Queue(CharacterSnapshot? character) =>
        character is { Key.IsKnown: true }
            ? configuration.ForCharacter(character.Key.ToString()).ProgressionQueue
            : null;

    internal ProgressionActionResult Start(CharacterSnapshot? character, int fromIndex = 0)
    {
        if (character is null || !character.Key.IsKnown)
            return new(false, "Log in before starting a progression queue.");
        CharacterConfiguration owner = configuration.ForCharacter(character.Key.ToString());
        if (!owner.AllowAutomation)
            return new(false, "Automation is disabled for this character in Nexus settings.");
        ProgressionQueueConfiguration queue = owner.ProgressionQueue;
        ProgressionQueuePolicy.Normalize(queue);
        if (progression.State?.Goal.Status is GoalStatus.Active or GoalStatus.Paused or GoalStatus.Blocked)
        {
            ProgressionActionResult handoff = progression.StopNow();
            if (!handoff.Success)
                return new(false, $"Nexus could not hand off the current-job goal to the queue: {handoff.Message}");
        }

        int next = ProgressionQueuePolicy.FindNextEnabledIndex(queue.Steps, fromIndex);
        if (next < 0)
            return SetResult(queue, false, "Add or reset at least one enabled queue step before starting.");

        queue.CurrentIndex = next;
        queue.IsRunning = true;
        queue.IsPaused = false;
        queue.ActiveGoalId = null;
        queue.EffectiveMethod = null;
        SetState(queue, ProgressionQueueRuntimeState.PreparingStep,
            progression.State?.Goal.Status is GoalStatus.Active or GoalStatus.Paused or GoalStatus.Blocked
                ? "Stopping the current-job activity before the queue starts. Nexus will continue automatically after inactivity is confirmed."
                : $"Preparing step {next + 1}: {JobLabel(queue.Steps[next].ClassJobId)} to level {queue.Steps[next].TargetLevel}.");
        return new(true, queue.StatusDetail);
    }

    internal ProgressionActionResult Resume(CharacterSnapshot? character)
    {
        ProgressionQueueConfiguration? queue = Queue(character);
        if (queue is null)
            return new(false, "Log in before resuming a progression queue.");
        if (!queue.IsPaused)
            return new(false, "The progression queue is not paused.");
        queue.IsPaused = false;
        queue.IsRunning = true;
        SetState(queue, ProgressionQueueRuntimeState.PreparingStep,
            "Resuming the queue by revalidating its current step and any saved Nexus goal.");
        return new(true, queue.StatusDetail);
    }

    internal ProgressionActionResult Stop(CharacterSnapshot? character)
    {
        ProgressionQueueConfiguration? queue = Queue(character);
        if (queue is null)
            return new(false, "No character progression queue is loaded.");
        if (progression.State?.Goal.Status is GoalStatus.Active or GoalStatus.Paused or GoalStatus.Blocked)
            progression.StopNow();
        queue.IsRunning = false;
        queue.IsPaused = false;
        queue.ActiveGoalId = null;
        queue.EffectiveMethod = null;
        if (CurrentStep(queue) is { } step && step.Status is not (ProgressionQueueStepStatus.Completed or
                ProgressionQueueStepStatus.AlreadySatisfied or ProgressionQueueStepStatus.Skipped))
            step.Status = step.Enabled ? ProgressionQueueStepStatus.Waiting : ProgressionQueueStepStatus.Disabled;
        SetState(queue, ProgressionQueueRuntimeState.Stopped,
            "Progression queue stopped. Nexus will not schedule another step.");
        return new(true, queue.StatusDetail);
    }

    internal void Reset(CharacterSnapshot? character)
    {
        ProgressionQueueConfiguration? queue = Queue(character);
        if (queue is null)
            return;
        if (queue.IsRunning || queue.IsPaused)
            Stop(character);
        foreach (ProgressionQueueStepConfiguration step in queue.Steps)
        {
            step.Status = step.Enabled ? ProgressionQueueStepStatus.Waiting : ProgressionQueueStepStatus.Disabled;
            step.StatusDetail = string.Empty;
        }
        queue.CurrentIndex = -1;
        queue.ActiveGoalId = null;
        queue.EffectiveMethod = null;
        SetState(queue, ProgressionQueueRuntimeState.Stopped, "Queue progress reset. No step was started.");
    }

    internal void Update(CharacterSnapshot? character)
    {
        if (character is null || !character.Key.IsKnown)
            return;
        string key = character.Key.ToString();
        if (!string.Equals(characterKey, key, StringComparison.Ordinal))
        {
            characterKey = key;
            requestedClassJobId = 0;
            sessionReconciled = false;
        }

        ProgressionQueueConfiguration queue = configuration.ForCharacter(key).ProgressionQueue;
        ProgressionQueuePolicy.Normalize(queue);
        if (!sessionReconciled)
        {
            sessionReconciled = true;
            if (queue.IsRunning)
            {
                queue.IsRunning = queue.Settings.ResumeAfterRestart;
                queue.IsPaused = !queue.Settings.ResumeAfterRestart;
                queue.State = queue.Settings.ResumeAfterRestart
                    ? ProgressionQueueRuntimeState.PreparingStep
                    : ProgressionQueueRuntimeState.Paused;
                queue.StatusDetail = queue.Settings.ResumeAfterRestart
                    ? "Recovering the queue from current game state. Prior provider work must stop before a fresh bounded plan can begin."
                    : "The queue was active before reload and remains paused by its restart setting.";
                queue.UpdatedAtUtc = DateTimeOffset.UtcNow;
                save();
            }
        }
        if (!queue.IsRunning || queue.IsPaused || DateTimeOffset.UtcNow < nextActionAtUtc)
            return;

        if (queue.ActiveGoalId is null && progression.State is { } prior &&
            prior.Goal.Status is not (GoalStatus.Cancelled or GoalStatus.Satisfied))
        {
            if (prior.ActiveTask is null)
                progression.StopNow();
            SetStateIfChanged(queue, ProgressionQueueRuntimeState.PreparingStep,
                "Waiting for the previous current-job activity to stop before the queue starts automatically.");
            return;
        }

        ProgressionQueueStepConfiguration? step = CurrentStep(queue);
        if (step is null || !step.Enabled)
        {
            Advance(queue);
            return;
        }

        int level = Level(step.ClassJobId);
        if (level >= step.TargetLevel)
        {
            if (queue.Settings.SkipTargetsAlreadyReached || queue.ActiveGoalId is not null)
            {
                step.Status = queue.ActiveGoalId is null
                    ? ProgressionQueueStepStatus.AlreadySatisfied
                    : ProgressionQueueStepStatus.Completed;
                step.StatusDetail = $"Verified {JobLabel(step.ClassJobId)} at level {level}.";
                queue.ActiveGoalId = null;
                queue.EffectiveMethod = null;
                Advance(queue);
                return;
            }
        }

        ProgressionGoalState? goalState = progression.State;
        if (queue.ActiveGoalId is { } goalId)
        {
            if (goalState is null || goalState.Goal.Id.Value != goalId)
            {
                Pause(queue, step, "The saved Nexus level goal no longer matches this queue step. Review it before resuming.");
                return;
            }
            switch (goalState.Goal.Status)
            {
                case GoalStatus.Active:
                    step.Status = ProgressionQueueStepStatus.Running;
                    SetStateIfChanged(queue, ProgressionQueueRuntimeState.RunningGoal,
                        goalState.Goal.StatusDetail ?? $"Running {JobLabel(step.ClassJobId)} to level {step.TargetLevel}.");
                    return;
                case GoalStatus.Satisfied:
                    step.Status = ProgressionQueueStepStatus.Completed;
                    step.StatusDetail = goalState.Goal.StatusDetail ?? "Target level verified.";
                    queue.ActiveGoalId = null;
                    queue.EffectiveMethod = null;
                    Advance(queue);
                    return;
                case GoalStatus.Paused:
                case GoalStatus.Blocked:
                    if (goalState.ActiveTask is not null)
                    {
                        SetStateIfChanged(queue, ProgressionQueueRuntimeState.RunningGoal,
                            "Waiting for the prior provider operation to stop before the queue can continue.");
                        return;
                    }
                    if (jobSwitcher.CurrentClassJobId != step.ClassJobId)
                        break;
                    ProgressionActionResult resumed = progression.Resume();
                    if (resumed.Success)
                    {
                        step.Status = ProgressionQueueStepStatus.Running;
                        SetState(queue, ProgressionQueueRuntimeState.RunningGoal, resumed.Message);
                    }
                    else
                        HandleFailure(queue, step, resumed.Message);
                    return;
                case GoalStatus.Cancelled:
                    queue.ActiveGoalId = null;
                    Pause(queue, step, "The Nexus level goal was cancelled. Resume to rebuild this queue step.");
                    return;
            }
        }

        if (jobSwitcher.CurrentClassJobId != step.ClassJobId)
        {
            SwitchJob(queue, step);
            return;
        }

        requestedClassJobId = 0;
        if (queue.State == ProgressionQueueRuntimeState.SwitchingJob)
        {
            SetState(queue, ProgressionQueueRuntimeState.StartingGoal,
                $"{JobLabel(step.ClassJobId)} confirmed. Allowing the job and gear change to settle.");
            nextActionAtUtc = DateTimeOffset.UtcNow + SettleTime;
            return;
        }
        if (DateTimeOffset.UtcNow < nextActionAtUtc)
            return;
        StartGoal(character, queue, step);
    }

    private void SwitchJob(ProgressionQueueConfiguration queue, ProgressionQueueStepConfiguration step)
    {
        if (!queue.Settings.AutomaticallySwitchJobs)
        {
            Pause(queue, step, $"Equip {JobLabel(step.ClassJobId)}, then resume the queue.");
            return;
        }
        if (!IsSafeToSwitch())
        {
            step.Status = ProgressionQueueStepStatus.SwitchingJob;
            SetStateIfChanged(queue, ProgressionQueueRuntimeState.WaitingForSafeState,
                "Waiting for combat, loading, duty, or occupied state to end before switching jobs.");
            return;
        }

        if (requestedClassJobId == step.ClassJobId)
        {
            if (DateTimeOffset.UtcNow >= switchDeadlineUtc)
            {
                requestedClassJobId = 0;
                HandleFailure(queue, step,
                    $"Timed out waiting for Fast Job Switcher to equip {JobLabel(step.ClassJobId)}.");
            }
            return;
        }

        if (!jobSwitcher.TryRequestSwitch(step.ClassJobId, out string message))
        {
            HandleFailure(queue, step, message);
            return;
        }
        requestedClassJobId = step.ClassJobId;
        switchDeadlineUtc = DateTimeOffset.UtcNow + JobSwitchTimeout;
        step.Status = ProgressionQueueStepStatus.SwitchingJob;
        SetState(queue, ProgressionQueueRuntimeState.SwitchingJob, message);
    }

    private void StartGoal(
        CharacterSnapshot character,
        ProgressionQueueConfiguration queue,
        ProgressionQueueStepConfiguration step)
    {
        CharacterConfiguration owner = configuration.ForCharacter(character.Key.ToString());
        ProgressionQueueMethod method = queue.EffectiveMethod ?? step.Method;
        ProgressionQueueMethodSelection selection = ProgressionQueuePolicy.SelectMethods(
            method,
            queue.Settings,
            owner.Progression.AllowMainScenario,
            owner.Progression.AllowJobQuests);
        ProgressionDraftConfiguration draft = new()
        {
            TargetLevel = step.TargetLevel,
            AllowMainScenario = selection.AllowMainScenario,
            AllowJobQuests = selection.AllowClassJobRole,
            AllowHuntingLog = selection.AllowHuntingLog,
            AllowSideQuests = selection.AllowSideQuests,
            AllowDuties = selection.AllowDuties,
            MinimumGilReserve = owner.Progression.MinimumGilReserve,
        };
        step.Status = ProgressionQueueStepStatus.Current;
        SetState(queue, ProgressionQueueRuntimeState.StartingGoal,
            $"Starting {JobLabel(step.ClassJobId)} to level {step.TargetLevel} with {FormatMethod(method)}.");
        Guid? priorGoalId = progression.State?.Goal.Id.Value;
        ProgressionActionResult result = progression.StartConfigured(character, draft, owner.AllowAutomation);
        if (!result.Success || progression.State is null)
        {
            if (progression.State is { } failedState && failedState.Goal.Id.Value != priorGoalId &&
                failedState.Goal.Status is GoalStatus.Paused or GoalStatus.Blocked)
                queue.ActiveGoalId = failedState.Goal.Id.Value;
            HandleFailure(queue, step, result.Message);
            return;
        }
        queue.ActiveGoalId = progression.State.Goal.Id.Value;
        step.Status = ProgressionQueueStepStatus.Running;
        SetState(queue, ProgressionQueueRuntimeState.RunningGoal, result.Message);
    }

    private void HandleFailure(
        ProgressionQueueConfiguration queue,
        ProgressionQueueStepConfiguration step,
        string message)
    {
        if (queue.ActiveGoalId is null && TryApplyMethodFallback(queue, step, message))
            return;

        ProgressionQueueFallbackPolicy policy = ProgressionQueuePolicy.EffectiveFailurePolicy(step, queue.Settings);
        step.StatusDetail = message;
        switch (policy)
        {
            case ProgressionQueueFallbackPolicy.SkipStep:
                if (queue.ActiveGoalId is not null)
                    progression.StopNow();
                step.Status = ProgressionQueueStepStatus.Skipped;
                queue.ActiveGoalId = null;
                queue.EffectiveMethod = null;
                Advance(queue);
                break;
            case ProgressionQueueFallbackPolicy.StopQueue:
                if (queue.ActiveGoalId is not null)
                    progression.StopNow();
                step.Status = ProgressionQueueStepStatus.Blocked;
                queue.IsRunning = false;
                queue.IsPaused = false;
                SetState(queue, ProgressionQueueRuntimeState.Stopped, message);
                break;
            default:
                Pause(queue, step, message);
                break;
        }
    }

    private bool TryApplyMethodFallback(
        ProgressionQueueConfiguration queue,
        ProgressionQueueStepConfiguration step,
        string reason)
    {
        ProgressionQueueMethod? fallback = step.FallbackPolicy switch
        {
            ProgressionQueueFallbackPolicy.Automatic => ProgressionQueueMethod.Automatic,
            ProgressionQueueFallbackPolicy.SideQuests => ProgressionQueueMethod.SideQuests,
            ProgressionQueueFallbackPolicy.Duties => ProgressionQueueMethod.Duties,
            _ => null,
        };
        if (fallback is null || fallback == (queue.EffectiveMethod ?? step.Method))
            return false;
        queue.EffectiveMethod = fallback;
        step.Status = ProgressionQueueStepStatus.Current;
        SetState(queue, ProgressionQueueRuntimeState.PreparingStep,
            $"{reason} Falling back to {FormatMethod(fallback.Value)}.");
        nextActionAtUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
        return true;
    }

    private void Advance(ProgressionQueueConfiguration queue)
    {
        int next = ProgressionQueuePolicy.FindNextEnabledIndex(queue.Steps, queue.CurrentIndex + 1);
        if (next < 0)
        {
            queue.CurrentIndex = -1;
            queue.IsRunning = false;
            queue.IsPaused = false;
            queue.ActiveGoalId = null;
            queue.EffectiveMethod = null;
            SetState(queue, ProgressionQueueRuntimeState.Completed,
                "Progression queue complete. Every enabled step is finished or skipped.");
            return;
        }
        if (!queue.Settings.AutomaticallyAdvance && queue.CurrentIndex >= 0)
        {
            queue.CurrentIndex = next;
            queue.IsRunning = false;
            queue.IsPaused = true;
            queue.ActiveGoalId = null;
            queue.EffectiveMethod = null;
            ProgressionQueueStepConfiguration nextStep = queue.Steps[next];
            SetState(queue, ProgressionQueueRuntimeState.Paused,
                $"Step complete. {JobLabel(nextStep.ClassJobId)} is ready; resume when you want to continue.");
            return;
        }
        queue.CurrentIndex = next;
        queue.ActiveGoalId = null;
        queue.EffectiveMethod = null;
        requestedClassJobId = 0;
        ProgressionQueueStepConfiguration step = queue.Steps[next];
        SetState(queue, ProgressionQueueRuntimeState.PreparingStep,
            $"Preparing step {next + 1}: {JobLabel(step.ClassJobId)} to level {step.TargetLevel}.");
        nextActionAtUtc = DateTimeOffset.UtcNow + SettleTime;
    }

    private void Pause(
        ProgressionQueueConfiguration queue,
        ProgressionQueueStepConfiguration step,
        string message)
    {
        queue.IsRunning = false;
        queue.IsPaused = true;
        step.Status = ProgressionQueueStepStatus.Paused;
        step.StatusDetail = message;
        SetState(queue, ProgressionQueueRuntimeState.Paused, message);
    }

    private bool IsSafeToSwitch() =>
        clientState.IsLoggedIn && objectTable.LocalPlayer is not null &&
        !condition[ConditionFlag.InCombat] &&
        !condition[ConditionFlag.BetweenAreas] &&
        !condition[ConditionFlag.BetweenAreas51] &&
        !condition[ConditionFlag.OccupiedInQuestEvent] &&
        !condition[ConditionFlag.OccupiedInCutSceneEvent] &&
        !condition[ConditionFlag.BoundByDuty] &&
        !condition[ConditionFlag.BoundByDuty56] &&
        !condition[ConditionFlag.BoundByDuty95] &&
        progression.State?.Goal.Status != GoalStatus.Active && progression.State?.ActiveTask is null;

    private static ProgressionQueueStepConfiguration? CurrentStep(ProgressionQueueConfiguration queue) =>
        queue.CurrentIndex >= 0 && queue.CurrentIndex < queue.Steps.Count
            ? queue.Steps[queue.CurrentIndex]
            : null;

    private ProgressionActionResult SetResult(ProgressionQueueConfiguration queue, bool success, string message)
    {
        SetState(queue, success ? ProgressionQueueRuntimeState.PreparingStep : ProgressionQueueRuntimeState.Stopped,
            message);
        return new(success, message);
    }

    private void SetState(
        ProgressionQueueConfiguration queue,
        ProgressionQueueRuntimeState state,
        string message)
    {
        queue.State = state;
        queue.StatusDetail = message;
        queue.UpdatedAtUtc = DateTimeOffset.UtcNow;
        save();
    }

    private void SetStateIfChanged(
        ProgressionQueueConfiguration queue,
        ProgressionQueueRuntimeState state,
        string message)
    {
        if (queue.State == state && string.Equals(queue.StatusDetail, message, StringComparison.Ordinal))
            return;
        SetState(queue, state, message);
    }

    private static string FormatMethod(ProgressionQueueMethod method) => method switch
    {
        ProgressionQueueMethod.Automatic => "Automatic / Smart",
        ProgressionQueueMethod.HuntingLog => "Hunting Log",
        ProgressionQueueMethod.SideQuests => "Side Quests",
        ProgressionQueueMethod.Duties => "Duties",
        _ => method.ToString(),
    };
}
