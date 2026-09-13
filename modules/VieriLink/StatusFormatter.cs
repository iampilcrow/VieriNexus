namespace VieriLink;

internal static class StatusFormatter
{
    public static object Build(Configuration c, AutoDutyStatus? s)
    {
        if (s is null || !s.Available)
            return Embed(c.StatusTitle, "⚫ Offline", 0x747f8d, [("Connection", "FFXIV or VieriNexus is unavailable")]);

        string indicator = s.IsPaused ? "🟡 Paused" : s.IsStopped ? "🔴 Stopped" : "🟢 Running";
        List<(string, string)> fields = [];
        if (c.ShowCharacter) fields.Add(("Character", s.Character));
        if (c.ShowLocation) fields.Add(("Location", Empty(s.Location, "Location unavailable")));
        if (c.ShowJob) fields.Add(("Job", $"{FriendlyJob(s.Job)} — Level {s.Level}"));
        if (c.ShowItemLevel) fields.Add(("Item Level", s.ItemLevel.ToString()));
        if (c.ShowDuty) fields.Add(("Current Duty", Empty(s.Duty, "Not in a configured duty")));
        if (c.ShowState) fields.Add(("State", DescribeState(s)));
        if (c.ShowRuns) fields.Add(("Duties", $"{s.DutiesCompleted} completed this Nexus session"));
        if (c.ShowInventory) fields.Add(("Inventory", $"{s.InventoryUsed} / {s.InventoryTotal}"));
        if (c.ShowDurability) fields.Add(("Durability", $"{s.DurabilityPercent:0}%"));
        if (c.ShowRuntime && s.RuntimeSeconds > 0) fields.Add(("Runtime", FormatDuration(s.RuntimeSeconds)));
        return Embed(c.StatusTitle, indicator, s.IsPaused ? 0xfee75c : s.IsStopped ? 0xed4245 : 0x57f287, fields);
    }

    private static object Embed(string title, string description, int color, IEnumerable<(string Name, string Value)> fields) => new
    {
        content = string.Empty,
        allowed_mentions = new { parse = Array.Empty<string>() },
        embeds = new[] { new { title, description, color, fields = fields.Select(x => new { name = x.Name, value = x.Value, inline = false }).ToArray(), timestamp = DateTimeOffset.UtcNow } }
    };

    private static string DescribeState(AutoDutyStatus s)
    {
        if (s.InCombat) return string.IsNullOrWhiteSpace(s.Action) ? "In combat" : $"In combat — {s.Action}";
        if (s.IsPaused) return "Paused";
        return Empty(s.Action, s.Stage);
    }

    private static string FriendlyJob(string job) => job.Replace('_', ' ');
    private static string Empty(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string FormatDuration(long seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";
    }
}
