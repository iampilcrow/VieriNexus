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
}
