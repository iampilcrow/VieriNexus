using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed record NavigationMigrationStatus(
    bool SourceFound,
    NavigationMigrationPreview? Preview,
    string Message,
    MigrationReceipt? LastReceipt,
    NavigationLibrarySnapshot? StagedSnapshot);

internal sealed class NavigationMigrationService
{
    private const string SourceId = "navplotter";
    private readonly LegacyConfigurationInventory inventory;
    private readonly NavigationRouteMigrationImporter importer = new();
    private readonly TransactionalMigrationStore store = new();
    private readonly string dataRoot;
    private string? cachedSourcePath;
    private DateTime cachedWriteTimeUtc;
    private NavigationMigrationPreview? cachedPreview;
    private string message = "Review the source before importing.";
    private MigrationReceipt? lastReceipt;
    private NavigationLibrarySnapshot? stagedSnapshot;

    internal NavigationMigrationService(
        LegacyConfigurationInventory inventory,
        string pluginConfigDirectory,
        Guid? persistedReceiptId)
    {
        this.inventory = inventory;
        dataRoot = Path.Combine(pluginConfigDirectory, "NexusData");
        if (persistedReceiptId is { } receiptId)
            Recover(receiptId);
    }

    internal NavigationLibrarySnapshot? StagedSnapshot => Volatile.Read(ref stagedSnapshot);

    internal NavigationMigrationStatus Status()
    {
        string? sourcePath = FindSourcePath();
        if (sourcePath is null)
            return new(false, null,
                lastReceipt is null ? "No VieriNavPlotter configuration was found on this computer." : message,
                lastReceipt,
                StagedSnapshot);

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
                    [new(MigrationIssueSeverity.Error, "The VieriNavPlotter configuration is temporarily unavailable.")]);
            }
        }

        return new(true, cachedPreview, message, lastReceipt, StagedSnapshot);
    }

    internal MigrationWriteResult Import()
    {
        NavigationMigrationStatus status = Status();
        if (!status.SourceFound || cachedSourcePath is null || status.Preview is not { CanImport: true, Snapshot: not null })
            return SetMessage(new(false, "Import is unavailable until the route configuration passes validation."));

        MigrationWriteResult result = store.Apply(
            SourceId,
            cachedSourcePath,
            Path.Combine(dataRoot, "routes.v1.json"),
            Path.Combine(dataRoot, "backups", SourceId),
            Path.Combine(dataRoot, "receipts"),
            status.Preview.Snapshot);
        if (result.Success)
        {
            lastReceipt = result.Receipt;
            Volatile.Write(ref stagedSnapshot, status.Preview.Snapshot);
        }
        return SetMessage(result);
    }

    internal MigrationWriteResult Rollback(Guid receiptId)
    {
        MigrationWriteResult result = store.Rollback(TransactionalMigrationStore.ReceiptPath(
            Path.Combine(dataRoot, "receipts"), receiptId));
        if (result.Success)
        {
            lastReceipt = null;
            Volatile.Write(ref stagedSnapshot, null);
        }
        return SetMessage(result);
    }

    private void Recover(Guid receiptId)
    {
        StagedNavigationReadResult result = store.ReadStagedNavigationState(
            TransactionalMigrationStore.ReceiptPath(Path.Combine(dataRoot, "receipts"), receiptId),
            SourceId,
            Path.Combine(dataRoot, "routes.v1.json"));
        message = result.Message;
        if (result.Success)
        {
            lastReceipt = result.Receipt;
            Volatile.Write(ref stagedSnapshot, result.Snapshot);
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
                string candidate = Path.Combine(path, "VieriNavPlotter.json");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}
