namespace VieriLink;

using Xunit;

public sealed class DurabilityNotificationPolicyTests
{
    [Fact]
    public void AcceptsRealThresholdCrossingForAvailableCharacter()
    {
        Assert.True(DurabilityNotificationPolicy.IsGenuineDrop(Status(61f), Status(20f), 25f));
    }

    [Fact]
    public void RejectsLoadingScreenZero()
    {
        AutoDutyStatus loading = Status(0f);
        loading.Available = false;

        Assert.False(DurabilityNotificationPolicy.IsGenuineDrop(Status(61f), loading, 25f));
    }

    [Fact]
    public void RejectsFirstSampleAfterLoading()
    {
        AutoDutyStatus loading = Status(0f);
        loading.Available = false;

        Assert.False(DurabilityNotificationPolicy.IsGenuineDrop(loading, Status(20f), 25f));
    }

    [Fact]
    public void RejectsCharacterChange()
    {
        Assert.False(DurabilityNotificationPolicy.IsGenuineDrop(Status(61f, 1), Status(0f, 2), 25f));
    }

    private static AutoDutyStatus Status(float durability, ulong contentId = 1) => new()
    {
        Available = true,
        ContentId = contentId,
        DurabilityPercent = durability,
    };
}
