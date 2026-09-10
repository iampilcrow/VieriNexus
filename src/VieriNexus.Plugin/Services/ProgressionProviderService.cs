using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

/// <summary>
/// Capability-checked adapters for the temporary Vieri providers and their intended stock replacements.
/// Merely having the expected plugin name is not enough: every required IPC member must be present.
/// Questing remains observation-only; the duty edge exposes one exact bounded run and Stop.
/// </summary>
internal sealed class ProgressionProviderService : IProgressionDutyProvider
{
    private static readonly ProviderId CodexProviderId = new("vieri.provider.codex-compat/v1");
    private static readonly ProviderId QuestionableProviderId = new("vieri.provider.questionable-stock/v1");
    private static readonly ProviderId AutoDutyCompatibilityProviderId = new("vieri.provider.autoduty-compat/v1");
    private static readonly ProviderId AutoDutyStockProviderId = new("vieri.provider.autoduty-stock/v1");

    private readonly DependencyService dependencies;
    private readonly IDataManager dataManager;
    private readonly ICallGateSubscriber<bool> codexIsRunning;
    private readonly ICallGateSubscriber<string, bool> codexStartSingleQuest;
    private readonly ICallGateSubscriber<string, bool> codexStop;
    private readonly ICallGateSubscriber<bool> questionableIsRunning;
    private readonly ICallGateSubscriber<string, bool> questionableStartSingleQuest;
    private readonly ICallGateSubscriber<string, bool> questionableStop;
    private readonly ICallGateSubscriber<uint, bool> autoDutyContentHasPath;
    private readonly ICallGateSubscriber<bool> autoDutyIsStopped;
    private readonly ICallGateSubscriber<uint, int, bool, object> autoDutyRun;
    private readonly ICallGateSubscriber<object> autoDutyStop;
    private readonly ICallGateSubscriber<int, object> autoDutySetLevelingMode;
    private readonly ICallGateSubscriber<int, string> vieriAutoDutyProgression;
    private long eligibleDutyCacheExpiresAt;
    private int eligibleDutyCacheLevel;
    private IReadOnlyList<ProgressionDutyCandidate> eligibleDutyCache = [];

    internal ProgressionProviderService(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies,
        IDataManager dataManager)
    {
        this.dependencies = dependencies;
        this.dataManager = dataManager;
        codexIsRunning = pluginInterface.GetIpcSubscriber<bool>("VieriCodex.IsRunning");
        codexStartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.StartSingleQuest");
        codexStop = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.Stop");
        questionableIsRunning = pluginInterface.GetIpcSubscriber<bool>("Questionable.IsRunning");
        questionableStartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.StartSingleQuest");
        questionableStop = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.Stop");
        autoDutyContentHasPath = pluginInterface.GetIpcSubscriber<uint, bool>("AutoDuty.ContentHasPath");
        autoDutyIsStopped = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        autoDutyRun = pluginInterface.GetIpcSubscriber<uint, int, bool, object>("AutoDuty.Run");
        autoDutyStop = pluginInterface.GetIpcSubscriber<object>("AutoDuty.Stop");
        autoDutySetLevelingMode = pluginInterface.GetIpcSubscriber<int, object>("AutoDuty.SetLevelingMode");
        vieriAutoDutyProgression = pluginInterface.GetIpcSubscriber<int, string>("AutoDuty.StartProgressionLeveling");
    }

    ProviderId IProgressionDutyProvider.Id =>
        Snapshot().Duties.Selected?.Id ?? AutoDutyCompatibilityProviderId;

    IReadOnlyList<ProgressionDutyCandidate> IProgressionDutyProvider.EligibleDuties(int currentLevel) =>
        EligibleDuties(currentLevel);

    ProgressionDutyProviderObservation IProgressionDutyProvider.Observe() => ObserveDuty();

    bool IProgressionDutyProvider.TryStart(uint territoryId, out string message) =>
        TryStartDuty(territoryId, out message);

    bool IProgressionDutyProvider.TryStop(out string message) => TryStopDuty(out message);

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
                codexIsRunning.HasFunction && codexStartSingleQuest.HasFunction && codexStop.HasFunction),
            QuestCandidate(
                "Questionable",
                "Questionable",
                QuestionableProviderId,
                ProgressionProviderFlavor.Stock,
                questionableIsRunning.HasFunction && questionableStartSingleQuest.HasFunction && questionableStop.HasFunction),
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
        if (!selection.IsReady || !autoDutyRun.HasAction || !autoDutySetLevelingMode.HasAction)
        {
            message = selection.Detail;
            return false;
        }

        try
        {
            if (!autoDutyIsStopped.InvokeFunc())
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
            autoDutySetLevelingMode.InvokeAction(0);
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
            "IsRunning, StartSingleQuest, and Stop");
    }

    private ProgressionProviderCandidate[] DutyCandidates()
    {
        PluginPresence presence = dependencies.FindPlugin("AutoDuty");
        bool stockContractReady = autoDutyContentHasPath.HasFunction && autoDutyIsStopped.HasFunction &&
                                  autoDutyRun.HasAction && autoDutyStop.HasAction && autoDutySetLevelingMode.HasAction;
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
                "ContentHasPath, SetLevelingMode, Run, IsStopped, and Stop")
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
                "ContentHasPath, SetLevelingMode, Run, IsStopped, and Stop")
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
