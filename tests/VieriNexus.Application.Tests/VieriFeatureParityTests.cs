using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class VieriFeatureParityTests
{
    [Theory]
    [InlineData("Main Scenario Quests (MSQ)")]
    [InlineData("Class/Job/Role Quests")]
    [InlineData("Aether Current quests")]
    [InlineData("Field Aether Currents")]
    [InlineData("Aetherytes & Aethernet")]
    [InlineData("World Exploration")]
    [InlineData("Hunting & Grand Company Logs")]
    [InlineData("Side Quests")]
    [InlineData("Achievements")]
    [InlineData("Expansion-organized quest and duty Progress Atlas")]
    [InlineData("Duty exploration")]
    [InlineData("One Click Navigation")]
    [InlineData("Multi-job automation queue")]
    public void CodexCustomFeaturesHaveExplicitNexusDestinations(string feature)
    {
        VieriFeatureContract contract = Assert.Single(VieriFeatureParity.All,
            item => item.SourceProduct == "VieriCodex" && item.Feature == feature);

        Assert.Equal(VieriFeatureOwnership.Nexus, contract.Ownership);
        Assert.False(string.IsNullOrWhiteSpace(contract.Destination));
    }

    [Theory]
    [InlineData("VieriRotationHelper", "Wrath Combo")]
    [InlineData("VieriAutoDuty", "AutoDuty")]
    [InlineData("VieriCodex", "Questionable")]
    [InlineData("VieriNavPlotter", "Lifestream and vnavmesh")]
    public void StockMechanicsRemainOwnedByUpdateableProviders(string source, string destination)
    {
        Assert.Contains(VieriFeatureParity.All, item =>
            item.SourceProduct == source &&
            item.Ownership == VieriFeatureOwnership.StockProvider &&
            item.Destination == destination);
    }

    [Fact]
    public void OperationsOverlayPreservesEveryVieriAutoDutyAction()
    {
        Assert.Equal(
            ["Barracks", "Inn", "GCSupply", "Flag Marker", "Summoning Bell", "Apartment",
                "Personal Home", "FC Estate", "Triple Triad Trader", "Striking Dummies"],
            VieriAutoDutyOverlayContract.GotoActions);
        Assert.Equal(["Shop for Upgrades", "Equip", "Repair", "Extract Materia", "Desynth"],
            VieriAutoDutyOverlayContract.GearActions);
        Assert.Equal(["Sell Inventory", "TurnIn", "Coffers", "Armoire"],
            VieriAutoDutyOverlayContract.InventoryActions);
        Assert.Equal(["Triple Triad", "Register TT Cards", "Sell TT Cards"],
            VieriAutoDutyOverlayContract.ExtraActions);
    }

    [Theory]
    [InlineData(1, "Maelstrom", 128, 177, 0, 536, 2007527)]
    [InlineData(2, "Twin Adder", 132, 179, 2, 534, 2006962)]
    [InlineData(3, "Immortal Flames", 130, 178, 1, 535, 2007529)]
    [InlineData(0, "Immortal Flames", 130, 178, 1, 535, 2007529)]
    public void GrandCompanyOverlayDestinationsMatchVieriAutoDuty(
        byte grandCompany,
        string name,
        uint headquarters,
        uint inn,
        int innShortcut,
        uint barracks,
        uint barracksDoor)
    {
        GrandCompanyOverlayDestination destination =
            VieriAutoDutyGrandCompanyContract.Resolve(grandCompany);

        Assert.Equal(name, destination.CompanyName);
        Assert.Equal(headquarters, destination.HeadquartersTerritoryId);
        Assert.Equal(inn, destination.InnTerritoryId);
        Assert.Equal(innShortcut, destination.InnShortcutIndex);
        Assert.Equal(barracks, destination.BarracksTerritoryId);
        Assert.Equal(barracksDoor, destination.BarracksDoorDataId);
    }

    [Fact]
    public void OverlayOverrideSwitchesRemainAuthoritativeLikeVieriAutoDuty()
    {
        AutoDutyProfileSnapshot profile = ProfileFrom("""
            "OverrideOverlayButtons": true,
            "GotoButton": false,
            "EquipButton": false,
            "RepairButton": false,
            "ExtractButton": false,
            "DesynthButton": false,
            "SellButton": false,
            "TurninButton": false,
            "CofferButton": false,
            "TTButton": false
            """);

        VieriAutoDutyOverlayButtonState state = VieriAutoDutyOverlayButtonPolicy.Evaluate(
            profile.Overlay, profile.Maintenance, true);

        Assert.False(state.Goto);
        Assert.False(state.Equip);
        Assert.False(state.Repair);
        Assert.False(state.Extract);
        Assert.False(state.Desynth);
        Assert.False(state.Sell);
        Assert.False(state.TurnIn);
        Assert.False(state.Coffers);
        Assert.False(state.TripleTriad);
    }

    [Fact]
    public void AutomaticSettingsGovernButtonsWhenOverrideIsOffLikeVieriAutoDuty()
    {
        AutoDutyProfileSnapshot profile = ProfileFrom("""
            "OverrideOverlayButtons": false,
            "AutoEquipRecommendedGear": false,
            "AutoRepair": false,
            "AutoExtract": false,
            "AutoDesynth": false,
            "AutoGCTurnin": false,
            "AutoOpenCoffers": false,
            "TripleTriadRegister": false,
            "TripleTriadSell": false
            """);

        VieriAutoDutyOverlayButtonState state = VieriAutoDutyOverlayButtonPolicy.Evaluate(
            profile.Overlay, profile.Maintenance, true);

        Assert.True(state.Goto);
        Assert.False(state.Equip);
        Assert.False(state.Repair);
        Assert.False(state.Extract);
        Assert.False(state.Desynth);
        Assert.True(state.Sell);
        Assert.False(state.TurnIn);
        Assert.False(state.Coffers);
        Assert.False(state.TripleTriad);
    }

    [Theory]
    [InlineData("VieriAvarice", "Combat/Rotation")]
    [InlineData("VieriDelvUI", "Custom UI")]
    [InlineData("VieriAutoMarket", "Market Helper")]
    [InlineData("VieriLink", "Communications")]
    public void CompleteEmbeddedInterfacesHaveNexusPages(string source, string destination)
    {
        Assert.Contains(VieriFeatureParity.All, item =>
            item.SourceProduct == source &&
            item.Ownership == VieriFeatureOwnership.EmbeddedNexusRuntime &&
            item.Destination == destination);
    }

    private static AutoDutyProfileSnapshot ProfileFrom(string settings)
    {
        AutoDutyMigrationPreview preview = new AutoDutyMigrationImporter().Preview($$"""
            {
              "DefaultConfigName": "Main",
              "profileData": [
                {
                  "Name": "Main",
                  "CIDs": [1],
                  "Config": {
                    {{settings}}
                  }
                }
              ]
            }
            """);
        Assert.True(preview.CanImport);
        return Assert.Single(preview.Snapshot!.Profiles);
    }
}
