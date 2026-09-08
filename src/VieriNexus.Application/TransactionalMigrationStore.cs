using System.Security.Cryptography;
using System.Text.Json;

namespace VieriNexus.Application;

public sealed class TransactionalMigrationStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string ReceiptPath(string receiptRoot, Guid receiptId) =>
        Path.Combine(receiptRoot, $"{receiptId:N}.json");

    public MigrationWriteResult Apply(
        string sourceId,
        string sourcePath,
        string nexusTargetPath,
        string backupRoot,
        string receiptRoot,
        NavigationLibrarySnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nexusTargetPath);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!File.Exists(sourcePath))
            return new(false, "The source configuration no longer exists.");

        Guid id = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string operationDirectory = Path.Combine(backupRoot, $"{now:yyyyMMdd-HHmmssfff}-{id:N}");
        string sourceBackupPath = Path.Combine(operationDirectory, "legacy-source.json");
        string? previousTargetBackupPath = null;
        string receiptPath = ReceiptPath(receiptRoot, id);
        bool targetExisted = File.Exists(nexusTargetPath);

        try
        {
            Directory.CreateDirectory(operationDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(nexusTargetPath)!);
            Directory.CreateDirectory(receiptRoot);
            File.Copy(sourcePath, sourceBackupPath, overwrite: false);
            if (targetExisted)
            {
                previousTargetBackupPath = Path.Combine(operationDirectory, "previous-nexus-state.json");
                File.Copy(nexusTargetPath, previousTargetBackupPath, overwrite: false);
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);
            AtomicWrite(nexusTargetPath, payload);
            MigrationReceipt receipt = new(
                1,
                id,
                sourceId,
                now,
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(sourceBackupPath),
                Path.GetFullPath(nexusTargetPath),
                targetExisted,
                previousTargetBackupPath is null ? null : Path.GetFullPath(previousTargetBackupPath),
                HashFile(sourcePath),
                HashFile(nexusTargetPath));
            AtomicWrite(receiptPath, JsonSerializer.SerializeToUtf8Bytes(receipt, Options));
            return new(true, $"Imported {snapshot.Routes.Count} route(s) into staged Nexus storage.", receipt);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TryRestoreTarget(nexusTargetPath, targetExisted, previousTargetBackupPath);
            return new(false, "The import could not be committed; the prior Nexus state was restored.");
        }
    }

    public MigrationWriteResult Rollback(string receiptPath)
    {
        if (!File.Exists(receiptPath))
            return new(false, "The migration receipt could not be found.");

        try
        {
            MigrationReceipt? receipt = JsonSerializer.Deserialize<MigrationReceipt>(File.ReadAllText(receiptPath), Options);
            if (receipt is null)
                return new(false, "The migration receipt could not be read.");
            if (!File.Exists(receipt.NexusTargetPath))
                return new(false, "The staged Nexus route library no longer exists; nothing was changed.");
            if (!string.Equals(HashFile(receipt.NexusTargetPath), receipt.NexusTargetSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, "Rollback stopped because the Nexus route library changed after this import.");

            TryRestoreTarget(receipt.NexusTargetPath, receipt.PreviousNexusTargetExisted, receipt.PreviousNexusTargetBackupPath);
            return new(true, "The prior staged Nexus route state was restored.", receipt);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(false, "Rollback could not be completed; no legacy source file was changed.");
        }
    }

    private static void AtomicWrite(string targetPath, byte[] payload)
    {
        string temporaryPath = targetPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void TryRestoreTarget(string targetPath, bool targetExisted, string? previousTargetBackupPath)
    {
        if (targetExisted && previousTargetBackupPath is not null && File.Exists(previousTargetBackupPath))
            AtomicWrite(targetPath, File.ReadAllBytes(previousTargetBackupPath));
        else if (!targetExisted && File.Exists(targetPath))
            File.Delete(targetPath);
    }
}
