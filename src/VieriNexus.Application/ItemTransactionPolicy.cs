using System.Security.Cryptography;
using System.Text;

namespace VieriNexus.Application;

public enum NexusItemTransactionKind
{
    Sell,
    Desynthesize,
    GrandCompanyTurnIn,
    EntrustArmoire,
    EntrustGlamourChest,
}

public sealed record NexusInventoryItemSnapshot(
    int Container,
    int Slot,
    uint ItemId,
    int Quantity,
    string Name,
    bool IsEquipment,
    uint VendorPrice,
    bool IsUntradeable,
    ushort Spiritbond,
    bool IsCollectable,
    bool IsExperienceBonusEquipment,
    bool IsReferencedByGearset);

public sealed record NexusItemTransactionPreview(
    NexusItemTransactionKind Kind,
    IReadOnlyList<NexusInventoryItemSnapshot> Items,
    string Signature,
    string Summary);

public static class ItemTransactionPolicy
{
    public static bool SellThresholdReached(AutoDutyMaintenancePolicy policy, int occupiedSlots,
        int totalSlots)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.AutoSell || totalSlots <= 0)
            return false;
        int boundedOccupied = Math.Clamp(occupiedSlots, 0, totalSlots);
        return policy.SellAtOccupiedSlotThreshold && boundedOccupied >= Math.Clamp(policy.SellOccupiedSlotThreshold, 1, totalSlots) ||
               policy.SellAtBagPercentThreshold && boundedOccupied * 100d / totalSlots >= Math.Clamp(policy.SellBagPercentThreshold, 1, 100);
    }

    public static bool CanSell(NexusInventoryItemSnapshot item, bool protectGearsets) =>
        item.ItemId != 0 && item.Quantity > 0 && item.IsEquipment && item.VendorPrice > 0 &&
        (item.IsUntradeable || item.Spiritbond > 0) && !item.IsCollectable &&
        !item.IsExperienceBonusEquipment && (!protectGearsets || !item.IsReferencedByGearset);

    public static NexusItemTransactionPreview CreateSellPreview(IEnumerable<NexusInventoryItemSnapshot> inventory,
        bool protectGearsets)
    {
        NexusInventoryItemSnapshot[] selected = inventory
            .Where(item => CanSell(item, protectGearsets))
            .OrderBy(item => item.Container).ThenBy(item => item.Slot).ToArray();
        return CreatePreview(NexusItemTransactionKind.Sell, selected,
            selected.Length == 0
                ? "No protected-selling candidates were found."
                : $"{selected.Length} exact bag slot(s) are eligible for protected selling.");
    }

    public static bool Matches(NexusItemTransactionPreview approval,
        IEnumerable<NexusInventoryItemSnapshot> currentInventory, bool protectGearsets) =>
        approval.Kind == NexusItemTransactionKind.Sell &&
        string.Equals(approval.Signature,
            CreateSellPreview(currentInventory, protectGearsets).Signature,
            StringComparison.Ordinal);

    private static NexusItemTransactionPreview CreatePreview(NexusItemTransactionKind kind,
        IReadOnlyList<NexusInventoryItemSnapshot> items, string summary)
    {
        string payload = string.Join('\n', items.Select(item =>
            $"{item.Container}:{item.Slot}:{item.ItemId}:{item.Quantity}:{item.VendorPrice}:{item.Spiritbond}"));
        string signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}\n{payload}")));
        return new(kind, items, signature, summary);
    }
}
