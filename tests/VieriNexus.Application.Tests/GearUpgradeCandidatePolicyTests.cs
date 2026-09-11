using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class GearUpgradeCandidatePolicyTests
{
    [Fact]
    public void SelectsHighestRoleAppropriateCandidatePerSlot()
    {
        GearUpgradePreview preview = GearUpgradeCandidatePolicy.BuildPreview(Snapshot(
            [Slot(3, "Body", 100, 120)],
            [
                Candidate(3, 200, 150, 8_000, primaryStat: 0),
                Candidate(3, 201, 145, 9_000, primaryStat: 20),
                Candidate(3, 202, 145, 7_000, primaryStat: 20),
            ]));

        GearUpgradeReplacement replacement = Assert.Single(preview.Slots).Replacement!;
        Assert.Equal(202u, replacement.ItemId);
    }

    [Fact]
    public void KeepsActiveExperienceEquipmentProtected()
    {
        GearUpgradePreview preview = GearUpgradeCandidatePolicy.BuildPreview(Snapshot(
            [Slot(2, "Head", 100, 1, activeExperienceBonus: true)],
            [Candidate(2, 200, 500, 1_000, primaryStat: 10)]));

        GearUpgradeSlot slot = Assert.Single(preview.Slots);
        Assert.True(slot.ActiveExperienceBonus);
        Assert.Null(slot.Replacement);
        Assert.False(slot.Recommended);
    }

    [Fact]
    public void SuppressesOffHandWhenSelectedMainHandIsTwoHanded()
    {
        GearUpgradePreview preview = GearUpgradeCandidatePolicy.BuildPreview(Snapshot(
            [
                Slot(0, "Weapon", 100, 100, isMainHand: true),
                Slot(1, "Shield", 101, 100, isOffHand: true),
            ],
            [
                Candidate(0, 200, 150, 10_000, 20, isMainHand: true, isTwoHanded: true),
                Candidate(1, 201, 150, 5_000, 20, isOffHand: true),
            ]));

        Assert.NotNull(preview.Slots.Single(slot => slot.SlotKey == 0).Replacement);
        Assert.Null(preview.Slots.Single(slot => slot.SlotKey == 1).Replacement);
    }

    [Fact]
    public void UsesCurrentTwoHandedWeaponWhenNoMainHandUpgradeExists()
    {
        GearUpgradeSnapshot snapshot = Snapshot(
            [
                Slot(0, "Weapon", 100, 200, isMainHand: true),
                Slot(1, "Shield", 101, 1, isOffHand: true),
            ],
            [Candidate(1, 201, 150, 5_000, 20, isOffHand: true)]) with
        {
            CurrentMainHandIsTwoHanded = true,
        };

        GearUpgradePreview preview = GearUpgradeCandidatePolicy.BuildPreview(snapshot);

        Assert.Null(preview.Slots.Single(slot => slot.SlotKey == 1).Replacement);
    }

    [Fact]
    public void RejectsMalformedOrUnverifiedSnapshot()
    {
        GearUpgradeSnapshot duplicateSlots = Snapshot(
            [Slot(3, "Body", 100, 1), Slot(3, "Body", 100, 1)],
            [Candidate(3, 200, 100, 5_000, 10)]);

        Assert.Contains("duplicate", GearUpgradeCandidatePolicy.BuildPreview(duplicateSlots)
            .UnavailableReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(GearUpgradeCandidatePolicy.BuildPreview(duplicateSlots with { CharacterId = 0 })
            .UnavailableReason);
    }

    private static GearUpgradeSnapshot Snapshot(
        IReadOnlyList<GearCurrentSlot> slots,
        IReadOnlyList<GearUpgradeCandidate> candidates) => new(
        GearUpgradeSnapshot.CurrentSchemaVersion,
        123,
        456,
        "MCH",
        79,
        78,
        slots,
        candidates);

    private static GearCurrentSlot Slot(
        int key,
        string name,
        uint itemId,
        uint itemLevel,
        bool activeExperienceBonus = false,
        bool isMainHand = false,
        bool isOffHand = false) => new(
        key,
        name,
        $"Current {itemId}",
        itemId,
        itemLevel,
        activeExperienceBonus,
        isMainHand,
        isOffHand);

    private static GearUpgradeCandidate Candidate(
        int slotKey,
        uint itemId,
        uint itemLevel,
        uint price,
        int primaryStat,
        bool isMainHand = false,
        bool isOffHand = false,
        bool isTwoHanded = false) => new(
        slotKey,
        itemId,
        $"Item {itemId}",
        itemLevel,
        78,
        price,
        "Vendor — Area",
        1,
        0,
        primaryStat,
        isMainHand,
        isOffHand,
        isTwoHanded);
}
