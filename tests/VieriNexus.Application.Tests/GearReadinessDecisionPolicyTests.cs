using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class GearReadinessDecisionPolicyTests
{
    [Fact]
    public void VerifiedNoUpgradeCompletesWithoutShopping()
    {
        GearReadinessDecision decision = GearReadinessDecisionPolicy.Build(
            Preview(new GearUpgradeSlot(0, "Weapon", "Current", false, false, null)),
            2_000_000,
            1_000_000);

        Assert.True(decision.Success);
        Assert.False(decision.RequiresShopping);
        Assert.Null(decision.Approval);
        Assert.Contains("no vendor upgrades", decision.Message);
    }

    [Fact]
    public void RecommendedUpgradesBecomeOneExactApproval()
    {
        GearUpgradeReplacement replacement = new(100, "Upgrade", 415, 78, 12_000, "Vendor", 1, 0);
        GearReadinessDecision decision = GearReadinessDecisionPolicy.Build(
            Preview(new GearUpgradeSlot(0, "Weapon", "Current", false, true, replacement)),
            2_000_000,
            1_000_000);

        Assert.True(decision.Success);
        Assert.True(decision.RequiresShopping);
        GearShoppingApproval approval = Assert.IsType<GearShoppingApproval>(decision.Approval);
        GearShoppingApprovalLine line = Assert.Single(approval.Lines);
        Assert.Equal(new GearShoppingApprovalLine(0, 100, 12_000, 1), line);
        Assert.Equal(1_000_000, approval.MinimumGilReserve);
    }

    [Fact]
    public void UnavailablePreviewNeverStartsShopping()
    {
        GearUpgradePreview preview = Preview() with { UnavailableReason = "Catalog unavailable." };
        GearReadinessDecision decision = GearReadinessDecisionPolicy.Build(preview, 2_000_000, 1_000_000);

        Assert.False(decision.Success);
        Assert.False(decision.RequiresShopping);
        Assert.Null(decision.Approval);
        Assert.Equal("Catalog unavailable.", decision.Message);
    }

    private static GearUpgradePreview Preview(params GearUpgradeSlot[] slots) => new(
        GearUpgradePreview.CurrentSchemaVersion,
        123,
        456,
        "MCH",
        79,
        78,
        slots);
}
