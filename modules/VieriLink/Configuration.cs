using Dalamud.Configuration;
using Dalamud.Plugin;

namespace VieriLink;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public bool Enabled { get; set; }
    public string StatusTitle { get; set; } = "Vieri Nexus";
    public string EncryptedBotToken { get; set; } = string.Empty;
    // Retained only to migrate installations from the original single-channel layout.
    public string ChannelId { get; set; } = string.Empty;
    public string StatusChannelId { get; set; } = string.Empty;
    public string CommandChannelId { get; set; } = string.Empty;
    public string AuthorizedUserIds { get; set; } = string.Empty;
    public string StatusMessageId { get; set; } = string.Empty;
    public string LastReadMessageId { get; set; } = string.Empty;
    public int StatusSeconds { get; set; } = 15;
    public int CommandPollSeconds { get; set; } = 3;

    public bool ShowCharacter { get; set; } = true;
    public bool ShowLocation { get; set; } = true;
    public bool ShowJob { get; set; } = true;
    public bool ShowItemLevel { get; set; } = true;
    public bool ShowDuty { get; set; } = true;
    public bool ShowState { get; set; } = true;
    public bool ShowRuns { get; set; } = true;
    public bool ShowInventory { get; set; } = true;
    public bool ShowDurability { get; set; } = true;
    public bool ShowRuntime { get; set; } = true;

    public bool NotifyLevelUp { get; set; } = true;
    public bool NotifyLevelTarget { get; set; }
    public int LevelTarget { get; set; } = 100;
    public bool NotifyQueueReady { get; set; } = true;
    public bool NotifyDutyCompleted { get; set; } = true;
    public bool NotifyGearBuying { get; set; } = true;
    public bool NotifyDeath { get; set; } = true;
    public bool NotifyDutyChanged { get; set; } = true;
    public bool NotifyStopped { get; set; } = true;
    public bool NotifyLowDurability { get; set; } = true;
    public int DurabilityAlertPercent { get; set; } = 25;
    public bool NotifyHighInventory { get; set; } = true;
    public int InventoryAlertPercent { get; set; } = 90;
    public bool AllowRunStop { get; set; } = true;
    public bool AllowLoopChanges { get; set; } = true;
    public bool AllowMaintenance { get; set; } = true;
    public bool AllowJobChanges { get; set; } = true;
    public bool AllowConfigChanges { get; set; }

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        pluginInterface = pi;
        if (Version < 3)
            ShowLocation = true;
        if (string.IsNullOrWhiteSpace(StatusChannelId))
            StatusChannelId = ChannelId;
        if (string.IsNullOrWhiteSpace(CommandChannelId))
            CommandChannelId = ChannelId;
        Version = 4;
        StatusSeconds = Math.Clamp(StatusSeconds, 10, 300);
        CommandPollSeconds = Math.Clamp(CommandPollSeconds, 2, 60);
        LevelTarget = Math.Clamp(LevelTarget, 1, 100);
        DurabilityAlertPercent = Math.Clamp(DurabilityAlertPercent, 1, 99);
        InventoryAlertPercent = Math.Clamp(InventoryAlertPercent, 1, 100);
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);
}
