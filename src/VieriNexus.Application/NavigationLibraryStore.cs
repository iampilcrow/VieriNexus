using System.Text.Json;

namespace VieriNexus.Application;

public sealed record NavigationLibraryWriteResult(
    bool Success,
    string Message,
    NavigationLibrarySnapshot? Snapshot = null);

public interface INavigationLibraryStore
{
    NavigationLibrarySnapshot? Load();

    void Save(NavigationLibrarySnapshot snapshot);
}

/// <summary>
/// Stores the Nexus-owned working route library separately from immutable migration staging.
/// Each replacement is atomic and retains the previous working copy for recovery.
/// </summary>
public sealed class FileNavigationLibraryStore(string path) : INavigationLibraryStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public NavigationLibrarySnapshot? Load()
    {
        if (!File.Exists(path))
            return null;

        NavigationLibrarySnapshot snapshot = JsonSerializer.Deserialize<NavigationLibrarySnapshot>(
            File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The Nexus route library is empty.");
        Validate(snapshot);
        return snapshot;
    }

    public void Save(NavigationLibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The Nexus route-library path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        string backupPath = path + ".previous";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, snapshot, Options);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Copy(path, backupPath, overwrite: true);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void Validate(NavigationLibrarySnapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported Nexus route-library schema {snapshot.SchemaVersion}.");
        if (!float.IsFinite(snapshot.RecordingIntervalSeconds))
            throw new InvalidDataException("The Nexus route-library capture interval is invalid.");
        if (!float.IsFinite(snapshot.MinimumPointDistance))
            throw new InvalidDataException("The Nexus route-library point spacing is invalid.");
        if (snapshot.Routes.Select(route => route.Id).Distinct().Count() != snapshot.Routes.Count)
            throw new InvalidDataException("The Nexus route library contains duplicate route IDs.");
        foreach (NavigationRouteSnapshot route in snapshot.Routes)
        {
            if (route.Id == Guid.Empty)
                throw new InvalidDataException("A Nexus route has no stable ID.");
            if (route.Points.Count > 10_000)
                throw new InvalidDataException($"Route '{route.Name}' exceeds the 10,000-point safety limit.");
            if (route.Points.Any(point =>
                    !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)))
                throw new InvalidDataException($"Route '{route.Name}' contains a non-finite coordinate.");
            if (!float.IsFinite(route.Tolerance) || !float.IsFinite(route.LastPointTolerance))
                throw new InvalidDataException($"Route '{route.Name}' contains an invalid tolerance.");
        }
    }
}
