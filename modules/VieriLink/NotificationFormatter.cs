namespace VieriLink;

internal static class NotificationFormatter
{
    internal static string DutyCompleted(string duty, int totalCompletions, long durationSeconds) =>
        $"🏁 **Duty complete:** {duty} — **Clear time:** {FormatDuration(durationSeconds)} — {totalCompletions} total recorded completion(s)";

    internal static string GearShoppingStarted(string trigger, int startingItemLevel)
    {
        string reason = string.IsNullOrWhiteSpace(trigger) ? "A useful current-job gear upgrade was detected" : trigger.Trim().TrimEnd('.');
        return $"🛍️ **Gear buying started.** Trigger: {reason}. Starting item level: {startingItemLevel}.";
    }

    internal static string GearShoppingCompleted(int purchased, int startingItemLevel, int endingItemLevel) =>
        purchased > 0
            ? $"🧰 **Gear buying complete:** {purchased} upgrade{(purchased == 1 ? string.Empty : "s")} purchased. **Item Level Changed From {startingItemLevel} to {endingItemLevel}.**"
            : $"🧰 **Gear buying complete:** no eligible upgrades were purchased. **Item level remained {endingItemLevel}.**";

    internal static string FormatDuration(long seconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds:00}s";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds:00}s";
        return $"{duration.Seconds}s";
    }
}
