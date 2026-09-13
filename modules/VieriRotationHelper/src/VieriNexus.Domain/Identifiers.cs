namespace VieriNexus.Domain;

public readonly record struct GoalId(Guid Value)
{
    public static GoalId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct TaskId(Guid Value)
{
    public static TaskId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct AttemptId(Guid Value)
{
    public static AttemptId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct CharacterKey(ulong ContentId, uint HomeWorldId)
{
    public bool IsKnown => ContentId != 0 && HomeWorldId != 0;
    public override string ToString() => IsKnown ? $"{ContentId:X16}:{HomeWorldId}" : "unknown";
}

public readonly record struct GoalKind(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct TaskKind(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct CapabilityId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ProviderId(string Value)
{
    public override string ToString() => Value;
}
