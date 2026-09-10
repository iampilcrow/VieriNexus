using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class GearShoppingApprovalPolicyTests
{
    [Fact]
    public void BuildsExactApprovalWithHardGilFloor()
    {
        GearUpgradePreview preview = Preview(
            Slot(0, "Weapon", 100, 12_000, 1, recommended: true),
            Slot(3, "Head", 200, 8_000, 1, recommended: true));

        GearShoppingApprovalResult result = GearShoppingApprovalPolicy.Build(
            preview, [0, 3], currentGil: 1_050_000, minimumGilReserve: 1_000_000);

        Assert.True(result.Success);
        Assert.Equal(20_000UL, result.EstimatedCost);
        Assert.Equal(1_000_000, result.Approval!.MinimumGilReserve);
        Assert.Collection(result.Approval.Lines,
            line => Assert.Equal(new GearShoppingApprovalLine(0, 100, 12_000, 1), line),
            line => Assert.Equal(new GearShoppingApprovalLine(3, 200, 8_000, 1), line));
    }

    [Fact]
    public void RejectsSelectionThatWouldCrossReserve()
    {
        GearShoppingApprovalResult result = GearShoppingApprovalPolicy.Build(
            Preview(Slot(0, "Weapon", 100, 12_000, 1, recommended: true)),
            [0], currentGil: 1_005_000, minimumGilReserve: 1_000_000);

        Assert.False(result.Success);
        Assert.Contains("protected reserve", result.Message);
    }

    [Fact]
    public void RejectsProtectedExperienceSlotEvenIfProviderMarksItRecommended()
    {
        GearUpgradeSlot protectedSlot = Slot(3, "Head", 200, 8_000, 1, recommended: true) with
        {
            ActiveExperienceBonus = true,
        };

        GearShoppingApprovalResult result = GearShoppingApprovalPolicy.Build(
            Preview(protectedSlot), [3], 2_000_000, 1_000_000);

        Assert.False(result.Success);
        Assert.Contains("protected EXP item", result.Message);
    }

    [Fact]
    public void RejectsStaleOrUnknownSlotAndEmptySelection()
    {
        GearUpgradePreview preview = Preview(Slot(0, "Weapon", 100, 12_000, 1, recommended: true));

        Assert.False(GearShoppingApprovalPolicy.Build(preview, [], 2_000_000, 0).Success);
        Assert.False(GearShoppingApprovalPolicy.Build(preview, [99], 2_000_000, 0).Success);
    }

    [Fact]
    public void OwnedUpgradeCarriesApprovalWithoutAddingPurchaseCost()
    {
        GearUpgradeSlot owned = Slot(12, "Rings", 300, 9_000, 0, recommended: true);
        GearShoppingApprovalResult result = GearShoppingApprovalPolicy.Build(
            Preview(owned), [12], currentGil: 1_000_000, minimumGilReserve: 1_000_000);

        Assert.True(result.Success);
        Assert.Equal(0UL, result.EstimatedCost);
        Assert.Equal(0, Assert.Single(result.Approval!.Lines).Quantity);
    }

    private static GearUpgradePreview Preview(params GearUpgradeSlot[] slots) => new(
        GearUpgradePreview.CurrentSchemaVersion,
        123,
        456,
        "VPR",
        79,
        78,
        slots);

    private static GearUpgradeSlot Slot(
        int key,
        string name,
        uint itemId,
        uint price,
        int quantity,
        bool recommended) => new(
            key,
            name,
            "Old item (iLvl 1)",
            false,
            recommended,
            new GearUpgradeReplacement(itemId, "Upgrade", 415, 78, price, "Vendor — Area", quantity, 0));
}
