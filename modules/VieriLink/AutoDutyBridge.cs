using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;

namespace VieriLink;

internal sealed class AutoDutyBridge
{
    private readonly ICallGateSubscriber<string> getStatus;
    private readonly ICallGateSubscriber<string, string, string> executeNexusCommand;

    public AutoDutyBridge(IDalamudPluginInterface pi)
    {
        getStatus = pi.GetIpcSubscriber<string>("VieriNexus.Link.V1.GetStatus");
        executeNexusCommand = pi.GetIpcSubscriber<string, string, string>("VieriNexus.Link.V1.Execute");
    }

    public AutoDutyStatus? ReadStatus()
    {
        try { return JsonConvert.DeserializeObject<AutoDutyStatus>(getStatus.InvokeFunc()); }
        catch { return null; }
    }

    public bool SetConfig(string name, string value)
    {
        return false;
    }

    public string? GetConfig(string name)
    {
        return null;
    }

    public bool Stop()
    {
        try { _ = executeNexusCommand.InvokeFunc("stop", string.Empty); return true; }
        catch { return false; }
    }

    public string Execute(string command, string argument = "")
    {
        try { return executeNexusCommand.InvokeFunc(command, argument); }
        catch (Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return $"VieriNexus did not accept the command: {ex.Message}";
        }
    }
}

internal sealed class AutoDutyStatus
{
    public bool Available { get; set; }
    public ulong ContentId { get; set; }
    public string Character { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
    public int Level { get; set; }
    public uint CurrentExperience { get; set; }
    public uint RequiredExperience { get; set; }
    public float LevelProgressPercent { get; set; }
    public int ItemLevel { get; set; }
    public string Duty { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool InCombat { get; set; }
    public int CurrentLoop { get; set; }
    public int ConfiguredLoops { get; set; }
    public int InventoryUsed { get; set; }
    public int InventoryTotal { get; set; }
    public float DurabilityPercent { get; set; }
    public long RuntimeSeconds { get; set; }
    public bool IsLooping { get; set; }
    public bool IsNavigating { get; set; }
    public bool IsPaused { get; set; }
    public bool IsStopped { get; set; }
    public bool InDutyQueue { get; set; }
    public long QueueReadySequence { get; set; }
    public DateTime LastQueueReadyUtc { get; set; }
    public int DutiesCompleted { get; set; }
    public int DeathsThisDuty { get; set; }
    public string LastCompletedDuty { get; set; } = string.Empty;
    public long LastDutyDurationSeconds { get; set; }
    public DateTime LastDutyCompletedUtc { get; set; }
    public long GearShoppingStartedSequence { get; set; }
    public long GearShoppingCompletedSequence { get; set; }
    public bool GearShoppingInProgress { get; set; }
    public string GearShoppingTrigger { get; set; } = string.Empty;
    public int GearShoppingStartingItemLevel { get; set; }
    public int GearShoppingEndingItemLevel { get; set; }
    public int GearShoppingItemsPurchased { get; set; }
}
