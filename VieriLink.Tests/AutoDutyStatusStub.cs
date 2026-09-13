namespace VieriLink;

internal sealed class AutoDutyStatus
{
    public bool Available { get; set; }
    public ulong ContentId { get; set; }
    public string Job { get; set; } = string.Empty;
    public int Level { get; set; }
    public float DurabilityPercent { get; set; }
}
