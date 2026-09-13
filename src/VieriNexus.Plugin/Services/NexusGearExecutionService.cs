using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VieriNexus.Application;

namespace VieriNexus.Services;

/// <summary>
/// Nexus-owned, bounded gil-vendor transaction. No AutoDuty code or IPC participates: Nexus
/// validates one exact approval, travels with its route provider, purchases only those item ids,
/// verifies each inventory mutation, equips them, updates the current gearset, and moves only the
/// equipment displaced by this transaction out of the Armoury Chest.
/// </summary>
internal sealed unsafe class NexusGearExecutionService
{
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan VendorTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PurchaseTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan EquipTimeout = TimeSpan.FromSeconds(4);
    private static readonly InventoryType[] Bags =
    [
        InventoryType.Inventory1, InventoryType.Inventory2,
        InventoryType.Inventory3, InventoryType.Inventory4,
    ];
    private static readonly InventoryType[] Armoury =
    [
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead,
        InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
    ];
    private static readonly InventoryType[] Sources = [.. Bags, .. Armoury];

    private readonly NexusGearCatalogService catalog;
    private readonly NavigationLibraryService routes;
    private readonly NexusRouteTravelProvider travel;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IGameGui gameGui;
    private readonly IDataManager dataManager;
    private readonly Func<int> currentItemLevel;

    private readonly List<ApprovedLine> lines = [];
    private readonly List<VendorWork> vendors = [];
    private readonly List<RetiredItem> retired = [];
    private readonly HashSet<string> processedMenus = [];
    private RunState state;
    private int vendorIndex;
    private int equipLineIndex;
    private int equipTargetIndex;
    private DateTimeOffset startedAt;
    private DateTimeOffset stateStartedAt;
    private DateTimeOffset nextActionAt;
    private ulong characterId;
    private string job = string.Empty;
    private byte vendorLevel;
    private int minimumGilReserve;
    private PendingPurchase? pendingPurchase;
    private PendingEquip? pendingEquip;
    private long startedSequence;
    private long completedSequence;
    private int startingItemLevel;
    private int endingItemLevel;
    private int itemsPurchased;
    private string detail = "Nexus gear shopping is inactive.";

    internal NexusGearExecutionService(
        NexusGearCatalogService catalog,
        NavigationLibraryService routes,
        NexusRouteTravelProvider travel,
        IClientState clientState,
        IPlayerState playerState,
        IObjectTable objectTable,
        ICondition condition,
        IGameGui gameGui,
        IDataManager dataManager,
        Func<int> currentItemLevel)
    {
        this.catalog = catalog;
        this.routes = routes;
        this.travel = travel;
        this.clientState = clientState;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.gameGui = gameGui;
        this.dataManager = dataManager;
        this.currentItemLevel = currentItemLevel;
    }

    internal bool IsReady => catalog.IsAvailable && travel.IsAvailable;
    internal bool IsBusy => state is not RunState.Idle;

    internal void EnsureSequenceAfter(long sequenceFloor)
    {
        startedSequence = Math.Max(startedSequence, sequenceFloor);
        completedSequence = Math.Max(completedSequence, sequenceFloor);
    }

    internal ProgressionGearProviderObservation Observe() => new(
        IsReady,
        IsBusy,
        startedSequence,
        completedSequence,
        startingItemLevel,
        endingItemLevel,
        itemsPurchased,
        detail);

    internal bool Start(GearShoppingApproval approval, out string message)
    {
        if (IsBusy)
        {
            message = "Nexus gear shopping is already running.";
            return false;
        }
        if (!IsReady)
        {
            message = "Nexus requires vnavmesh and a logged-in character before shopping.";
            return false;
        }
        if (condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ||
            condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        {
            message = "Gear shopping cannot start during combat or a duty.";
            return false;
        }

        GearUpgradeSnapshot snapshot = catalog.BuildSnapshot();
        GearUpgradePreview preview = GearUpgradeCandidatePolicy.BuildPreview(snapshot);
        if (!ValidateApproval(approval, preview, out message))
            return false;

        lines.Clear();
        vendors.Clear();
        retired.Clear();
        CaptureEquippedAtStart();
        processedMenus.Clear();
        foreach (GearShoppingApprovalLine approved in approval.Lines)
        {
            GearUpgradeSlot slot = preview.Slots.Single(candidate => candidate.SlotKey == approved.SlotKey);
            GearUpgradeReplacement replacement = slot.Replacement!;
            int desiredCopies = Math.Max(1, replacement.Quantity + replacement.OwnedCopies);
            int[] targets = slot.Name.Equals("Rings", StringComparison.OrdinalIgnoreCase)
                ? RingTargets(replacement.ItemId, replacement.ItemLevel, desiredCopies)
                : [approved.SlotKey];
            lines.Add(new ApprovedLine(approved, replacement.Name, replacement.ItemLevel,
                replacement.OwnedCopies, targets));
        }

        foreach (IGrouping<(uint Territory, uint DataId), ApprovedLine> group in lines
                     .Where(line => line.RemainingPurchases > 0)
                     .GroupBy(line => (line.Approval.VendorTerritoryId, line.Approval.VendorDataId)))
            vendors.Add(new VendorWork(group.Key.Territory, group.Key.DataId, group.ToArray()));

        characterId = approval.CharacterId;
        job = preview.Job;
        vendorLevel = preview.VendorLevel;
        minimumGilReserve = approval.MinimumGilReserve;
        startingItemLevel = currentItemLevel();
        endingItemLevel = startingItemLevel;
        itemsPurchased = 0;
        startedSequence++;
        startedAt = DateTimeOffset.UtcNow;
        nextActionAt = startedAt;
        vendorIndex = 0;
        equipLineIndex = 0;
        equipTargetIndex = 0;
        pendingPurchase = null;
        pendingEquip = null;

        if (vendors.Count > 0)
        {
            if (!BeginVendorTravel(startedAt, out message))
            {
                ResetRun(message);
                return false;
            }
        }
        else
        {
            SetState(RunState.Equipping, "Nexus verified the approved gear is already owned and is equipping it.", startedAt);
            message = detail;
        }
        return true;
    }

    internal void Update(DateTimeOffset now)
    {
        if (!IsBusy)
            return;
        if (now - startedAt > OverallTimeout)
        {
            Fail("Nexus stopped gear shopping because the complete transaction exceeded 15 minutes.");
            return;
        }
        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
            return;
        if (playerState.ContentId != characterId || objectTable.LocalPlayer is not { } player ||
            !string.Equals(player.ClassJob.Value.Abbreviation.ExtractText(), job, StringComparison.OrdinalIgnoreCase))
        {
            Fail("Nexus stopped gear shopping because the character or job changed.");
            return;
        }
        if (condition[ConditionFlag.InCombat])
        {
            detail = "Nexus paused gear shopping while combat is active.";
            return;
        }

        try
        {
            switch (state)
            {
                case RunState.Traveling: UpdateTravel(now); break;
                case RunState.Interacting: UpdateInteraction(now); break;
                case RunState.Shopping: UpdateShop(now); break;
                case RunState.Equipping: UpdateEquip(now); break;
                case RunState.Cleaning: UpdateCleanup(now); break;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Nexus native gear transaction failed in {State}.", state);
            Fail("Nexus stopped gear shopping after a verified operation failed. No unapproved item was purchased.");
        }
    }

    internal bool Stop(out string message)
    {
        if (!IsBusy)
        {
            message = "No Nexus gear shopping is active.";
            return false;
        }
        travel.Stop();
        CloseOwnedAddons();
        ResetRun("Nexus stopped gear shopping. Any confirmed purchases remain safe and no further actions will run.");
        message = detail;
        return true;
    }

    private bool ValidateApproval(GearShoppingApproval approval, GearUpgradePreview preview, out string message)
    {
        if (approval.SchemaVersion != GearShoppingApproval.CurrentSchemaVersion ||
            preview.SchemaVersion != GearUpgradePreview.CurrentSchemaVersion ||
            approval.CharacterId == 0 || approval.CharacterId != preview.CharacterId ||
            approval.EquipmentSignature == 0 || approval.EquipmentSignature != preview.EquipmentSignature)
        {
            message = "The character or equipment changed after preview. Refresh upgrades before shopping.";
            return false;
        }
        if (approval.Lines.Count == 0 || approval.Lines.GroupBy(line => line.SlotKey).Any(group => group.Count() != 1))
        {
            message = "The shopping approval is empty or contains duplicate equipment slots.";
            return false;
        }

        ulong cost = 0;
        foreach (GearShoppingApprovalLine line in approval.Lines)
        {
            GearUpgradeSlot? slot = preview.Slots.SingleOrDefault(candidate => candidate.SlotKey == line.SlotKey);
            GearUpgradeReplacement? live = slot?.Replacement;
            if (slot is null || slot.ActiveExperienceBonus || live is null || live.ItemId != line.ItemId ||
                live.UnitPrice > line.MaximumUnitPrice || live.Quantity != line.Quantity ||
                live.VendorDataId == 0 || live.VendorTerritoryId == 0 ||
                live.VendorDataId != line.VendorDataId || live.VendorTerritoryId != line.VendorTerritoryId)
            {
                message = "A selected item, vendor, quantity, or price changed after preview. Refresh upgrades before shopping.";
                return false;
            }
            cost += (ulong)live.UnitPrice * (ulong)live.Quantity;
        }
        uint gil = InventoryManager.Instance()->GetGil();
        if (cost + (ulong)Math.Max(0, approval.MinimumGilReserve) > gil)
        {
            message = "The approved purchases would cross the protected gil reserve. Shopping was not started.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private bool BeginVendorTravel(DateTimeOffset now, out string message)
    {
        VendorWork vendor = vendors[vendorIndex];
        NavigationRouteSnapshot route = ResolveRoute(vendor);
        SuiteRouteDispatchResult dispatch = travel.Dispatch(
            NavigationSuiteRouteRequest.Create(route, NavigationRoutePlanKind.Playback));
        if (!dispatch.Started)
        {
            message = dispatch.Message;
            return false;
        }
        processedMenus.Clear();
        SetState(RunState.Traveling,
            $"Nexus is traveling to {route.Name} for {vendor.Lines.Sum(line => line.RemainingPurchases)} approved purchase(s).", now);
        message = detail;
        return true;
    }

    private NavigationRouteSnapshot ResolveRoute(VendorWork vendor)
    {
        NavigationRouteSnapshot? route = routes.Current?.Routes.SingleOrDefault(candidate =>
            candidate.OverrideEnabled && candidate.BindingKind == 1 &&
            candidate.TerritoryId == vendor.TerritoryId && candidate.TargetDataId == vendor.DataId);
        route ??= NavigationBuiltInRouteCatalog.Find(vendor.TerritoryId, vendor.DataId);
        if (route is null)
            throw new InvalidOperationException($"No verified Nexus route exists for vendor {vendor.DataId}.");
        return route;
    }

    private void UpdateTravel(DateTimeOffset now)
    {
        SuiteRouteProviderObservation observation = travel.Observe(now);
        if (observation.State == SuiteRouteProviderState.Failed)
        {
            Fail(observation.Message);
            return;
        }
        if (observation.State == SuiteRouteProviderState.Completed)
            SetState(RunState.Interacting, "Nexus reached the approved vendor and is opening the shop.", now);
        else
            detail = observation.Message;
    }

    private void UpdateInteraction(DateTimeOffset now)
    {
        if (now - stateStartedAt > VendorTimeout)
        {
            Fail("Nexus could not open the approved vendor shop within three minutes.");
            return;
        }
        if (TryAddon("Shop", out AtkUnitBase* shop))
        {
            SetState(RunState.Shopping, "Nexus opened the vendor and is matching the exact approved item ids.", now);
            UpdateShop(now);
            return;
        }
        if (now < nextActionAt)
            return;
        if (condition[ConditionFlag.Mounted])
        {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
            detail = "Nexus reached the vendor and is dismounting before interaction.";
            Throttle(now);
            return;
        }
        if (TryAddon<AddonTalk>("Talk", out AddonTalk* talk))
        {
            ClickTalk(talk);
            Throttle(now);
            return;
        }
        if (TryAddon<AddonSelectIconString>("SelectIconString", out AddonSelectIconString* icon))
        {
            SelectRelevantMenu((AtkUnitBase*)icon, Enumerable.Range(0, icon->PopupMenu.PopupMenu.EntryCount)
                .Select(index => icon->PopupMenu.PopupMenu.EntryNames[index].ExtractText()).ToArray(), now);
            return;
        }
        if (TryAddon<AddonSelectString>("SelectString", out AddonSelectString* list))
        {
            SelectRelevantMenu((AtkUnitBase*)list, Enumerable.Range(0, list->PopupMenu.PopupMenu.EntryCount)
                .Select(index => list->PopupMenu.PopupMenu.EntryNames[index].ExtractText()).ToArray(), now);
            return;
        }

        VendorWork vendor = vendors[vendorIndex];
        IGameObject? target = objectTable.Where(candidate => candidate.BaseId == vendor.DataId)
            .OrderBy(candidate => Vector3.Distance(playerPosition(), candidate.Position))
            .FirstOrDefault();
        if (target is not { IsTargetable: true } || Vector3.Distance(playerPosition(), target.Position) > 7f)
        {
            detail = "Nexus reached the standing point and is waiting for the approved vendor to become interactable.";
            if (now - stateStartedAt > TimeSpan.FromSeconds(15))
                Fail("Nexus reached the standing point but could not find the approved vendor in interaction range.");
            return;
        }
        TargetSystem.Instance()->InteractWithObject((GameObject*)target.Address, false);
        Throttle(now);
    }

    private void SelectRelevantMenu(AtkUnitBase* addon, string[] entries, DateTimeOffset now)
    {
        int selected = Enumerable.Range(0, entries.Length).FirstOrDefault(index =>
            RelevantMenu(entries[index]) && processedMenus.Add(string.Join('\n', entries) + $"\n#{index}"), -1);
        if (selected < 0)
        {
            Fail("The approved vendor did not expose a matching combat-gear shop menu.");
            return;
        }
        Fire(addon, selected);
        Throttle(now);
    }

    private bool RelevantMenu(string text)
    {
        if (!text.Contains("Purchase", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("fieldcraft", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("tradecraft", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Hand/Land", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("Purchase Items", StringComparison.OrdinalIgnoreCase))
            return false;
        bool magic = job is "CNJ" or "WHM" or "SCH" or "AST" or "SGE" or
            "THM" or "BLM" or "ACN" or "SMN" or "RDM" or "PCT" or "BLU";
        if (text.Contains("Disciple of War", StringComparison.OrdinalIgnoreCase) && magic ||
            text.Contains("Disciple of Magic", StringComparison.OrdinalIgnoreCase) && !magic)
            return false;
        Match level = Regex.Match(text, @"Lv\.\s*(\d+)(?:\s*-\s*(\d+))?", RegexOptions.IgnoreCase);
        if (!level.Success)
            return true;
        int minimum = int.Parse(level.Groups[1].Value);
        int maximum = level.Groups[2].Success ? int.Parse(level.Groups[2].Value) : minimum;
        return vendorLevel >= minimum && vendorLevel <= maximum;
    }

    private void UpdateShop(DateTimeOffset now)
    {
        if (pendingPurchase is not null)
        {
            UpdatePendingPurchase(now);
            return;
        }
        if (!TryAddon("Shop", out AtkUnitBase* shop))
        {
            SetState(RunState.Interacting, "Nexus is checking the vendor's next approved shop page.", now);
            return;
        }
        if (now < nextActionAt)
            return;

        VendorWork vendor = vendors[vendorIndex];
        ApprovedLine? line = vendor.Lines.FirstOrDefault(candidate => candidate.RemainingPurchases > 0 &&
            FindShopItem(shop, candidate.Approval.ItemId, out _, out _));
        if (line is null)
        {
            if (vendor.Lines.All(candidate => candidate.RemainingPurchases == 0))
            {
                shop->Close(true);
                AdvanceVendor(now);
                return;
            }
            shop->Close(true);
            SetState(RunState.Interacting, "Nexus is checking another combat-gear category at this vendor.", now);
            Throttle(now);
            return;
        }

        if (!FindShopItem(shop, line.Approval.ItemId, out int shopIndex, out uint livePrice))
            return;
        uint price = livePrice > 0 ? livePrice : dataManager.GetExcelSheet<Item>().GetRow(line.Approval.ItemId).PriceMid;
        if (price == 0 || price > line.Approval.MaximumUnitPrice ||
            (ulong)price + (ulong)minimumGilReserve > InventoryManager.Instance()->GetGil())
        {
            Fail($"The live price for {line.Name} no longer matches the approval or would cross the gil reserve.");
            return;
        }
        if (!TryFirstFreeBagSlot(out _, out _))
        {
            Fail("Inventory is full. Nexus stopped before making another purchase.");
            return;
        }

        int before = OwnedCopies(line.Approval.ItemId);
        Fire(shop, 0, shopIndex, 1);
        pendingPurchase = new PendingPurchase(line, before, now, 1, false);
        detail = $"Nexus is verifying the purchase of {line.Name}.";
        Throttle(now);
    }

    private void UpdatePendingPurchase(DateTimeOffset now)
    {
        PendingPurchase pending = pendingPurchase!;
        if (OwnedCopies(pending.Line.Approval.ItemId) > pending.OwnedBefore)
        {
            pending.Line.RemainingPurchases--;
            pending.Line.AvailableCopies++;
            itemsPurchased++;
            pendingPurchase = null;
            detail = $"Nexus confirmed {pending.Line.Name}; {RemainingPurchases()} approved purchase(s) remain.";
            Throttle(now);
            return;
        }
        if (!pending.ConfirmationAccepted && TryAddon<AddonSelectYesno>("SelectYesno", out AddonSelectYesno* yesNo) &&
            TryAddon("Shop", out _) && PromptContains(yesNo, pending.Line.Name))
        {
            if (yesNo->YesButton != null && yesNo->YesButton->IsEnabled)
                ClickButton(yesNo->YesButton, (AtkUnitBase*)yesNo);
            pendingPurchase = pending with { ConfirmationAccepted = true, StartedAt = now };
            Throttle(now);
            return;
        }
        if (now - pending.StartedAt < PurchaseTimeout)
            return;
        if (pending.Attempts >= 2 || !TryAddon("Shop", out AtkUnitBase* shop) ||
            !FindShopItem(shop, pending.Line.Approval.ItemId, out int index, out _))
        {
            Fail($"Nexus could not confirm the approved purchase of {pending.Line.Name}.");
            return;
        }
        Fire(shop, 0, index, 1);
        pendingPurchase = pending with { Attempts = pending.Attempts + 1, StartedAt = now };
        Throttle(now);
    }

    private void AdvanceVendor(DateTimeOffset now)
    {
        vendorIndex++;
        if (vendorIndex < vendors.Count)
        {
            if (!BeginVendorTravel(now, out string message))
                Fail(message);
            return;
        }
        CloseOwnedAddons();
        equipLineIndex = 0;
        equipTargetIndex = 0;
        SetState(RunState.Equipping, "All approved purchases are confirmed. Nexus is equipping and verifying them.", now);
    }

    private void UpdateEquip(DateTimeOffset now)
    {
        if (pendingEquip is not null)
        {
            UpdatePendingEquip(now);
            return;
        }
        while (equipLineIndex < lines.Count)
        {
            ApprovedLine line = lines[equipLineIndex];
            if (equipTargetIndex >= line.TargetSlots.Length)
            {
                equipLineIndex++;
                equipTargetIndex = 0;
                continue;
            }
            int target = line.TargetSlots[equipTargetIndex];
            InventoryContainer* equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (equipped is null || !equipped->IsLoaded)
                return;
            if (BaseItemId(equipped->Items[target].ItemId) == line.Approval.ItemId)
            {
                equipTargetIndex++;
                continue;
            }
            if (!FindOwnedItem(line.Approval.ItemId, out InventoryType source, out ushort sourceSlot))
            {
                Fail($"The approved item {line.Name} is no longer present for equipping.");
                return;
            }
            if (now < nextActionAt)
                return;
            EquipmentIdentity? displaced = Identity(equipped->GetInventorySlot(target));
            int armouryFloor = displaced is null ? 0 : CountArmoury(displaced.Fingerprint);
            InventoryManager.Instance()->MoveItemSlot(source, sourceSlot, InventoryType.EquippedItems,
                checked((ushort)target), true);
            pendingEquip = new PendingEquip(line, target, source, sourceSlot, displaced, armouryFloor, now, 1, null);
            detail = $"Nexus is verifying {line.Name} in its approved equipment slot.";
            Throttle(now);
            return;
        }

        RaptureGearsetModule* gearsets = RaptureGearsetModule.Instance();
        if (gearsets is not null && gearsets->CurrentGearsetIndex >= 0)
            gearsets->UpdateGearset(gearsets->CurrentGearsetIndex);
        CaptureAdditionalRetiredItems();
        SetState(RunState.Cleaning, "Equipment is verified and the current gearset is updated. Nexus is moving only displaced items to inventory.", now);
    }

    private void UpdatePendingEquip(DateTimeOffset now)
    {
        PendingEquip pending = pendingEquip!;
        InventoryContainer* equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
            return;
        if (BaseItemId(equipped->Items[pending.TargetSlot].ItemId) == pending.Line.Approval.ItemId)
        {
            DateTimeOffset stable = pending.ObservedAt ?? now;
            if (pending.ObservedAt is null)
            {
                pendingEquip = pending with { ObservedAt = stable };
                return;
            }
            if (now - stable < TimeSpan.FromMilliseconds(500))
                return;
            pendingEquip = null;
            equipTargetIndex++;
            detail = $"Nexus confirmed {pending.Line.Name} equipped.";
            Throttle(now);
            return;
        }
        if (now - pending.StartedAt < EquipTimeout)
            return;
        if (pending.Attempts >= 3 || !FindOwnedItem(pending.Line.Approval.ItemId,
                out InventoryType source, out ushort sourceSlot))
        {
            Fail($"Nexus could not confirm {pending.Line.Name} in the approved equipment slot.");
            return;
        }
        InventoryManager.Instance()->MoveItemSlot(source, sourceSlot, InventoryType.EquippedItems,
            checked((ushort)pending.TargetSlot), true);
        pendingEquip = pending with
        {
            Source = source,
            SourceSlot = sourceSlot,
            StartedAt = now,
            Attempts = pending.Attempts + 1,
            ObservedAt = null,
        };
        Throttle(now);
    }

    private void UpdateCleanup(DateTimeOffset now)
    {
        if (now < nextActionAt)
            return;
        RetiredItem? pending = retired.FirstOrDefault(item => CountArmoury(item.Identity.Fingerprint) > item.ArmouryFloor);
        if (pending is not null)
        {
            if (!TryFirstFreeBagSlot(out InventoryType bag, out ushort bagSlot))
            {
                Complete("Nexus equipped every approved upgrade and updated the gearset. Inventory is full, so displaced gear remains safe in the Armoury Chest.");
                return;
            }
            if (FindArmouryIdentity(pending.Identity.Fingerprint, out InventoryType source, out ushort sourceSlot))
            {
                InventoryManager.Instance()->MoveItemSlot(source, sourceSlot, bag, bagSlot, true);
                detail = $"Nexus is moving displaced item {pending.Identity.ItemId} to inventory.";
                Throttle(now);
                return;
            }
        }
        Complete($"Nexus completed the gear transaction: {itemsPurchased} purchase(s), verified equipment, updated gearset, and displaced-item cleanup.");
    }

    private void Complete(string message)
    {
        endingItemLevel = currentItemLevel();
        completedSequence = startedSequence;
        ResetRun(message);
    }

    private void Fail(string message)
    {
        travel.Stop();
        CloseOwnedAddons();
        ResetRun(message);
    }

    private void ResetRun(string message)
    {
        state = RunState.Idle;
        pendingPurchase = null;
        pendingEquip = null;
        detail = message;
    }

    private void SetState(RunState value, string message, DateTimeOffset now)
    {
        state = value;
        stateStartedAt = now;
        detail = message;
    }

    private void Throttle(DateTimeOffset now) => nextActionAt = now.AddMilliseconds(500);
    private int RemainingPurchases() => lines.Sum(line => Math.Max(0, line.RemainingPurchases));
    private Vector3 playerPosition() => objectTable.LocalPlayer?.Position ?? Vector3.Zero;

    private int[] RingTargets(uint itemId, uint itemLevel, int desiredCopies)
    {
        InventoryContainer* equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        int[] slots =
        [
            (int)RaptureGearsetModule.GearsetItemIndex.RingLeft,
            (int)RaptureGearsetModule.GearsetItemIndex.RingRight,
        ];
        return slots.Where(slot => BaseItemId(equipped->Items[slot].ItemId) != itemId &&
                                  ItemLevel(BaseItemId(equipped->Items[slot].ItemId)) < itemLevel)
            .OrderBy(slot => ItemLevel(BaseItemId(equipped->Items[slot].ItemId)))
            .Take(Math.Min(2, desiredCopies))
            .ToArray();
    }

    private uint ItemLevel(uint itemId) => itemId == 0 ? 0 :
        dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.LevelItem.RowId ?? 0;

    private int OwnedCopies(uint itemId)
    {
        int count = 0;
        foreach (InventoryType type in Sources)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(type);
            if (container is null || !container->IsLoaded)
                continue;
            for (int index = 0; index < container->Size; index++)
                if (BaseItemId(container->Items[index].ItemId) == itemId)
                    count++;
        }
        return count;
    }

    private static bool FindShopItem(AtkUnitBase* shop, uint itemId, out int index, out uint price)
    {
        index = -1;
        price = 0;
        if (shop->AtkValuesCount <= 441)
            return false;
        int count = checked((int)shop->AtkValues[2].UInt);
        for (int candidate = 0; candidate < count; candidate++)
        {
            if (441 + candidate >= shop->AtkValuesCount || shop->AtkValues[441 + candidate].UInt != itemId)
                continue;
            index = candidate;
            price = 75 + candidate < shop->AtkValuesCount ? shop->AtkValues[75 + candidate].UInt : 0;
            return true;
        }
        return false;
    }

    private static bool PromptContains(AddonSelectYesno* addon, string itemName) =>
        addon->PromptText is not null && addon->PromptText->NodeText.ToString()
            .Contains(itemName, StringComparison.OrdinalIgnoreCase);

    private bool FindOwnedItem(uint itemId, out InventoryType type, out ushort slot)
    {
        foreach (InventoryType candidate in Sources)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(candidate);
            if (container is null || !container->IsLoaded)
                continue;
            for (ushort index = 0; index < container->Size; index++)
            {
                if (BaseItemId(container->Items[index].ItemId) != itemId)
                    continue;
                type = candidate;
                slot = index;
                return true;
            }
        }
        type = InventoryType.Invalid;
        slot = 0;
        return false;
    }

    private static bool TryFirstFreeBagSlot(out InventoryType type, out ushort slot)
    {
        foreach (InventoryType candidate in Bags)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(candidate);
            if (container is null || !container->IsLoaded)
                continue;
            for (ushort index = 0; index < container->Size; index++)
            {
                if (container->Items[index].ItemId != 0)
                    continue;
                type = candidate;
                slot = index;
                return true;
            }
        }
        type = InventoryType.Invalid;
        slot = 0;
        return false;
    }

    private static EquipmentIdentity? Identity(InventoryItem* item)
    {
        if (item is null || item->ItemId == 0)
            return null;
        return new EquipmentIdentity(item->GetItemId(),
            $"{item->GetItemId()}:{item->Flags}:{item->Condition}:{item->SpiritbondOrCollectability}:" +
            $"{item->CrafterContentId}:{item->GlamourId}:{string.Join(',', item->Materia.ToArray())}:" +
            $"{string.Join(',', item->MateriaGrades.ToArray())}:{string.Join(',', item->Stains.ToArray())}");
    }

    private static int CountArmoury(string fingerprint)
    {
        int count = 0;
        foreach (InventoryType type in Armoury)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(type);
            if (container is null || !container->IsLoaded)
                continue;
            for (int index = 0; index < container->Size; index++)
                if (Identity(container->GetInventorySlot(index))?.Fingerprint == fingerprint)
                    count++;
        }
        return count;
    }

    private void CaptureEquippedAtStart()
    {
        InventoryContainer* equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
            return;
        for (int slot = 0; slot < Math.Min(13, equipped->Size); slot++)
        {
            EquipmentIdentity? identity = Identity(equipped->GetInventorySlot(slot));
            if (identity is not null)
                retired.Add(new RetiredItem(identity, CountArmoury(identity.Fingerprint)));
        }
    }

    private void CaptureAdditionalRetiredItems()
    {
        InventoryContainer* equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
            return;
        Dictionary<string, int> stillEquipped = [];
        for (int slot = 0; slot < Math.Min(13, equipped->Size); slot++)
        {
            EquipmentIdentity? identity = Identity(equipped->GetInventorySlot(slot));
            if (identity is not null)
                stillEquipped[identity.Fingerprint] = stillEquipped.GetValueOrDefault(identity.Fingerprint) + 1;
        }
        RetiredItem[] before = retired.ToArray();
        retired.Clear();
        foreach (IGrouping<string, RetiredItem> group in before.GroupBy(item => item.Identity.Fingerprint))
        {
            int displaced = Math.Max(0, group.Count() - stillEquipped.GetValueOrDefault(group.Key));
            retired.AddRange(group.Take(displaced));
        }
    }

    private static bool FindArmouryIdentity(string fingerprint, out InventoryType type, out ushort slot)
    {
        foreach (InventoryType candidate in Armoury)
        {
            InventoryContainer* container = InventoryManager.Instance()->GetInventoryContainer(candidate);
            if (container is null || !container->IsLoaded)
                continue;
            for (ushort index = 0; index < container->Size; index++)
            {
                if (Identity(container->GetInventorySlot(index))?.Fingerprint != fingerprint)
                    continue;
                type = candidate;
                slot = index;
                return true;
            }
        }
        type = InventoryType.Invalid;
        slot = 0;
        return false;
    }

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

    private static void Fire(AtkUnitBase* addon, params int[] arguments)
    {
        AtkValue* values = stackalloc AtkValue[arguments.Length];
        for (int index = 0; index < arguments.Length; index++)
        {
            values[index].Type = AtkValueType.Int;
            values[index].Int = arguments[index];
        }
        addon->FireCallback((uint)arguments.Length, values, true);
    }

    private static void ClickTalk(AddonTalk* addon)
    {
        AtkUnitBase* unit = (AtkUnitBase*)addon;
        AtkEvent* evt = stackalloc AtkEvent[1];
        *evt = new AtkEvent
        {
            Listener = (AtkEventListener*)unit,
            Target = &AtkStage.Instance()->AtkEventTarget,
            State = new AtkEventState { StateFlags = (AtkEventStateFlags)132 },
        };
        AtkEventData* data = stackalloc AtkEventData[1];
        *data = default;
        unit->ReceiveEvent(AtkEventType.MouseDown, 0, evt, data);
        unit->ReceiveEvent(AtkEventType.MouseClick, 0, evt, data);
        unit->ReceiveEvent(AtkEventType.MouseUp, 0, evt, data);
    }

    private static void ClickButton(AtkComponentButton* button, AtkUnitBase* addon)
    {
        AtkComponentNode* node = button->AtkComponentBase.OwnerNode;
        AtkEvent* evt = (AtkEvent*)node->AtkEventManager.Event;
        if (evt is not null)
            addon->ReceiveEvent(evt->State.EventType, checked((int)evt->Param), evt);
    }

    private void CloseOwnedAddons()
    {
        foreach (string name in new[] { "Shop", "SelectString", "SelectIconString", "SelectYesno" })
            if (TryAddon(name, out AtkUnitBase* addon))
                addon->Close(true);
    }

    private static uint BaseItemId(uint itemId) => itemId % 1_000_000;

    private sealed class ApprovedLine(
        GearShoppingApprovalLine approval,
        string name,
        uint itemLevel,
        int ownedCopies,
        int[] targetSlots)
    {
        internal GearShoppingApprovalLine Approval { get; } = approval;
        internal string Name { get; } = name;
        internal uint ItemLevel { get; } = itemLevel;
        internal int AvailableCopies { get; set; } = ownedCopies;
        internal int RemainingPurchases { get; set; } = approval.Quantity;
        internal int[] TargetSlots { get; } = targetSlots;
    }

    private sealed record VendorWork(uint TerritoryId, uint DataId, ApprovedLine[] Lines);
    private sealed record PendingPurchase(ApprovedLine Line, int OwnedBefore, DateTimeOffset StartedAt,
        int Attempts, bool ConfirmationAccepted);
    private sealed record PendingEquip(ApprovedLine Line, int TargetSlot, InventoryType Source, ushort SourceSlot,
        EquipmentIdentity? Displaced, int ArmouryFloor, DateTimeOffset StartedAt, int Attempts,
        DateTimeOffset? ObservedAt);
    private sealed record EquipmentIdentity(uint ItemId, string Fingerprint);
    private sealed record RetiredItem(EquipmentIdentity Identity, int ArmouryFloor);

    private enum RunState
    {
        Idle,
        Traveling,
        Interacting,
        Shopping,
        Equipping,
        Cleaning,
    }
}
