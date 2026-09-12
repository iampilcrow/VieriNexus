using System.Text.Json;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed record CommandCenterMigrationStatus(
    bool SourceFound,
    CommandCenterMigrationPreview? Preview,
    string Message,
    MigrationReceipt? LastReceipt,
    CommandCenterSnapshot? WorkingSnapshot);

internal sealed class CommandCenterMigrationService
{
    private const string SourceId = "deck";
    private readonly LegacyConfigurationInventory inventory;
    private readonly CommandCenterMigrationImporter importer = new();
    private readonly TransactionalMigrationStore migrationStore = new();
    private readonly CommandCenterWorkingStore workingStore;
    private readonly string dataRoot;
    private string? cachedSourcePath;
    private DateTime cachedWriteTimeUtc;
    private CommandCenterMigrationPreview? cachedPreview;
    private string message = "Review the source before importing.";
    private MigrationReceipt? lastReceipt;
    private Guid? workingReceiptId;
    private CommandCenterSnapshot? workingSnapshot;

    internal CommandCenterMigrationService(
        LegacyConfigurationInventory inventory,
        string pluginConfigDirectory,
        Guid? persistedReceiptId)
    {
        this.inventory = inventory;
        dataRoot = Path.Combine(pluginConfigDirectory, "NexusData");
        workingStore = new CommandCenterWorkingStore(Path.Combine(dataRoot, "command-center-working.v1.json"));
        try
        {
            CommandCenterWorkingLibrary? library = workingStore.Load();
            workingSnapshot = library?.Snapshot;
            workingReceiptId = library?.SourceReceiptId;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            message = "The prior Nexus command-center working copy could not be verified; import remains available.";
        }
        if (persistedReceiptId is { } receiptId)
            Recover(receiptId);
    }

    internal CommandCenterSnapshot? WorkingSnapshot => Volatile.Read(ref workingSnapshot);

    internal bool StartFresh(out string result)
    {
        if (WorkingSnapshot is not null)
        {
            result = "The Nexus Command Center is already initialized.";
            return true;
        }
        try
        {
            Guid localId = Guid.NewGuid();
            CommandCenterSnapshot snapshot = new(
                1, 0, true, false, false, false, false, string.Empty,
                455f, 650f, 680f, 1f, false, 0f, 0f,
                true, 0, false, false, false, true,
                [], [],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, IReadOnlyList<CommandCenterCustomCommand>>(StringComparer.OrdinalIgnoreCase));
            workingStore.SaveImported(new CommandCenterWorkingLibrary(1, localId, snapshot));
            workingReceiptId = localId;
            Volatile.Write(ref workingSnapshot, snapshot);
            result = message = "Started a fresh Nexus Command Center.";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            result = message = "Nexus could not initialize the Command Center; no existing settings were changed.";
            return false;
        }
    }

    internal CommandCenterMigrationStatus Status()
    {
        string? sourcePath = FindSourcePath();
        if (sourcePath is null)
            return new(false, null, lastReceipt is null ? "No VieriDeck configuration was found on this computer." : message,
                lastReceipt, WorkingSnapshot);
        DateTime writeTime = File.GetLastWriteTimeUtc(sourcePath);
        if (!string.Equals(cachedSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) ||
            cachedWriteTimeUtc != writeTime || cachedPreview is null)
        {
            cachedSourcePath = sourcePath;
            cachedWriteTimeUtc = writeTime;
            try
            {
                cachedPreview = importer.Preview(File.ReadAllText(sourcePath));
            }
            catch (IOException)
            {
                cachedPreview = new(null, [new(MigrationIssueSeverity.Error, "The VieriDeck configuration is temporarily unavailable.")]);
            }
        }
        return new(true, cachedPreview, message, lastReceipt, WorkingSnapshot);
    }

    internal MigrationWriteResult Import()
    {
        CommandCenterMigrationStatus status = Status();
        if (!status.SourceFound || cachedSourcePath is null || status.Preview is not { CanImport: true, Snapshot: not null })
            return SetMessage(new(false, "Import is unavailable until the VieriDeck configuration passes validation."));
        MigrationWriteResult result = migrationStore.ApplyCommandCenter(
            SourceId,
            cachedSourcePath,
            Path.Combine(dataRoot, "command-center-staging.v1.json"),
            Path.Combine(dataRoot, "backups", SourceId),
            Path.Combine(dataRoot, "receipts"),
            status.Preview.Snapshot);
        if (result.Success && result.Receipt is { } receipt)
        {
            lastReceipt = receipt;
            try
            {
                workingStore.SaveImported(new CommandCenterWorkingLibrary(1, receipt.Id, status.Preview.Snapshot));
                Volatile.Write(ref workingSnapshot, status.Preview.Snapshot);
                workingReceiptId = receipt.Id;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                return SetMessage(result with { Message = result.Message + " The Nexus working copy could not be written; re-import before disabling VieriDeck." });
            }
        }
        return SetMessage(result);
    }

    internal MigrationWriteResult Rollback(Guid receiptId)
    {
        CommandCenterSnapshot? prior = WorkingSnapshot;
        MigrationWriteResult result = migrationStore.Rollback(TransactionalMigrationStore.ReceiptPath(
            Path.Combine(dataRoot, "receipts"), receiptId));
        if (result.Success)
        {
            if (workingStore.Rollback(receiptId))
            {
                CommandCenterWorkingLibrary? restored = workingStore.Load();
                Volatile.Write(ref workingSnapshot, restored?.Snapshot);
                workingReceiptId = restored?.SourceReceiptId;
            }
            else
                Volatile.Write(ref workingSnapshot, prior);
            lastReceipt = null;
        }
        return SetMessage(result);
    }

    internal bool Update(Func<CommandCenterSnapshot, CommandCenterSnapshot> change, out string result)
    {
        CommandCenterSnapshot? current = WorkingSnapshot;
        if (current is null || workingReceiptId is null)
        {
            result = "Import VieriDeck settings on the Migration page first.";
            return false;
        }
        try
        {
            CommandCenterSnapshot updated = change(current);
            workingStore.Save(new CommandCenterWorkingLibrary(1, workingReceiptId.Value, updated));
            Volatile.Write(ref workingSnapshot, updated);
            result = "Command Center settings saved.";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            result = "Command Center settings could not be saved; the prior working copy remains active.";
            return false;
        }
    }

    private void Recover(Guid receiptId)
    {
        StagedCommandCenterReadResult staged = migrationStore.ReadStagedCommandCenterState(
            TransactionalMigrationStore.ReceiptPath(Path.Combine(dataRoot, "receipts"), receiptId),
            SourceId,
            Path.Combine(dataRoot, "command-center-staging.v1.json"));
        message = staged.Message;
        if (!staged.Success)
            return;
        lastReceipt = staged.Receipt;
        if (WorkingSnapshot is null && staged.Snapshot is not null)
        {
            try
            {
                workingStore.SaveImported(new CommandCenterWorkingLibrary(1, receiptId, staged.Snapshot));
                Volatile.Write(ref workingSnapshot, staged.Snapshot);
                workingReceiptId = receiptId;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                message = "The staged Command Center import is verified, but its working copy could not be recovered.";
            }
        }
    }

    private MigrationWriteResult SetMessage(MigrationWriteResult result)
    {
        message = result.Message;
        return result;
    }

    private string? FindSourcePath()
    {
        LegacySource? source = inventory.Scan().FirstOrDefault(item => item.Id == SourceId);
        if (source is null)
            return null;
        foreach (string path in source.ExistingPaths)
        {
            if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                return path;
            if (Directory.Exists(path))
            {
                string candidate = Path.Combine(path, "VieriDeck.json");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}
