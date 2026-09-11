using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed record NexusMaintenanceStatus(
    bool IsActive,
    NexusMaintenanceOperation? Operation,
    string Message,
    int CompletedOperations,
    int TotalOperations);

/// <summary>
/// Nexus-owned maintenance orchestration. Item selection and verification remain in Nexus; narrow
/// stock providers are used only for mechanics they already expose (GC turn-ins and collection storage).
/// </summary>
internal sealed unsafe class NexusMaintenanceRuntimeService
{
    private delegate void SellItemDelegate(uint inventorySlot, InventoryType inventoryType, uint unknown);

    [Signature("48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B F2 8B E9")]
    private SellItemDelegate sellItem = null!;

    private static readonly InventoryType[] Bags =
    [
        InventoryType.Inventory1, InventoryType.Inventory2,
        InventoryType.Inventory3, InventoryType.Inventory4,
    ];
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(3);

    private readonly ResourceLeaseManager leases;
    private readonly AutoDutyMigrationService profiles;
    private readonly IPlayerState playerState;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IGameGui gameGui;
    private readonly IDataManager dataManager;
    private readonly ICallGateSubscriber<bool> autoRetainerBusy;
    private readonly ICallGateSubscriber<object> autoRetainerTurnIn;
    private readonly ICallGateSubscriber<object> autoRetainerAbort;
    private readonly ICallGateSubscriber<bool> glamourLogBusy;
    private readonly ICallGateSubscriber<bool> glamourLogEntrust;
    private readonly Queue<NexusMaintenanceOperation> queue = [];
    private readonly Dictionary<uint, int> skippedQuantities = [];
    private ResourceLeaseHandle? lease;
    private NexusMaintenanceOperation? current;
    private AutoDutyMaintenancePolicy? policy;
    private DateTimeOffset operationStartedAt;
    private DateTimeOffset nextActionAt;
    private int completed;
    private int total;
    private int extractionCategory;
    private bool extractionCategorySelected;
    private bool repairClicked;
    private int initialGearset = -1;
    private uint pendingItemId;
    private int pendingItemQuantity;
    private int pendingItemAttempts;
    private DateTimeOffset pendingItemStartedAt;
    private string message = "No Nexus maintenance is running.";
    private ulong characterId;
    private bool providerStarted;
    private bool sawProviderBusy;
    private Queue<NexusInventoryItemSnapshot> approvedSale = [];
    private NexusInventoryItemSnapshot? pendingSale;
    private NexusItemTransactionPreview? latestSalePreview;
    private AgentSalvage.SalvageItemCategory desynthCategory;
    private bool desynthCategoryInitialized;
    private bool stopRequested;

    internal NexusMaintenanceRuntimeService(
        ResourceLeaseManager leases,
        AutoDutyMigrationService profiles,
        IPlayerState playerState,
        IObjectTable objectTable,
        ICondition condition,
        IGameGui gameGui,
        IDataManager dataManager,
        IDalamudPluginInterface pluginInterface,
        IGameInteropProvider gameInteropProvider)
    {
        this.leases = leases;
        this.profiles = profiles;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.gameGui = gameGui;
        this.dataManager = dataManager;
        autoRetainerBusy = pluginInterface.GetIpcSubscriber<bool>("PluginState.IsBusy");
        autoRetainerTurnIn = pluginInterface.GetIpcSubscriber<object>("AutoRetainer.GC.EnqueueInitiation");
        autoRetainerAbort = pluginInterface.GetIpcSubscriber<object>("PluginState.AbortAllTasks");
        glamourLogBusy = pluginInterface.GetIpcSubscriber<bool>("GlamourLog.IsBusy");
        glamourLogEntrust = pluginInterface.GetIpcSubscriber<bool>("GlamourLog.EntrustAll");
        gameInteropProvider.InitializeFromAttributes(this);
    }

    internal NexusMaintenanceStatus Status => new(
        current is not null, current, message, completed, total);

    internal AutoDutyProfileSnapshot? CurrentProfile => profiles.ProfileFor(playerState.ContentId);

    internal bool HasWorkingProfile => CurrentProfile is not null;

    internal NexusItemTransactionPreview PreviewProtectedSelling()
    {
        AutoDutyMaintenancePolicy? selectedPolicy = CurrentProfile?.Maintenance;
        latestSalePreview = ItemTransactionPolicy.CreateSellPreview(
            ReadBagSnapshot(), selectedPolicy?.ProtectGearsetsFromSelling != false);
        return latestSalePreview;
    }

    internal bool StartApprovedSelling(string signature, out string result)
    {
        AutoDutyMaintenancePolicy? selectedPolicy = CurrentProfile?.Maintenance;
        if (selectedPolicy is null)
        {
            result = "Import operations settings on the Migration page before approving protected selling.";
            return false;
        }
        NexusItemTransactionPreview current = ItemTransactionPolicy.CreateSellPreview(
            ReadBagSnapshot(), selectedPolicy.ProtectGearsetsFromSelling);
        if (latestSalePreview is null || latestSalePreview.Items.Count == 0 ||
            !string.Equals(signature, latestSalePreview.Signature, StringComparison.Ordinal) ||
            !string.Equals(signature, current.Signature, StringComparison.Ordinal))
        {
            result = "The protected-selling preview changed. Review the exact items again before approving.";
            latestSalePreview = current;
            return false;
        }
        approvedSale = new Queue<NexusInventoryItemSnapshot>(current.Items);
        latestSalePreview = null;
        return Start([NexusMaintenanceOperation.Sell], selectedPolicy, out result);
    }

    internal bool StartConfigured(out string result)
    {
        AutoDutyProfileSnapshot? profile = CurrentProfile;
        if (profile is null)
        {
            result = "Import VieriAutoDuty operations on the Migration page first.";
            return false;
        }
        NexusMaintenanceOperation[] operations = OperationsExecutionPolicy
            .ConfiguredOperations(profile.Maintenance).ToArray();
        if (operations.Length == 0)
        {
            result = "This profile has no currently supported native maintenance actions enabled.";
            return false;
        }
        return Start(operations, profile.Maintenance, out result);
    }

    internal bool Start(NexusMaintenanceOperation operation, out string result)
    {
        AutoDutyMaintenancePolicy? selectedPolicy = CurrentProfile?.Maintenance;
        if (selectedPolicy is null)
        {
            result = "Import VieriAutoDuty operations on the Migration page first.";
            return false;
        }
        return Start([operation], selectedPolicy, out result);
    }

    internal bool StartRegistrations(out string result)
    {
        AutoDutyMaintenancePolicy? selectedPolicy = CurrentProfile?.Maintenance;
        if (selectedPolicy is null)
        {
            result = "Import VieriAutoDuty operations on the Migration page first.";
            return false;
        }
        return Start([
            NexusMaintenanceOperation.RegisterTripleTriadCards,
            NexusMaintenanceOperation.RegisterMinions,
            NexusMaintenanceOperation.RegisterOrchestrionRolls,
        ], selectedPolicy, out result);
    }

    private bool Start(
        IReadOnlyList<NexusMaintenanceOperation> operations,
        AutoDutyMaintenancePolicy selectedPolicy,
        out string result)
    {
        if (current is not null)
        {
            result = "Nexus maintenance is already running.";
            return false;
        }
        if (objectTable.LocalPlayer is null || playerState.ContentId == 0)
        {
            result = "Log into a character before starting maintenance.";
            return false;
        }
        if (condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ||
            condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95] ||
            condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
        {
            result = "Maintenance cannot start during combat, a duty, or a loading transition.";
            return false;
        }

        var owner = new LeaseOwner(GoalId.New(), TaskId.New(), AttemptId.New(), 60,
            "Nexus native inventory maintenance");
        List<ResourceKind> resources = [ResourceKind.UiInteraction, ResourceKind.InventoryMutation];
        if (operations.Contains(NexusMaintenanceOperation.GrandCompanyTurnIn))
            resources.Add(ResourceKind.Retainer);
        if (!leases.TryAcquire(owner,
                resources,
                LeaseLifetime, out lease, out ResourceLeaseSnapshot? blocking))
        {
            result = blocking is null
                ? "The maintenance resources are unavailable."
                : $"Nexus is already using the required resources for {blocking.Owner.Reason}.";
            return false;
        }

        queue.Clear();
        foreach (NexusMaintenanceOperation operation in operations.Distinct())
            queue.Enqueue(operation);
        policy = selectedPolicy;
        characterId = playerState.ContentId;
        total = queue.Count;
        completed = 0;
        skippedQuantities.Clear();
        BeginNext(DateTimeOffset.UtcNow);
        result = message;
        return true;
    }

    internal void Update(DateTimeOffset now)
    {
        if (current is null)
            return;
        if (lease is null || !lease.Heartbeat(LeaseLifetime))
        {
            Fail("Nexus stopped maintenance because its inventory lease expired.");
            return;
        }
        if (objectTable.LocalPlayer is null || playerState.ContentId == 0 || playerState.ContentId != characterId)
        {
            Fail("Nexus stopped maintenance because the active character changed.");
            return;
        }
        if (stopRequested)
        {
            ReconcileProviderStop();
            return;
        }
        if (condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ||
            condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        {
            Fail("Nexus stopped maintenance because combat or a duty began.");
            return;
        }
        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51] || now < nextActionAt)
            return;
        if (now - operationStartedAt > OperationTimeout)
        {
            Fail($"Nexus stopped because {Display(current.Value)} did not finish within three minutes.");
            return;
        }

        try
        {
            if (FFXIVClientStructs.FFXIV.Client.Game.Conditions.Instance()->Mounted)
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                Throttle(now, 1_000);
                return;
            }

            switch (current.Value)
            {
                case NexusMaintenanceOperation.Repair: UpdateRepair(now); break;
                case NexusMaintenanceOperation.ExtractMateria: UpdateExtraction(now); break;
                case NexusMaintenanceOperation.RegisterTripleTriadCards: UpdateRegistration(now, RegistrationKind.TripleTriad); break;
                case NexusMaintenanceOperation.RegisterMinions: UpdateRegistration(now, RegistrationKind.Minion); break;
                case NexusMaintenanceOperation.RegisterOrchestrionRolls: UpdateRegistration(now, RegistrationKind.Orchestrion); break;
                case NexusMaintenanceOperation.OpenCoffers: UpdateCoffers(now); break;
                case NexusMaintenanceOperation.Sell: UpdateSelling(now); break;
                case NexusMaintenanceOperation.Desynthesize: UpdateDesynthesis(now); break;
                case NexusMaintenanceOperation.GrandCompanyTurnIn: UpdateProviderOperation(now, true); break;
                case NexusMaintenanceOperation.EntrustArmoire:
                case NexusMaintenanceOperation.EntrustGlamourChest: UpdateProviderOperation(now, false); break;
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Nexus native maintenance failed during {Operation}.", current);
            Fail($"Nexus stopped {Display(current.Value)} after a verified operation failed.");
        }
    }

    internal bool Stop(out string result)
    {
        if (current is null)
        {
            result = "No Nexus maintenance is active.";
            return false;
        }
        if (providerStarted && current is NexusMaintenanceOperation.GrandCompanyTurnIn or
            NexusMaintenanceOperation.EntrustArmoire or NexusMaintenanceOperation.EntrustGlamourChest)
        {
            queue.Clear();
            stopRequested = true;
            if (current == NexusMaintenanceOperation.GrandCompanyTurnIn)
                TryInvoke(autoRetainerAbort, "stop the Grand Company turn-in provider");
            message = "Nexus requested Stop and is retaining ownership until the provider confirms it is inactive.";
            result = message;
            return true;
        }
        CloseOwnedAddons();
        Reset("Nexus maintenance stopped. No additional items will be changed.");
        result = message;
        return true;
    }

    internal void Shutdown() => Stop(out _);

    private void BeginNext(DateTimeOffset now)
    {
        ResetOperationState();
        if (queue.Count == 0)
        {
            Reset($"Nexus completed {completed} maintenance operation{(completed == 1 ? string.Empty : "s")}.");
            return;
        }
        current = queue.Dequeue();
        operationStartedAt = now;
        nextActionAt = now;
        if (current == NexusMaintenanceOperation.OpenCoffers)
        {
            RaptureGearsetModule* gearsets = RaptureGearsetModule.Instance();
            initialGearset = gearsets is not null ? gearsets->CurrentGearsetIndex : -1;
        }
        message = $"Nexus is {Display(current.Value)} ({completed + 1}/{total}).";
    }

    private void CompleteCurrent(DateTimeOffset now)
    {
        completed++;
        BeginNext(now);
    }

    private void UpdateRepair(DateTimeOffset now)
    {
        if (LowestEquippedDurability() >= 100f)
        {
            CloseAddon("Repair");
            CompleteCurrent(now);
            return;
        }
        if (TryAddon<AddonSelectYesno>("SelectYesno", out AddonSelectYesno* confirm) && repairClicked)
        {
            ClickButton(confirm->YesButton, (AtkUnitBase*)confirm);
            Throttle(now, 750);
            return;
        }
        if (TryAddon<AddonRepair>("Repair", out AddonRepair* repair))
        {
            ClickButton(repair->RepairAllButton, (AtkUnitBase*)repair);
            repairClicked = true;
            Throttle(now, 750);
            return;
        }
        if (now - operationStartedAt > TimeSpan.FromSeconds(12))
        {
            Fail("Self-repair did not open. The current character may not have the required crafter repair capability or dark matter.");
            return;
        }
        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);
        Throttle(now, 1_000);
    }

    private void UpdateExtraction(DateTimeOffset now)
    {
        if (EmptyBagSlots() < 1)
        {
            Fail("Nexus stopped materia extraction because the inventory has no free slot.");
            return;
        }
        if (TryAddon<AddonMaterializeDialog>("MaterializeDialog", out AddonMaterializeDialog* dialog))
        {
            ClickButton(dialog->YesButton, (AtkUnitBase*)dialog);
            Throttle(now, 600);
            return;
        }
        if (!TryAddon("Materialize", out AtkUnitBase* materialize))
        {
            if (now - operationStartedAt > TimeSpan.FromSeconds(12))
            {
                Fail("Materia extraction did not open. Confirm that Forging the Spirit is complete and extraction is available.");
                return;
            }
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 14);
            Throttle(now, 1_000);
            return;
        }

        int stoppingCategory = policy!.ExtractAllCategories ? 6 : 0;
        if (extractionCategory > stoppingCategory)
        {
            materialize->Close(true);
            CompleteCurrent(now);
            return;
        }
        AtkResNode* listNode = materialize->GetNodeById(12);
        AtkComponentList* list = listNode is null ? null : listNode->GetAsAtkComponentList();
        if (list is null || list->ListLength <= 0 || list->UldManager.NodeListCount <= 2)
        {
            extractionCategory++;
            extractionCategorySelected = false;
            Throttle(now, 300);
            return;
        }
        if (!extractionCategorySelected)
        {
            Fire(materialize, false, 1, extractionCategory);
            extractionCategorySelected = true;
            Throttle(now, 400);
            return;
        }
        AtkResNode* entryNode = list->UldManager.NodeList[2];
        AtkComponentBase* entry = entryNode is null ? null : entryNode->GetComponent();
        AtkTextNode* spiritbond = entry is null ? null : entry->GetTextNodeById(5);
        if (spiritbond is not null && spiritbond->NodeText.ToString().Replace(" ", string.Empty) == "100%")
        {
            Fire(materialize, true, 2, 0);
            Throttle(now, 500);
            return;
        }
        extractionCategory++;
        extractionCategorySelected = false;
        Throttle(now, 300);
    }

    private void UpdateRegistration(DateTimeOffset now, RegistrationKind kind)
    {
        if (pendingItemId != 0)
        {
            int quantity = QuantityInBags(pendingItemId);
            if (quantity < pendingItemQuantity || IsUnlocked(pendingItemId, kind))
            {
                pendingItemId = 0;
                pendingItemAttempts = 0;
            }
            else if (now - pendingItemStartedAt > TimeSpan.FromSeconds(3))
            {
                if (pendingItemAttempts >= 2)
                    skippedQuantities[pendingItemId] = quantity;
                else
                {
                    ActionManager.Instance()->UseAction(ActionType.Item, pendingItemId, extraParam: 65535);
                    pendingItemAttempts++;
                    pendingItemStartedAt = now;
                    Throttle(now, 1_000);
                    return;
                }
                pendingItemId = 0;
            }
            else
                return;
        }

        InventoryItem? next = FindBagItem(item => IsRegistrationItem(item, kind) &&
            !IsUnlocked(item.GetItemId(), kind) && skippedQuantities.GetValueOrDefault(item.GetItemId(), -1) != item.Quantity);
        if (next is null)
        {
            CompleteCurrent(now);
            return;
        }
        pendingItemId = next.Value.GetItemId();
        pendingItemQuantity = next.Value.Quantity;
        pendingItemAttempts = 1;
        pendingItemStartedAt = now;
        ActionManager.Instance()->UseAction(ActionType.Item, pendingItemId, extraParam: 65535);
        Throttle(now, 1_000);
    }

    private void UpdateCoffers(DateTimeOffset now)
    {
        RaptureGearsetModule* gearsets = RaptureGearsetModule.Instance();
        if (policy!.CofferGearset is { } desired && gearsets is not null &&
            gearsets->CurrentGearsetIndex != desired && gearsets->IsValidGearset(desired))
        {
            gearsets->EquipGearset(desired);
            Throttle(now, 1_000);
            return;
        }
        if (EmptyBagSlots() < 1)
        {
            RestoreGearset();
            Fail("Nexus stopped opening coffers because the inventory has no free slot.");
            return;
        }
        if (pendingItemId != 0)
        {
            int observedQuantity = QuantityInBags(pendingItemId);
            if (observedQuantity < pendingItemQuantity)
            {
                pendingItemId = 0;
                pendingItemAttempts = 0;
            }
            else if (now - pendingItemStartedAt > TimeSpan.FromSeconds(4))
            {
                if (pendingItemAttempts >= 2)
                {
                    skippedQuantities[pendingItemId] = observedQuantity;
                    pendingItemId = 0;
                }
                else
                {
                    ActionManager.Instance()->UseAction(ActionType.Item, pendingItemId, extraParam: 65535);
                    pendingItemAttempts++;
                    pendingItemStartedAt = now;
                    Throttle(now, 1_000);
                    return;
                }
            }
            else
                return;
        }
        InventoryItem? next = FindBagItem(IsEligibleCoffer);
        if (next is null)
        {
            if (!RestoreGearset())
                CompleteCurrent(now);
            else
                Throttle(now, 1_000);
            return;
        }
        uint itemId = next.Value.GetItemId();
        int quantity = next.Value.Quantity;
        pendingItemId = itemId;
        pendingItemQuantity = quantity;
        pendingItemAttempts = 1;
        pendingItemStartedAt = now;
        ActionManager.Instance()->UseAction(ActionType.Item, itemId, extraParam: 65535);
        Throttle(now, 1_500);
    }

    private void UpdateSelling(DateTimeOffset now)
    {
        if (!TryAddon("Shop", out _))
        {
            Fail("Open a normal NPC shop, then approve the protected-selling preview again. Nexus will never choose an unknown vendor or sell outside its exact approval.");
            return;
        }
        if (pendingSale is { } pending)
        {
            InventoryItem* slot = InventoryManager.Instance()->GetInventorySlot(
                (InventoryType)pending.Container, pending.Slot);
            if (slot is null || slot->GetItemId() != pending.ItemId || slot->Quantity < pending.Quantity)
            {
                pendingSale = null;
                Throttle(now, 350);
                return;
            }
            if (now - pendingItemStartedAt > TimeSpan.FromSeconds(3))
            {
                Fail($"Nexus could not verify that {pending.Name} was sold, so no other item was touched.");
                return;
            }
            return;
        }
        if (approvedSale.Count == 0)
        {
            CompleteCurrent(now);
            return;
        }
        NexusInventoryItemSnapshot next = approvedSale.Dequeue();
        InventoryItem* currentItem = InventoryManager.Instance()->GetInventorySlot(
            (InventoryType)next.Container, next.Slot);
        if (currentItem is null || currentItem->GetItemId() != next.ItemId || currentItem->Quantity != next.Quantity)
        {
            Fail("A protected-selling item changed after approval. Nexus stopped before selecting another item.");
            return;
        }
        sellItem((uint)next.Slot, (InventoryType)next.Container, 0);
        pendingSale = next;
        pendingItemStartedAt = now;
        Throttle(now, 500);
    }

    private void UpdateDesynthesis(DateTimeOffset now)
    {
        if (EmptyBagSlots() < 1)
        {
            Fail("Nexus stopped desynthesis because the inventory has no free slot for results.");
            return;
        }
        if (TryAddon("SalvageResult", out AtkUnitBase* result))
        {
            result->Close(true);
            Throttle(now, 300);
            return;
        }
        if (TryAddon("SalvageDialog", out AtkUnitBase* dialog))
        {
            FireBoolean(dialog, 15, policy!.DesynthNormalQualityOnly);
            FireBoolean(dialog, 0, false);
            Throttle(now, 500);
            return;
        }
        if (!TryAddon<AddonSalvageItemSelector>("SalvageItemSelector", out AddonSalvageItemSelector* selector))
        {
            AgentSalvage.Instance()->AgentInterface.Show();
            Throttle(now, 1_000);
            return;
        }
        AgentSalvage* agent = AgentSalvage.Instance();
        agent->ItemListRefresh(true);
        if (!desynthCategoryInitialized && !SelectNextDesynthCategory(reset: true))
        {
            selector->AtkUnitBase.Close(true);
            CompleteCurrent(now);
            return;
        }
        if (agent->SelectedCategory != desynthCategory)
        {
            agent->SelectedCategory = desynthCategory;
            Throttle(now, 350);
            return;
        }
        HashSet<uint> gearsetItems = policy!.ProtectGearsetsFromDesynth ? GearsetItemIds() : [];
        for (int index = 0; index < agent->ItemCount; index++)
        {
            AgentSalvage.SalvageListItem entry = agent->ItemList[index];
            InventoryItem* item = InventoryManager.Instance()->GetInventorySlot(entry.InventoryType, (int)entry.InventorySlot);
            if (item is null || IsExperienceBonusEquipment(item->GetBaseItemId()) ||
                gearsetItems.Contains(item->GetItemId()))
                continue;
            Item? row = dataManager.GetExcelSheet<Item>().GetRowOrDefault(item->GetBaseItemId());
            if (row is null)
                continue;
            if (policy.DesynthForSkill)
            {
                float skill = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance()
                    ->GetDesynthesisLevel(entry.ClassJob);
                uint itemLevel = row.Value.LevelItem.RowId;
                uint maximumItemLevel = dataManager.GetExcelSheet<Item>()
                    .Where(value => value.Desynth > 0).Max(value => value.LevelItem.RowId);
                if (skill >= itemLevel + policy.DesynthSkillGapLimit || skill >= maximumItemLevel)
                    continue;
            }
            Fire((AtkUnitBase*)selector, true, 12, index);
            Throttle(now, 600);
            return;
        }
        if (!SelectNextDesynthCategory(reset: false))
        {
            selector->AtkUnitBase.Close(true);
            CompleteCurrent(now);
        }
        else
            Throttle(now, 350);
    }

    private void UpdateProviderOperation(DateTimeOffset now, bool grandCompany)
    {
        bool busy;
        try { busy = grandCompany ? autoRetainerBusy.InvokeFunc() : glamourLogBusy.InvokeFunc(); }
        catch
        {
            Fail(grandCompany
                ? "AutoRetainer is not ready for the Grand Company turn-in contract."
                : "Glamour Log is not ready for the storage contract.");
            return;
        }
        sawProviderBusy |= busy;
        if (!providerStarted)
        {
            try
            {
                if (grandCompany)
                    autoRetainerTurnIn.InvokeAction();
                else if (!glamourLogEntrust.InvokeFunc())
                    throw new InvalidOperationException("Storage provider rejected the request.");
                providerStarted = true;
                Throttle(now, 500);
            }
            catch
            {
                Fail(grandCompany
                    ? "AutoRetainer rejected the bounded Grand Company turn-in request."
                    : "Glamour Log rejected the bounded storage request.");
            }
            return;
        }
        if (!busy && (sawProviderBusy || !grandCompany && now - operationStartedAt > TimeSpan.FromSeconds(2)))
            CompleteCurrent(now);
        else if (!busy && now - operationStartedAt > TimeSpan.FromSeconds(15))
            Fail("The provider never confirmed that the requested operation started.");
    }

    private void ReconcileProviderStop()
    {
        bool busy;
        try
        {
            busy = current == NexusMaintenanceOperation.GrandCompanyTurnIn
                ? autoRetainerBusy.InvokeFunc()
                : glamourLogBusy.InvokeFunc();
        }
        catch
        {
            message = "Nexus is retaining maintenance ownership because provider inactivity cannot be confirmed.";
            return;
        }
        if (busy)
        {
            message = "Nexus is waiting for the provider to confirm Stop before releasing maintenance ownership.";
            return;
        }
        CloseOwnedAddons();
        Reset("Nexus confirmed that maintenance stopped. No additional items will be changed.");
    }

    private bool IsEligibleCoffer(InventoryItem item)
    {
        uint itemId = item.GetItemId();
        Item? row = dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        return row is { } value && value.ItemUICategory.RowId == 61 &&
               value.ItemAction.RowId is 1085 or 388 or 367 &&
               (!policy!.UseCofferBlacklist || !policy.CofferBlacklist.ContainsKey(itemId)) &&
               skippedQuantities.GetValueOrDefault(itemId, -1) != item.Quantity;
    }

    private bool IsRegistrationItem(InventoryItem item, RegistrationKind kind)
    {
        Item? row = dataManager.GetExcelSheet<Item>().GetRowOrDefault(item.GetItemId());
        return row is { } value && kind switch
        {
            RegistrationKind.TripleTriad => value.ItemUICategory.RowId == 86,
            RegistrationKind.Minion => value.ItemAction.Value.Action.RowId == 853,
            RegistrationKind.Orchestrion => value.ItemAction.Value.Action.RowId == 25183,
            _ => false,
        };
    }

    private bool IsUnlocked(uint itemId, RegistrationKind kind)
    {
        Item? row = dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        if (row is not { } value)
            return true;
        return kind switch
        {
            RegistrationKind.TripleTriad => UIState.Instance()->IsTripleTriadCardUnlocked((ushort)value.AdditionalData.RowId),
            RegistrationKind.Minion => UIState.Instance()->IsCompanionUnlocked(value.ItemAction.Value.Data[0]),
            RegistrationKind.Orchestrion => FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance()
                ->IsOrchestrionRollUnlocked(value.AdditionalData.RowId),
            _ => true,
        };
    }

    private static float LowestEquippedDurability()
    {
        InventoryContainer* equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
            return 100f;
        float lowest = 100f;
        bool found = false;
        for (int index = 0; index < Math.Min(13, equipped->Size); index++)
        {
            InventoryItem item = equipped->Items[index];
            if (item.ItemId == 0)
                continue;
            found = true;
            lowest = Math.Min(lowest, item.Condition / 300f);
        }
        return found ? Math.Clamp(lowest, 0, 100) : 100;
    }

    private static int EmptyBagSlots()
    {
        int count = 0;
        foreach (InventoryType type in Bags)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(type);
            if (container is null || !container->IsLoaded)
                continue;
            for (int index = 0; index < container->Size; index++)
                if (container->Items[index].ItemId == 0)
                    count++;
        }
        return count;
    }

    private static InventoryItem? FindBagItem(Func<InventoryItem, bool> predicate)
    {
        foreach (InventoryType type in Bags)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(type);
            if (container is null || !container->IsLoaded)
                continue;
            for (int index = 0; index < container->Size; index++)
            {
                InventoryItem item = container->Items[index];
                if (item.ItemId != 0 && predicate(item))
                    return item;
            }
        }
        return null;
    }

    private static int QuantityInBags(uint itemId)
    {
        int quantity = 0;
        foreach (InventoryType type in Bags)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(type);
            if (container is null || !container->IsLoaded)
                continue;
            for (int index = 0; index < container->Size; index++)
                if (container->Items[index].GetItemId() == itemId)
                    quantity += container->Items[index].Quantity;
        }
        return quantity;
    }

    private IReadOnlyList<NexusInventoryItemSnapshot> ReadBagSnapshot()
    {
        HashSet<uint> gearsets = GearsetItemIds();
        List<NexusInventoryItemSnapshot> result = [];
        foreach (InventoryType type in Bags)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(type);
            if (container is null || !container->IsLoaded)
                continue;
            for (int index = 0; index < container->Size; index++)
            {
                InventoryItem item = container->Items[index];
                if (item.ItemId == 0)
                    continue;
                uint baseId = item.GetBaseItemId();
                Item? row = dataManager.GetExcelSheet<Item>().GetRowOrDefault(baseId);
                if (row is null)
                    continue;
                result.Add(new NexusInventoryItemSnapshot(
                    (int)type, index, item.GetItemId(), item.Quantity, row.Value.Name.ExtractText(),
                    row.Value.EquipSlotCategory.RowId > 0, row.Value.PriceLow, row.Value.IsUntradable,
                    item.SpiritbondOrCollectability,
                    item.Flags.HasFlag(InventoryItem.ItemFlags.Collectable),
                    IsExperienceBonusEquipment(baseId), gearsets.Contains(item.GetItemId())));
            }
        }
        return result;
    }

    private static HashSet<uint> GearsetItemIds()
    {
        HashSet<uint> result = [];
        RaptureGearsetModule* gearsets = RaptureGearsetModule.Instance();
        if (gearsets is null)
            return result;
        for (int index = 0; index < gearsets->Entries.Length; index++)
        {
            if (!gearsets->IsValidGearset(index))
                continue;
            foreach (RaptureGearsetModule.GearsetItem item in gearsets->Entries[index].Items)
                if (item.ItemId > 0)
                    result.Add(item.ItemId);
        }
        return result;
    }

    private bool IsExperienceBonusEquipment(uint itemId)
    {
        Item? item = dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        return item is { EquipSlotCategory.RowId: > 0 } equipment &&
               (equipment.ItemSpecialBonus.RowId == 6 && equipment.ItemSpecialBonusParam > 0 ||
                equipment.Description.ExtractText().Contains("EXP earned", StringComparison.OrdinalIgnoreCase) ||
                equipment.Description.ExtractText().Contains("EXP Bonus", StringComparison.OrdinalIgnoreCase));
    }

    private bool SelectNextDesynthCategory(bool reset)
    {
        AgentSalvage.SalvageItemCategory[] categories = Enum.GetValues<AgentSalvage.SalvageItemCategory>();
        int start = reset ? 0 : (int)desynthCategory + 1;
        for (int index = start; index < categories.Length; index++)
        {
            if ((policy!.DesynthCategories & 1UL << index) == 0)
                continue;
            desynthCategory = categories[index];
            desynthCategoryInitialized = true;
            return true;
        }
        return false;
    }

    private static bool TryInvoke(ICallGateSubscriber<object> subscriber, string purpose)
    {
        try
        {
            subscriber.InvokeAction();
            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Nexus could not {Purpose}.", purpose);
            return false;
        }
    }

    private bool RestoreGearset()
    {
        RaptureGearsetModule* gearsets = RaptureGearsetModule.Instance();
        if (initialGearset < 0 || gearsets is null || gearsets->CurrentGearsetIndex == initialGearset ||
            !gearsets->IsValidGearset(initialGearset))
            return false;
        gearsets->EquipGearset(initialGearset);
        initialGearset = -1;
        return true;
    }

    private void Fail(string result)
    {
        RestoreGearset();
        CloseOwnedAddons();
        Reset(result);
    }

    private void Reset(string result)
    {
        queue.Clear();
        current = null;
        policy = null;
        lease?.Dispose();
        lease = null;
        characterId = 0;
        approvedSale.Clear();
        message = result;
        ResetOperationState();
    }

    private void ResetOperationState()
    {
        extractionCategory = 0;
        extractionCategorySelected = false;
        repairClicked = false;
        pendingItemId = 0;
        pendingItemQuantity = 0;
        pendingItemAttempts = 0;
        pendingItemStartedAt = default;
        initialGearset = -1;
        providerStarted = false;
        sawProviderBusy = false;
        pendingSale = null;
        desynthCategory = default;
        desynthCategoryInitialized = false;
        stopRequested = false;
    }

    private void Throttle(DateTimeOffset now, int milliseconds) => nextActionAt = now.AddMilliseconds(milliseconds);

    private bool TryAddon(string name, out AtkUnitBase* addon)
    {
        nint address = gameGui.GetAddonByName(name, 1);
        addon = (AtkUnitBase*)address;
        return addon is not null && addon->IsReady && addon->IsVisible;
    }

    private bool TryAddon<T>(string name, out T* addon) where T : unmanaged
    {
        nint address = gameGui.GetAddonByName(name, 1);
        addon = (T*)address;
        AtkUnitBase* unit = (AtkUnitBase*)addon;
        return addon is not null && unit->IsReady && unit->IsVisible;
    }

    private void CloseAddon(string name)
    {
        if (TryAddon(name, out AtkUnitBase* addon))
            addon->Close(true);
    }

    private void CloseOwnedAddons()
    {
        if (repairClicked)
            CloseAddon("SelectYesno");
        foreach (string name in new[] { "Repair", "Materialize", "MaterializeDialog", "SalvageResult", "SalvageDialog", "SalvageItemSelector" })
            CloseAddon(name);
    }

    private static void Fire(AtkUnitBase* addon, bool updateState, params int[] arguments)
    {
        AtkValue* values = stackalloc AtkValue[arguments.Length];
        for (int index = 0; index < arguments.Length; index++)
        {
            values[index].Type = AtkValueType.Int;
            values[index].Int = arguments[index];
        }
        addon->FireCallback((uint)arguments.Length, values, updateState);
    }

    private static void FireBoolean(AtkUnitBase* addon, int callback, bool value)
    {
        AtkValue* values = stackalloc AtkValue[2];
        values[0].Type = AtkValueType.Int;
        values[0].Int = callback;
        values[1].Type = AtkValueType.Bool;
        values[1].Byte = value ? (byte)1 : (byte)0;
        addon->FireCallback(2, values, true);
    }

    private static void ClickButton(AtkComponentButton* button, AtkUnitBase* addon)
    {
        if (button is null || !button->IsEnabled)
            return;
        AtkComponentNode* node = button->AtkComponentBase.OwnerNode;
        if (node is null)
            return;
        AtkEvent* evt = (AtkEvent*)node->AtkEventManager.Event;
        if (evt is not null)
            addon->ReceiveEvent(evt->State.EventType, checked((int)evt->Param), evt);
    }

    private static string Display(NexusMaintenanceOperation operation) => operation switch
    {
        NexusMaintenanceOperation.Repair => "repairing equipped gear",
        NexusMaintenanceOperation.ExtractMateria => "extracting materia",
        NexusMaintenanceOperation.RegisterTripleTriadCards => "registering Triple Triad cards",
        NexusMaintenanceOperation.RegisterMinions => "registering minions",
        NexusMaintenanceOperation.RegisterOrchestrionRolls => "registering orchestrion rolls",
        NexusMaintenanceOperation.OpenCoffers => "opening eligible coffers",
        NexusMaintenanceOperation.Sell => "selling the exact approved items",
        NexusMaintenanceOperation.Desynthesize => "desynthesizing eligible protected items",
        NexusMaintenanceOperation.GrandCompanyTurnIn => "running Grand Company turn-ins",
        NexusMaintenanceOperation.EntrustArmoire => "entrusting eligible Armoire items",
        NexusMaintenanceOperation.EntrustGlamourChest => "entrusting eligible Glamour Dresser items",
        _ => "running maintenance",
    };

    private enum RegistrationKind
    {
        TripleTriad,
        Minion,
        Orchestrion,
    }
}
