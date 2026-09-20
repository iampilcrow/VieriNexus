namespace VieriNexus.Application;

public enum ProgressAtlasEntryState
{
    Complete,
    InProgress,
    Ready,
    Locked,
    Unsupported,
    ProviderUnavailable,
    Loading,
}

public sealed record ProgressAtlasQuestTarget(
    string QuestId,
    string Name,
    int RequiredLevel,
    ProgressionQuestKind Kind,
    string Expansion);

public sealed record ProgressAtlasQuestObservation(
    ProgressAtlasEntryState State,
    string Detail,
    bool CanStart);

public sealed record ProgressAtlasDutyTarget(
    uint ContentFinderConditionId,
    uint TerritoryId,
    uint ContentId,
    string Name,
    string Category,
    string Expansion,
    int RequiredLevel,
    int RequiredItemLevel,
    string? UnlockQuestId = null,
    string? UnlockQuestName = null,
    int UnlockQuestLevel = 0,
    ProgressionQuestKind UnlockQuestKind = ProgressionQuestKind.GeneralSideQuest);

public sealed record ProgressAtlasDutyObservation(
    ProgressAtlasEntryState State,
    string Detail,
    bool CanRun,
    bool CanStartUnlockQuest = false);

public static class ProgressAtlasCatalog
{
    public static string ExpansionName(uint id) => id switch
    {
        0 => "A Realm Reborn",
        1 => "Heavensward",
        2 => "Stormblood",
        3 => "Shadowbringers",
        4 => "Endwalker",
        5 => "Dawntrail",
        _ => "Other",
    };

    public static int ExpansionOrder(string expansion) => expansion switch
    {
        "A Realm Reborn" => 0,
        "Heavensward" => 1,
        "Stormblood" => 2,
        "Shadowbringers" => 3,
        "Endwalker" => 4,
        "Dawntrail" => 5,
        _ => 99,
    };

    public static string QuestCategoryName(ProgressionQuestKind kind) => kind switch
    {
        ProgressionQuestKind.MainScenario => "Main Scenario Quests (MSQ)",
        ProgressionQuestKind.ClassJobRole => "Class/Job/Role Quests",
        ProgressionQuestKind.GeneralSideQuest => "Side Quests",
        ProgressionQuestKind.AetherCurrent => "Aether Current Quests",
        ProgressionQuestKind.Achievement => "Achievement Quests",
        _ => "Quests",
    };

    public static int QuestCategoryOrder(ProgressionQuestKind kind) => kind switch
    {
        ProgressionQuestKind.MainScenario => 0,
        ProgressionQuestKind.ClassJobRole => 1,
        ProgressionQuestKind.GeneralSideQuest => 2,
        ProgressionQuestKind.AetherCurrent => 3,
        ProgressionQuestKind.Achievement => 4,
        _ => 99,
    };

    public static string StateLabel(ProgressAtlasEntryState state) => state switch
    {
        ProgressAtlasEntryState.Complete => "Complete",
        ProgressAtlasEntryState.InProgress => "In progress",
        ProgressAtlasEntryState.Ready => "Ready",
        ProgressAtlasEntryState.Locked => "Locked",
        ProgressAtlasEntryState.Unsupported => "No provider path",
        ProgressAtlasEntryState.ProviderUnavailable => "Provider unavailable",
        ProgressAtlasEntryState.Loading => "Checking",
        _ => state.ToString(),
    };
}
