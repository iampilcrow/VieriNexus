namespace VieriLink;

using Xunit;

public sealed class LevelNotificationPolicyTests
{
    [Fact]
    public void AcceptsPermanentLevelIncreaseForSameCharacterAndJob()
    {
        Assert.True(LevelNotificationPolicy.IsGenuineIncrease(Status(81), Status(82)));
    }

    [Fact]
    public void RejectsUnchangedLevel()
    {
        Assert.False(LevelNotificationPolicy.IsGenuineIncrease(Status(100), Status(100)));
    }

    [Fact]
    public void RejectsJobChange()
    {
        Assert.False(LevelNotificationPolicy.IsGenuineIncrease(Status(81, job: "BRD"), Status(100, job: "SAM")));
    }

    [Fact]
    public void RejectsCharacterChange()
    {
        Assert.False(LevelNotificationPolicy.IsGenuineIncrease(Status(81, contentId: 1), Status(82, contentId: 2)));
    }

    [Fact]
    public void RejectsLoginTransition()
    {
        AutoDutyStatus previous = Status(0);
        previous.Available = false;
        Assert.False(LevelNotificationPolicy.IsGenuineIncrease(previous, Status(82)));
    }

    private static AutoDutyStatus Status(int level, ulong contentId = 1, string job = "BRD") => new()
    {
        Available = true,
        ContentId = contentId,
        Job = job,
        Level = level,
    };
}
