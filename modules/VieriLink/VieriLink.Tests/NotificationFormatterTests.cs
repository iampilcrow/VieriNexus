using Xunit;

namespace VieriLink.Tests;

public sealed class NotificationFormatterTests
{
    [Theory]
    [InlineData(42, "42s")]
    [InlineData(785, "13m 05s")]
    [InlineData(7384, "2h 3m 04s")]
    public void ClearDurationIsReadable(long seconds, string expected) =>
        Assert.Equal(expected, NotificationFormatter.FormatDuration(seconds));

    [Fact]
    public void DutyCompletionIncludesClearTime() => Assert.Equal(
        "🏁 **Duty complete:** The Vault — **Clear time:** 13m 05s — 4 total recorded completion(s)",
        NotificationFormatter.DutyCompleted("The Vault", 4, 785));

    [Fact]
    public void GearMessagesExplainTriggerAndItemLevelChange()
    {
        Assert.Equal(
            "🛍️ **Gear buying started.** Trigger: The Aery requires item level 110. Starting item level: 96.",
            NotificationFormatter.GearShoppingStarted("The Aery requires item level 110", 96));
        Assert.Equal(
            "🧰 **Gear buying complete:** 6 upgrades purchased. **Item Level Changed From 96 to 139.**",
            NotificationFormatter.GearShoppingCompleted(6, 96, 139));
    }
}
