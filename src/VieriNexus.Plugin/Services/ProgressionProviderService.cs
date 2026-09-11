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
/// Capability-checked adapters for the temporary Vieri providers and their intended stock replacements.
/// Merely having the expected plugin name is not enough: every required IPC member must be present.
/// Nexus selects exact Class / Job / Role quests and delegates one bounded quest or duty at a time.
/// Vieri compatibility remains available only while the same contract is proven against stock plugins.
/// </summary>
internal sealed class ProgressionProviderService : IProgressionDutyProvider, IProgressionQuestProvider,
    IProgressionGearProvider,
    IManualGearShoppingProvider
{
    private static readonly ProviderId CodexProviderId = new("vieri.provider.codex-compat/v1");
    private static readonly ProviderId QuestionableProviderId = new("vieri.provider.questionable-stock/v1");
    private static readonly ProviderId AutoDutyCompatibilityProviderId = new("vieri.provider.autoduty-compat/v1");
    private static readonly ProviderId AutoDutyStockProviderId = new("vieri.provider.autoduty-stock/v1");
    private static readonly ProviderId NexusGearProviderId = new("vieri.provider.nexus-gear/v1");

    private readonly DependencyService dependencies;
    private readonly IDataManager dataManager;
    private readonly ICallGateSubscriber<bool> codexIsRunning;
    private readonly ICallGateSubscriber<string?> codexCurrentQuest;
    private readonly ICallGateSubscriber<string, bool> codexStartSingleQuest;
    private readonly ICallGateSubscriber<string, bool> codexIsQuestLocked;
    private readonly ICallGateSubscriber<string, bool> codexIsQuestComplete;
    private readonly ICallGateSubscriber<string, bool> codexIsReadyToAcceptQuest;
    private readonly ICallGateSubscriber<string, bool> codexIsQuestAccepted;
    private readonly ICallGateSubscriber<string, bool> codexStop;
    private readonly ICallGateSubscriber<bool> questionableIsRunning;
    private readonly ICallGateSubscriber<string?> questionableCurrentQuest;
    private readonly ICallGateSubscriber<string, bool> questionableStartSingleQuest;
    private readonly ICallGateSubscriber<string, bool> questionableIsQuestLocked;
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
    private readonly ICallGateSubscriber<int, string> vieriAutoDutyProgression;
    private readonly NexusGearCatalogService gearCatalog;
    private readonly NexusGearExecutionService gearExecution;
    private long eligibleDutyCacheExpiresAt;
    private int eligibleDutyCacheLevel;
    private IReadOnlyList<ProgressionDutyCandidate> eligibleDutyCache = [];
    private long eligibleQuestCacheExpiresAt;
    private uint eligibleQuestCacheClassJob;
    private int eligibleQuestCacheLevel;
    private string? eligibleQuestCacheProvider;
    private IReadOnlyList<ProgressionQuestCandidate> eligibleQuestCache = [];
    private long nexusGearStartedSequence;
    private long nexusGearCompletedSequence;

    internal ProgressionProviderService(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies,
        IDataManager dataManager,
        IPlayerState playerState,
        IObjectTable objectTable,
        IClientState clientState,
        ICondition condition,
        IGameGui gameGui,
        NavigationLibraryService navigationLibrary,
        NexusRouteTravelProvider routeTravel)
    {
        this.dependencies = dependencies;
        this.dataManager = dataManager;
        codexIsRunning = pluginInterface.GetIpcSubscriber<bool>("VieriCodex.IsRunning");
        codexCurrentQuest = pluginInterface.GetIpcSubscriber<string?>("VieriCodex.GetCurrentQuestId");
        codexStartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.StartSingleQuest");
        codexIsQuestLocked = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.IsQuestLocked");
        codexIsQuestComplete = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.IsQuestComplete");
        codexIsReadyToAcceptQuest = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.IsReadyToAcceptQuest");
        codexIsQuestAccepted = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.IsQuestAccepted");
        codexStop = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.Stop");
        questionableIsRunning = pluginInterface.GetIpcSubscriber<bool>("Questionable.IsRunning");
        questionableCurrentQuest = pluginInterface.GetIpcSubscriber<string?>("Questionable.GetCurrentQuestId");
        questionableStartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.StartSingleQuest");
        questionableIsQuestLocked = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.IsQuestLocked");
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
        vieriAutoDutyProgression = pluginInterface.GetIpcSubscriber<int, string>("AutoDuty.StartProgressionLeveling");
        gearCatalog = new NexusGearCatalogService(dataManager, playerState, objectTable);
        gearExecution = new NexusGearExecutionService(
            gearCatalog, navigationLibrary, routeTravel, clientState, playerState, objectTable,
            condition, gameGui, dataManager, CurrentItemLevel);
    }

    ProviderId IProgressionDutyProvider.Id =>
        Snapshot().Duties.Selected?.Id ?? AutoDutyCompatibilityProviderId;

    IReadOnlyList<ProgressionDutyCandidate> IProgressionDutyProvider.EligibleDuties(int currentLevel) =>
        EligibleDuties(currentLevel);

    ProgressionDutyProviderObservation IProgressionDutyProvider.Observe() => ObserveDuty();

    bool IProgressionDutyProvider.TryStart(uint territoryId, out string message) =>
        TryStartDuty(territoryId, out message);

    bool IProgressionDutyProvider.TryStop(out string message) => TryStopDuty(out message);

    ProviderId IProgressionQuestProvider.Id =>
        Snapshot().Questing.Selected?.Id ?? QuestionableProviderId;

    IReadOnlyList<ProgressionQuestCandidate> IProgressionQuestProvider.EligibleClassJobRoleQuests(
        uint classJobId,
        int currentLevel) => EligibleClassJobRoleQuests(classJobId, currentLevel);

    ProgressionQuestProviderObservation IProgressionQuestProvider.ObserveQuest(string questId) =>
        ObserveQuest(questId);

    bool IProgressionQuestProvider.TryStartQuest(string questId, out string message) =>
        TryStartQuest(questId, out message);

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

    internal bool IsGearShoppingPreviewReady => gearCatalog.IsAvailable;

    internal bool IsGearShoppingExecutionReady => gearExecution.IsReady;

    internal ProgressionCharacterMetrics CharacterMetrics() => new(CurrentItemLevel(), CurrentGil());

    internal void UpdateGearAdapter() => gearExecution.Update(DateTimeOffset.UtcNow);

    internal ProgressionProviderSnapshot Snapshot()
    {
        ProgressionProviderCandidate[] dutyCandidates = DutyCandidates();
        ProgressionProviderCandidate[] candidates =
        [
            QuestCandidate(
                "VieriCodex",
                "VieriCodex",
                CodexProviderId,
                ProgressionProviderFlavor.VieriCompatibility,
                codexIsRunning.HasFunction && codexCurrentQuest.HasFunction &&
                codexStartSingleQuest.HasFunction && codexIsQuestLocked.HasFunction &&
                codexIsQuestComplete.HasFunction && codexIsReadyToAcceptQuest.HasFunction &&
                codexIsQuestAccepted.HasFunction && codexStop.HasFunction),
            QuestCandidate(
                "Questionable",
                "Questionable",
                QuestionableProviderId,
                ProgressionProviderFlavor.Stock,
                questionableIsRunning.HasFunction && questionableCurrentQuest.HasFunction &&
                questionableStartSingleQuest.HasFunction && questionableIsQuestLocked.HasFunction &&
                questionableIsQuestComplete.HasFunction && questionableIsReadyToAcceptQuest.HasFunction &&
                questionableIsQuestAccepted.HasFunction && questionableStop.HasFunction),
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

    internal IReadOnlyList<ProgressionQuestCandidate> EligibleClassJobRoleQuests(
        uint classJobId,
        int currentLevel)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady)
            return [];

        long now = Environment.TickCount64;
        string selectedProvider = selection.Selected!.Id.Value;
        if (eligibleQuestCacheClassJob == classJobId && eligibleQuestCacheLevel == currentLevel &&
            eligibleQuestCacheProvider == selectedProvider && now < eligibleQuestCacheExpiresAt)
            return eligibleQuestCache;

        HashSet<uint> chapters = ClassJobRoleQuestPolicy.Chapters(classJobId).ToHashSet();
        if (chapters.Count == 0)
            return [];

        var chapterSheet = dataManager.GetExcelSheet<QuestChapter>();
        var questSheet = dataManager.GetExcelSheet<Quest>();
        if (chapterSheet is null || questSheet is null)
            return [];

        List<ProgressionQuestCandidate> candidates = [];
        foreach (QuestChapter chapter in chapterSheet.Where(row =>
                     row.RowId > 0 && row.Quest.RowId > 0 && chapters.Contains(row.Redo.RowId)))
        {
            Quest? quest = questSheet.GetRowOrDefault(chapter.Quest.RowId);
            if (quest is null)
                continue;

            int requiredLevel = quest.Value.ClassJobLevel[0];
            if (requiredLevel > currentLevel)
                continue;

            string questId = ((ushort)(quest.Value.RowId & 0xFFFF)).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                if (InvokeQuestBool(selection, codexIsQuestComplete, questionableIsQuestComplete, questId))
                    continue;
                bool accepted = InvokeQuestBool(selection, codexIsQuestAccepted, questionableIsQuestAccepted, questId);
                bool ready = InvokeQuestBool(selection, codexIsReadyToAcceptQuest,
                    questionableIsReadyToAcceptQuest, questId);
                bool locked = InvokeQuestBool(selection, codexIsQuestLocked, questionableIsQuestLocked, questId);
                if (!accepted && (!ready || locked))
                    continue;

                string name = quest.Value.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Quest {questId}";
                candidates.Add(new ProgressionQuestCandidate(questId, name, requiredLevel, accepted));
            }
            catch
            {
                // A single malformed or temporarily unavailable quest must not invalidate the provider.
            }
        }

        eligibleQuestCacheClassJob = classJobId;
        eligibleQuestCacheLevel = currentLevel;
        eligibleQuestCacheProvider = selectedProvider;
        eligibleQuestCacheExpiresAt = now + 5_000;
        eligibleQuestCache = candidates
            .DistinctBy(candidate => candidate.QuestId)
            .OrderByDescending(candidate => candidate.IsAccepted)
            .ThenBy(candidate => candidate.RequiredLevel)
            .ThenBy(candidate => candidate.QuestId, StringComparer.Ordinal)
            .ToArray();
        return eligibleQuestCache;
    }

    internal ProgressionQuestProviderObservation ObserveQuest(string questId)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady)
            return new(false, null, null, null, selection.Detail);

        try
        {
            bool running = InvokeQuestBool(selection, codexIsRunning, questionableIsRunning);
            string? current = InvokeQuestValue(selection, codexCurrentQuest, questionableCurrentQuest);
            bool complete = InvokeQuestBool(selection, codexIsQuestComplete, questionableIsQuestComplete, questId);
            if (complete)
                eligibleQuestCacheExpiresAt = 0;
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

    internal bool TryStartQuest(string questId, out string message)
    {
        ProgressionProviderSelection selection = Snapshot().Questing;
        if (!selection.IsReady)
        {
            message = selection.Detail;
            return false;
        }

        try
        {
            if (InvokeQuestBool(selection, codexIsRunning, questionableIsRunning))
            {
                message = $"{selection.Selected!.DisplayName} is already running work Nexus does not own. Stop it before starting this goal.";
                return false;
            }

            bool accepted = selection.Selected!.Id == CodexProviderId
                ? codexStartSingleQuest.InvokeFunc(questId)
                : questionableStartSingleQuest.InvokeFunc(questId);
            message = accepted
                ? $"Nexus asked {selection.Selected.DisplayName} to complete one exact Class / Job / Role quest, then return control for verification."
                : $"{selection.Selected.DisplayName} does not have a supported path for the selected quest.";
            return accepted;
        }
        catch (Exception ex)
        {
            message = $"The quest provider could not start: {ex.Message}";
            return false;
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
            bool stopped = selection.Selected!.Id == CodexProviderId
                ? codexStop.InvokeFunc("Nexus Stop")
                : questionableStop.InvokeFunc("Nexus Stop");
            message = stopped
                ? $"Nexus asked {selection.Selected.DisplayName} to stop the selected quest."
                : $"{selection.Selected.DisplayName} rejected the Stop request.";
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
                row.Content.RowId != 0 &&
                row.ContentType.RowId == 2);
            if (condition is null || condition.Value.RowId == 0)
                continue;

            ContentFinderCondition row = condition.Value;
            if (row.ClassJobLevelRequired > currentLevel || row.ItemLevelRequired > currentItemLevel)
                continue;
            if (!UIState.IsInstanceContentUnlocked(row.Content.RowId))
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
                row.Content.RowId,
                name,
                row.ClassJobLevelRequired,
                checked((int)row.ItemLevelRequired)));
        }

        eligibleDutyCacheLevel = currentLevel;
        eligibleDutyCacheExpiresAt = now + 5_000;
        eligibleDutyCache = result;
        return result;
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

            // Nexus selects one exact bounded duty. Disable AutoDuty's own leveling scheduler so
            // AutoDuty.Run cannot substitute a different duty or continue an internal loop.
            if (autoDutySetLevelingMode.HasAction)
                autoDutySetLevelingMode.InvokeAction(0);
            else
                autoDutySetConfig.InvokeAction("leveling", "None");
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

            if (!TryStartApprovedGearShopping(decision.Approval!, out message))
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
        return gearExecution.Start(approval, out message);
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

    private static bool InvokeQuestBool(
        ProgressionProviderSelection selection,
        ICallGateSubscriber<bool> codex,
        ICallGateSubscriber<bool> questionable) =>
        selection.Selected!.Id == CodexProviderId ? codex.InvokeFunc() : questionable.InvokeFunc();

    private static bool InvokeQuestBool(
        ProgressionProviderSelection selection,
        ICallGateSubscriber<string, bool> codex,
        ICallGateSubscriber<string, bool> questionable,
        string questId) =>
        selection.Selected!.Id == CodexProviderId
            ? codex.InvokeFunc(questId)
            : questionable.InvokeFunc(questId);

    private static string? InvokeQuestValue(
        ProgressionProviderSelection selection,
        ICallGateSubscriber<string?> codex,
        ICallGateSubscriber<string?> questionable) =>
        selection.Selected!.Id == CodexProviderId ? codex.InvokeFunc() : questionable.InvokeFunc();

    private ProgressionProviderCandidate QuestCandidate(
        string internalName,
        string displayName,
        ProviderId providerId,
        ProgressionProviderFlavor flavor,
        bool contractReady)
    {
        PluginPresence presence = dependencies.FindPlugin(internalName);
        return Candidate(
            providerId,
            displayName,
            ProgressionProviderRole.Questing,
            flavor,
            presence,
            contractReady,
            "IsRunning, GetCurrentQuestId, StartSingleQuest, eligibility, completion, and Stop");
    }

    private ProgressionProviderCandidate[] DutyCandidates()
    {
        PluginPresence presence = dependencies.FindPlugin("AutoDuty");
        bool canResetLeveling = autoDutySetLevelingMode.HasAction || autoDutySetConfig.HasAction;
        bool stockContractReady = autoDutyContentHasPath.HasFunction && autoDutyIsStopped.HasFunction &&
                                  autoDutyRun.HasAction && autoDutyStop.HasAction && canResetLeveling;
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
        bool isVieriCompatibilityProvider = vieriAutoDutyProgression.HasFunction ||
            string.Equals(presence.DisplayName, "VieriAutoDuty", StringComparison.OrdinalIgnoreCase);
        ProgressionProviderCandidate compatibility = isVieriCompatibilityProvider
            ? Candidate(
                AutoDutyCompatibilityProviderId,
                "VieriAutoDuty",
                ProgressionProviderRole.Duties,
                ProgressionProviderFlavor.VieriCompatibility,
                presence,
                stockContractReady,
                contract)
            : UnavailableDutyCandidate(
                AutoDutyCompatibilityProviderId,
                "VieriAutoDuty",
                ProgressionProviderFlavor.VieriCompatibility,
                "The VieriAutoDuty migration source is not active.");
        ProgressionProviderCandidate stock = !isVieriCompatibilityProvider
            ? Candidate(
                AutoDutyStockProviderId,
                "AutoDuty",
                ProgressionProviderRole.Duties,
                ProgressionProviderFlavor.Stock,
                presence,
                stockContractReady,
                contract)
            : UnavailableDutyCandidate(
                AutoDutyStockProviderId,
                "AutoDuty",
                ProgressionProviderFlavor.Stock,
                "Target provider after Nexus absorbs the remaining VieriAutoDuty behavior; do not enable it beside the fork.");
        return [compatibility, stock];
    }

    private static ProgressionProviderCandidate UnavailableDutyCandidate(
        ProviderId id,
        string displayName,
        ProgressionProviderFlavor flavor,
        string detail) => new(
        id,
        displayName,
        ProgressionProviderRole.Duties,
        flavor,
        ProgressionProviderReadiness.Missing,
        null,
        detail);

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
