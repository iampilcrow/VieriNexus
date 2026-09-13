namespace VieriNexus.Domain;

public enum NexusTaskStatus
{
    Proposed,
    Waiting,
    Ready,
    Acquiring,
    Running,
    Verifying,
    Succeeded,
    Failed,
    NeedsReconciliation,
    Cancelling,
    Cancelled,
}

public enum FailureKind
{
    TransientExternal,
    RateLimited,
    PreconditionChanged,
    DependencyUnavailable,
    ResourceConflict,
    UserIntervention,
    UnsafeState,
    Unsupported,
    PermanentData,
    Cancelled,
}

public enum ResourceKind
{
    Movement,
    Navigation,
    Teleport,
    Targeting,
    Combat,
    Rotation,
    UiInteraction,
    DutyQueue,
    JobChange,
    InventoryMutation,
    Retainer,
    Market,
}

public sealed record TaskFailure(
    FailureKind Kind,
    string Code,
    string UserMessage,
    string? TechnicalDetail,
    bool IsRetryable);

public sealed record NexusTask(
    TaskId Id,
    GoalId GoalId,
    TaskKind Kind,
    int SchemaVersion,
    string Title,
    string Reason,
    CapabilityId Capability,
    ProviderId? Provider,
    IReadOnlySet<ResourceKind> RequiredResources,
    NexusTaskStatus Status,
    string PayloadJson,
    string? StatusDetail,
    TaskFailure? Failure);
