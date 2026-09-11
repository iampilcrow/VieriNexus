using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using VieriNexus.Application;
using AchievementState = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;
using SheetAchievement = Lumina.Excel.Sheets.Achievement;

namespace VieriNexus.Services;

internal sealed class ProgressAtlasService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AchievementRequestInterval = TimeSpan.FromSeconds(5);

    private readonly IClientState clientState;
    private readonly uint[] aetheryteIds;
    private readonly uint[] aetherCurrentIds;
    private readonly uint[] achievementIds;
    private DateTimeOffset lastRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset lastAchievementRequest = DateTimeOffset.MinValue;
    private ProgressAtlasSnapshot current;

    internal ProgressAtlasService(IDataManager dataManager, IClientState clientState)
    {
        this.clientState = clientState;
        aetheryteIds = dataManager.GetExcelSheet<Aetheryte>()
            .Where(row => row.RowId > 0 && row.Territory.RowId > 0)
            .Where(row => row.IsAetheryte ||
                          row.AethernetGroup != 0 && row.AethernetName.ValueNullable is not null)
            .Select(row => row.RowId)
            .Distinct()
            .Order()
            .ToArray();
        aetherCurrentIds = dataManager.GetExcelSheet<AetherCurrentCompFlgSet>()
            .Where(row => row.RowId > 0 && row.Territory.IsValid)
            .SelectMany(row => row.AetherCurrents)
            .Where(current => current.RowId > 0)
            .Select(current => current.RowId)
            .Distinct()
            .Order()
            .ToArray();
        achievementIds = dataManager.GetExcelSheet<SheetAchievement>()
            .Where(row => row.RowId > 0 && !row.Name.IsEmpty && row.AchievementCategory.RowId > 0)
            .Where(row => row.AchievementCategory.Value.AchievementKind.RowId != 9)
            .Select(row => row.RowId)
            .Distinct()
            .Order()
            .ToArray();
        current = Empty(DateTimeOffset.MinValue);
    }

    internal ProgressAtlasSnapshot Current => current;

    internal void Update(DateTimeOffset now)
    {
        if (!clientState.IsLoggedIn)
        {
            current = Empty(now);
            lastRefresh = now;
            return;
        }
        if (now - lastRefresh < RefreshInterval)
            return;

        lastRefresh = now;
        current = Capture(now);
    }

    private unsafe ProgressAtlasSnapshot Capture(DateTimeOffset now)
    {
        UIState* uiState = UIState.Instance();
        PlayerState* playerState = PlayerState.Instance();
        AchievementState* achievements = AchievementState.Instance();

        bool travelLoaded = uiState != null;
        int unlockedAetherytes = travelLoaded
            ? aetheryteIds.Count(id => uiState->IsAetheryteUnlocked(id))
            : 0;

        bool currentsLoaded = playerState != null;
        int unlockedCurrents = currentsLoaded
            ? aetherCurrentIds.Count(id => playerState->IsAetherCurrentUnlocked(id))
            : 0;

        bool achievementsLoaded = achievements != null && achievements->IsLoaded();
        if (achievements != null && !achievementsLoaded && now - lastAchievementRequest >= AchievementRequestInterval)
        {
            lastAchievementRequest = now;
            achievements->RequestCompletedAchievements();
        }
        int completedAchievements = achievementsLoaded
            ? achievementIds.Count(id => achievements->IsComplete((int)id))
            : 0;

        ProgressAtlasCategorySnapshot[] categories =
        [
            ProgressAtlasModel.Category(
                ProgressAtlasCategoryId.Aetherytes,
                "Aetherytes & Aethernet",
                unlockedAetherytes,
                aetheryteIds.Length,
                travelLoaded,
                travelLoaded
                    ? "Read directly from this character's unlocked travel network."
                    : "Waiting for the character's travel network."),
            ProgressAtlasModel.Category(
                ProgressAtlasCategoryId.AetherCurrents,
                "Aether Currents",
                unlockedCurrents,
                aetherCurrentIds.Length,
                currentsLoaded,
                currentsLoaded
                    ? "Includes open-world and quest-earned currents from current game data."
                    : "Waiting for the character's Aether Current data."),
            ProgressAtlasModel.Category(
                ProgressAtlasCategoryId.Achievements,
                "Achievements",
                completedAchievements,
                achievementIds.Length,
                achievementsLoaded,
                achievementsLoaded
                    ? "Completed achievements are read from this character only."
                    : "Requesting this character's completed-achievement list from the game."),
        ];
        return new ProgressAtlasSnapshot(now, true, categories);
    }

    private ProgressAtlasSnapshot Empty(DateTimeOffset now) => new(
        now,
        false,
        [
            ProgressAtlasModel.Category(ProgressAtlasCategoryId.Aetherytes, "Aetherytes & Aethernet", 0,
                aetheryteIds?.Length ?? 0, false, "Log in to load this character's travel network."),
            ProgressAtlasModel.Category(ProgressAtlasCategoryId.AetherCurrents, "Aether Currents", 0,
                aetherCurrentIds?.Length ?? 0, false, "Log in to load this character's Aether Currents."),
            ProgressAtlasModel.Category(ProgressAtlasCategoryId.Achievements, "Achievements", 0,
                achievementIds?.Length ?? 0, false, "Log in to load this character's achievements."),
        ]);
}
