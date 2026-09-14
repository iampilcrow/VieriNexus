using VieriNexus.Application;

namespace VieriNexus.Services;

internal enum WorldAutomationRuntimeState
{
    Stopped,
    Waiting,
    Running,
    Completed,
    Blocked,
}

internal sealed record WorldAutomationRuntimeStatus(
    WorldAutomationRuntimeState State,
    bool IsActive,
    int CompletedActivities,
    string Message);

/// <summary>
/// Continually chooses one verified world-progression objective at a time. The underlying Atlas
/// executor owns travel, interaction, provider verification, and Stop; this coordinator only
/// selects the next bounded activity after the prior operation has a terminal result.
/// </summary>
internal sealed class WorldAutomationRuntimeService(
    ProgressAtlasActionService actions,
    Func<AtlasAutomationConfiguration?> configuration,
    Action save)
{
    private static readonly TimeSpan ReplanDelay = TimeSpan.FromSeconds(1);
    private Guid observedOperationId;
    private DateTimeOffset nextPlanAt;
    private int completedActivities;
    private WorldAutomationRuntimeState state = WorldAutomationRuntimeState.Stopped;
    private string message = "World Progression is stopped.";

    internal WorldAutomationRuntimeStatus Status => new(
        state,
        state is WorldAutomationRuntimeState.Waiting or WorldAutomationRuntimeState.Running,
        completedActivities,
        message);

    internal bool Start(out string result)
    {
        AtlasAutomationConfiguration? settings = configuration();
        if (settings is null)
        {
            result = "Log in before starting World Progression.";
            return false;
        }
        if (!Selection(settings).Equals(new WorldAutomationSelection(false, false, false, false)))
        {
            completedActivities = 0;
            observedOperationId = Guid.Empty;
            nextPlanAt = DateTimeOffset.UtcNow;
            state = WorldAutomationRuntimeState.Waiting;
            message = "World Progression started. Nexus is selecting the next reachable objective.";
            save();
            result = message;
            return true;
        }

        result = "Select at least one World Progression activity before starting.";
        return false;
    }

    internal bool Stop(out string result)
    {
        bool wasActive = Status.IsActive || actions.Status.IsActive;
        state = WorldAutomationRuntimeState.Stopped;
        if (actions.Status.IsActive)
            actions.Stop(out _);
        observedOperationId = Guid.Empty;
        message = "World Progression stopped. Nexus will not schedule another objective.";
        result = message;
        return wasActive;
    }

    internal void Update(DateTimeOffset now)
    {
        if (!Status.IsActive)
            return;

        ProgressAtlasActionStatus action = actions.Status;
        if (action.IsActive)
        {
            observedOperationId = action.OperationId;
            state = WorldAutomationRuntimeState.Running;
            message = action.Message;
            return;
        }

        if (observedOperationId != Guid.Empty && action.OperationId == observedOperationId)
        {
            observedOperationId = Guid.Empty;
            if (action.Outcome == ProgressAtlasActionOutcome.Completed)
            {
                completedActivities++;
                nextPlanAt = now + ReplanDelay;
                state = WorldAutomationRuntimeState.Waiting;
                message = $"{action.Message} Selecting the next enabled objective.";
            }
            else if (action.Outcome is ProgressAtlasActionOutcome.Failed or ProgressAtlasActionOutcome.Stopped)
            {
                state = action.Outcome == ProgressAtlasActionOutcome.Failed
                    ? WorldAutomationRuntimeState.Blocked
                    : WorldAutomationRuntimeState.Stopped;
                message = action.Message;
                return;
            }
        }

        if (now < nextPlanAt)
            return;

        AtlasAutomationConfiguration? settings = configuration();
        if (settings is null)
        {
            state = WorldAutomationRuntimeState.Stopped;
            message = "World Progression stopped because its character is no longer available.";
            return;
        }

        WorldAutomationActivity? next = WorldAutomationPolicy.SelectNext(
            Selection(settings),
            new WorldAutomationAvailability(
                actions.RemainingAetherytes,
                actions.RemainingFieldCurrents,
                actions.ReadyAetherCurrentQuests,
                actions.RemainingExplorationRegions,
                actions.RemainingSupportedAchievements));
        if (next is null)
        {
            state = WorldAutomationRuntimeState.Completed;
            message = completedActivities == 0
                ? "No selected world objective is currently reachable. Progress Atlas still shows locked and guided requirements."
                : $"World Progression completed {completedActivities} objective(s). No other selected objective is currently reachable.";
            return;
        }

        bool started = next switch
        {
            WorldAutomationActivity.Aetheryte => actions.StartNextAetheryte(out message),
            WorldAutomationActivity.FieldAetherCurrent => actions.StartNextFieldCurrent(out message),
            WorldAutomationActivity.AetherCurrentQuest => actions.StartNextAetherCurrentQuest(out message),
            WorldAutomationActivity.Exploration => actions.StartNextExploration(out message),
            WorldAutomationActivity.Achievement => actions.StartNextSupportedAchievement(out message),
            _ => false,
        };
        if (started)
        {
            observedOperationId = actions.Status.OperationId;
            state = WorldAutomationRuntimeState.Running;
        }
        else
        {
            state = WorldAutomationRuntimeState.Waiting;
            nextPlanAt = now + TimeSpan.FromSeconds(2);
        }
    }

    internal void Shutdown()
    {
        if (Status.IsActive)
            Stop(out _);
    }

    private static WorldAutomationSelection Selection(AtlasAutomationConfiguration value) => new(
        value.AllowAetheryteAttunements,
        value.AllowFieldAetherCurrents || value.AllowAetherCurrentQuests,
        value.AllowMapExploration,
        value.AllowAchievements);
}
