using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class ClassJobRoleQuestPolicyTests
{
    [Fact]
    public void MachinistIncludesJobAndPhysicalRangedRoleChains()
    {
        IReadOnlyList<uint> chapters = ClassJobRoleQuestPolicy.Chapters(31);

        Assert.Equal([117u, 118u, 119u, 138u, 157u, 181u], chapters);
    }

    [Fact]
    public void CombatRolesReceiveOnlyTheirApplicableRoleChains()
    {
        Assert.Equal([72u, 73u, 74u, 136u, 154u, 178u], ClassJobRoleQuestPolicy.Chapters(19));
        Assert.Equal([86u, 87u, 88u, 137u, 155u, 179u], ClassJobRoleQuestPolicy.Chapters(24));
        Assert.Equal([123u, 124u, 125u, 139u, 158u, 182u], ClassJobRoleQuestPolicy.Chapters(25));
    }

    [Fact]
    public void CraftingGatheringAndLimitedJobsKeepTheirOwnChains()
    {
        Assert.Equal([30u, 31u, 32u], ClassJobRoleQuestPolicy.Chapters(8));
        Assert.Equal([54u, 55u, 56u], ClassJobRoleQuestPolicy.Chapters(16));
        Assert.Equal([134u, 135u, 146u, 170u], ClassJobRoleQuestPolicy.Chapters(36));
        Assert.Equal([206u], ClassJobRoleQuestPolicy.Chapters(43));
    }

    [Fact]
    public void UnknownJobHasNoQuestFamily()
    {
        Assert.Empty(ClassJobRoleQuestPolicy.Chapters(999));
    }
}
