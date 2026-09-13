namespace VieriNexus.Application;

/// <summary>
/// Pure family policy for ordinary, non-repeatable side quests. Runtime game data supplies the
/// quest facts and Questionable supplies readiness/completion; Nexus owns the selection boundary.
/// </summary>
public static class GeneralSideQuestPolicy
{
    // Stable quest IDs for the four quest-awarded Aether Currents in each expansion field zone.
    // These are exploration progression, not general leveling side quests.
    private static readonly HashSet<uint> AetherCurrentQuestIds =
    [
        1744, 1759, 1760, 2111, 1771, 1790, 1797, 1802, 1936, 1945, 1963, 1966,
        1819, 1823, 1828, 1835, 1748, 1874, 1909, 1910,
        2639, 2661, 2816, 2821, 2632, 2673, 2687, 2693, 2724, 2728, 2730, 2733,
        2655, 2842, 2851, 2860, 2877, 2880, 2881, 2883, 2760, 2771, 2782, 2791,
        3380, 3384, 3385, 3386, 3360, 3371, 3537, 3556, 3375, 3503, 3511, 3525,
        3395, 3398, 3404, 3427, 3444, 3467, 3478, 3656, 3588, 3592, 3593, 3594,
        4320, 4329, 4480, 4484, 4203, 4257, 4259, 4489, 4216, 4232, 4498, 4502,
        4240, 4241, 4253, 4516, 4342, 4346, 4354, 4355, 4288, 4313, 4507, 4511,
        5039, 5047, 5051, 5055, 5064, 5074, 5081, 5085, 5094, 5103, 5110, 5114,
        5130, 5138, 5140, 5144, 5153, 5156, 5159, 5160, 5174, 5176, 5178, 5179,
    ];

    public static bool IsGeneralSideQuest(
        uint questId,
        bool isMainScenario,
        bool isRepeatable,
        bool isSeasonal,
        bool isAlliedSociety,
        bool hasJournalGenre,
        uint newGamePlusChapter,
        IReadOnlySet<uint> classJobRoleChapters) =>
        questId > 0 &&
        !isMainScenario &&
        !isRepeatable &&
        !isSeasonal &&
        !isAlliedSociety &&
        hasJournalGenre &&
        !AetherCurrentQuestIds.Contains(questId) &&
        !classJobRoleChapters.Contains(newGamePlusChapter);
}
