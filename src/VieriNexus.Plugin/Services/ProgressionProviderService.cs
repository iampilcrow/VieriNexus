using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

/// <summary>
/// Capability-checked adapters for stock runtime providers.
/// Merely having the expected plugin name is not enough: every required IPC member must be present.
/// Nexus selects exact Class / Job / Role or general side quests and delegates one bounded quest or duty at a time.
/// VieriCodex is intentionally not a runtime candidate; it remains available only to the migration importer.
/// </summary>
internal sealed class ProgressionProviderService : IProgressionDutyProvider, IProgressionQuestProvider,
    IProgressionGearProvider,
    IManualGearShoppingProvider
{
    private static readonly ProviderId QuestionableProviderId = new("vieri.provider.questionable-stock/v1");
    private static readonly ProviderId AutoDutyStockProviderId = new("vieri.provider.autoduty-stock/v1");
    private static readonly ProviderId NexusGearProviderId = new("vieri.provider.nexus-gear/v1");

    private readonly DependencyService dependencies;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly HashSet<uint> aetherCurrentQuestIds;
    private readonly Quest[] atlasQuestRows;
    private readonly Dictionary<uint, uint> atlasQuestChapters;
    private readonly HashSet<uint> allClassJobRoleChapters;
    private readonly ProgressAtlasDutyTarget[] atlasDutyTargets;
    private readonly Dictionary<uint, IReadOnlyList<ProgressAtlasQuestTarget>> atlasQuestTargetsByClassJob = [];
    private readonly Dictionary<string, (long ExpiresAt, ProgressAtlasQuestObservation Observation)> atlasQuestObservations = [];
    private long atlasObservationBudgetWindow;
    private int atlasObservationBudgetUsed;
    private readonly ICallGateSubscriber<bool> questionableIsRunning;
    private readonly QuestionableCompatibilityService questionableCompatibility;
    private readonly ICallGateSubscriber<string?> questionableCurrentQuest;
    private readonly ICallGateSubscriber<string, bool> questionableStartSingleQuest;
    private readonly ICallGateSubscriber<string, bool> questionableIsQuestLocked;
    private readonly ICallGateSubscriber<string, (bool Locked, string Reason)> questionableIsQuestLockedReason;
    private readonly ICallGateSubscriber<string, bool> questionableIsQuestComplete;
    private readonly ICallGateSubscriber<string, bool> questionableIsReadyToAcceptQuest;
    private readonly ICallGateSubscriber<string, bool> questionableIsQuestAccepted;
    private readonly ICallGateSubscriber<string, bool> questionableStop;
    private readonly ICallGateSubscriber<uint, bool> autoDutyContentHasPath;
    private readonly ICallGateSubscriber<bool> autoDutyIsStopped;
    private readonly ICallGateSubscriber<uint, int, bool, object> autoDutyRun;
    private readonly ICallGateSubscriber<object> autoDutyStop;
    private readonly ICallGateSubscriber<int, object> autoDutySetLevelingMode;
    private readonly ICallGateSubscriber<string, object, object> autoDutySetConfig;
    // Identity marker only. Nexus never dispatches this VieriAutoDuty-only endpoint.
    private readonly ICallGateSubscriber<int, string> vieriAutoDutyIdentityMarker;
    private readonly VnavmeshNavigationStopProvider navigationStop;
    private readonly AutoDutyMigrationService operationsProfiles;
    private readonly NexusGearCatalogService gearCatalog;
    private readonly NexusGearExecutionService gearExecution;
    private long eligibleDutyCacheExpiresAt;
    private int eligibleDutyCacheLevel;
    private IReadOnlyList<ProgressionDutyCandidate> eligibleDutyCache = [];
    private long eligibleQuestCacheExpiresAt;
    private uint eligibleQuestCacheClassJob;
    private int eligibleQuestCacheLevel;
    private bool eligibleQuestCacheIncludesMainScenario;
    private bool eligibleQuestCacheIncludesClassJobRole;
    private bool eligibleQuestCacheIncludesSideQuests;
    private string? eligibleQuestCacheProvider;
    private IReadOnlyList<ProgressionQuestCandidate> eligibleQuestCache = [];
    private long eligibleAetherCurrentQuestCacheExpiresAt;
    private string? eligibleAetherCurrentQuestCacheProvider;
    private int eligibleAetherCurrentQuestCacheLevel;
    private IReadOnlyList<ProgressionQuestCandidate> eligibleAetherCurrentQuestCache = [];
    private long eligibleAchievementQuestCacheExpiresAt;
    private uint eligibleAchievementQuestCacheClassJob;
    private int eligibleAchievementQuestCacheLevel;
    private string? eligibleAchievementQuestCacheProvider;
    private IReadOnlyList<ProgressionQuestCandidate> eligibleAchievementQuestCache = [];
    private readonly Dictionary<string, HashSet<string>> unsupportedQuestIdsByProvider = [];
    private long nexusGearStartedSequence;
    private long nexusGearCompletedSequence;
    private bool dutyPresentationInitialized;

    internal ProgressionProviderService(
        IDalamudPluginInterface pluginInterface,
        QuestionableCompatibilityService questionableCompatibility,
        DependencyService dependencies,
        IDataManager dataManager,
        IPlayerState playerState,
        IObjectTable objectTable,
        IClientState clientState,
        ICondition condition,
        IGameGui gameGui,
        AutoDutyMigrationService operationsProfiles,
        NavigationLibraryService navigationLibrary,
        NexusRouteTravelProvider routeTravel,
        VnavmeshNavigationStopProvider navigationStop)
    {
        this.dependencies = dependencies;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.playerState = playerState;
        this.navigationStop = navigationStop;
        this.operationsProfiles = operationsProfiles;
        aetherCurrentQuestIds = dataManager.GetExcelSheet<AetherCurrentCompFlgSet>()
            .Where(row => row.RowId > 0)
            .SelectMany(row => row.AetherCurrents)
            .Where(current => current.RowId > 0 && current.Value.Quest.RowId > 0)
            .Select(current => current.Value.Quest.RowId & 0xFFFF)
            .ToHashSet();
        atlasQuestRows = dataManager.GetExcelSheet<Quest>()
            .Where(quest => quest.RowId > 0 && quest.IssuerLocation.RowId > 0)
            .ToArray();
        atlasQuestChapters = dataManager.GetExcelSheet<QuestChapter>()
            .Where(row => row.RowId > 0 && row.Quest.RowId > 0)
            .GroupBy(row => row.Quest.RowId)
            .ToDictionary(group => group.Key, group => group.First().Redo.RowId);
        allClassJobRoleChapters = Enumerable.Range(1, 43)
            .SelectMany(id => ClassJobRoleQuestPolicy.Chapters((uint)id))
            .ToHashSet();
        Dictionary<uint, Quest> dutyUnlockQuests = atlasQuestRows
            .Select(quest => (Quest: quest,
                ContentFinderConditionId:
                quest.InstanceContentUnlock.ValueNullable?.ContentFinderCondition.RowId ?? 0))
            .Where(item => item.ContentFinderConditionId > 0)
            .GroupBy(item => item.ContentFinderConditionId)
            .ToDictionary(group => group.Key, group => group.First().Quest);
        atlasDutyTargets = dataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(row => row.RowId > 0 && row.TerritoryType.RowId > 0 && !row.Name.IsEmpty &&
                          row.ContentType.RowId is 2 or 3 or 4 or 5 or 21 or 28 or 30 or 37)
            .Select(row => (Row: row, ContentId: ContentRowId(row)))
            .Where(item => item.ContentId > 0)
            .Select(item => (item.Row, item.ContentId, UnlockQuest: dutyUnlockQuests.GetValueOrDefault(item.Row.RowId)))
            .Select(item => new ProgressAtlasDutyTarget(
                item.Row.RowId,
                item.Row.TerritoryType.RowId,
                item.ContentId,
                item.Row.Name.ExtractText(),
                DutyCategoryName(item.Row.ContentType.RowId),
                ProgressAtlasCatalog.ExpansionName(item.Row.TerritoryType.Value.ExVersion.RowId),
                item.Row.ClassJobLevelRequired,
                checked((int)item.Row.ItemLevelRequired),
                item.UnlockQuest.RowId > 0
                    ? ((ushort)(item.UnlockQuest.RowId & 0xFFFF)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null,
                item.UnlockQuest.RowId > 0 ? item.UnlockQuest.Name.ExtractText() : null,
                item.UnlockQuest.RowId > 0 ? item.UnlockQuest.ClassJobLevel[0] : 0,
                item.UnlockQuest.RowId > 0 && item.UnlockQuest.JournalGenre.ValueNullable?.Icon == 61412
                    ? ProgressionQuestKind.MainScenario
                    : ProgressionQuestKind.GeneralSideQuest))
            .DistinctBy(target => target.ContentFinderConditionId)
            .OrderBy(target => ProgressAtlasCatalog.ExpansionOrder(target.Expansion))
            .ThenBy(target => target.Category, StringComparer.CurrentCulture)
            .ThenBy(target => target.RequiredLevel)
            .ThenBy(target => target.Name, StringComparer.CurrentCulture)
            .ToArray();
        this.questionableCompatibility = questionableCompatibility;
        questionableIsRunning = pluginInterface.GetIpcSubscriber<bool>("Questionable.IsRunning");
        questionableCurrentQuest = pluginInterface.GetIpcSubscriber<string?>("Questionable.GetCurrentQuestId");
        questionableStartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.StartSingleQuest");
        questionableIsQuestLocked = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.IsQuestLocked");
        questionableIsQuestLockedReason = pluginInterface
            .GetIpcSubscriber<string, (bool Locked, string Reason)>("Questionable.IsQuestLockedReason");
        questionableIsQuestComplete = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.IsQuestComplete");
        questionableIsReadyToAcceptQuest = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.IsReadyToAcceptQuest");
        questionableIsQuestAccepted = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.IsQuestAccepted");
        questionableStop = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.Stop");
        autoDutyContentHasPath = pluginInterface.GetIpcSubscriber<uint, bool>("AutoDuty.ContentHasPath");
        autoDutyIsStopped = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        autoDutyRun = pluginInterface.GetIpcSubscriber<uint, int, bool, object>("AutoDuty.Run");
        autoDutyStop = pluginInterface.GetIpcSubscriber<object>("AutoDuty.Stop");
        autoDutySetLevelingMode = pluginInterface.GetIpcSubscriber<int, object>("AutoDuty.SetLevelingMode");
        autoDutySetConfig = pluginInterface.GetIpcSubscriber<string, object, object>("AutoDuty.SetConfig");
        vieriAutoDutyIdentityMarker = pluginInterface.GetIpcSubscriber<int, string>("AutoDuty.StartProgressionLeveling");
        gearCatalog = new NexusGearCatalogService(dataManager, playerState, objectTable);
        gearExecution = new NexusGearExecutionService(
            gearCatalog, navigationLibrary, routeTravel, clientState, playerState, objectTable,
            condition, gameGui, dataManager, CurrentItemLevel);
    }

    ProviderId IProgressionDutyProvider.Id =>
        Snapshot().Duties.Selected?.Id ?? AutoDutyStockProviderId;

    IReadOnlyList<ProgressionDutyCandidate> IProgressionDutyProvider.EligibleDuties(int currentLevel) =>
        EligibleDuties(currentLevel);

    ProgressionDutyProviderObservation IProgressionDutyProvider.Observe() => ObserveDuty();

    bool IProgressionDutyProvider.TryStart(uint territoryId, out string message) =>
        TryStartDuty(territoryId, out message);

    bool IProgressionDutyProvider.TryStop(out string message) => TryStopDuty(out message);

    ProviderId IProgressionQuestProvider.Id =>
        Snapshot().Questing.Selected?.Id ?? QuestionableProviderId;

    IReadOnlyList<ProgressionQuestCandidate> IProgressionQuestProvider.EligibleQuests(
        uint classJobId,
        int currentLevel,
        bool includeMainScenario,
        bool includeClassJobRole,
        bool includeGeneralSideQuests) => EligibleQuests(
            classJobId, currentLevel, includeMainScenario, includeClassJobRole, includeGeneralSideQuests);

    ProgressionQuestProviderObservation IProgressionQuestProvider.ObserveQuest(string questId) =>
        ObserveQuest(questId);

    ProgressionQuestStartResult IProgressionQuestProvider.TryStartQuest(ProgressionQuestCandidate quest) =>
        TryStartQuest(quest);

    bool IProgressionQuestProvider.TryStopQuest(out string message) => TryStopQuest(out message);

    ProviderId IProgressionGearProvider.Id => NexusGearProviderId;

    ProgressionGearProviderObservation IProgressionGearProvider.ObserveGearReadiness() =>
        ObserveGearReadiness();

    bool IProgressionGearProvider.TryStartGearReadiness(int minimumGilReserve, out string message) =>
        TryStartGearReadiness(minimumGilReserve, out message);

    bool IProgressionGearProvider.TryStopGearReadiness(out string message) =>
        TryStopGearReadiness(out message);

    ProviderId IManualGearShoppingProvider.Id => NexusGearProviderId;

    ProgressionGearProviderObservation IManualGearShoppingProvider.Observe() => ObserveGearReadiness();

    bool IManualGearShoppingProvider.TryStart(GearShoppingApproval approval, out string message) =>
        TryStartApprovedGearShopping(approval, out message);

    bool IManualGearShoppingProvider.TryStop(out string message) => TryStopGearReadiness(out message);

    internal bool IsGearReadinessReady => gearExecution.IsReady;

    internal bool IsStockQuestionableSelected =>
        Snapshot().Questing.Selected?.Id == QuestionableProviderId;

    internal bool IsGearShoppingPreviewReady => gearCatalog.IsAvailable;

    internal bool IsGearShoppingExecutionReady => gearExecution.IsReady;

    internal ProgressionCharacterMetrics CharacterMetrics() => new(CurrentItemLevel(), CurrentGil());

    internal void UpdateGearAdapter()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        gearExecution.Update(now);
        ReconcileDutyPresentation(now);
    }

    private void ReconcileDutyPresentation(DateTimeOffset now)
    {
        ProgressionProviderSelection selection = Snapshot().Duties;
        bool stockReady = selection.IsReady && selection.Selected?.Id == AutoDutyStockProviderId;
        if (!stockReady || !autoDutySetConfig.HasAction)
        {
            dutyPresentationInitialized = false;
            return;
        }
        if (dutyPresentationInitialized)
            return;

        try
        {
            // Current stock AutoDuty exposes its profile settings under these nested names.
            // Apply them once when the provider becomes ready. Re-reading or rewriting an
            // unsupported setting every frame used to flood AutoDuty's log and could interfere
            // with real duty work; presentation preferences must never be a runtime dependency.
            autoDutySetConfig.InvokeAction("DutyConfig.AutoManageRotationPluginState", "true");
            autoDutySetConfig.InvokeAction("DutyConfig.RotationPlugin", "WrathCombo");
            autoDutySetConfig.InvokeAction("Overlay.Show", "false");
            dutyPresentationInitialized = true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex,
                "Stock AutoDuty presentation preferences were unavailable; duty execution remains usable.");
            // Do not repeatedly call a provider that rejected optional presentation settings.
            dutyPresentationInitialized = true;
        }
    }

    internal ProgressionProviderSnapshot Snapshot()
    {
        ProgressionProviderCandidate[] dutyCandidates = DutyCandidates();
        ProgressionProviderCandidate[] candidates =
        [
            .. QuestCandidates(),
            .. dutyCandidates,
        ];

        return new ProgressionProviderSnapshot(
            ProgressionProviderPolicy.Select(ProgressionProviderRole.Questing, candidates),
            ProgressionProviderPolicy.Select(ProgressionProviderRole.Duties, candidates));
    }

    internal IReadOnlyDictionary<ProviderId, ProviderHealthSnapshot> ProviderHealth()
    {
        ProgressionProviderSnapshot snapshot = Snapshot();
        return snapshot.Questing.Candidates
            .Concat(snapshot.Duties.Candidates)
            .ToDictionary(
                candidate => candidate.Id,
                candidate => new ProviderHealthSnapshot(
                    candidate.Id,
                    candidate.DisplayName,
                    candidate.Readiness.ToString(),
                    candidate.Version,
                    candidate.Detail));
    }

    internal IReadOnlyList<ProgressionQuestCandidate> EligibleQuests(
        uint classJobId,
        int currentLevel,
        bool includeMainScenario,
        bool includeClassJobRole,
        bool includeGeneralSideQuests)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady || !includeMainScenario && !includeClassJobRole && !includeGeneralSideQuests)
            return [];

        long now = Environment.TickCount64;
        string selectedProvider = selection.Selected!.Id.Value;
        if (eligibleQuestCacheClassJob == classJobId && eligibleQuestCacheLevel == currentLevel &&
            eligibleQuestCacheIncludesMainScenario == includeMainScenario &&
            eligibleQuestCacheIncludesClassJobRole == includeClassJobRole &&
            eligibleQuestCacheIncludesSideQuests == includeGeneralSideQuests &&
            eligibleQuestCacheProvider == selectedProvider && now < eligibleQuestCacheExpiresAt)
            return eligibleQuestCache;

        var chapterSheet = dataManager.GetExcelSheet<QuestChapter>();
        var questSheet = dataManager.GetExcelSheet<Quest>();
        if (chapterSheet is null || questSheet is null)
            return [];

        Dictionary<uint, uint> questChapters = chapterSheet
            .Where(row => row.RowId > 0 && row.Quest.RowId > 0)
            .GroupBy(row => row.Quest.RowId)
            .ToDictionary(group => group.Key, group => group.First().Redo.RowId);
        HashSet<uint> currentChapters = ClassJobRoleQuestPolicy.Chapters(classJobId).ToHashSet();
        HashSet<uint> allClassJobRoleChapters = Enumerable.Range(1, 43)
            .SelectMany(id => ClassJobRoleQuestPolicy.Chapters((uint)id))
            .ToHashSet();
        HashSet<string> unsupported = unsupportedQuestIdsByProvider.GetValueOrDefault(selectedProvider) ?? [];
        List<ProgressionQuestCandidate> candidates = [];
        IEnumerable<(Quest Quest, ProgressionQuestKind Kind)> rows = questSheet
            .Where(quest => quest.RowId > 0 && quest.IssuerLocation.RowId > 0)
            .Select(quest => (Quest: quest, Kind: ClassifyQuest(
                quest,
                classJobId,
                questChapters.GetValueOrDefault(quest.RowId),
                currentChapters,
                allClassJobRoleChapters)))
            .Where(item => item.Kind == ProgressionQuestKind.ClassJobRole && includeClassJobRole ||
                           item.Kind == ProgressionQuestKind.MainScenario && includeMainScenario ||
                           item.Kind == ProgressionQuestKind.GeneralSideQuest && includeGeneralSideQuests)
            .Select(item => (item.Quest, Kind: item.Kind!.Value))
            .OrderBy(item => item.Kind == ProgressionQuestKind.MainScenario ? 0 :
                item.Kind == ProgressionQuestKind.ClassJobRole ? 1 : 2)
            .ThenBy(item => item.Quest.ClassJobLevel[0])
            .ThenBy(item => item.Quest.SortKey)
            .ThenBy(item => item.Quest.RowId);

        foreach ((Quest quest, ProgressionQuestKind kind) in rows)
        {
            int requiredLevel = quest.ClassJobLevel[0];
            if (requiredLevel > currentLevel)
                continue;

            string questId = ((ushort)(quest.RowId & 0xFFFF)).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            if (unsupported.Contains(questId))
                continue;
            try
            {
                if (questionableIsQuestComplete.InvokeFunc(questId))
                    continue;
                bool accepted = questionableIsQuestAccepted.InvokeFunc(questId);
                bool ready = questionableIsReadyToAcceptQuest.InvokeFunc(questId);
                bool locked = questionableIsQuestLocked.InvokeFunc(questId);
                if (!accepted && (!ready || locked))
                    continue;

                string name = quest.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Quest {questId}";
                candidates.Add(new ProgressionQuestCandidate(questId, name, requiredLevel, accepted, kind));
                if (candidates.Count >= 24)
                    break;
            }
            catch
            {
                // A single malformed or temporarily unavailable quest must not invalidate the provider.
            }
        }

        eligibleQuestCacheClassJob = classJobId;
        eligibleQuestCacheLevel = currentLevel;
        eligibleQuestCacheIncludesMainScenario = includeMainScenario;
        eligibleQuestCacheIncludesClassJobRole = includeClassJobRole;
        eligibleQuestCacheIncludesSideQuests = includeGeneralSideQuests;
        eligibleQuestCacheProvider = selectedProvider;
        eligibleQuestCacheExpiresAt = now + 30_000;
        eligibleQuestCache = candidates
            .DistinctBy(candidate => candidate.QuestId)
            .OrderByDescending(candidate => candidate.IsAccepted)
            .ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.RequiredLevel)
            .ThenBy(candidate => candidate.QuestId, StringComparer.Ordinal)
            .ToArray();
        return eligibleQuestCache;
    }

    internal IReadOnlyList<ProgressAtlasQuestTarget> AtlasQuestTargets(uint classJobId)
    {
        if (atlasQuestTargetsByClassJob.TryGetValue(classJobId, out IReadOnlyList<ProgressAtlasQuestTarget>? cached))
            return cached;

        HashSet<uint> currentChapters = ClassJobRoleQuestPolicy.Chapters(classJobId).ToHashSet();
        cached = atlasQuestRows
            .Select(quest => (Quest: quest, Kind: ClassifyQuest(
                quest,
                classJobId,
                atlasQuestChapters.GetValueOrDefault(quest.RowId),
                currentChapters,
                allClassJobRoleChapters)))
            .Where(item => item.Kind is not null)
            .Select(item => new ProgressAtlasQuestTarget(
                ((ushort)(item.Quest.RowId & 0xFFFF)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(item.Quest.Name.ExtractText())
                    ? $"Quest {item.Quest.RowId & 0xFFFF}"
                    : item.Quest.Name.ExtractText(),
                item.Quest.ClassJobLevel[0],
                item.Kind!.Value,
                ProgressAtlasCatalog.ExpansionName(item.Quest.Expansion.RowId)))
            .DistinctBy(target => target.QuestId)
            .OrderBy(target => ProgressAtlasCatalog.ExpansionOrder(target.Expansion))
            .ThenBy(target => ProgressAtlasCatalog.QuestCategoryOrder(target.Kind))
            .ThenBy(target => target.RequiredLevel)
            .ThenBy(target => target.Name, StringComparer.CurrentCulture)
            .ToArray();
        atlasQuestTargetsByClassJob[classJobId] = cached;
        return cached;
    }

    internal ProgressAtlasQuestObservation ObserveAtlasQuest(
        ProgressAtlasQuestTarget target,
        int currentLevel)
    {
        long now = Environment.TickCount64;
        if (atlasQuestObservations.TryGetValue(target.QuestId, out var cached) && now < cached.ExpiresAt)
            return cached.Observation;

        if (now - atlasObservationBudgetWindow >= 100)
        {
            atlasObservationBudgetWindow = now;
            atlasObservationBudgetUsed = 0;
        }
        if (atlasObservationBudgetUsed >= 12)
            return new(ProgressAtlasEntryState.Loading,
                "Reading this quest's live state from stock Questionable.", false);
        atlasObservationBudgetUsed++;

        ProgressionProviderSelection selection = Snapshot().Questing;
        ProgressAtlasQuestObservation observation;
        if (!selection.IsReady)
        {
            observation = new(ProgressAtlasEntryState.ProviderUnavailable, selection.Detail, false);
        }
        else if (unsupportedQuestIdsByProvider.GetValueOrDefault(selection.Selected!.Id.Value)?.Contains(target.QuestId) == true)
        {
            observation = new(ProgressAtlasEntryState.Unsupported,
                "Stock Questionable does not currently provide a route for this quest.", false);
        }
        else
        {
            try
            {
                bool complete = questionableIsQuestComplete.InvokeFunc(target.QuestId);
                bool accepted = !complete && questionableIsQuestAccepted.InvokeFunc(target.QuestId);
                bool ready = !complete && !accepted && questionableIsReadyToAcceptQuest.InvokeFunc(target.QuestId);
                bool locked = false;
                string lockReason = string.Empty;
                if (!complete && !accepted)
                {
                    if (questionableIsQuestLockedReason.HasFunction)
                    {
                        (locked, lockReason) = questionableIsQuestLockedReason.InvokeFunc(target.QuestId);
                    }
                    else
                    {
                        locked = questionableIsQuestLocked.InvokeFunc(target.QuestId);
                    }
                }
                observation = complete
                    ? new(ProgressAtlasEntryState.Complete, "Completed on this character.", false)
                    : accepted
                        ? new(ProgressAtlasEntryState.InProgress,
                            "Accepted and ready to continue through stock Questionable.", true)
                        : target.RequiredLevel > currentLevel
                            ? new(ProgressAtlasEntryState.Locked,
                                $"Requires level {target.RequiredLevel}; current level is {currentLevel}.", false)
                            : ready && !locked
                                ? new(ProgressAtlasEntryState.Ready,
                                    "Ready to start through stock Questionable.", true)
                                : new(ProgressAtlasEntryState.Locked,
                                    string.IsNullOrWhiteSpace(lockReason)
                                        ? "Requires earlier story, class/job/role, travel, or quest progress."
                                        : $"Prerequisites: {lockReason.Replace(",", " • ")}.", false);
            }
            catch (Exception ex)
            {
                observation = new(ProgressAtlasEntryState.ProviderUnavailable,
                    $"Questionable could not report this quest: {ex.Message}", false);
            }
        }

        atlasQuestObservations[target.QuestId] = (now + 30_000, observation);
        return observation;
    }

    internal ProgressionQuestStartResult StartAtlasQuest(ProgressAtlasQuestTarget target, int currentLevel)
    {
        ProgressAtlasQuestObservation observation = ObserveAtlasQuest(target, currentLevel);
        if (!observation.CanStart)
            return new(false, observation.State == ProgressAtlasEntryState.Unsupported, observation.Detail);

        ProgressionQuestStartResult result = TryStartQuest(new ProgressionQuestCandidate(
            target.QuestId,
            target.Name,
            target.RequiredLevel,
            observation.State == ProgressAtlasEntryState.InProgress,
            target.Kind));
        atlasQuestObservations.Remove(target.QuestId);
        return result;
    }

    internal IReadOnlyList<ProgressAtlasDutyTarget> AtlasDutyTargets => atlasDutyTargets;

    internal unsafe ProgressAtlasDutyObservation ObserveAtlasDuty(
        ProgressAtlasDutyTarget target,
        int currentLevel)
    {
        if (UIState.Instance() is null)
            return new(ProgressAtlasEntryState.ProviderUnavailable,
                "Waiting for this character's duty-unlock state.", false);

        bool unlocked = UIState.IsInstanceContentUnlocked(target.ContentId);
        bool completed = unlocked && UIState.IsInstanceContentCompleted(target.ContentId);
        if (!unlocked)
        {
            if (target.UnlockQuestId is not null && target.UnlockQuestName is not null)
            {
                var unlockTarget = new ProgressAtlasQuestTarget(
                    target.UnlockQuestId,
                    target.UnlockQuestName,
                    target.UnlockQuestLevel,
                    target.UnlockQuestKind,
                    target.Expansion);
                ProgressAtlasQuestObservation quest = ObserveAtlasQuest(unlockTarget, currentLevel);
                string detail = quest.State == ProgressAtlasEntryState.Complete
                    ? $"Unlock quest \"{target.UnlockQuestName}\" is complete; finish its related unlock conversation or prerequisite duty."
                    : $"Unlock quest: \"{target.UnlockQuestName}\". {quest.Detail}";
                return new(ProgressAtlasEntryState.Locked, detail, false, quest.CanStart);
            }
            string level = target.RequiredLevel > currentLevel
                ? $" Requires level {target.RequiredLevel}; current level is {currentLevel}."
                : string.Empty;
            return new(ProgressAtlasEntryState.Locked,
                $"Requires its unlock quest or prerequisite duty chain.{level}", false);
        }

        ProgressionDutyCandidate? runnable = EligibleDutyForTerritory(target.TerritoryId, currentLevel);
        return new(
            completed ? ProgressAtlasEntryState.Complete : ProgressAtlasEntryState.Ready,
            completed ? "Unlocked and cleared." : "Unlocked; not yet cleared.",
            runnable is not null);
    }

    internal bool StartAtlasDuty(ProgressAtlasDutyTarget target, int currentLevel, out string message)
    {
        ProgressAtlasDutyObservation observation = ObserveAtlasDuty(target, currentLevel);
        if (!observation.CanRun)
        {
            message = observation.State is ProgressAtlasEntryState.Complete or ProgressAtlasEntryState.Ready
                ? "This duty is unlocked, but stock AutoDuty does not expose a runnable path for it. Use the stock Duty Finder."
                : observation.Detail;
            return false;
        }
        return TryStartDuty(target.TerritoryId, out message);
    }

    internal ProgressionQuestStartResult StartAtlasDutyUnlockQuest(
        ProgressAtlasDutyTarget target,
        int currentLevel)
    {
        if (target.UnlockQuestId is null || target.UnlockQuestName is null)
            return new(false, false, "No exact unlock quest is known for this duty.");
        return StartAtlasQuest(new ProgressAtlasQuestTarget(
            target.UnlockQuestId,
            target.UnlockQuestName,
            target.UnlockQuestLevel,
            target.UnlockQuestKind,
            target.Expansion), currentLevel);
    }

    internal IReadOnlyList<ProgressionQuestCandidate> EligibleAetherCurrentQuests(
        IReadOnlyList<ProgressAtlasService.AetherCurrentQuestAtlasTarget> targets,
        int currentLevel)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady)
            return [];

        long now = Environment.TickCount64;
        string selectedProvider = selection.Selected!.Id.Value;
        if (eligibleAetherCurrentQuestCacheLevel == currentLevel &&
            eligibleAetherCurrentQuestCacheProvider == selectedProvider &&
            now < eligibleAetherCurrentQuestCacheExpiresAt)
            return eligibleAetherCurrentQuestCache;

        HashSet<string> unsupported = unsupportedQuestIdsByProvider.GetValueOrDefault(selectedProvider) ?? [];
        List<ProgressionQuestCandidate> candidates = [];
        foreach (ProgressAtlasService.AetherCurrentQuestAtlasTarget target in targets
                     .Where(target => target.RequiredLevel <= currentLevel)
                     .Where(target => !ProgressAtlasService.IsAetherCurrentUnlocked(target.AetherCurrentId))
                     .OrderByDescending(target => target.TerritoryId == clientState.TerritoryType)
                     .ThenBy(target => target.RequiredLevel)
                     .ThenBy(target => target.QuestId))
        {
            string questId = target.QuestId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (unsupported.Contains(questId))
                continue;
            try
            {
                if (questionableIsQuestComplete.InvokeFunc(questId))
                    continue;
                bool accepted = questionableIsQuestAccepted.InvokeFunc(questId);
                bool ready = questionableIsReadyToAcceptQuest.InvokeFunc(questId);
                bool locked = questionableIsQuestLocked.InvokeFunc(questId);
                if (!accepted && (!ready || locked))
                    continue;

                candidates.Add(new ProgressionQuestCandidate(
                    questId,
                    target.QuestName,
                    target.RequiredLevel,
                    accepted,
                    ProgressionQuestKind.AetherCurrent,
                    target.TerritoryId,
                    target.AetherCurrentId));
            }
            catch
            {
                // One temporarily unreadable quest must not hide other exact Aether Current work.
            }
        }

        eligibleAetherCurrentQuestCacheLevel = currentLevel;
        eligibleAetherCurrentQuestCacheProvider = selectedProvider;
        eligibleAetherCurrentQuestCacheExpiresAt = now + 30_000;
        eligibleAetherCurrentQuestCache = candidates
            .OrderByDescending(candidate => candidate.IsAccepted)
            .ThenByDescending(candidate => candidate.TerritoryId == clientState.TerritoryType)
            .ThenBy(candidate => candidate.RequiredLevel)
            .ThenBy(candidate => candidate.QuestId, StringComparer.Ordinal)
            .ToArray();
        return eligibleAetherCurrentQuestCache;
    }

    internal IReadOnlyList<ProgressionQuestCandidate> EligibleAchievementQuests(
        IReadOnlyCollection<uint> relatedQuestIds,
        int currentLevel)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        uint classJobId = playerState.ClassJob.RowId;
        if (!selection.IsReady || classJobId == 0 || relatedQuestIds.Count == 0)
            return [];

        long now = Environment.TickCount64;
        string selectedProvider = selection.Selected!.Id.Value;
        if (eligibleAchievementQuestCacheClassJob == classJobId &&
            eligibleAchievementQuestCacheLevel == currentLevel &&
            eligibleAchievementQuestCacheProvider == selectedProvider &&
            now < eligibleAchievementQuestCacheExpiresAt)
            return eligibleAchievementQuestCache;

        HashSet<uint> requested = relatedQuestIds
            .Where(id => id != 0)
            .Select(id => id & 0xFFFF)
            .ToHashSet();
        HashSet<string> unsupported = unsupportedQuestIdsByProvider.GetValueOrDefault(selectedProvider) ?? [];
        List<ProgressionQuestCandidate> candidates = [];
        foreach (Quest quest in dataManager.GetExcelSheet<Quest>()
                     .Where(row => row.RowId > 0 && requested.Contains(row.RowId & 0xFFFF))
                     .Where(row => row.ClassJobLevel[0] <= currentLevel && QuestAllowsClassJob(row, classJobId))
                     .OrderBy(row => row.ClassJobLevel[0])
                     .ThenBy(row => row.SortKey)
                     .ThenBy(row => row.RowId))
        {
            string questId = (quest.RowId & 0xFFFF).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            if (unsupported.Contains(questId))
                continue;
            try
            {
                if (questionableIsQuestComplete.InvokeFunc(questId))
                    continue;
                bool accepted = questionableIsQuestAccepted.InvokeFunc(questId);
                bool ready = questionableIsReadyToAcceptQuest.InvokeFunc(questId);
                bool locked = questionableIsQuestLocked.InvokeFunc(questId);
                if (!accepted && (!ready || locked))
                    continue;

                string name = quest.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Quest {questId}";
                candidates.Add(new ProgressionQuestCandidate(
                    questId,
                    name,
                    quest.ClassJobLevel[0],
                    accepted,
                    ProgressionQuestKind.Achievement,
                    quest.IssuerLocation.ValueNullable?.Territory.RowId ?? 0));
            }
            catch
            {
                // One temporarily unreadable achievement quest must not hide other exact work.
            }
        }

        eligibleAchievementQuestCacheClassJob = classJobId;
        eligibleAchievementQuestCacheLevel = currentLevel;
        eligibleAchievementQuestCacheProvider = selectedProvider;
        eligibleAchievementQuestCacheExpiresAt = now + 30_000;
        eligibleAchievementQuestCache = candidates
            .DistinctBy(candidate => candidate.QuestId)
            .OrderByDescending(candidate => candidate.IsAccepted)
            .ThenBy(candidate => candidate.RequiredLevel)
            .ThenBy(candidate => candidate.QuestId, StringComparer.Ordinal)
            .ToArray();
        return eligibleAchievementQuestCache;
    }

    private ProgressionQuestKind? ClassifyQuest(
        Quest quest,
        uint classJobId,
        uint chapter,
        IReadOnlySet<uint> currentClassJobRoleChapters,
        IReadOnlySet<uint> allClassJobRoleChapters)
    {
        if (currentClassJobRoleChapters.Contains(chapter))
            return ProgressionQuestKind.ClassJobRole;

        bool isMainScenario = quest.JournalGenre.ValueNullable?.Icon == 61412;
        uint questId = quest.RowId & 0xFFFF;
        if (aetherCurrentQuestIds.Contains(questId))
            return null;
        if (isMainScenario)
            return QuestAllowsClassJob(quest, classJobId)
                ? ProgressionQuestKind.MainScenario
                : null;
        bool sideQuest = GeneralSideQuestPolicy.IsGeneralSideQuest(
            questId,
            isMainScenario,
            quest.IsRepeatable,
            quest.Festival.RowId != 0,
            quest.BeastTribe.RowId != 0,
            quest.JournalGenre.RowId != 0,
            chapter,
            allClassJobRoleChapters);
        return sideQuest && QuestAllowsClassJob(quest, classJobId)
            ? ProgressionQuestKind.GeneralSideQuest
            : null;
    }

    private static string DutyCategoryName(uint contentTypeId) => contentTypeId switch
    {
        2 => "Dungeon Unlocks",
        3 => "Guildhest Unlocks",
        4 => "Trial Unlocks",
        5 => "Raid & Alliance Raid Unlocks",
        21 => "Deep Dungeon Unlocks",
        28 => "Ultimate Raid Unlocks",
        30 => "Variant & Criterion Dungeon Unlocks",
        37 => "Chaotic Alliance Raid Unlocks",
        _ => "Other Duty Unlocks",
    };

    private static bool QuestAllowsClassJob(Quest quest, uint classJobId)
    {
        ClassJobCategory? category = quest.ClassJobCategory0.ValueNullable;
        if (category is null)
            return false;

        string abbreviation = classJobId switch
        {
            1 => "GLA",
            2 => "PGL",
            3 => "MRD",
            4 => "LNC",
            5 => "ARC",
            6 => "CNJ",
            7 => "THM",
            8 => "CRP",
            9 => "BSM",
            10 => "ARM",
            11 => "GSM",
            12 => "LTW",
            13 => "WVR",
            14 => "ALC",
            15 => "CUL",
            16 => "MIN",
            17 => "BTN",
            18 => "FSH",
            19 => "PLD",
            20 => "MNK",
            21 => "WAR",
            22 => "DRG",
            23 => "BRD",
            24 => "WHM",
            25 => "BLM",
            26 => "ACN",
            27 => "SMN",
            28 => "SCH",
            29 => "ROG",
            30 => "NIN",
            31 => "MCH",
            32 => "DRK",
            33 => "AST",
            34 => "SAM",
            35 => "RDM",
            36 => "BLU",
            37 => "GNB",
            38 => "DNC",
            39 => "RPR",
            40 => "SGE",
            41 => "VPR",
            42 => "PCT",
            43 => "BST",
            _ => string.Empty,
        };
        if (abbreviation.Length == 0)
            return false;

        object boxed = category.Value;
        return boxed.GetType().GetProperty(abbreviation)?.GetValue(boxed) is true;
    }

    internal ProgressionQuestProviderObservation ObserveQuest(string questId)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady)
            return new(false, null, null, null, selection.Detail);

        try
        {
            bool running = questionableIsRunning.InvokeFunc();
            string? current = questionableCurrentQuest.InvokeFunc();
            bool complete = questionableIsQuestComplete.InvokeFunc(questId);
            if (complete)
            {
                eligibleQuestCacheExpiresAt = 0;
                eligibleAetherCurrentQuestCacheExpiresAt = 0;
                eligibleAchievementQuestCacheExpiresAt = 0;
            }
            return new(true, running, current, complete,
                complete
                    ? "The selected quest is complete."
                    : running
                        ? "The quest provider is running bounded Nexus work."
                        : "The quest provider is inactive.");
        }
        catch (Exception ex)
        {
            return new(false, null, null, null, ex.Message);
        }
    }

    internal ProgressionQuestStartResult TryStartQuest(ProgressionQuestCandidate quest)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady)
            return new(false, false, selection.Detail);

        try
        {
            if (questionableIsRunning.InvokeFunc())
                return new(false, false,
                    $"{selection.Selected!.DisplayName} is already running work Nexus does not own. Stop it before starting this goal.");

            if (selection.Selected!.Id == QuestionableProviderId && !questionableCompatibility.CanRun(quest.QuestId))
                return new(false, false,
                    $"Nexus is still preparing the protected Questionable route for {quest.Name}. It will retry after the compatibility pack is active.");

            bool accepted = questionableStartSingleQuest.InvokeFunc(quest.QuestId);
            string kind = quest.Kind switch
            {
                ProgressionQuestKind.GeneralSideQuest => "general side quest",
                ProgressionQuestKind.AetherCurrent => "Aether Current quest",
                ProgressionQuestKind.MainScenario => "Main Scenario quest",
                ProgressionQuestKind.Achievement => "achievement quest",
                _ => "Class / Job / Role quest",
            };
            if (accepted)
                return new(true, false,
                    $"Nexus asked {selection.Selected.DisplayName} to complete one exact {kind}, then return control for verification.");

            if (!unsupportedQuestIdsByProvider.TryGetValue(selection.Selected.Id.Value, out HashSet<string>? rejected))
                unsupportedQuestIdsByProvider[selection.Selected.Id.Value] = rejected = [];
            rejected.Add(quest.QuestId);
            eligibleQuestCacheExpiresAt = 0;
            eligibleAetherCurrentQuestCacheExpiresAt = 0;
            eligibleAchievementQuestCacheExpiresAt = 0;
            return new(false, true,
                $"{selection.Selected.DisplayName} has no path for {quest.Name}; Nexus skipped it and will choose another eligible activity.");
        }
        catch (Exception ex)
        {
            return new(false, false, $"The quest provider could not start: {ex.Message}");
        }
    }

    internal bool TryStopQuest(out string message)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady)
        {
            message = selection.Detail;
            return false;
        }

        try
        {
            string displayName = selection.Selected!.DisplayName;
            bool stopped = questionableStop.InvokeFunc("Nexus Stop");
            message = stopped
                ? $"Nexus asked {displayName} to stop the selected quest."
                : $"{displayName} rejected the Stop request.";
            return stopped;
        }
        catch (Exception ex)
        {
            message = $"The quest provider Stop request failed: {ex.Message}";
            return false;
        }
    }

    internal unsafe IReadOnlyList<ProgressionDutyCandidate> EligibleDuties(int currentLevel)
    {
        long now = Environment.TickCount64;
        if (eligibleDutyCacheLevel == currentLevel && now < eligibleDutyCacheExpiresAt)
            return eligibleDutyCache;

        ProgressionProviderSelection selection = Snapshot().Duties;
        if (!selection.IsReady || !autoDutyContentHasPath.HasFunction)
            return eligibleDutyCache = [];

        var sheet = dataManager.GetExcelSheet<ContentFinderCondition>();
        if (sheet is null || UIState.Instance() is null)
            return eligibleDutyCache = [];

        int currentItemLevel = CurrentItemLevel();
        List<ProgressionDutyCandidate> result = [];
        foreach (uint territoryId in LevelingDutyPolicy.StableTerritories)
        {
            ContentFinderCondition? condition = sheet.FirstOrDefault(row =>
                row.TerritoryType.RowId == territoryId &&
                row.ContentType.RowId == 2 &&
                ContentRowId(row) != 0);
            if (condition is null || condition.Value.RowId == 0)
                continue;

            ContentFinderCondition row = condition.Value;
            uint contentId = ContentRowId(row);
            if (contentId == 0)
                continue;
            if (row.ClassJobLevelRequired > currentLevel || row.ItemLevelRequired > currentItemLevel)
                continue;
            if (!UIState.IsInstanceContentUnlocked(contentId))
                continue;

            bool hasPath;
            try
            {
                hasPath = autoDutyContentHasPath.InvokeFunc(territoryId);
            }
            catch
            {
                hasPath = false;
            }
            if (!hasPath)
                continue;

            string name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Duty {territoryId}";
            result.Add(new ProgressionDutyCandidate(
                territoryId,
                contentId,
                name,
                row.ClassJobLevelRequired,
                checked((int)row.ItemLevelRequired)));
        }

        eligibleDutyCacheLevel = currentLevel;
        eligibleDutyCacheExpiresAt = now + 5_000;
        eligibleDutyCache = result;
        return result;
    }

    internal bool IsDutyProviderReady => Snapshot().Duties.IsReady &&
                                         autoDutyContentHasPath.HasFunction &&
                                         autoDutyIsStopped.HasFunction &&
                                         autoDutyRun.HasAction &&
                                         (autoDutySetLevelingMode.HasAction || autoDutySetConfig.HasAction) &&
                                         autoDutyStop.HasAction;

    internal unsafe ProgressionDutyCandidate? EligibleDutyForTerritory(uint territoryId, int currentLevel)
    {
        if (!IsDutyProviderReady || UIState.Instance() is null)
            return null;

        ContentFinderCondition? match = dataManager.GetExcelSheet<ContentFinderCondition>()
            .FirstOrDefault(row => row.TerritoryType.RowId == territoryId &&
                                   row.ContentType.RowId == 2 && ContentRowId(row) != 0);
        uint contentId = match is { } matched ? ContentRowId(matched) : 0;
        if (match is not { RowId: > 0 } row || row.ClassJobLevelRequired > currentLevel ||
            contentId == 0 || row.ItemLevelRequired > CurrentItemLevel() || !UIState.IsInstanceContentUnlocked(contentId))
            return null;
        try
        {
            if (!autoDutyContentHasPath.InvokeFunc(territoryId))
                return null;
        }
        catch
        {
            return null;
        }

        string name = row.Name.ExtractText();
        return new ProgressionDutyCandidate(
            territoryId,
            contentId,
            string.IsNullOrWhiteSpace(name) ? $"Duty {territoryId}" : name,
            row.ClassJobLevelRequired,
            checked((int)row.ItemLevelRequired));
    }

    private static uint ContentRowId(ContentFinderCondition row)
    {
        if (row.RowId == 0 || row.ContentType.RowId == 0)
            return 0;

        try
        {
            return row.Content.RowId;
        }
        catch (Exception exception) when (exception is NullReferenceException or InvalidOperationException or ArgumentException)
        {
            // Dynamic content links can be unresolved in otherwise enumerable live sheet rows.
            return 0;
        }
    }

    internal ProgressionDutyProviderObservation ObserveDuty()
    {
        ProgressionProviderSelection selection = Snapshot().Duties;
        if (!selection.IsReady || !autoDutyIsStopped.HasFunction)
            return new(false, null, selection.Detail);

        try
        {
            bool stopped = autoDutyIsStopped.InvokeFunc();
            return new(true, stopped,
                stopped ? "Duty provider is inactive." : "Duty provider is running Nexus-owned bounded work.");
        }
        catch (Exception ex)
        {
            return new(false, null, ex.Message);
        }
    }

    internal bool TryStartDuty(uint territoryId, out string message)
    {
        ProgressionProviderSelection selection = Snapshot().Duties;
        bool canResetLeveling = autoDutySetLevelingMode.HasAction || autoDutySetConfig.HasAction;
        if (!selection.IsReady || !autoDutyRun.HasAction || !canResetLeveling)
        {
            message = selection.Detail;
            return false;
        }

        try
        {
            if (autoDutyIsStopped.HasFunction && !autoDutyIsStopped.InvokeFunc())
            {
                message = "AutoDuty is already running work that Nexus does not own. Stop it before starting this goal.";
                return false;
            }

            if (!autoDutyContentHasPath.InvokeFunc(territoryId))
            {
                message = "The selected duty has no provider path.";
                return false;
            }

            // A previous path can survive a provider stop or zone transition. Clear it before
            // handing a fresh bounded duty to stock AutoDuty so it never inherits movement intent
            // from a Nexus route, a city transfer, or a prior instance.
            if (navigationStop.IsAvailable)
                navigationStop.RequestStop();

            // Nexus selects one exact bounded duty. Disable AutoDuty's own leveling scheduler so
            // AutoDuty.Run cannot substitute a different duty or continue an internal loop.
            if (autoDutySetLevelingMode.HasAction)
                autoDutySetLevelingMode.InvokeAction(0);
            else
                autoDutySetConfig.InvokeAction("leveling", "None");
            if (autoDutySetConfig.HasAction)
            {
                // Stock AutoDuty keeps duty mechanics; Nexus supplies the unified controls and
                // stock Wrath supplies rotations. These public settings prevent two overlays or
                // an accidental BossMod-only rotation selection from competing with that model.
                if (!dutyPresentationInitialized)
                    ReconcileDutyPresentation(DateTimeOffset.UtcNow);
            }
            autoDutyRun.InvokeAction(territoryId, 1, false);
            message = $"Nexus asked {selection.Selected!.DisplayName} to run one duty, then return control for verification.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"The duty provider could not start: {ex.Message}";
            return false;
        }
    }

    internal bool TryStopDuty(out string message)
    {
        if (!autoDutyStop.HasAction)
        {
            message = "The duty provider Stop contract is unavailable.";
            return false;
        }

        try
        {
            autoDutyStop.InvokeAction();
            message = "Nexus asked the duty provider to stop its bounded task.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"The duty provider Stop request failed: {ex.Message}";
            return false;
        }
    }

    internal ProgressionGearProviderObservation ObserveGearReadiness()
    {
        ProgressionGearProviderObservation observation = gearExecution.Observe();
        return observation with
        {
            StartedSequence = Math.Max(observation.StartedSequence, nexusGearStartedSequence),
            CompletedSequence = Math.Max(observation.CompletedSequence, nexusGearCompletedSequence),
        };
    }

    internal bool TryStartGearReadiness(int minimumGilReserve, out string message)
    {
        if (!IsGearReadinessReady)
        {
            message = "Nexus gear shopping requires a logged-in character and vnavmesh.";
            return false;
        }

        try
        {
            AutoDutyMaintenancePolicy? maintenance = operationsProfiles.ProfileFor(playerState.ContentId)?.Maintenance;
            if (maintenance is { AutoBuyVendorGear: false })
            {
                CompleteGearReadinessLocally();
                message = "Automatic vendor gear upgrades are disabled in Gear & Inventory settings.";
                return true;
            }

            if (autoDutyIsStopped.HasFunction && !autoDutyIsStopped.InvokeFunc())
            {
                message = "AutoDuty is already running duty work. Stop it before Nexus starts gear readiness.";
                return false;
            }

            GearUpgradeSnapshot snapshot = gearCatalog.BuildSnapshot();
            GearUpgradePreview preview = GearUpgradeCandidatePolicy.BuildPreview(snapshot);
            if (!string.IsNullOrWhiteSpace(preview.UnavailableReason))
            {
                message = preview.UnavailableReason;
                return false;
            }

            GearReadinessDecision decision = GearReadinessDecisionPolicy.Build(
                preview,
                CurrentGil(),
                Math.Max(0, minimumGilReserve));
            if (!decision.Success)
            {
                message = decision.Message;
                return false;
            }
            if (!decision.RequiresShopping)
            {
                ProgressionGearProviderObservation baseline = ObserveGearReadiness();
                long sequence = Math.Max(
                    Math.Max(baseline.StartedSequence, baseline.CompletedSequence),
                    Math.Max(nexusGearStartedSequence, nexusGearCompletedSequence)) + 1;
                nexusGearStartedSequence = sequence;
                nexusGearCompletedSequence = sequence;
                message = decision.Message;
                return true;
            }

            bool equipAfterPurchase = maintenance?.AutoEquipRecommendedGear != false;
            if (!TryStartApprovedGearShopping(decision.Approval!, equipAfterPurchase, out message))
                return false;
            message = $"Nexus selected and approved {decision.Approval!.Lines.Count} exact gear upgrade(s) " +
                      $"with a protected {minimumGilReserve:N0}-gil floor and started its native gear transaction.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Gear readiness could not start: {ex.Message}";
            return false;
        }
    }

    internal bool TryGetGearUpgradePreview(out GearUpgradePreview? preview, out string message)
    {
        preview = null;
        if (!IsGearShoppingPreviewReady)
        {
            message = "Log into a character before checking gear upgrades.";
            return false;
        }

        try
        {
            GearUpgradeSnapshot snapshot = gearCatalog.BuildSnapshot();
            preview = GearUpgradeCandidatePolicy.BuildPreview(snapshot);
            message = preview.UnavailableReason ??
                      $"Found {preview.Slots.Count(slot => slot.Replacement is not null)} verified upgrade option(s) for {preview.Job}.";
            return string.IsNullOrWhiteSpace(preview.UnavailableReason);
        }
        catch (Exception ex)
        {
            message = $"The gear preview could not be read: {ex.Message}";
            return false;
        }
    }

    internal bool TryStartApprovedGearShopping(GearShoppingApproval approval, out string message)
        => TryStartApprovedGearShopping(approval, true, out message);

    private bool TryStartApprovedGearShopping(
        GearShoppingApproval approval,
        bool equipAfterPurchase,
        out string message)
    {
        if (!IsGearShoppingExecutionReady)
        {
            message = "Nexus gear shopping is unavailable until the character and vnavmesh are ready.";
            return false;
        }
        if (autoDutyIsStopped.HasFunction && !autoDutyIsStopped.InvokeFunc())
        {
            message = "A duty is already running. Stop it before starting gear shopping.";
            return false;
        }
        ProgressionGearProviderObservation baseline = ObserveGearReadiness();
        gearExecution.EnsureSequenceAfter(Math.Max(baseline.StartedSequence, baseline.CompletedSequence));
        return gearExecution.Start(approval, equipAfterPurchase, out message);
    }

    private void CompleteGearReadinessLocally()
    {
        ProgressionGearProviderObservation baseline = ObserveGearReadiness();
        long sequence = Math.Max(
            Math.Max(baseline.StartedSequence, baseline.CompletedSequence),
            Math.Max(nexusGearStartedSequence, nexusGearCompletedSequence)) + 1;
        nexusGearStartedSequence = sequence;
        nexusGearCompletedSequence = sequence;
    }

    internal bool TryStopGearReadiness(out string message)
    {
        bool stopped = gearExecution.Stop(out message);
        if (stopped)
            eligibleDutyCacheExpiresAt = 0;
        return stopped;
    }

    private unsafe int CurrentItemLevel()
    {
        InventoryManager* manager = InventoryManager.Instance();
        InventoryContainer* equipped = manager is null
            ? null
            : manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
            return 0;

        var items = dataManager.GetExcelSheet<Item>();
        if (items is null)
            return 0;

        uint sum = 0;
        int calculatedSlots = 12;
        uint[] canHaveOffhand = [2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32];
        for (int slot = 0; slot < 13; slot++)
        {
            if (slot == 5)
                continue;

            InventoryItem* inventoryItem = equipped->GetInventorySlot(slot);
            if (inventoryItem is null || inventoryItem->ItemId == 0)
                continue;

            Item? item = items.GetRowOrDefault(inventoryItem->ItemId);
            if (item is null)
                continue;

            if (item.Value.ItemUICategory.RowId == 105)
            {
                if (slot == 0)
                    calculatedSlots--;
                calculatedSlots--;
                continue;
            }

            if (slot == 0 && !canHaveOffhand.Contains(item.Value.ItemUICategory.RowId))
            {
                sum += item.Value.LevelItem.RowId;
                slot++;
            }

            sum += item.Value.LevelItem.RowId;
        }

        return calculatedSlots <= 0 ? 0 : checked((int)(sum / (uint)calculatedSlots));
    }

    private static unsafe int CurrentGil()
    {
        InventoryManager* manager = InventoryManager.Instance();
        return manager is null ? 0 : checked((int)Math.Min(manager->GetGil(), int.MaxValue));
    }

    private ProgressionProviderCandidate[] QuestCandidates()
    {
        PluginPresence migrationSource = dependencies.FindPlugin("VieriCodex");
        PluginPresence stock = dependencies.FindPlugin("Questionable");
        if (migrationSource.IsLoaded)
        {
            return
            [
                new ProgressionProviderCandidate(
                    QuestionableProviderId,
                    "Questionable",
                    ProgressionProviderRole.Questing,
                    ProgressionProviderFlavor.Stock,
                    ProgressionProviderReadiness.Conflict,
                    stock.Version,
                    "VieriCodex is still loaded. It is a migration source only; disable it before Nexus can delegate quest work to stock Questionable."),
            ];
        }
        bool questionableContractReady = questionableIsRunning.HasFunction && questionableCurrentQuest.HasFunction &&
                                         questionableStartSingleQuest.HasFunction && questionableIsQuestLocked.HasFunction &&
                                         questionableIsQuestComplete.HasFunction && questionableIsReadyToAcceptQuest.HasFunction &&
                                         questionableIsQuestAccepted.HasFunction && questionableStop.HasFunction;
        const string contract = "IsRunning, GetCurrentQuestId, StartSingleQuest, eligibility, completion, and Stop";
        return
        [
            Candidate(
                QuestionableProviderId,
                "Questionable",
                ProgressionProviderRole.Questing,
                ProgressionProviderFlavor.Stock,
                stock,
                questionableContractReady,
                contract),
        ];
    }

    private ProgressionProviderCandidate[] DutyCandidates()
    {
        IReadOnlyList<PluginPresence> family = dependencies.FindPlugins("AutoDuty");
        PluginPresence[] loaded = family.Where(candidate => candidate.IsLoaded).ToArray();
        AutoDutyProviderIdentityAssessment identity = AutoDutyProviderIdentityPolicy.Assess(
            family.Select(candidate => new AutoDutyProviderInstance(
                candidate.IsLoaded,
                candidate.DisplayName,
                candidate.Version)),
            vieriAutoDutyIdentityMarker.HasFunction);
        if (identity.Identity == AutoDutyProviderIdentity.Conflict)
        {
            return loaded.Select((presence, index) => new ProgressionProviderCandidate(
                new ProviderId($"vieri.provider.autoduty-conflict-{index + 1}/v1"),
                presence.DisplayName ?? "AutoDuty",
                ProgressionProviderRole.Duties,
                IsVieriName(presence.DisplayName)
                    ? ProgressionProviderFlavor.VieriCompatibility
                    : ProgressionProviderFlavor.Stock,
                ProgressionProviderReadiness.Conflict,
                presence.Version,
                identity.Detail)).ToArray();
        }

        bool canResetLeveling = autoDutySetLevelingMode.HasAction || autoDutySetConfig.HasAction;
        bool stockContractReady = autoDutyContentHasPath.HasFunction && autoDutyIsStopped.HasFunction &&
                                  autoDutyRun.HasAction && autoDutyStop.HasAction && canResetLeveling;
        bool noStockContractVisible = !autoDutyContentHasPath.HasFunction && !autoDutyIsStopped.HasFunction &&
                                      !autoDutyRun.HasAction && !autoDutyStop.HasAction &&
                                      !autoDutySetLevelingMode.HasAction && !autoDutySetConfig.HasAction;
        List<string> missing = [];
        if (!autoDutyContentHasPath.HasFunction)
            missing.Add("ContentHasPath");
        if (!autoDutyIsStopped.HasFunction)
            missing.Add("IsStopped");
        if (!autoDutyRun.HasAction)
            missing.Add("Run");
        if (!autoDutyStop.HasAction)
            missing.Add("Stop");
        if (!canResetLeveling)
            missing.Add("SetLevelingMode or SetConfig");
        string contract = missing.Count == 0
            ? "ContentHasPath, Run, IsStopped, Stop, and leveling-mode reset"
            : $"missing {string.Join(", ", missing)}";
        PluginPresence? active = loaded.SingleOrDefault();
        bool activeIsVieri = identity.Identity == AutoDutyProviderIdentity.VieriCompatibility;
        PluginPresence? stockPresence = active is not null && !activeIsVieri
            ? active
            : family.FirstOrDefault(candidate => !IsVieriName(candidate.DisplayName));
        if (activeIsVieri)
        {
            return
            [
                new ProgressionProviderCandidate(
                    AutoDutyStockProviderId,
                    "AutoDuty",
                    ProgressionProviderRole.Duties,
                    ProgressionProviderFlavor.Stock,
                    ProgressionProviderReadiness.Conflict,
                    active?.Version,
                    "VieriAutoDuty is a settings-migration source only. Disable it and enable stock AutoDuty for duty execution."),
            ];
        }
        if (active is not null && !activeIsVieri && noStockContractVisible &&
            family.Any(candidate => !candidate.IsLoaded && IsVieriName(candidate.DisplayName)))
        {
            return
            [
                new ProgressionProviderCandidate(
                    AutoDutyStockProviderId,
                    "AutoDuty",
                    ProgressionProviderRole.Duties,
                    ProgressionProviderFlavor.Stock,
                    ProgressionProviderReadiness.ReloadRequired,
                    active.Version,
                    "Stock AutoDuty was enabled before VieriAutoDuty finished unloading, so the shared duty connection was released afterward. Restart FFXIV once with VieriAutoDuty disabled; Nexus will then connect to stock AutoDuty normally."),
            ];
        }
        ProgressionProviderCandidate stock = Candidate(
            AutoDutyStockProviderId,
            "AutoDuty",
            ProgressionProviderRole.Duties,
            ProgressionProviderFlavor.Stock,
            stockPresence ?? new PluginPresence(false, false, null),
            active is not null && !activeIsVieri && stockContractReady,
            contract);
        return [stock];
    }

    private static bool IsVieriName(string? displayName) =>
        string.Equals(displayName, "VieriAutoDuty", StringComparison.OrdinalIgnoreCase);

    private static ProgressionProviderCandidate Candidate(
        ProviderId id,
        string displayName,
        ProgressionProviderRole role,
        ProgressionProviderFlavor flavor,
        PluginPresence presence,
        bool contractReady,
        string contract)
    {
        ProgressionProviderReadiness readiness = !presence.IsInstalled
            ? ProgressionProviderReadiness.Missing
            : !presence.IsLoaded
                ? ProgressionProviderReadiness.Disabled
                : contractReady
                    ? ProgressionProviderReadiness.Ready
                    : ProgressionProviderReadiness.Incompatible;
        string detail = readiness switch
        {
            ProgressionProviderReadiness.Missing => $"{displayName} is not installed.",
            ProgressionProviderReadiness.Disabled => $"{displayName} is installed but disabled.",
            ProgressionProviderReadiness.Incompatible =>
                $"{displayName} is loaded but does not expose the required contract: {contract}.",
            _ => $"Required contract detected: {contract}.",
        };
        return new ProgressionProviderCandidate(
            id,
            displayName,
            role,
            flavor,
            readiness,
            presence.Version,
            detail);
    }
}

internal sealed record ProgressionProviderSnapshot(
    ProgressionProviderSelection Questing,
    ProgressionProviderSelection Duties);
