using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class OperationsExecutionPolicyTests
{
    [Fact]
    public void ConfiguredBatchIncludesEveryOwnedActionInSafeStableOrder()
    {
        AutoDutyMaintenancePolicy policy = Policy() with
        {
            AutoRepair = true,
            RepairWithCrafter = true,
            AutoExtract = true,
            RegisterTripleTriadCards = true,
            RegisterMinions = true,
            RegisterOrchestrionRolls = true,
            AutoOpenCoffers = true,
            AutoSell = true,
            AutoDesynth = true,
            AutoGrandCompanyTurnIn = true,
        };

        Assert.Equal([
            NexusMaintenanceOperation.ExtractMateria,
            NexusMaintenanceOperation.Desynthesize,
            NexusMaintenanceOperation.RegisterTripleTriadCards,
            NexusMaintenanceOperation.RegisterMinions,
            NexusMaintenanceOperation.RegisterOrchestrionRolls,
            NexusMaintenanceOperation.OpenCoffers,
            NexusMaintenanceOperation.GrandCompanyTurnIn,
            NexusMaintenanceOperation.Repair,
        ], OperationsExecutionPolicy.ConfiguredOperations(policy));
    }

    [Fact]
    public void ProtectedSellingRejectsEveryUnsafeItemClassAndProducesStableApproval()
    {
        NexusInventoryItemSnapshot eligible = Item(0, 2);
        NexusInventoryItemSnapshot[] inventory =
        [
            eligible,
            Item(0, 3) with { IsExperienceBonusEquipment = true },
            Item(0, 4) with { IsReferencedByGearset = true },
            Item(0, 5) with { IsCollectable = true },
            Item(0, 6) with { IsEquipment = false },
            Item(0, 7) with { VendorPrice = 0 },
            Item(0, 8) with { IsUntradeable = false, Spiritbond = 0 },
        ];

        NexusItemTransactionPreview preview = ItemTransactionPolicy.CreateSellPreview(inventory, true);

        Assert.Equal([eligible], preview.Items);
        Assert.True(ItemTransactionPolicy.Matches(preview, inventory, true));
        Assert.False(ItemTransactionPolicy.Matches(preview,
            inventory.Select(item => item == eligible ? item with { Quantity = 2 } : item), true));
    }

    [Theory]
    [InlineData(120, 140, true)]
    [InlineData(119, 140, false)]
    [InlineData(119, 140, true, false, true)]
    public void SellingThresholdsAreBounded(int occupied, int total, bool expected,
        bool useSlots = true, bool usePercent = false)
    {
        AutoDutyMaintenancePolicy policy = Policy() with
        {
            AutoSell = true,
            SellAtOccupiedSlotThreshold = useSlots,
            SellOccupiedSlotThreshold = 120,
            SellAtBagPercentThreshold = usePercent,
            SellBagPercentThreshold = 85,
        };
        Assert.Equal(expected, ItemTransactionPolicy.SellThresholdReached(policy, occupied, total));
    }

    [Fact]
    public void VendorRepairIsNotSilentlyReplacedBySelfRepair()
    {
        AutoDutyMaintenancePolicy policy = Policy() with { AutoRepair = true, RepairWithCrafter = false };
        Assert.DoesNotContain(NexusMaintenanceOperation.Repair,
            OperationsExecutionPolicy.ConfiguredOperations(policy));
    }

    [Fact]
    public void StrikingDummyCatalogPreservesExpansionCoverageAndCoordinateConversion()
    {
        Assert.Contains(StrikingDummyCatalog.Destinations, item => item.Expansion == "A Realm Reborn");
        Assert.Contains(StrikingDummyCatalog.Destinations, item => item.Expansion == "Dawntrail");
        (float x, float z) = StrikingDummyCatalog.MapToWorld(24, 19.5f, 100, 0, 0);
        Assert.True(float.IsFinite(x));
        Assert.True(float.IsFinite(z));
    }

    private static AutoDutyMaintenancePolicy Policy() => new(
        false, 1_000_000, false, false, 50, true, null, false, false, false, null, false,
        new Dictionary<uint, string>(), false, false, 50, false, true, 1, false, false, 5, false,
        false, false, false, false, false, false, 1, 1, false, "UnneededEquipment", true, 120,
        true, 85, true, null, false, true, 20, true, false, false, false);

    private static NexusInventoryItemSnapshot Item(int container, int slot) => new(
        container, slot, 10_000u + (uint)slot, 1, $"Item {slot}", true, 100, true, 0,
        false, false, false);
}
