namespace VieriNexus.Application;

public enum WorldAutomationActivity
{
    Aetheryte,
    FieldAetherCurrent,
    AetherCurrentQuest,
    Exploration,
    Achievement,
}

public sealed record WorldAutomationSelection(
    bool Aetherytes,
    bool UnlockFlying,
    bool Exploration,
    bool Achievements);

public sealed record WorldAutomationAvailability(
    int ReachableAetherytes,
    int ReachableFieldAetherCurrents,
    int ReadyAetherCurrentQuests,
    int ReachableExplorationRegions,
    int RunnableAchievements);

public static class WorldAutomationPolicy
{
    public static WorldAutomationActivity? SelectNext(
        WorldAutomationSelection selection,
        WorldAutomationAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(availability);

        if (selection.Aetherytes && availability.ReachableAetherytes > 0)
            return WorldAutomationActivity.Aetheryte;
        if (selection.UnlockFlying && availability.ReachableFieldAetherCurrents > 0)
            return WorldAutomationActivity.FieldAetherCurrent;
        if (selection.UnlockFlying && availability.ReadyAetherCurrentQuests > 0)
            return WorldAutomationActivity.AetherCurrentQuest;
        if (selection.Exploration && availability.ReachableExplorationRegions > 0)
            return WorldAutomationActivity.Exploration;
        if (selection.Achievements && availability.RunnableAchievements > 0)
            return WorldAutomationActivity.Achievement;
        return null;
    }
}
