namespace VieriNexus.Application;

public enum ProgressAtlasCategoryId
{
    Aetherytes,
    AetherCurrents,
    Achievements,
    HuntingLogs,
    Exploration,
}

public sealed record ProgressAtlasCategorySnapshot(
    ProgressAtlasCategoryId Id,
    string Name,
    int Completed,
    int Total,
    bool IsLoaded,
    string Detail)
{
    public int Remaining => Math.Max(0, Total - Completed);
    public float Completion => Total <= 0 ? 0f : Math.Clamp((float)Completed / Total, 0f, 1f);
}

public sealed record ProgressAtlasSnapshot(
    DateTimeOffset CapturedAt,
    bool IsCharacterAvailable,
    IReadOnlyList<ProgressAtlasCategorySnapshot> Categories)
{
    public int Completed => Categories.Where(category => category.IsLoaded).Sum(category => category.Completed);
    public int Total => Categories.Where(category => category.IsLoaded).Sum(category => category.Total);
    public int Remaining => Math.Max(0, Total - Completed);
}

public static class ProgressAtlasModel
{
    public static ProgressAtlasCategorySnapshot Category(
        ProgressAtlasCategoryId id,
        string name,
        int completed,
        int total,
        bool isLoaded,
        string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(detail);
        if (completed < 0)
            throw new ArgumentOutOfRangeException(nameof(completed));
        if (total < 0)
            throw new ArgumentOutOfRangeException(nameof(total));
        if (completed > total)
            throw new ArgumentException("Completed progress cannot exceed the category total.", nameof(completed));

        return new ProgressAtlasCategorySnapshot(id, name, completed, total, isLoaded, detail);
    }

    public static int CompletedHuntingLogRanks(int currentRank, int rankCount) =>
        rankCount <= 0 ? 0 : Math.Clamp(currentRank, 0, rankCount);

    public static int HuntingLogKills(int currentRank, int targetRank, int observedKills, int requiredKills)
    {
        if (targetRank < 0)
            throw new ArgumentOutOfRangeException(nameof(targetRank));
        if (requiredKills < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredKills));
        if (currentRank > targetRank)
            return requiredKills;
        if (currentRank < targetRank)
            return 0;
        return Math.Clamp(observedKills, 0, requiredKills);
    }
}
