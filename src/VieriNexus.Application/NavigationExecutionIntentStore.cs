using System.Text.Json;
using System.Text.Json.Serialization;

namespace VieriNexus.Application;

public enum NavigationExecutionIntentState
{
    Running,
    StopPending,
    AwaitingExplicitResume,
    Completed,
}

public sealed record NavigationExecutionIntent(
    int SchemaVersion,
    Guid ExecutionId,
    Guid RouteId,
    Guid LeaseId,
    NavigationExecutionIntentState State,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;
}

public interface INavigationExecutionIntentStore
{
    NavigationExecutionIntent? Load();

    void Save(NavigationExecutionIntent intent);
}

/// <summary>
/// Persists only the minimum navigation intent required to stop safely after a reload.
/// It deliberately stores no instruction pointer: recovery may stop and require a fresh
/// explicit decision, but can never replay movement from a stale position.
/// </summary>
public sealed class FileNavigationExecutionIntentStore(string path) : INavigationExecutionIntentStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public NavigationExecutionIntent? Load()
    {
        if (!File.Exists(path))
            return null;

        NavigationExecutionIntent intent = JsonSerializer.Deserialize<NavigationExecutionIntent>(
            File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidDataException("The navigation execution intent is empty.");
        Validate(intent);
        return intent;
    }

    public void Save(NavigationExecutionIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        Validate(intent);

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The navigation execution intent path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(intent, SerializerOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void Validate(NavigationExecutionIntent intent)
    {
        if (intent.SchemaVersion != NavigationExecutionIntent.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported navigation execution intent schema {intent.SchemaVersion}.");
        if (intent.ExecutionId == Guid.Empty)
            throw new InvalidDataException("The navigation execution ID is missing.");
        if (intent.RouteId == Guid.Empty)
            throw new InvalidDataException("The navigation route ID is missing.");
        if (intent.LeaseId == Guid.Empty)
            throw new InvalidDataException("The navigation lease ID is missing.");
        if (!Enum.IsDefined(intent.State))
            throw new InvalidDataException("The navigation execution intent state is invalid.");
    }
}
