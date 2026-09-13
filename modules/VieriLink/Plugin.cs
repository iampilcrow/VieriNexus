using System.Globalization;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VieriLink.Windows;

namespace VieriLink;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    internal Configuration Config { get; }
    internal string ConnectionState { get; private set; } = "Not configured";
    internal string LastCommand { get; private set; } = "None";
    internal string TokenInput = string.Empty;

    private readonly WindowSystem windows = new("VieriLink");
    private readonly SettingsWindow settings;
    private readonly DiscordClient discord = new();
    private readonly AutoDutyBridge autoDuty;
    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentQueue<PendingDiscordCommand> commandQueue = new();
    private DateTime nextStatus = DateTime.MinValue;
    private DateTime nextPoll = DateTime.MinValue;
    private int tickRunning;
    private int saveRequested;
    private AutoDutyStatus? previous;

    public Plugin()
    {
        Config = Pi.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(Pi);
        autoDuty = new AutoDutyBridge(Pi);
        settings = new SettingsWindow(this);
        windows.AddWindow(settings);
        Commands.AddHandler("/vierilink", new CommandInfo((_, _) => settings.Toggle()) { HelpMessage = "Open VieriLink settings." });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenConfigUi += OpenConfig;
        Pi.UiBuilder.OpenMainUi += OpenConfig;
    }

    public void Dispose()
    {
        shutdown.Cancel();
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenConfigUi -= OpenConfig;
        Pi.UiBuilder.OpenMainUi -= OpenConfig;
        Commands.RemoveHandler("/vierilink");
        windows.RemoveAllWindows();
        discord.Dispose();
        shutdown.Dispose();
        Config.Save();
    }

    private void OpenConfig() => settings.IsOpen = true;

    private void Draw()
    {
        windows.Draw();
        if (Interlocked.Exchange(ref saveRequested, 0) != 0)
            Config.Save();
        ProcessCommandQueue();
        if (!Config.Enabled || DateTime.UtcNow < nextPoll && DateTime.UtcNow < nextStatus ||
            Interlocked.CompareExchange(ref tickRunning, 1, 0) != 0)
            return;

        DateTime now = DateTime.UtcNow;
        bool updateStatus = now >= nextStatus;
        bool pollCommands = now >= nextPoll;
        if (updateStatus) nextStatus = now.AddSeconds(Config.StatusSeconds);
        if (pollCommands) nextPoll = now.AddSeconds(Config.CommandPollSeconds);
        AutoDutyStatus? status = autoDuty.ReadStatus();
        _ = TickAsync(status, updateStatus, pollCommands);
    }

    private async Task TickAsync(AutoDutyStatus? status, bool updateStatus, bool pollCommands)
    {
        try
        {
            string token = TokenProtector.Unprotect(Config.EncryptedBotToken);
            if (string.IsNullOrWhiteSpace(token) ||
                !ulong.TryParse(Config.StatusChannelId, out _) ||
                !ulong.TryParse(Config.CommandChannelId, out _))
            {
                ConnectionState = "Bot token or one of the channel IDs is missing";
                return;
            }

            discord.SetToken(token);
            if (updateStatus)
            {
                await UpdateStatusAsync(status);
                await SendNotificationsAsync(status);
            }

            if (pollCommands)
            {
                await PollCommandsAsync();
            }
            ConnectionState = "Connected";
        }
        catch (Exception ex)
        {
            ConnectionState = ex.Message;
            Log.Warning(ex, "VieriLink Discord update failed");
        }
        finally { Interlocked.Exchange(ref tickRunning, 0); }
    }

    private async Task UpdateStatusAsync(AutoDutyStatus? status)
    {
        object payload = StatusFormatter.Build(Config, status);
        if (string.IsNullOrWhiteSpace(Config.StatusMessageId))
        {
            Config.StatusMessageId = await discord.FindStatusMessageAsync(Config.StatusChannelId, Config.StatusTitle, shutdown.Token)
                ?? await discord.SendAsync(Config.StatusChannelId, payload, shutdown.Token);
            RequestSave();
            await discord.EditAsync(Config.StatusChannelId, Config.StatusMessageId, payload, shutdown.Token);
        }
        else
        {
            try { await discord.EditAsync(Config.StatusChannelId, Config.StatusMessageId, payload, shutdown.Token); }
            catch (DiscordApiException ex) when (ex.IsUnknownMessage)
            {
                Config.StatusMessageId = await discord.FindStatusMessageAsync(Config.StatusChannelId, Config.StatusTitle, shutdown.Token)
                    ?? await discord.SendAsync(Config.StatusChannelId, payload, shutdown.Token);
                RequestSave();
                await discord.EditAsync(Config.StatusChannelId, Config.StatusMessageId, payload, shutdown.Token);
            }
        }
    }

    private async Task SendNotificationsAsync(AutoDutyStatus? current)
    {
        if (current is null || previous is null) { previous = current; return; }
        bool genuineLevelIncrease = LevelNotificationPolicy.IsGenuineIncrease(previous, current);
        if (Config.NotifyLevelUp && genuineLevelIncrease)
            await Notify($"🎉 **{current.Character} reached level {current.Level} on {current.Job}!**");
        if (Config.NotifyLevelTarget && genuineLevelIncrease && current.Level >= Config.LevelTarget && previous.Level < Config.LevelTarget)
            await Notify($"🏆 **{current.Character} reached the configured level target: {Config.LevelTarget}.**");
        if (Config.NotifyQueueReady && current.QueueReadySequence > previous.QueueReadySequence)
            await Notify($"✅ **Queue ready:** {Empty(current.Duty, "A duty is ready to enter")}");
        if (Config.NotifyDutyCompleted && current.DutiesCompleted > previous.DutiesCompleted)
            await Notify(NotificationFormatter.DutyCompleted(
                Empty(current.LastCompletedDuty, Empty(previous.Duty, current.Duty)),
                current.DutiesCompleted,
                current.LastDutyDurationSeconds));
        if (Config.NotifyGearBuying && current.GearShoppingStartedSequence > previous.GearShoppingStartedSequence)
            await Notify(NotificationFormatter.GearShoppingStarted(current.GearShoppingTrigger, current.GearShoppingStartingItemLevel));
        if (Config.NotifyGearBuying && current.GearShoppingCompletedSequence > previous.GearShoppingCompletedSequence)
            await Notify(NotificationFormatter.GearShoppingCompleted(current.GearShoppingItemsPurchased,
                current.GearShoppingStartingItemLevel, current.GearShoppingEndingItemLevel));
        if (Config.NotifyDeath && current.DeathsThisDuty > previous.DeathsThisDuty)
            await Notify($"💀 **{current.Character} was defeated.** Deaths this duty: {current.DeathsThisDuty}");
        if (Config.NotifyDutyChanged && !string.Equals(current.Duty, previous.Duty, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(current.Duty))
            await Notify($"🗺️ **Current duty:** {current.Duty}");
        if (Config.NotifyStopped && current.IsStopped && !previous.IsStopped)
            await Notify($"🔴 **VieriAutoDuty stopped.** Last state: {current.Action}");
        float currentInventoryPercent = current.InventoryTotal <= 0 ? 0 : current.InventoryUsed * 100f / current.InventoryTotal;
        float previousInventoryPercent = previous.InventoryTotal <= 0 ? 0 : previous.InventoryUsed * 100f / previous.InventoryTotal;
        if (Config.NotifyHighInventory && currentInventoryPercent >= Config.InventoryAlertPercent && previousInventoryPercent < Config.InventoryAlertPercent)
            await Notify($"🎒 **Inventory alert:** {current.InventoryUsed} / {current.InventoryTotal} slots used ({currentInventoryPercent:0}%).");
        if (Config.NotifyLowDurability && DurabilityNotificationPolicy.IsGenuineDrop(previous, current, Config.DurabilityAlertPercent))
            await Notify($"🔧 **Durability alert:** lowest equipped item is at {current.DurabilityPercent:0}%.");
        previous = current;
    }

    private async Task PollCommandsAsync()
    {
        List<DiscordMessage> messages = await discord.ReadAfterAsync(Config.CommandChannelId, Config.LastReadMessageId, shutdown.Token);
        if (string.IsNullOrWhiteSpace(Config.LastReadMessageId))
        {
            Config.LastReadMessageId = messages
                .Select(x => x.Id)
                .Where(x => ulong.TryParse(x, out _))
                .MaxBy(x => ulong.Parse(x)) ?? string.Empty;
            RequestSave();
            return;
        }
        foreach (DiscordMessage message in messages.OrderBy(x => ulong.TryParse(x.Id, out ulong id) ? id : 0))
        {
            Config.LastReadMessageId = message.Id;
            if (message.Author.Bot || !IsAuthorized(message.Author.Id) || !message.Content.TrimStart().StartsWith("!vieri", StringComparison.OrdinalIgnoreCase)) continue;
            commandQueue.Enqueue(new PendingDiscordCommand(message.Author.Username, message.Content));
        }
        RequestSave();
    }

    private bool IsAuthorized(string userId) => Config.AuthorizedUserIds.Split([',', ';', ' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Contains(userId, StringComparer.Ordinal);

    private string Execute(string input)
    {
        string[] p = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 2) return CommandHelp();
        string cmd = p[1].ToLowerInvariant();
        if (cmd == "help") return CommandHelp();
        if (cmd == "status") { nextStatus = DateTime.MinValue; return "Live status refresh requested."; }
        if (cmd is "start" or "stop" or "pause" or "resume" or "leave")
        {
            if (!Config.AllowRunStop) return "Run controls are disabled in VieriLink settings.";
            return autoDuty.Execute(cmd);
        }
        if (cmd == "loops")
        {
            if (!Config.AllowLoopChanges) return "Loop changes are disabled in VieriLink settings.";
            if (p.Length < 3 || !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int loops)) return "Use `!vieri loops 10`, `!vieri loops +5`, or `!vieri loops -2`.";
            if ((p[2].StartsWith('+') || p[2].StartsWith('-')) && int.TryParse(autoDuty.GetConfig("LoopTimes"), out int currentLoops))
                loops += currentLoops;
            return autoDuty.Execute("loops", loops.ToString(CultureInfo.InvariantCulture));
        }
        if (cmd is "sell" or "repair" or "inn")
        {
            if (!Config.AllowMaintenance) return "Maintenance commands are disabled in VieriLink settings.";
            return autoDuty.Execute(cmd);
        }
        if (cmd == "job")
        {
            if (!Config.AllowJobChanges) return "Remote job changes are disabled in VieriLink settings.";
            if (p.Length < 3) return "Use `!vieri job 3` or `!vieri job Gearset Name`.";
            return autoDuty.Execute("job", string.Join(' ', p[2..]));
        }
        if (cmd == "get")
        {
            if (p.Length < 3) return "Use `!vieri get SettingName`.";
            string? value = autoDuty.GetConfig(p[2]);
            return value is null ? $"Could not read {p[2]}." : $"{p[2]} = {value}";
        }
        if (cmd is "config" or "set")
        {
            if (!Config.AllowConfigChanges) return "Remote configuration changes are disabled in VieriLink settings.";
            if (p.Length < 4) return "Use `!vieri config SettingName value`.";
            return autoDuty.SetConfig(p[2], string.Join(' ', p[3..])) ? $"Updated {p[2]}." : $"Could not update {p[2]}.";
        }
        return "Unknown command. Use `!vieri` for the command list.";
    }

    private static string CommandHelp() =>
        "Commands: `status`, `start`, `stop`, `pause`, `resume`, `leave`, `loops 10`, `loops +5`, `sell`, `repair`, `inn`, `job 3`, `get SettingName`, and—when enabled—`set SettingName value`. `leave` stops automation and exits the duty after combat ends.";

    private async Task Notify(string text) =>
        _ = await discord.SendAsync(Config.CommandChannelId, new { content = text, allowed_mentions = new { parse = Array.Empty<string>() } }, shutdown.Token);

    private void ProcessCommandQueue()
    {
        for (int i = 0; i < 5 && commandQueue.TryDequeue(out PendingDiscordCommand? pending); i++)
        {
            string result;
            try { result = Execute(pending.Content); }
            catch (Exception ex)
            {
                Log.Error(ex, "VieriLink command failed");
                result = $"Command failed: {ex.Message}";
            }
            LastCommand = $"{pending.Username}: {pending.Content} → {result}";
            _ = Notify($"✅ `{pending.Content}`\n{result}");
        }
    }

    private void RequestSave() => Interlocked.Exchange(ref saveRequested, 1);
    private static string Empty(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    internal void SaveToken()
    {
        Config.EncryptedBotToken = TokenProtector.Protect(TokenInput);
        TokenInput = string.Empty;
        Config.LastReadMessageId = string.Empty;
        Config.Save();
        nextPoll = nextStatus = DateTime.MinValue;
    }

    internal void Save()
    {
        Config.Save();
        nextPoll = nextStatus = DateTime.MinValue;
    }
}

internal sealed record PendingDiscordCommand(string Username, string Content);
