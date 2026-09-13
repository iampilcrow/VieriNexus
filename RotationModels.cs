namespace VieriRotationHelper;

internal enum RotationMode
{
    SingleTarget,
    Aoe,
    Dynamic,
}

internal enum PositionalKind
{
    None,
    Flank,
    Rear,
}

internal enum SuggestionSource
{
    None,
    LiveWrath,
    EmbeddedVieri,
}

internal readonly record struct RotationAnchor(
    byte JobId,
    string Job,
    uint SingleTargetAction,
    uint AoeAction,
    uint[] SingleTargetCombo,
    uint[] AoeCombo);

internal sealed record RotationSuggestion(
    uint ActionId,
    RotationMode Mode,
    SuggestionSource Source,
    bool Authoritative,
    string Reason,
    bool UsesEntryButton = false);

internal sealed record RotationFrame(
    RotationAnchor? Anchor,
    RotationSuggestion? Lead,
    IReadOnlyList<RotationSuggestion> Forecast,
    bool WrathLoaded,
    bool ParityVerified,
    RotationMode EffectiveMode,
    int EnemyCount,
    int ActionEnemyCount,
    string Status);

internal readonly record struct TargetSnapshot(int EnemyCount, float? RangeToTarget);

internal readonly record struct ActionInfo(string Name, uint Icon, int Range, int Radius);
