using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System.Text.Json;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

/// <summary>
/// Capability-checked adapters for the temporary Vieri providers and their intended stock replacements.
/// Merely having the expected plugin name is not enough: every required IPC member must be present.
/// Questing remains observation-only; the duty edge exposes one exact bounded run and Stop.
/// </summary>
internal sealed class ProgressionProviderService : IProgressionDutyProvider, IProgressionGearProvider,
    IManualGearShoppingProvider
{
    private static readonly JsonSerializerOptions GearJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
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
    private readonly ICallGateSubscriber<string, object, object> autoDutySetConfig;
    private readonly ICallGateSubscriber<int, string> vieriAutoDutyProgression;
    private readonly ICallGateSubscriber<bool> vieriAutoDutyGearBusy;
    private readonly ICallGateSubscriber<string> vieriAutoDutyStartGear;
    private readonly ICallGateSubscriber<string> vieriAutoDutyGetGearPreview;
    private readonly ICallGateSubscriber<string, string> vieriAutoDutyStartApprovedGear;
    private readonly ICallGateSubscriber<string, string, string> vieriAutoDutyCommand;
    private readonly ICallGateSubscriber<object, bool> autoDutyPushConfigOverrides;
    private readonly ICallGateSubscriber<bool> autoDutyPopConfigOverrides;
    private readonly ICallGateSubscriber<string> vieriAutoDutyStatus;
    private bool gearOverridesActive;
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
        autoDutySetConfig = pluginInterface.GetIpcSubscriber<string, object, object>("AutoDuty.SetConfig");
        vieriAutoDutyProgression = pluginInterface.GetIpcSubscriber<int, string>("AutoDuty.StartProgressionLeveling");
        vieriAutoDutyGearBusy = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsGearReadinessBusy");
        vieriAutoDutyStartGear = pluginInterface.GetIpcSubscriber<string>("AutoDuty.StartGearReadiness");
        vieriAutoDutyGetGearPreview = pluginInterface.GetIpcSubscriber<string>("AutoDuty.GetGearUpgradePreview");
        vieriAutoDutyStartApprovedGear = pluginInterface.GetIpcSubscriber<string, string>("AutoDuty.StartApprovedGearShopping");
        vieriAutoDutyCommand = pluginInterface.GetIpcSubscriber<string, string, string>("AutoDuty.ExecuteVieriCommand");
        autoDutyPushConfigOverrides = pluginInterface.GetIpcSubscriber<object, bool>("AutoDuty.PushConfigOverrides");
        autoDutyPopConfigOverrides = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.PopConfigOverrides");
        vieriAutoDutyStatus = pluginInterface.GetIpcSubscriber<string>("AutoDuty.GetVieriStatus");
    }

    ProviderId IProgressionDutyProvider.Id =>
        Snapshot().Duties.Selected?.Id ?? AutoDutyCompatibilityProviderId;

    IReadOnlyList<ProgressionDutyCandidate> IProgressionDutyProvider.EligibleDuties(int currentLevel) =>
        EligibleDuties(currentLevel);

    ProgressionDutyProviderObservation IProgressionDutyProvider.Observe() => ObserveDuty();

    bool IProgressionDutyProvider.TryStart(uint territoryId, out string message) =>
        TryStartDuty(territoryId, out message);

    bool IProgressionDutyProvider.TryStop(out string message) => TryStopDuty(out message);

    ProviderId IProgressionGearProvider.Id => AutoDutyCompatibilityProviderId;

    ProgressionGearProviderObservation IProgressionGearProvider.ObserveGearReadiness() =>
        ObserveGearReadiness();

    bool IProgressionGearProvider.TryStartGearReadiness(int minimumGilReserve, out string message) =>
        TryStartGearReadiness(minimumGilReserve, out message);

    bool IProgressionGearProvider.TryStopGearReadiness(out string message) =>
        TryStopGearReadiness(out message);

    ProviderId IManualGearShoppingProvider.Id => AutoDutyCompatibilityProviderId;

    ProgressionGearProviderObservation IManualGearShoppingProvider.Observe() => ObserveGearReadiness();

    bool IManualGearShoppingProvider.TryStart(GearShoppingApproval approval, out string message) =>
        TryStartApprovedGearShopping(approval, out message);

    bool IManualGearShoppingProvider.TryStop(out string message) => TryStopGearReadiness(out message);

    internal bool IsGearReadinessReady => GearContractReady() && IsVieriAutoDutyActive();

    internal bool IsGearShoppingPreviewReady => IsVieriAutoDutyActive() &&
        vieriAutoDutyGetGearPreview.HasFunction && vieriAutoDutyStartApprovedGear.HasFunction &&
        vieriAutoDutyGearBusy.HasFunction && autoDutyIsStopped.HasFunction &&
        autoDutyPushConfigOverrides.HasFunction && autoDutyPopConfigOverrides.HasFunction;

    internal ProgressionCharacterMetrics CharacterMetrics() => new(CurrentItemLevel(), CurrentGil());

    internal void UpdateGearAdapter()
    {
        if (gearOverridesActive)
            _ = ObserveGearReadiness();
    }

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
        bool canResetLeveling = autoDutySetLevelingMode.HasAction || autoDutySetConfig.HasAction;
        if (!selection.IsReady || !autoDutyRun.HasAction || !canResetLeveling)
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
        if (!IsGearReadinessReady)
            return new(false, null, 0, 0, 0, 0, 0,
                "VieriAutoDuty's temporary gear-readiness contract is unavailable. Nexus will not use stock AutoDuty's overlay or scheduler as a substitute.");

        try
        {
            bool busy = vieriAutoDutyGearBusy.InvokeFunc();
            long startedSequence = 0;
            long completedSequence = 0;
            int startingItemLevel = 0;
            int endingItemLevel = 0;
            int itemsPurchased = 0;
            if (vieriAutoDutyStatus.HasFunction)
            {
                using JsonDocument document = JsonDocument.Parse(vieriAutoDutyStatus.InvokeFunc());
                JsonElement root = document.RootElement;
                startedSequence = ReadInt64(root, "gearShoppingStartedSequence");
                completedSequence = ReadInt64(root, "gearShoppingCompletedSequence");
                startingItemLevel = ReadInt32(root, "gearShoppingStartingItemLevel");
                endingItemLevel = ReadInt32(root, "gearShoppingEndingItemLevel");
                itemsPurchased = ReadInt32(root, "gearShoppingItemsPurchased");
            }

            if (!busy && gearOverridesActive)
            {
                if (!TryRestoreGearOverrides())
                    return new(false, null, startedSequence, completedSequence, startingItemLevel,
                        endingItemLevel, itemsPurchased,
                        "Gear work ended, but Nexus could not yet restore the provider's prior settings.");
                eligibleDutyCacheExpiresAt = 0;
            }

            string detail = busy
                ? "The Nexus-owned gear-readiness transaction is running through the temporary VieriAutoDuty mechanics adapter."
                : "The gear-readiness provider is inactive.";
            return new(true, busy, startedSequence, completedSequence, startingItemLevel,
                endingItemLevel, itemsPurchased, detail);
        }
        catch (Exception ex)
        {
            return new(false, null, 0, 0, 0, 0, 0, ex.Message);
        }
    }

    internal bool TryStartGearReadiness(int minimumGilReserve, out string message)
    {
        if (!IsGearReadinessReady)
        {
            message = "VieriAutoDuty does not expose the temporary gear-readiness contract required for this migration step.";
            return false;
        }

        try
        {
            if (vieriAutoDutyGearBusy.InvokeFunc())
            {
                message = "VieriAutoDuty is already running gear work that Nexus does not own.";
                return false;
            }
            if (!autoDutyIsStopped.InvokeFunc())
            {
                message = "VieriAutoDuty is already running duty work. Stop it before Nexus starts gear readiness.";
                return false;
            }

            Dictionary<string, string> overrides = new(StringComparer.OrdinalIgnoreCase)
            {
                ["AutoBuyGilVendorGear"] = "true",
                ["AutoBuyGilVendorKeepGil"] = Math.Max(0, minimumGilReserve)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            if (!autoDutyPushConfigOverrides.InvokeFunc(overrides))
            {
                message = "VieriAutoDuty rejected the temporary Nexus gear policy; no shopping was started.";
                return false;
            }
            gearOverridesActive = true;

            string result = vieriAutoDutyStartGear.InvokeFunc();
            if (!result.StartsWith("Started gear readiness", StringComparison.OrdinalIgnoreCase))
            {
                bool restored = TryRestoreGearOverrides();
                message = restored ? result : $"{result} The prior provider settings could not yet be restored.";
                return false;
            }

            message = $"Nexus started gear readiness with a protected {minimumGilReserve:N0}-gil floor. " +
                      "VieriAutoDuty is supplying temporary vendor/equip mechanics only.";
            return true;
        }
        catch (Exception ex)
        {
            bool restored = TryRestoreGearOverrides();
            message = $"Gear readiness could not start: {ex.Message}" +
                      (restored ? string.Empty : " The prior provider settings could not yet be restored.");
            return false;
        }
    }

    internal bool TryGetGearUpgradePreview(out GearUpgradePreview? preview, out string message)
    {
        preview = null;
        if (!IsGearShoppingPreviewReady)
        {
            message = "Update and enable VieriAutoDuty to use the temporary live gear-scanning adapter.";
            return false;
        }

        try
        {
            preview = JsonSerializer.Deserialize<GearUpgradePreview>(
                vieriAutoDutyGetGearPreview.InvokeFunc(), GearJsonOptions);
            if (preview is null || preview.SchemaVersion != GearUpgradePreview.CurrentSchemaVersion)
            {
                preview = null;
                message = "The gear provider returned an unsupported preview. Update both VieriNexus and VieriAutoDuty.";
                return false;
            }

            message = preview.UnavailableReason ??
                      $"Found {preview.Slots.Count(slot => slot.Replacement is not null)} verified upgrade option(s) for {preview.Job}.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"The gear preview could not be read: {ex.Message}";
            return false;
        }
    }

    internal bool TryStartApprovedGearShopping(GearShoppingApproval approval, out string message)
    {
        if (!IsGearShoppingPreviewReady)
        {
            message = "The approved-shopping adapter is unavailable.";
            return false;
        }

        try
        {
            if (vieriAutoDutyGearBusy.InvokeFunc())
            {
                message = "Gear shopping is already running.";
                return false;
            }
            if (!autoDutyIsStopped.InvokeFunc())
            {
                message = "A duty is already running. Stop it before starting gear shopping.";
                return false;
            }

            Dictionary<string, string> overrides = new(StringComparer.OrdinalIgnoreCase)
            {
                ["AutoBuyGilVendorKeepGil"] = approval.MinimumGilReserve
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            if (!autoDutyPushConfigOverrides.InvokeFunc(overrides))
            {
                message = "The temporary mechanics adapter rejected the Nexus spending floor; shopping was not started.";
                return false;
            }
            gearOverridesActive = true;

            string request = JsonSerializer.Serialize(approval, GearJsonOptions);
            string result = vieriAutoDutyStartApprovedGear.InvokeFunc(request);
            if (!result.StartsWith("Shop For Upgrades started", StringComparison.OrdinalIgnoreCase))
            {
                bool restored = TryRestoreGearOverrides();
                message = restored ? result : $"{result} The provider settings still require restoration.";
                return false;
            }

            message = "Nexus approved the exact displayed upgrades and started the temporary vendor/equip mechanics adapter.";
            return true;
        }
        catch (Exception ex)
        {
            bool restored = TryRestoreGearOverrides();
            message = $"Approved gear shopping could not start: {ex.Message}" +
                      (restored ? string.Empty : " The provider settings still require restoration.");
            return false;
        }
    }

    internal bool TryStopGearReadiness(out string message)
    {
        if (!vieriAutoDutyCommand.HasFunction)
        {
            message = "The temporary gear-readiness Stop contract is unavailable.";
            return false;
        }

        try
        {
            string result = vieriAutoDutyCommand.InvokeFunc("cancelgear", string.Empty);
            if (!result.StartsWith("Gear readiness stopped", StringComparison.OrdinalIgnoreCase))
            {
                message = result;
                return false;
            }
            if (!TryRestoreGearOverrides())
            {
                message = "Gear readiness stopped, but Nexus could not yet restore the provider's prior settings.";
                return false;
            }
            eligibleDutyCacheExpiresAt = 0;
            message = $"Nexus stopped gear readiness. {result}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"The gear-readiness Stop request failed: {ex.Message}";
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

    private static unsafe int CurrentGil()
    {
        InventoryManager* manager = InventoryManager.Instance();
        return manager is null ? 0 : checked((int)Math.Min(manager->GetGil(), int.MaxValue));
    }

    private bool GearContractReady() =>
        vieriAutoDutyGearBusy.HasFunction &&
        vieriAutoDutyStartGear.HasFunction &&
        vieriAutoDutyCommand.HasFunction &&
        autoDutyPushConfigOverrides.HasFunction &&
        autoDutyPopConfigOverrides.HasFunction;

    private bool IsVieriAutoDutyActive()
    {
        PluginPresence presence = dependencies.FindPlugin("AutoDuty");
        return presence.IsLoaded && (vieriAutoDutyProgression.HasFunction ||
            string.Equals(presence.DisplayName, "VieriAutoDuty", StringComparison.OrdinalIgnoreCase));
    }

    private bool TryRestoreGearOverrides()
    {
        if (!gearOverridesActive)
            return true;
        if (!autoDutyPopConfigOverrides.HasFunction)
            return false;
        try
        {
            autoDutyPopConfigOverrides.InvokeFunc();
            gearOverridesActive = false;
            return true;
        }
        catch
        {
            // A provider Stop also restores overrides. The coordinator still verifies the live
            // transaction result and never assumes that a failed restoration means success.
            return false;
        }
    }

    private static int ReadInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) ? result : 0;

    private static long ReadInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long result) ? result : 0;

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
