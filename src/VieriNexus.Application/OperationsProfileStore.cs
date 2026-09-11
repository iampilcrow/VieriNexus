using System.Text.Json;

namespace VieriNexus.Application;

public sealed record OperationsProfileWriteResult(
    bool Success,
    string Message,
    AutoDutyMigrationSnapshot? Snapshot = null);

public sealed record OperationsProfileLibrary(
    int SchemaVersion,
    Guid SourceReceiptId,
    AutoDutyMigrationSnapshot Snapshot);

public interface IOperationsProfileStore
{
    OperationsProfileLibrary? Load();
    void Save(OperationsProfileLibrary library);
    bool Rollback(Guid sourceReceiptId);
}

/// <summary>
/// Nexus-owned operations profiles. This is deliberately separate from immutable import staging:
/// every user imports from their own local Dalamud configuration, then Nexus works from this copy.
/// </summary>
public sealed class FileOperationsProfileStore(string path) : IOperationsProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public OperationsProfileLibrary? Load()
    {
        if (!File.Exists(path))
            return null;
        OperationsProfileLibrary library = JsonSerializer.Deserialize<OperationsProfileLibrary>(
            File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The Nexus operations profile library is empty.");
        Validate(library);
        return library;
    }

    public void Save(OperationsProfileLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        Validate(library);
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The Nexus operations-profile path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        string backupPath = path + ".previous";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, library, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
                File.Copy(path, backupPath, overwrite: true);
            else if (File.Exists(backupPath))
                File.Delete(backupPath);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public bool Rollback(Guid sourceReceiptId)
    {
        OperationsProfileLibrary? current = Load();
        if (current is null || current.SourceReceiptId != sourceReceiptId)
            return false;
        string backupPath = path + ".previous";
        if (File.Exists(backupPath))
            File.Copy(backupPath, path, overwrite: true);
        else
            File.Delete(path);
        return true;
    }

    internal static void Validate(OperationsProfileLibrary library)
    {
        if (library.SchemaVersion != 1 || library.SourceReceiptId == Guid.Empty)
            throw new InvalidDataException("The Nexus operations-profile working-copy metadata is invalid.");
        AutoDutyMigrationSnapshot snapshot = library.Snapshot;
        if (snapshot.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported Nexus operations-profile schema {snapshot.SchemaVersion}.");
        if (snapshot.Profiles.Count == 0)
            throw new InvalidDataException("The Nexus operations-profile library contains no profiles.");
        if (snapshot.Profiles.Any(profile => string.IsNullOrWhiteSpace(profile.Name)) ||
            snapshot.Profiles.Select(profile => profile.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Profiles.Count)
            throw new InvalidDataException("The Nexus operations-profile library contains invalid or duplicate names.");
        if (snapshot.Profiles.SelectMany(profile => profile.CharacterIds)
            .GroupBy(id => id).Any(group => group.Count() > 1))
            throw new InvalidDataException("A character is assigned to more than one Nexus operations profile.");
    }
}

public static class OperationsProfilePolicy
{
    public static AutoDutyProfileSnapshot? Resolve(AutoDutyMigrationSnapshot? snapshot, ulong characterId)
    {
        if (snapshot is null)
            return null;
        AutoDutyProfileSnapshot? assigned = characterId == 0
            ? null
            : snapshot.Profiles.FirstOrDefault(profile => profile.CharacterIds.Contains(characterId));
        return assigned
            ?? snapshot.Profiles.FirstOrDefault(profile => string.Equals(
                profile.Name, snapshot.DefaultProfileName, StringComparison.OrdinalIgnoreCase))
            ?? snapshot.Profiles.FirstOrDefault();
    }
}
