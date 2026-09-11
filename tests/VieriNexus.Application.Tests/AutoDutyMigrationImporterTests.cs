using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class AutoDutyMigrationImporterTests
{
    [Fact]
    public void PreviewMapsAllProfilesAssignmentsOverlayAndMaintenancePolicy()
    {
        string json = """
        {
          "DefaultConfigName": "Main",
          "profileData": [
            {
              "Name": "Main",
              "CIDs": [123, 456],
              "Config": {
                "ShowOverlay": true,
                "HideOverlayWhenStopped": true,
                "LockOverlay": true,
                "OverlayNoBG": true,
                "OverlayAnchorBottom": true,
                "RepairButton": false,
                "AutoBuyGilVendorGear": true,
                "AutoBuyGilVendorKeepGil": 1000000,
                "AutoRepair": true,
                "AutoRepairPct": 42,
                "AutoRepairSelf": true,
                "PreferredRepairNPC": { "DataId": 77 },
                "AutoExtract": true,
                "AutoExtractAll": true,
                "AutoOpenCoffers": true,
                "AutoOpenCoffersGearset": 3,
                "AutoOpenCoffersBlacklistUse": true,
                "AutoOpenCoffersBlacklist": { "100": "Protected" },
                "AutoDesynth": true,
                "AutoDesynthSkillUp": true,
                "AutoDesynthSkillUpLimit": 25,
                "AutoDesynthNQOnly": true,
                "AutoDesynthNoGearset": true,
                "AutoDesynthCategories": 9,
                "AutoGCTurnin": true,
                "AutoGCTurninSlotsLeftBool": true,
                "AutoGCTurninSlotsLeft": 8,
                "AutoGCTurninUseTicket": true,
                "ArmoireEntrust": true,
                "GlamourChestEntrust": true,
                "TripleTriadRegister": true,
                "MinionRegister": true,
                "OrchestrionRegister": true,
                "TripleTriadSell": true,
                "TripleTriadSellMinItemCount": 2,
                "TripleTriadSellMinSlotCount": 4,
                "AutoSell": true,
                "AutoSellMode": 2,
                "AutoSellUseOccupiedSlots": true,
                "AutoSellOccupiedSlots": 111,
                "AutoSellUseBagPercent": true,
                "AutoSellBagPercent": 79,
                "AutoSellProtectGearsets": true,
                "PreferredSellNPC": { "DataId": 88 },
                "InDutyMaintenanceEnabled": true,
                "InDutyDurabilityEnabled": true,
                "InDutyDurabilityPercent": 17,
                "InDutyInventoryEnabled": true,
                "InDutyExtractBeforeSelling": true,
                "InDutyDesynthBeforeSelling": true,
                "InDutyReturnToInnAfterMaintenance": true
              }
            },
            { "Name": "Alt", "CIDs": [], "Config": {} }
          ],
          "RetiredEquipmentTransfers": [{ "CharacterId": 123, "ItemId": 999 }]
        }
        """;

        AutoDutyMigrationPreview preview = new AutoDutyMigrationImporter().Preview(json);

        Assert.True(preview.CanImport);
        AutoDutyMigrationSnapshot snapshot = Assert.IsType<AutoDutyMigrationSnapshot>(preview.Snapshot);
        Assert.Equal("Main", snapshot.DefaultProfileName);
        Assert.Equal(2, snapshot.Profiles.Count);
        AutoDutyProfileSnapshot profile = snapshot.Profiles[0];
        Assert.Equal([123UL, 456UL], profile.CharacterIds);
        Assert.True(profile.Overlay.ShowOverlay);
        Assert.True(profile.Overlay.HideWhenStopped);
        Assert.True(profile.Overlay.LockPosition);
        Assert.True(profile.Overlay.TransparentBackground);
        Assert.True(profile.Overlay.AnchorBottom);
        Assert.False(profile.Overlay.ShowRepair);
        Assert.True(profile.Maintenance.AutoBuyVendorGear);
        Assert.Equal(1_000_000u, profile.Maintenance.MinimumGilReserve);
        Assert.Equal(42u, profile.Maintenance.RepairBelowPercent);
        Assert.Contains("77", profile.Maintenance.PreferredRepairVendorJson);
        Assert.Equal("Protected", profile.Maintenance.CofferBlacklist[100]);
        Assert.Equal(9UL, profile.Maintenance.DesynthCategories);
        Assert.Equal("2", profile.Maintenance.AutoSellMode);
        Assert.Equal(17, profile.Maintenance.InDutyDurabilityPercent);
        Assert.True(profile.Maintenance.ReturnToInnAfterMaintenance);
        Assert.Single(snapshot.RetiredEquipmentTransfersJson);
    }

    [Fact]
    public void PreviewUsesSafeForkDefaultsForOlderProfiles()
    {
        AutoDutyMigrationPreview preview = new AutoDutyMigrationImporter().Preview(
            """{"DefaultConfigName":"Bare","profileData":[{"Name":"Bare","CIDs":[],"Config":{}}]}""");

        AutoDutyMaintenancePolicy policy = Assert.Single(preview.Snapshot!.Profiles).Maintenance;
        Assert.Equal(50u, policy.RepairBelowPercent);
        Assert.True(policy.ProtectGearsetsFromDesynth);
        Assert.True(policy.SellAtBagPercentThreshold);
        Assert.Equal(85, policy.SellBagPercentThreshold);
        Assert.True(policy.WithdrawForDurability);
        Assert.True(policy.WithdrawForInventory);
    }

    [Fact]
    public void PreviewRejectsDuplicateProfiles()
    {
        AutoDutyMigrationPreview preview = new AutoDutyMigrationImporter().Preview(
            """{"profileData":[{"Name":"Main","Config":{}},{"Name":"main","Config":{}}]}""");

        Assert.False(preview.CanImport);
        Assert.Contains(preview.Issues, issue => issue.Severity == MigrationIssueSeverity.Error && issue.Message.Contains("duplicated"));
    }
}
