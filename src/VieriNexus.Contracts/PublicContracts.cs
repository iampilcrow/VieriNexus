namespace VieriNexus.Contracts;

public static class NexusIpc
{
    public const int CurrentVersion = 1;
    public const string GetStatus = "VieriNexus.Status.V1.Get";
    public const string ExecuteCommand = "VieriNexus.Commands.V1.Execute";
    public const string GetDependencies = "VieriNexus.Dependencies.V1.Get";
}

public sealed record NexusStatusDto(
    int ContractVersion,
    bool IsReady,
    bool IsPaused,
    string State,
    string? Goal,
    string? CurrentTask,
    string? Reason,
    string? Provider,
    string? NextTask,
    string? BlockedReason,
    long SnapshotRevision);

public sealed record NexusCommandDto(
    int ContractVersion,
    Guid RequestId,
    string Command,
    string PayloadJson,
    ulong? CharacterContentId);

public sealed record NexusCommandResultDto(
    Guid RequestId,
    bool Accepted,
    string Code,
    string Message);

public sealed record DependencyDto(
    string Id,
    string Name,
    string State,
    string? Version,
    bool Required,
    string Capability,
    string Detail);
