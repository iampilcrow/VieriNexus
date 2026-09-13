using System.Text.Json;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed record CodexMigrationStatus(
    bool SourceFound,
    CodexMigrationPreview? Preview,
    string Message,
    MigrationReceipt? LastReceipt,
    CodexMigrationSnapshot? StagedSnapshot);

internal sealed class CodexMigrationService
{
    private const string SourceId = "codex";
    private readonly LegacyConfigurationInventory inventory;
    private readonly CodexMigrationImporter importer = new();
    private readonly TransactionalMigrationStore store = new();
    private readonly string dataRoot;
    private string? cachedSourcePath;
    private DateTime cachedWriteTimeUtc;
    private CodexMigrationPreview? cachedPreview;
    private string message = "Review the source before importing.";
    private MigrationReceipt? lastReceipt;
    private CodexMigrationSnapshot? stagedSnapshot;

    internal CodexMigrationService(
        LegacyConfigurationInventory inventory,
        string pluginConfigDirectory,
        Guid? persistedReceiptId)
    {
        this.inventory = inventory;
        dataRoot = Path.Combine(pluginConfigDirectory, "NexusData");
        if (persistedReceiptId is { } receiptId)
            Recover(receiptId);
    }

    internal CodexMigrationStatus Status()
    {
        string? sourcePath = FindSourcePath();
        if (sourcePath is null)
            return new(false, null,
                lastReceipt is null ? "No VieriCodex configuration was found on this computer." : message,
                lastReceipt, stagedSnapshot);
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
                    [new(MigrationIssueSeverity.Error, "The VieriCodex configuration is temporarily unavailable.")]);
            }
        }
        return new(true, cachedPreview, message, lastReceipt, stagedSnapshot);
    }

    internal MigrationWriteResult Import()
    {
        CodexMigrationStatus status = Status();
        if (!status.SourceFound || cachedSourcePath is null || status.Preview is not { CanImport: true, Snapshot: not null })
            return SetMessage(new(false, "Import is unavailable until the VieriCodex configuration passes validation."));
        MigrationWriteResult result = store.ApplyCodex(
            SourceId,
            cachedSourcePath,
            StagingPath,
            Path.Combine(dataRoot, "backups", SourceId),
            ReceiptRoot,
            status.Preview.Snapshot);
        if (result.Success && result.Receipt is not null)
        {
            lastReceipt = result.Receipt;
            stagedSnapshot = status.Preview.Snapshot;
        }
        return SetMessage(result);
    }

    internal MigrationWriteResult Rollback(Guid receiptId)
    {
        MigrationWriteResult result = store.Rollback(TransactionalMigrationStore.ReceiptPath(ReceiptRoot, receiptId));
        if (result.Success)
        {
            lastReceipt = null;
            stagedSnapshot = null;
        }
        return SetMessage(result);
    }

    private string StagingPath => Path.Combine(dataRoot, "codex-staging.v1.json");
    private string ReceiptRoot => Path.Combine(dataRoot, "receipts");

    private void Recover(Guid receiptId)
    {
        StagedCodexReadResult result = store.ReadStagedCodexState(
            TransactionalMigrationStore.ReceiptPath(ReceiptRoot, receiptId), SourceId, StagingPath);
        message = result.Message;
        if (!result.Success)
            return;
        lastReceipt = result.Receipt;
        stagedSnapshot = result.Snapshot;
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
            string candidate = Path.Combine(path, "VieriCodex.json");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
