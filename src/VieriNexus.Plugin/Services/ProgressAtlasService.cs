using System.Reflection;
using System.Text.Json;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using VieriNexus.Application;
using AchievementState = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using SheetAchievement = Lumina.Excel.Sheets.Achievement;
using SheetMap = Lumina.Excel.Sheets.Map;

namespace VieriNexus.Services;

internal sealed class ProgressAtlasService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AchievementRequestInterval = TimeSpan.FromSeconds(5);

    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly uint[] aetheryteIds;
    private readonly uint[] aetherCurrentIds;
    private readonly uint[] achievementIds;
    private readonly MapDiscoveryRegion[] mapDiscoveryRegions;
    private readonly HuntingLogCatalog huntingLogCatalog;
    private DateTimeOffset lastRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset lastAchievementRequest = DateTimeOffset.MinValue;
    private ProgressAtlasSnapshot current;
    private IReadOnlyList<HuntingLogTargetProgress> huntingTargets = [];

    internal ProgressAtlasService(IDataManager dataManager, IClientState clientState, IPlayerState playerState)
    {
        this.clientState = clientState;
        this.playerState = playerState;
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
        mapDiscoveryRegions = BuildMapDiscoveryRegions(dataManager);
        huntingLogCatalog = LoadHuntingLogCatalog();
        current = Empty(DateTimeOffset.MinValue);
    }

    internal ProgressAtlasSnapshot Current => current;
    internal IReadOnlyList<HuntingLogTargetProgress> HuntingTargets => huntingTargets;

    internal void Update(DateTimeOffset now)
    {
        if (!clientState.IsLoggedIn)
        {
            current = Empty(now);
            huntingTargets = [];
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
        NativePlayerState* nativePlayerState = NativePlayerState.Instance();
        AchievementState* achievements = AchievementState.Instance();

        bool travelLoaded = uiState != null;
        int unlockedAetherytes = travelLoaded
            ? aetheryteIds.Count(id => uiState->IsAetheryteUnlocked(id))
            : 0;

        bool currentsLoaded = nativePlayerState != null;
        int unlockedCurrents = currentsLoaded
            ? aetherCurrentIds.Count(id => nativePlayerState->IsAetherCurrentUnlocked(id))
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

        MapDiscoveryManager* discovery = MapDiscoveryManager.Instance();
        bool explorationLoaded = discovery != null;
        int discoveredRegions = explorationLoaded
            ? mapDiscoveryRegions.Count(region => discovery->IsMapRegionDiscovered(region.MapId, region.DiscoveryId))
            : 0;

        (int HuntingCompleted, int HuntingTotal, bool HuntingLoaded, string HuntingDetail) hunting =
            CaptureHuntingLogTargets(nativePlayerState);

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
            ProgressAtlasModel.Category(
                ProgressAtlasCategoryId.Exploration,
                "World Exploration",
                discoveredRegions,
                mapDiscoveryRegions.Length,
                explorationLoaded,
                explorationLoaded
                    ? "Mapping and Remapping the Realm regions are discovered from the current game world catalog."
                    : "Waiting for this character's map-discovery state."),
            ProgressAtlasModel.Category(
                ProgressAtlasCategoryId.HuntingLogs,
                "Hunting & Grand Company Logs",
                hunting.HuntingCompleted,
                hunting.HuntingTotal,
                hunting.HuntingLoaded,
                hunting.HuntingDetail),
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
            ProgressAtlasModel.Category(ProgressAtlasCategoryId.Exploration, "World Exploration", 0,
                mapDiscoveryRegions?.Length ?? 0, false, "Log in to load this character's map discoveries."),
            ProgressAtlasModel.Category(ProgressAtlasCategoryId.HuntingLogs, "Hunting & Grand Company Logs", 0,
                0, false, "Log in to load the current class and Grand Company logs."),
        ]);

    private unsafe (int Completed, int Total, bool Loaded, string Detail) CaptureHuntingLogTargets(
        NativePlayerState* nativePlayerState)
    {
        MonsterNoteManager* manager = MonsterNoteManager.Instance();
        if (manager is null || nativePlayerState is null)
            return (0, 0, false, "Waiting for this character's Hunting Log state.");

        List<(uint LogKey, int MemoryIndex, string Name)> logs = [];
        uint classId = playerState.ClassJob.ValueNullable?.ClassJobParent.RowId ?? 0;
        int classIndex = HuntingLogMemoryIndex(classId);
        if (classIndex >= 0 && huntingLogCatalog.JobRanks.ContainsKey(classId))
            logs.Add((classId, classIndex, "Class Hunting Log"));

        byte grandCompany = nativePlayerState->GrandCompany;
        int companyIndex = grandCompany is >= 1 and <= 3 ? 7 + grandCompany : -1;
        uint companyKey = 10_000u + grandCompany;
        if (companyIndex >= 0 && huntingLogCatalog.JobRanks.ContainsKey(companyKey))
            logs.Add((companyKey, companyIndex, GrandCompanyName(grandCompany)));

        int completed = 0;
        int total = 0;
        List<HuntingLogTargetProgress> targetViews = [];
        foreach ((uint logKey, int memoryIndex, string logName) in logs)
        {
            int currentRank = manager->RankData[memoryIndex].Rank;
            IReadOnlyList<HuntingLogRank> ranks = huntingLogCatalog.JobRanks[logKey];
            for (int rankIndex = 0; rankIndex < ranks.Count; rankIndex++)
            {
                HuntingLogRank rank = ranks[rankIndex];
                for (int taskIndex = 0; taskIndex < rank.Tasks.Count; taskIndex++)
                {
                    HuntingLogTask task = rank.Tasks[taskIndex];
                    for (int monsterIndex = 0; monsterIndex < task.Monsters.Count; monsterIndex++)
                    {
                        HuntingLogMonster monster = task.Monsters[monsterIndex];
                        int observed = currentRank == rankIndex && monsterIndex < 4
                            ? manager->RankData[memoryIndex].RankData[taskIndex].Counts[monsterIndex]
                            : 0;
                        int killed = ProgressAtlasModel.HuntingLogKills(
                            currentRank,
                            rankIndex,
                            observed,
                            monster.Count);
                        total++;
                        if (killed >= monster.Count)
                            completed++;
                        targetViews.Add(new HuntingLogTargetProgress(
                            logKey,
                            logName,
                            rankIndex,
                            taskIndex,
                            monsterIndex,
                            monster.Id,
                            monster.Name,
                            killed,
                            monster.Count,
                            currentRank == rankIndex,
                            monster.Locations.Select(location => new HuntingLogLocation(
                                location.Terri,
                                location.Map,
                                location.Zone,
                                location.XCoord,
                                location.YCoord)).ToArray()));
                    }
                }
            }
        }
        string detail = total == 0
            ? "The current job has no class Hunting Log and no Grand Company log is available."
            : "Tracks every monster target and required kill for the current class and Grand Company directly from live Hunting Log memory.";
        huntingTargets = targetViews;
        return (completed, total, true, detail);
    }

    private static HuntingLogCatalog LoadHuntingLogCatalog()
    {
        const string resourceName = "VieriNexus.Data.hunting_log_targets.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                              ?? throw new InvalidDataException($"Missing Nexus Hunting Log resource {resourceName}.");
        return JsonSerializer.Deserialize<HuntingLogCatalog>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("Could not deserialize the Nexus Hunting Log target catalog.");
    }

    private static int HuntingLogMemoryIndex(uint classId) => classId switch
    {
        1 => 0,
        2 => 1,
        3 => 2,
        4 => 3,
        5 => 4,
        6 => 5,
        7 => 6,
        26 => 7,
        29 => 11,
        _ => -1,
    };

    private static string GrandCompanyName(byte grandCompany) => grandCompany switch
    {
        1 => "Maelstrom Hunting Log",
        2 => "Order of the Twin Adder Hunting Log",
        3 => "Immortal Flames Hunting Log",
        _ => "Grand Company Hunting Log",
    };

    private static MapDiscoveryRegion[] BuildMapDiscoveryRegions(IDataManager dataManager)
    {
        HashSet<uint> achievementMaps = dataManager.GetExcelSheet<SheetAchievement>()
            .Where(row => row.RowId > 0 && row.Type == 8 && row.Key.RowId > 0 && !row.Name.IsEmpty)
            .Select(row => row.Key.RowId)
            .ToHashSet();
        Dictionary<uint, SheetMap> maps = dataManager.GetExcelSheet<SheetMap>()
            .Where(row => row.RowId > 0 && row.DiscoveryFlag != 0 && achievementMaps.Contains(row.RowId))
            .ToDictionary(row => row.RowId);
        HashSet<MapDiscoveryRegion> regions = [];

        foreach (TerritoryType territory in dataManager.GetExcelSheet<TerritoryType>()
                     .Where(row => row.RowId > 0 && !row.Bg.IsEmpty))
        {
            string background = territory.Bg.ToString();
            int lastSlash = background.LastIndexOf('/');
            if (lastSlash < 0)
                continue;

            LgbFile? planMap;
            try
            {
                planMap = dataManager.GetFile<LgbFile>($"bg/{background[..lastSlash]}/planmap.lgb");
            }
            catch (Exception)
            {
                continue;
            }
            if (planMap is null)
                continue;

            foreach (LayerCommon.InstanceObject instance in planMap.Layers.SelectMany(layer => layer.InstanceObjects))
            {
                if (instance.Object is not LayerCommon.MapRangeInstanceObject range ||
                    range.DiscoveryEnabled == 0 || range.DiscoveryId == 0)
                    continue;

                uint mapId = range.Map != 0 ? range.Map : territory.Map.RowId;
                if (!maps.TryGetValue(mapId, out SheetMap map) || range.DiscoveryId >= 32 ||
                    (map.DiscoveryFlag & (1u << range.DiscoveryId)) == 0)
                    continue;
                regions.Add(new MapDiscoveryRegion(mapId, range.DiscoveryId));
            }
        }

        return regions.OrderBy(region => region.MapId).ThenBy(region => region.DiscoveryId).ToArray();
    }

    private readonly record struct MapDiscoveryRegion(uint MapId, byte DiscoveryId);

    private sealed class HuntingLogCatalog
    {
        public Dictionary<uint, List<HuntingLogRank>> JobRanks { get; set; } = [];
    }

    private sealed class HuntingLogRank
    {
        public List<HuntingLogTask> Tasks { get; set; } = [];
    }

    private sealed class HuntingLogTask
    {
        public List<HuntingLogMonster> Monsters { get; set; } = [];
    }

    private sealed class HuntingLogMonster
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<CatalogHuntingLogLocation> Locations { get; set; } = [];
    }

    private sealed class CatalogHuntingLogLocation
    {
        public uint Terri { get; set; }
        public uint Map { get; set; }
        public uint Zone { get; set; }
        public float XCoord { get; set; }
        public float YCoord { get; set; }
    }
}
