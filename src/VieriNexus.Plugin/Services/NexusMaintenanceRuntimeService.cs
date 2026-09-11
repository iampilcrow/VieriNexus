using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
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
/// Native, bounded maintenance that does not call AutoDuty. Destructive selling, desynthesis,
/// turn-ins, and storage are intentionally kept outside this engine until their review/verification
/// contracts are implemented; this engine owns the safe inventory-use and self-service actions.
/// </summary>
internal sealed unsafe class NexusMaintenanceRuntimeService
{
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

    internal NexusMaintenanceRuntimeService(
        ResourceLeaseManager leases,
        AutoDutyMigrationService profiles,
        IPlayerState playerState,
        IObjectTable objectTable,
        ICondition condition,
        IGameGui gameGui,
        IDataManager dataManager)
    {
        this.leases = leases;
        this.profiles = profiles;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.gameGui = gameGui;
        this.dataManager = dataManager;
    }

    internal NexusMaintenanceStatus Status => new(
        current is not null, current, message, completed, total);

    internal AutoDutyProfileSnapshot? CurrentProfile => profiles.ProfileFor(playerState.ContentId);

    internal bool HasWorkingProfile => CurrentProfile is not null;

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
        if (!leases.TryAcquire(owner,
                [ResourceKind.UiInteraction, ResourceKind.InventoryMutation],
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
        foreach (string name in new[] { "Repair", "Materialize", "MaterializeDialog" })
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
        _ => "running maintenance",
    };

    private enum RegistrationKind
    {
        TripleTriad,
        Minion,
        Orchestrion,
    }
}
