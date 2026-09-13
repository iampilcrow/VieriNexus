using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace VieriLink.Windows;

internal sealed class SettingsWindow(Plugin plugin) : Window("VieriLink Settings###VieriLinkSettings")
{
    public override void OnOpen() => Size = new Vector2(650, 720);

    public override void Draw()
    {
        Configuration c = plugin.Config;
        bool enabled = c.Enabled;
        if (ImGui.Checkbox("Enable Discord connection", ref enabled)) { c.Enabled = enabled; plugin.Save(); }
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.ConnectionState);

        Section("Discord connection");
        string title = c.StatusTitle;
        if (ImGui.InputText("Status title", ref title, 100)) c.StatusTitle = title;
        string statusChannel = c.StatusChannelId;
        if (ImGui.InputText("Status Channel ID", ref statusChannel, 32))
        {
            statusChannel = statusChannel.Trim();
            if (!string.Equals(c.StatusChannelId, statusChannel, StringComparison.Ordinal))
            {
                c.StatusChannelId = statusChannel;
                c.StatusMessageId = string.Empty;
            }
        }
        ImGui.TextDisabled("Contains only the permanent live status card.");
        string commandChannel = c.CommandChannelId;
        if (ImGui.InputText("Commands & Notifications Channel ID", ref commandChannel, 32))
        {
            commandChannel = commandChannel.Trim();
            if (!string.Equals(c.CommandChannelId, commandChannel, StringComparison.Ordinal))
            {
                c.CommandChannelId = commandChannel;
                c.LastReadMessageId = string.Empty;
            }
        }
        ImGui.TextDisabled("Commands, command responses, and enabled alerts are posted here.");
        string users = c.AuthorizedUserIds;
        if (ImGui.InputTextMultiline("Authorized Discord user IDs", ref users, 500, new Vector2(-1, 65))) c.AuthorizedUserIds = users;
        ImGui.TextDisabled("Separate multiple user IDs with commas or spaces. Only these users can issue commands.");
        ImGui.InputText("Bot token", ref plugin.TokenInput, 200, ImGuiInputTextFlags.Password);
        if (ImGui.Button("Encrypt and save token")) plugin.SaveToken();
        ImGui.SameLine();
        ImGui.TextDisabled(string.IsNullOrWhiteSpace(c.EncryptedBotToken) ? "No token saved" : "Token encrypted for this Windows account");

        Section("Live status fields");
        Check("Character", c.ShowCharacter, v => c.ShowCharacter = v); Check("Location", c.ShowLocation, v => c.ShowLocation = v); Check("Job, level, and level progress", c.ShowJob, v => c.ShowJob = v); Check("Item level", c.ShowItemLevel, v => c.ShowItemLevel = v);
        Check("Current duty", c.ShowDuty, v => c.ShowDuty = v); Check("State / action", c.ShowState, v => c.ShowState = v); Check("Runs", c.ShowRuns, v => c.ShowRuns = v);
        Check("Inventory", c.ShowInventory, v => c.ShowInventory = v); Check("Durability", c.ShowDurability, v => c.ShowDurability = v); Check("Runtime", c.ShowRuntime, v => c.ShowRuntime = v);
        int statusSeconds = c.StatusSeconds;
        if (ImGui.SliderInt("Status refresh seconds", ref statusSeconds, 10, 120)) c.StatusSeconds = statusSeconds;

        Section("Notifications");
        Check("Every level gained", c.NotifyLevelUp, v => c.NotifyLevelUp = v);
        Check("Specific level reached", c.NotifyLevelTarget, v => c.NotifyLevelTarget = v);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        int targetLevel = c.LevelTarget;
        if (ImGui.InputInt("##LevelTarget", ref targetLevel)) c.LevelTarget = Math.Clamp(targetLevel, 1, 100);
        Check("Duty queue ready", c.NotifyQueueReady, v => c.NotifyQueueReady = v);
        Check("Duty completed", c.NotifyDutyCompleted, v => c.NotifyDutyCompleted = v);
        Check("Gear buying started and completed", c.NotifyGearBuying, v => c.NotifyGearBuying = v);
        Check("Character defeated", c.NotifyDeath, v => c.NotifyDeath = v);
        Check("Duty changed", c.NotifyDutyChanged, v => c.NotifyDutyChanged = v);
        Check("Automation stopped", c.NotifyStopped, v => c.NotifyStopped = v);
        Check("Low durability", c.NotifyLowDurability, v => c.NotifyLowDurability = v);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        int durability = c.DurabilityAlertPercent;
        if (ImGui.InputInt("%##DurabilityAlert", ref durability)) c.DurabilityAlertPercent = Math.Clamp(durability, 1, 99);
        Check("Inventory nearly full", c.NotifyHighInventory, v => c.NotifyHighInventory = v);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        int inventory = c.InventoryAlertPercent;
        if (ImGui.InputInt("%##InventoryAlert", ref inventory)) c.InventoryAlertPercent = Math.Clamp(inventory, 1, 100);

        Section("Remote command permissions");
        Check("Start, stop, pause, and resume Nexus", c.AllowRunStop, v => c.AllowRunStop = v);
        Check("Open protected selling review or run repair", c.AllowMaintenance, v => c.AllowMaintenance = v);
        Check("Change jobs through Fast Job Switcher", c.AllowJobChanges, v => c.AllowJobChanges = v);
        ImGui.TextWrapped("Commands use `!vieri`. Examples: `!vieri status`, `!vieri start`, `!vieri stop`, `!vieri resume`, `!vieri sell`, `!vieri repair`, and `!vieri job MCH`. Commands that need an exact in-game choice open or direct you to the matching Nexus review instead of guessing.");

        Section("Audit");
        ImGui.TextWrapped($"Last command: {plugin.LastCommand}");

        if (ImGui.Button("Save and refresh now")) plugin.Save();
    }

    private static void Check(string label, bool value, Action<bool> setter)
    {
        if (ImGui.Checkbox(label, ref value)) setter(value);
    }

    private static void Section(string label)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted(label);
    }
}
