using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class ProgressAtlasCatalogTests
{
    [Theory]
    [InlineData(0u, "A Realm Reborn")]
    [InlineData(1u, "Heavensward")]
    [InlineData(5u, "Dawntrail")]
    [InlineData(99u, "Other")]
    public void ExpansionNamesAreStable(uint id, string expected) =>
        Assert.Equal(expected, ProgressAtlasCatalog.ExpansionName(id));

    [Theory]
    [InlineData(ProgressionQuestKind.MainScenario, "Main Scenario Quests (MSQ)")]
    [InlineData(ProgressionQuestKind.ClassJobRole, "Class/Job/Role Quests")]
    [InlineData(ProgressionQuestKind.GeneralSideQuest, "Side Quests")]
    [InlineData(ProgressionQuestKind.AetherCurrent, "Aether Current Quests")]
    public void QuestLabelsMatchTheProductLanguage(ProgressionQuestKind kind, string expected) =>
        Assert.Equal(expected, ProgressAtlasCatalog.QuestCategoryName(kind));

    [Fact]
    public void ExpansionAndQuestOrderingMatchTheAtlasHierarchy()
    {
        Assert.True(ProgressAtlasCatalog.ExpansionOrder("A Realm Reborn") <
                    ProgressAtlasCatalog.ExpansionOrder("Dawntrail"));
        Assert.True(ProgressAtlasCatalog.QuestCategoryOrder(ProgressionQuestKind.MainScenario) <
                    ProgressAtlasCatalog.QuestCategoryOrder(ProgressionQuestKind.GeneralSideQuest));
    }
}
