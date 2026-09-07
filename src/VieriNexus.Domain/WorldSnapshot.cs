namespace VieriNexus.Domain;

public enum KnowledgeState
{
    Known,
    Unknown,
    Stale,
    Unavailable,
}

public readonly record struct Observed<T>(T? Value, KnowledgeState State, DateTimeOffset CapturedAt)
{
    public static Observed<T> Known(T value, DateTimeOffset at) => new(value, KnowledgeState.Known, at);
    public static Observed<T> Unknown(DateTimeOffset at) => new(default, KnowledgeState.Unknown, at);
}

public sealed record SessionSnapshot(
    bool IsLoggedIn,
    bool IsLoading,
    bool IsBetweenAreas,
    bool IsPlayerAvailable,
    uint TerritoryId);

public sealed record CharacterSnapshot(
    CharacterKey Key,
    string Name,
    uint ClassJobId,
    int Level,
    bool IsInCombat);

public sealed record ProviderHealthSnapshot(
    ProviderId Id,
    string DisplayName,
    string State,
    string? Version,
    string Detail);

public sealed record WorldSnapshot(
    long Revision,
    DateTimeOffset CapturedAt,
    SessionSnapshot Session,
    Observed<CharacterSnapshot> Character,
    IReadOnlyDictionary<ProviderId, ProviderHealthSnapshot> Providers)
{
    public static WorldSnapshot Empty { get; } = new(
        0,
        DateTimeOffset.MinValue,
        new(false, true, false, false, 0),
        Observed<CharacterSnapshot>.Unknown(DateTimeOffset.MinValue),
        new Dictionary<ProviderId, ProviderHealthSnapshot>());
}
