using System.Text.Json;

namespace VieriNexus.Application;

public sealed record CommandCenterWorkingLibrary(int SchemaVersion, Guid SourceReceiptId, CommandCenterSnapshot Snapshot);

public sealed class CommandCenterWorkingStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public CommandCenterWorkingLibrary? Load()
    {
        if (!File.Exists(path))
            return null;
        CommandCenterWorkingLibrary library = JsonSerializer.Deserialize<CommandCenterWorkingLibrary>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The Nexus command-center working copy is empty.");
        Validate(library);
        return library;
    }

    public void Save(CommandCenterWorkingLibrary library)
        => SaveCore(library);

    public void SaveImported(CommandCenterWorkingLibrary library)
    {
        Validate(library);
        string baseline = ImportBaselinePath(library.SourceReceiptId);
        if (File.Exists(path))
            File.Copy(path, baseline, overwrite: true);
        else if (File.Exists(baseline))
            File.Delete(baseline);
        SaveCore(library);
    }

    private void SaveCore(CommandCenterWorkingLibrary library)
    {
        Validate(library);
        string directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The command-center path has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, library, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
                File.Copy(path, path + ".previous", overwrite: true);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public bool Rollback(Guid receiptId)
    {
        CommandCenterWorkingLibrary? current = Load();
        if (current is null || current.SourceReceiptId != receiptId)
            return false;
        string baseline = ImportBaselinePath(receiptId);
        if (File.Exists(baseline))
        {
            File.Copy(baseline, path, overwrite: true);
            File.Delete(baseline);
        }
        else
            File.Delete(path);
        return true;
    }

    private string ImportBaselinePath(Guid receiptId) => path + $".{receiptId:N}.before-import";

    internal static bool IsValid(CommandCenterSnapshot snapshot) =>
        snapshot.SchemaVersion == 1 &&
        float.IsFinite(snapshot.SourceListWidth) && float.IsFinite(snapshot.SourceCommandWidth) &&
        float.IsFinite(snapshot.SourceWindowHeight) && float.IsFinite(snapshot.SourceUiScale) &&
        float.IsFinite(snapshot.SourceWindowPositionX) && float.IsFinite(snapshot.SourceWindowPositionY) &&
        snapshot.Favorites.Count <= 2_000 && snapshot.HiddenPlugins.Count <= 2_000 &&
        snapshot.PreferredCommands.Count <= 2_000 && snapshot.CustomCommands.Count <= 2_000 &&
        snapshot.CustomCommands.Values.Sum(commands => commands.Count) <= 2_000;

    private static void Validate(CommandCenterWorkingLibrary library)
    {
        if (library.SchemaVersion != 1 || library.SourceReceiptId == Guid.Empty || !IsValid(library.Snapshot))
            throw new InvalidDataException("The Nexus command-center working copy is invalid.");
    }
}
