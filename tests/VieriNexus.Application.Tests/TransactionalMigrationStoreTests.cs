using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class TransactionalMigrationStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "VieriNexus.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ApplyBacksUpSourceLeavesItUntouchedAndCanRollbackFirstImport()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "VieriNavPlotter.json");
        string original = "{\"Version\":1,\"Routes\":[]}";
        File.WriteAllText(source, original);
        string target = Path.Combine(root, "nexus", "routes.v1.json");
        string backups = Path.Combine(root, "backups");
        string receipts = Path.Combine(root, "receipts");
        NavigationLibrarySnapshot snapshot = Snapshot("Imported");
        TransactionalMigrationStore store = new();

        MigrationWriteResult applied = store.Apply("navplotter", source, target, backups, receipts, snapshot);

        Assert.True(applied.Success);
        Assert.NotNull(applied.Receipt);
        Assert.Equal(original, File.ReadAllText(source));
        Assert.Equal(original, File.ReadAllText(applied.Receipt.SourceBackupPath));
        Assert.True(File.Exists(target));
        Assert.True(File.Exists(TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt.Id)));

        MigrationWriteResult rolledBack = store.Rollback(TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt.Id));
        Assert.True(rolledBack.Success);
        Assert.False(File.Exists(target));
        Assert.Equal(original, File.ReadAllText(source));
    }

    [Fact]
    public void RollbackRestoresPriorNexusStateButRefusesToOverwriteNewerData()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "VieriNavPlotter.json");
        File.WriteAllText(source, "{\"Version\":1,\"Routes\":[]}");
        string target = Path.Combine(root, "nexus", "routes.v1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "prior-state");
        string receipts = Path.Combine(root, "receipts");
        TransactionalMigrationStore store = new();

        MigrationWriteResult applied = store.Apply("navplotter", source, target, Path.Combine(root, "backups"), receipts, Snapshot("Imported"));
        Assert.True(applied.Success);
        string receipt = TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt!.Id);
        byte[] importedState = File.ReadAllBytes(target);
        File.WriteAllText(target, "newer-state");

        MigrationWriteResult guarded = store.Rollback(receipt);
        Assert.False(guarded.Success);
        Assert.Equal("newer-state", File.ReadAllText(target));

        File.WriteAllBytes(target, importedState);
        MigrationWriteResult rolledBack = store.Rollback(receipt);
        Assert.True(rolledBack.Success);
        Assert.Equal("prior-state", File.ReadAllText(target));
    }

    [Fact]
    public void ApplyExplainsThatSettingsAreImportedWhenThereAreNoPersonalRoutes()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "VieriNavPlotter.json");
        File.WriteAllText(source, "{\"Version\":1,\"Routes\":[]}");
        NavigationLibrarySnapshot snapshot = Snapshot("Unused") with { Routes = [] };
        TransactionalMigrationStore store = new();

        MigrationWriteResult result = store.Apply(
            "navplotter",
            source,
            Path.Combine(root, "nexus", "routes.v1.json"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "receipts"),
            snapshot);

        Assert.True(result.Success);
        Assert.Equal("Imported settings and 0 personal routes into staged Nexus storage.", result.Message);
    }

    public void Dispose()
    {
        if (!Directory.Exists(root))
            return;
        string resolved = Path.GetFullPath(root);
        string allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "VieriNexus.Tests"));
        if (resolved.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            Directory.Delete(resolved, recursive: true);
    }

    private static NavigationLibrarySnapshot Snapshot(string name) => new(
        1, 1, 1f, .75f, true, true, true, 355f, null,
        [new(Guid.NewGuid(), name, 1, [new(1, 2, 3)], string.Empty, string.Empty, true, false, .75f, 3f, 0, 0, string.Empty, false, DateTime.UtcNow)]);
}
