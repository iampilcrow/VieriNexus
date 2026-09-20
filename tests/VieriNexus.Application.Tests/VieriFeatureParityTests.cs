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
}
