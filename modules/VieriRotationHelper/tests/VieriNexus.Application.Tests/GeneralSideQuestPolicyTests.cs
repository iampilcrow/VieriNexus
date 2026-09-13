using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class GeneralSideQuestPolicyTests
{
    private static readonly IReadOnlySet<uint> ClassJobRoleChapters = new HashSet<uint> { 900 };

    [Fact]
    public void AcceptsOrdinaryNonRepeatableSideQuest()
    {
        Assert.True(GeneralSideQuestPolicy.IsGeneralSideQuest(
            6000, false, false, false, false, true, 100, ClassJobRoleChapters));
    }

    [Theory]
    [InlineData(0, false, false, false, false, true, 100)]
    [InlineData(6000, true, false, false, false, true, 100)]
    [InlineData(6000, false, true, false, false, true, 100)]
    [InlineData(6000, false, false, true, false, true, 100)]
    [InlineData(6000, false, false, false, true, true, 100)]
    [InlineData(6000, false, false, false, false, false, 100)]
    [InlineData(1744, false, false, false, false, true, 100)]
    [InlineData(6000, false, false, false, false, true, 900)]
    public void RejectsQuestFamiliesOwnedByOtherPolicies(
        uint questId,
        bool mainScenario,
        bool repeatable,
        bool seasonal,
        bool alliedSociety,
        bool hasJournalGenre,
        uint chapter)
    {
        Assert.False(GeneralSideQuestPolicy.IsGeneralSideQuest(
            questId,
            mainScenario,
            repeatable,
            seasonal,
            alliedSociety,
            hasJournalGenre,
            chapter,
            ClassJobRoleChapters));
    }
}
