using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Reflection;
using System.Text.RegularExpressions;
using VieriNexus.Application;

namespace VieriNexus.Services;

/// <summary>
/// Nexus-owned live equipment and ordinary gil-vendor catalog reader. It deliberately resolves
/// stock only through each curated NPC's ENpcData graph; items merely having a gil price are not
/// evidence that the selected vendor sells them.
/// </summary>
internal sealed unsafe class NexusGearCatalogService(
    IDataManager dataManager,
    IPlayerState playerState,
    IObjectTable objectTable)
{
    private const uint ExperienceSetBonusId = 6;
    private const uint TwoHandedWeaponCategoryId = 13;

    private sealed record Vendor(uint DataId, uint TerritoryId);
    private sealed record VendorTier(byte EquipLevel, ushort ItemLevel, Vendor[] Vendors);

    private static readonly Vendor[] LowPhysical =
    [
        new(1001203, 129), new(1001205, 129), new(1001202, 129),
    ];

    private static readonly Vendor[] LowMagical =
    [
        new(1000215, 133), new(1000217, 133), new(1001202, 129),
    ];

    private static readonly Vendor[] LowHeavensward =
    [
        new(1011200, 419), new(1011203, 419), new(1011204, 419),
    ];

    private static readonly VendorTier[] Tiers =
    [
        Tier(50, 115, (1011200, 419), (1011203, 419), (1011204, 419)),
        Tier(52, 125, (1011200, 419), (1011203, 419), (1011204, 419)),
        Tier(54, 133, (1011200, 419), (1011203, 419), (1011204, 419)),
        Tier(56, 139, (1011200, 419), (1011203, 419), (1011204, 419)),
        Tier(58, 145, (1011200, 419), (1011203, 419), (1011204, 419)),
        Tier(60, 255, (1011200, 419), (1011203, 419), (1011204, 419)),
        Tier(62, 265, (1018988, 628), (1018989, 628), (1018990, 628)),
        Tier(64, 273, (1019296, 614), (1018989, 628), (1018990, 628)),
        Tier(66, 279, (1019269, 614), (1018989, 628), (1018990, 628)),
        Tier(68, 285, (1020866, 620), (1018989, 628), (1018990, 628)),
        Tier(70, 385, (1018988, 628), (1018989, 628), (1018990, 628)),
        Tier(72, 395, (1027242, 819), (1027243, 819), (1027991, 819)),
        Tier(74, 403, (1027242, 819), (1027243, 819), (1027991, 819)),
        Tier(76, 409, (1027242, 819), (1027243, 819), (1027991, 819)),
        Tier(78, 415, (1027242, 819), (1027243, 819), (1027991, 819)),
        Tier(80, 515, (1037049, 962)),
        Tier(82, 525, (1037720, 958)),
        Tier(84, 533, (1037791, 959)),
        Tier(86, 539, (1037907, 961)),
        Tier(88, 545, (1038003, 960)),
        Tier(90, 645, (1048377, 1185)),
        Tier(92, 655, (1048851, 1188)),
        Tier(94, 663, (1048971, 1189)),
        Tier(96, 669, (1049371, 1190)),
        Tier(98, 675, (1049486, 1191)),
    ];

    internal bool IsAvailable => objectTable.LocalPlayer is not null && playerState.ContentId != 0;

    internal GearUpgradeSnapshot BuildSnapshot()
    {
        if (objectTable.LocalPlayer is not { } player || playerState.ContentId == 0)
            return Unavailable("Unavailable", 0, 0, "Log into a character before shopping.");

        string job = player.ClassJob.Value.Abbreviation.ExtractText();
        short level = checked((short)player.Level);
        uint primaryStat = PrimaryStatForJob(job);
        if (level < 1 || primaryStat == 0)
            return Unavailable(job, level, 0, "Shop For Upgrades is available for combat jobs.");

        VendorTier? tier = level < 50
            ? new VendorTier((byte)level, (ushort)level, LowLevelVendors(job))
            : Tiers.LastOrDefault(candidate => candidate.EquipLevel <= level);
        if (tier is null)
            return Unavailable(job, level, 0, "No gil-vendor gear band is available for this level.");

        InventoryManager* manager = InventoryManager.Instance();
        InventoryContainer* equipped = manager is null
            ? null
            : manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded || equipped->Size < 13)
            return Unavailable(job, level, tier.EquipLevel, "Equipment data is still loading. Try again in a moment.");

        try
        {
            List<GearCurrentSlot> slots = [];
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.MainHand, "Weapon", level, isMainHand: true);
            if (job is "GLA" or "PLD")
                AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.OffHand, "Shield", level, isOffHand: true);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Head, "Head", level);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Body, "Body", level);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Hands, "Hands", level);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Legs, "Legs", level);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Feet, "Feet", level);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Ears, "Earrings", level);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Neck, "Necklace", level);
            AddSlot(slots, equipped, RaptureGearsetModule.GearsetItemIndex.Wrists, "Bracelet", level);
            AddRings(slots, equipped, level);

            List<GearUpgradeCandidate> candidates = [];
            foreach (Vendor vendor in tier.Vendors)
            {
                string vendorName = dataManager.GetExcelSheet<ENpcResident>().GetRow(vendor.DataId).Singular.ExtractText();
                string area = dataManager.GetExcelSheet<TerritoryType>().GetRow(vendor.TerritoryId)
                    .PlaceName.Value.Name.ExtractText();
                foreach (Item item in ReadVendorItems(vendor.DataId).Where(item => Eligible(item, tier, job)))
                {
                    int slotKey = LogicalSlot(item);
                    if (slotKey == int.MaxValue)
                        continue;
                    int owned = OwnedCopies(item.RowId);
                    int quantity = PurchaseQuantity(item, equipped, level, owned);
                    bool mainHand = item.EquipSlotCategory.Value.MainHand > 0;
                    bool offHand = item.EquipSlotCategory.Value.OffHand > 0;
                    candidates.Add(new GearUpgradeCandidate(
                        slotKey,
                        item.RowId,
                        item.Name.ExtractText(),
                        item.LevelItem.RowId,
                        item.LevelEquip,
                        item.PriceMid,
                        $"{vendorName} — {area}",
                        quantity,
                        owned,
                        BaseParam(item, primaryStat),
                        mainHand,
                        offHand,
                        mainHand && IsTwoHanded(item)));
                }
            }

            uint currentMainHandId = BaseItemId(equipped->Items[(int)RaptureGearsetModule.GearsetItemIndex.MainHand].ItemId);
            return new GearUpgradeSnapshot(
                GearUpgradeSnapshot.CurrentSchemaVersion,
                playerState.ContentId,
                EquipmentSignature(equipped, level),
                job,
                level,
                tier.EquipLevel,
                slots,
                candidates,
                IsTwoHanded(Item(currentMainHandId)));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Nexus could not build its native gear catalog snapshot.");
            return Unavailable(job, level, tier.EquipLevel,
                "Vendor item data could not be loaded. No purchases will be made. Try Refresh or reopen the window.");
        }
    }

    private Item[] ReadVendorItems(uint vendorId)
    {
        ENpcBase npc = dataManager.GetExcelSheet<ENpcBase>().GetRow(vendorId);
        HashSet<uint> visited = [];
        HashSet<uint> shops = [];
        foreach (RowRef reference in npc.ENpcData)
            Visit(reference, visited, shops);
        if (shops.Count == 0)
            throw new InvalidOperationException($"No ordinary gil-shop catalog resolved for vendor {vendorId}.");
        SubrowExcelSheet<GilShopItem> stock = dataManager.GetSubrowExcelSheet<GilShopItem>();
        return shops.SelectMany(id => stock.GetRow(id).Select(row => row.Item.Value))
            .Where(item => item.RowId != 0)
            .DistinctBy(item => item.RowId)
            .ToArray();
    }

    private static void Visit(RowRef reference, HashSet<uint> visited, HashSet<uint> shops)
    {
        if (reference.RowId == 0 || !visited.Add(reference.RowId))
            return;
        if (reference.Is<GilShop>())
            shops.Add(reference.RowId);
        else if (reference.TryGetValue(out PreHandler preHandler))
            Visit(preHandler.Target, visited, shops);
        else if (reference.TryGetValue(out TopicSelect topic))
            foreach (RowRef shop in topic.Shop)
                Visit(shop, visited, shops);
    }

    private void AddSlot(List<GearCurrentSlot> slots, InventoryContainer* equipped,
        RaptureGearsetModule.GearsetItemIndex slot, string name, short level,
        bool isMainHand = false, bool isOffHand = false)
    {
        uint id = BaseItemId(equipped->Items[(int)slot].ItemId);
        uint itemLevel = ItemLevel(id);
        slots.Add(new((int)slot, name, Describe(id, itemLevel), id, itemLevel,
            IsActiveExperienceItem(id, level), isMainHand, isOffHand));
    }

    private void AddRings(List<GearCurrentSlot> slots, InventoryContainer* equipped, short level)
    {
        uint leftId = BaseItemId(equipped->Items[(int)RaptureGearsetModule.GearsetItemIndex.RingLeft].ItemId);
        uint rightId = BaseItemId(equipped->Items[(int)RaptureGearsetModule.GearsetItemIndex.RingRight].ItemId);
        uint leftLevel = ItemLevel(leftId);
        uint rightLevel = ItemLevel(rightId);
        bool leftProtected = IsActiveExperienceItem(leftId, level);
        bool rightProtected = IsActiveExperienceItem(rightId, level);
        (uint Id, uint Level) weakest = new[]
            {
                (leftId, leftLevel, leftProtected),
                (rightId, rightLevel, rightProtected),
            }
            .Where(ring => !ring.Item3)
            .Select(ring => (ring.Item1, ring.Item2))
            .DefaultIfEmpty((0u, uint.MaxValue))
            .OrderBy(ring => ring.Item2)
            .First();
        slots.Add(new((int)RaptureGearsetModule.GearsetItemIndex.RingLeft, "Rings",
            $"{Describe(leftId, leftLevel)} / {Describe(rightId, rightLevel)}",
            weakest.Id, weakest.Level, leftProtected && rightProtected, IsRing: true));
    }

    private bool Eligible(Item item, VendorTier tier, string job) =>
        item.LevelEquip <= tier.EquipLevel &&
        (tier.EquipLevel < 50 || item.LevelItem.RowId <= tier.ItemLevel) &&
        item.EquipSlotCategory.RowId > 0 && JobCanEquip(item, job) &&
        !IsProtectedExperienceItem(item.RowId) &&
        (tier.EquipLevel < 50 || item.LevelEquip == tier.EquipLevel || IsAccessory(item));

    private static bool JobCanEquip(Item item, string job)
    {
        object category = item.ClassJobCategory.Value;
        PropertyInfo? property = category.GetType().GetProperty(job,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(category) is true;
    }

    private int PurchaseQuantity(Item item, InventoryContainer* equipped, short level, int owned)
    {
        if (LogicalSlot(item) == (int)RaptureGearsetModule.GearsetItemIndex.RingLeft)
        {
            int upgrades = new[]
                {
                    RaptureGearsetModule.GearsetItemIndex.RingLeft,
                    RaptureGearsetModule.GearsetItemIndex.RingRight,
                }
                .Count(slot =>
                {
                    uint id = BaseItemId(equipped->Items[(int)slot].ItemId);
                    return !IsActiveExperienceItem(id, level) && id != item.RowId && item.LevelItem.RowId > ItemLevel(id);
                });
            if (item.IsUnique)
                upgrades = Math.Min(upgrades, 1);
            return Math.Max(0, upgrades - owned);
        }

        int slotKey = LogicalSlot(item);
        if (slotKey == int.MaxValue)
            return 0;
        uint currentId = BaseItemId(equipped->Items[slotKey].ItemId);
        if (item.IsUnique && currentId == item.RowId)
            return 0;
        int needed = !IsActiveExperienceItem(currentId, level) && currentId != item.RowId &&
                     item.LevelItem.RowId > ItemLevel(currentId) ? 1 : 0;
        return Math.Max(0, needed - owned);
    }

    private int OwnedCopies(uint itemId)
    {
        InventoryType[] containers =
        [
            InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
            InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead,
            InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
        ];
        InventoryManager* manager = InventoryManager.Instance();
        int count = 0;
        foreach (InventoryType type in containers)
        {
            InventoryContainer* container = manager->GetInventoryContainer(type);
            if (container is null || !container->IsLoaded)
                continue;
            for (int index = 0; index < container->Size; index++)
                if (BaseItemId(container->Items[index].ItemId) == itemId)
                    count++;
        }
        return count;
    }

    private ulong EquipmentSignature(InventoryContainer* equipped, short level)
    {
        ulong hash = 14695981039346656037UL;
        for (int slot = 0; slot < 13; slot++)
        {
            if (slot == 5) // Retired waist slot is not part of the live equipment signature.
                continue;
            uint id = BaseItemId(equipped->Items[slot].ItemId);
            hash = (hash ^ id) * 1099511628211UL;
            hash = (hash ^ (IsActiveExperienceItem(id, level) ? 1UL : 0UL)) * 1099511628211UL;
        }
        return hash;
    }

    private bool IsProtectedExperienceItem(uint itemId)
    {
        Item? item = Item(itemId);
        if (item is not { EquipSlotCategory.RowId: > 0 } equipment)
            return false;
        return equipment.ItemSpecialBonus.RowId == ExperienceSetBonusId && equipment.ItemSpecialBonusParam > 0 ||
               equipment.Description.ExtractText().Contains("EXP earned", StringComparison.OrdinalIgnoreCase) ||
               equipment.Description.ExtractText().Contains("EXP Bonus", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsActiveExperienceItem(uint itemId, short level)
    {
        if (!IsProtectedExperienceItem(itemId))
            return false;
        string description = Item(itemId)?.Description.ExtractText() ?? string.Empty;
        Match match = Regex.Match(description, @"\blevel\s+(\d+)\s+(?:and|or)\s+below\b", RegexOptions.IgnoreCase);
        return !match.Success || !int.TryParse(match.Groups[1].Value, out int cap) || level <= cap;
    }

    private uint ItemLevel(uint itemId) => Item(itemId)?.LevelItem.RowId ?? 0;
    private Item? Item(uint itemId) => itemId == 0 ? null : dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId);
    private string Describe(uint itemId, uint level) => itemId == 0
        ? "Empty"
        : $"{Item(itemId)?.Name.ExtractText() ?? $"Item {itemId}"} (iLvl {level})";

    private static int BaseParam(Item item, uint id)
    {
        int total = 0;
        int count = Math.Min(item.BaseParam.Count, item.BaseParamValue.Count);
        for (int index = 0; index < count; index++)
            if (item.BaseParam[index].RowId == id)
                total += item.BaseParamValue[index];
        return total;
    }

    private static int LogicalSlot(Item item) => item.EquipSlotCategory.Value switch
    {
        { MainHand: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.MainHand,
        { OffHand: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.OffHand,
        { Head: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Head,
        { Body: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Body,
        { Gloves: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Hands,
        { Legs: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Legs,
        { Feet: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Feet,
        { Ears: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Ears,
        { Neck: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Neck,
        { Wrists: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.Wrists,
        { FingerL: > 0 } or { FingerR: > 0 } => (int)RaptureGearsetModule.GearsetItemIndex.RingLeft,
        _ => int.MaxValue,
    };

    private static bool IsAccessory(Item item) => item.EquipSlotCategory.Value is
        { Ears: > 0 } or { Neck: > 0 } or { Wrists: > 0 } or { FingerL: > 0 } or { FingerR: > 0 };
    private static bool IsTwoHanded(Item? item) => item?.EquipSlotCategory.RowId == TwoHandedWeaponCategoryId;
    private static uint BaseItemId(uint id) => id % 1_000_000;

    private static uint PrimaryStatForJob(string job) => job.ToUpperInvariant() switch
    {
        "GLA" or "PLD" or "MRD" or "WAR" or "DRK" or "GNB" or "PGL" or "MNK" or "LNC" or "DRG" or "SAM" or "RPR" => 1,
        "ROG" or "NIN" or "VPR" or "ARC" or "BRD" or "MCH" or "DNC" => 2,
        "THM" or "BLM" or "ACN" or "SMN" or "RDM" or "PCT" or "BLU" => 4,
        "CNJ" or "WHM" or "SCH" or "AST" or "SGE" => 5,
        _ => 0,
    };

    private static Vendor[] LowLevelVendors(string job) => job.ToUpperInvariant() switch
    {
        "DRK" or "MCH" or "AST" => LowHeavensward,
        "CNJ" or "THM" or "ACN" or "WHM" or "BLM" or "SMN" or "SCH" or "BLU" => LowMagical,
        _ => LowPhysical,
    };

    private static VendorTier Tier(byte equipLevel, ushort itemLevel,
        params (uint DataId, uint TerritoryId)[] vendors) =>
        new(equipLevel, itemLevel, vendors.Select(vendor => new Vendor(vendor.DataId, vendor.TerritoryId)).ToArray());

    private static GearUpgradeSnapshot Unavailable(string job, short level, byte vendorLevel, string reason) => new(
        GearUpgradeSnapshot.CurrentSchemaVersion, 0, 0, job, level, vendorLevel, [], [], UnavailableReason: reason);
}
