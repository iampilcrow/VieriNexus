using System.Text.Json;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed record AutoDutyMigrationStatus(
    bool SourceFound,
    AutoDutyMigrationPreview? Preview,
    string Message,
    MigrationReceipt? LastReceipt,
    AutoDutyMigrationSnapshot? StagedSnapshot);

internal sealed class AutoDutyMigrationService
{
    private const string SourceId = "autoduty";
    private readonly LegacyConfigurationInventory inventory;
    private readonly AutoDutyMigrationImporter importer = new();
    private readonly TransactionalMigrationStore store = new();
    private readonly FileOperationsProfileStore workingStore;
    private readonly string dataRoot;
    private string? cachedSourcePath;
    private DateTime cachedWriteTimeUtc;
    private AutoDutyMigrationPreview? cachedPreview;
    private string message = "Review the source before importing.";
    private MigrationReceipt? lastReceipt;
    private AutoDutyMigrationSnapshot? stagedSnapshot;
    private AutoDutyMigrationSnapshot? workingSnapshot;

    internal AutoDutyMigrationService(
        LegacyConfigurationInventory inventory,
        string pluginConfigDirectory,
        Guid? persistedReceiptId)
    {
        this.inventory = inventory;
        dataRoot = Path.Combine(pluginConfigDirectory, "NexusData");
        workingStore = new FileOperationsProfileStore(Path.Combine(dataRoot, "operations-profiles.v1.json"));
        try
        {
            workingSnapshot = workingStore.Load()?.Snapshot;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            message = "The prior Nexus operations working copy could not be verified; the staged import remains available for recovery.";
        }
        if (persistedReceiptId is { } receiptId)
            Recover(receiptId);
    }

    internal AutoDutyMigrationSnapshot? StagedSnapshot => Volatile.Read(ref stagedSnapshot);
    internal AutoDutyMigrationSnapshot? WorkingSnapshot => Volatile.Read(ref workingSnapshot);
    internal bool HasWorkingProfiles => WorkingSnapshot is not null;
    internal AutoDutyProfileSnapshot? ProfileFor(ulong characterId) =>
        OperationsProfilePolicy.Resolve(WorkingSnapshot, characterId);

    internal AutoDutyMigrationStatus Status()
    {
        string? sourcePath = FindSourcePath();
        if (sourcePath is null)
            return new(false, null,
                lastReceipt is null ? "No VieriAutoDuty configuration was found on this computer." : message,
                lastReceipt, StagedSnapshot);

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
                cachedPreview = new(null,
                    [new(MigrationIssueSeverity.Error, "The VieriAutoDuty configuration is temporarily unavailable.")]);
            }
        }
        return new(true, cachedPreview, message, lastReceipt, StagedSnapshot);
    }

    internal MigrationWriteResult Import()
    {
        AutoDutyMigrationStatus status = Status();
        if (!status.SourceFound || cachedSourcePath is null || status.Preview is not { CanImport: true, Snapshot: not null })
            return SetMessage(new(false, "Import is unavailable until the VieriAutoDuty configuration passes validation."));

        MigrationWriteResult result = store.ApplyAutoDuty(
            SourceId,
            cachedSourcePath,
            Path.Combine(dataRoot, "autoduty-operations.v1.json"),
            Path.Combine(dataRoot, "backups", SourceId),
            Path.Combine(dataRoot, "receipts"),
            status.Preview.Snapshot);
        if (result.Success)
        {
            lastReceipt = result.Receipt;
            Volatile.Write(ref stagedSnapshot, status.Preview.Snapshot);
            if (!TryPromote(status.Preview.Snapshot, result.Receipt!.Id))
                return SetMessage(result with { Message = result.Message + " The Nexus working copy could not be written; re-import before disabling VieriAutoDuty." });
        }
        return SetMessage(result);
    }

    internal MigrationWriteResult Rollback(Guid receiptId)
    {
        AutoDutyMigrationSnapshot? priorWorking = WorkingSnapshot;
        MigrationWriteResult result = store.Rollback(TransactionalMigrationStore.ReceiptPath(
            Path.Combine(dataRoot, "receipts"), receiptId));
        if (result.Success)
        {
            if (workingStore.Rollback(receiptId))
                Volatile.Write(ref workingSnapshot, workingStore.Load()?.Snapshot);
            else
                Volatile.Write(ref workingSnapshot, priorWorking);
            lastReceipt = null;
            Volatile.Write(ref stagedSnapshot, null);
        }
        return SetMessage(result);
    }

    private void Recover(Guid receiptId)
    {
        StagedAutoDutyReadResult result = store.ReadStagedAutoDutyState(
            TransactionalMigrationStore.ReceiptPath(Path.Combine(dataRoot, "receipts"), receiptId),
            SourceId,
            Path.Combine(dataRoot, "autoduty-operations.v1.json"));
        message = result.Message;
        if (result.Success)
        {
            lastReceipt = result.Receipt;
            Volatile.Write(ref stagedSnapshot, result.Snapshot);
            if (WorkingSnapshot is null && result.Snapshot is not null)
                TryPromote(result.Snapshot, receiptId);
        }
    }

    private bool TryPromote(AutoDutyMigrationSnapshot snapshot, Guid receiptId)
    {
        try
        {
            workingStore.Save(new OperationsProfileLibrary(1, receiptId, snapshot));
            Volatile.Write(ref workingSnapshot, snapshot);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            message = "The staged operations import is verified, but its Nexus working copy could not be written.";
            return false;
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
            if (!Directory.Exists(path))
                continue;
            foreach (string fileName in new[] { "AutoDutyConfig.json", "VieriAutoDuty.json", "AutoDuty.json" })
            {
                string candidate = Path.Combine(path, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}
