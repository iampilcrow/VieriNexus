namespace VieriNexus.Domain;

public enum GoalStatus
{
    Draft,
    Ready,
    Active,
    Paused,
    Blocked,
    Satisfied,
    Cancelled,
}

public enum ConstraintStrength
{
    Preference,
    Required,
}

public sealed record GoalConstraint(
    string Kind,
    ConstraintStrength Strength,
    string Value,
    string Explanation);

public sealed record NexusGoal(
    GoalId Id,
    GoalKind Kind,
    int SchemaVersion,
    CharacterKey Character,
    string Title,
    string DesiredStateJson,
    IReadOnlyList<GoalConstraint> Constraints,
    int Priority,
    GoalStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long PlanRevision,
    string? StatusDetail);
