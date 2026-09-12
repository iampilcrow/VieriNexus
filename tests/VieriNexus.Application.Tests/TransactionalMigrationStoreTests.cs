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

    [Fact]
    public void SavedReceiptReloadsAndVerifiesStagedState()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "VieriNavPlotter.json");
        File.WriteAllText(source, "{\"Version\":1,\"Routes\":[]}");
        string target = Path.Combine(root, "nexus", "routes.v1.json");
        string receipts = Path.Combine(root, "receipts");
        TransactionalMigrationStore writer = new();
        MigrationWriteResult applied = writer.Apply(
            "navplotter", source, target, Path.Combine(root, "backups"), receipts, Snapshot("Reloaded"));

        TransactionalMigrationStore reloadedStore = new();
        StagedNavigationReadResult reloaded = reloadedStore.ReadStagedNavigationState(
            TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt!.Id), "navplotter", target);

        Assert.True(reloaded.Success);
        Assert.Equal(applied.Receipt.Id, reloaded.Receipt!.Id);
        Assert.Equal("Reloaded", Assert.Single(reloaded.Snapshot!.Routes).Name);
        Assert.Equal("Imported settings and 1 personal route into staged Nexus storage.", reloaded.Message);
        Assert.Equal("{\"Version\":1,\"Routes\":[]}", File.ReadAllText(source));
    }

    [Fact]
    public void SavedReceiptIsRejectedWhenStagedStateChanged()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "VieriNavPlotter.json");
        File.WriteAllText(source, "{\"Version\":1,\"Routes\":[]}");
        string target = Path.Combine(root, "nexus", "routes.v1.json");
        string receipts = Path.Combine(root, "receipts");
        TransactionalMigrationStore store = new();
        MigrationWriteResult applied = store.Apply(
            "navplotter", source, target, Path.Combine(root, "backups"), receipts, Snapshot("Imported"));
        File.WriteAllText(target, "changed after import");

        StagedNavigationReadResult reloaded = store.ReadStagedNavigationState(
            TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt!.Id), "navplotter", target);

        Assert.False(reloaded.Success);
        Assert.Null(reloaded.Receipt);
        Assert.Equal("changed after import", File.ReadAllText(target));
        Assert.Equal("{\"Version\":1,\"Routes\":[]}", File.ReadAllText(source));
    }

    [Fact]
    public void AutoDutyProfilesAreBackedUpRecoveredAndRolledBackTransactionally()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "AutoDutyConfig.json");
        string original = "{\"DefaultConfigName\":\"Main\"}";
        File.WriteAllText(source, original);
        string target = Path.Combine(root, "nexus", "autoduty-operations.v1.json");
        string receipts = Path.Combine(root, "receipts");
        AutoDutyMigrationSnapshot snapshot = new(1, "Main",
            [new AutoDutyProfileSnapshot("Main", [123],
                new AutoDutyOverlayPreferences(true, false, false, false, false, true, true, true, true, true, true, true, true, true, true, true),
                new AutoDutyMaintenancePolicy(false, 0, false, false, 50, false, null, false, false,
                    false, null, false, new Dictionary<uint, string>(), false, false, 50, false, true, 1,
                    false, false, 5, false, false, false, false, false, false, false, 1, 1,
                    false, "UnneededEquipment", true, 120, true, 85, true, null,
                    false, true, 20, true, false, false, false))],
            ["{\"CharacterId\":123}"]);
        TransactionalMigrationStore store = new();

        MigrationWriteResult applied = store.ApplyAutoDuty(
            "autoduty", source, target, Path.Combine(root, "backups"), receipts, snapshot);
        StagedAutoDutyReadResult recovered = store.ReadStagedAutoDutyState(
            TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt!.Id), "autoduty", target);

        Assert.True(applied.Success);
        Assert.True(recovered.Success);
        Assert.Equal("Main", recovered.Snapshot!.DefaultProfileName);
        Assert.Equal(original, File.ReadAllText(source));
        Assert.Equal(original, File.ReadAllText(applied.Receipt.SourceBackupPath));

        MigrationWriteResult rolledBack = store.Rollback(
            TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt.Id));
        Assert.True(rolledBack.Success);
        Assert.False(File.Exists(target));
        Assert.Equal(original, File.ReadAllText(source));
    }

    [Fact]
    public void CodexPreferencesAndQueueAreBackedUpRecoveredAndRolledBackTransactionally()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "VieriCodex.json");
        const string original = "{\"Codex\":{\"MainScenarioQuests\":true}}";
        File.WriteAllText(source, original);
        string target = Path.Combine(root, "nexus", "codex-staging.v1.json");
        string receipts = Path.Combine(root, "receipts");
        CodexMigrationSnapshot snapshot = new(
            1, 2, true, true, true, true, true, true, true, true, true, true, true,
            false, 50,
            [new(Guid.NewGuid(), true, 31, 100, 3, 0)],
            new(true, true, true, false, true, true, true, 0));
        TransactionalMigrationStore store = new();

        MigrationWriteResult applied = store.ApplyCodex(
            "codex", source, target, Path.Combine(root, "backups"), receipts, snapshot);
        StagedCodexReadResult recovered = store.ReadStagedCodexState(
            TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt!.Id), "codex", target);

        Assert.True(applied.Success);
        Assert.True(recovered.Success);
        Assert.True(recovered.Snapshot!.MainScenarioQuests);
        Assert.Equal(31u, Assert.Single(recovered.Snapshot.QueueSteps).ClassJobId);
        Assert.Equal(original, File.ReadAllText(source));
        Assert.Equal(original, File.ReadAllText(applied.Receipt.SourceBackupPath));

        MigrationWriteResult rolledBack = store.Rollback(
            TransactionalMigrationStore.ReceiptPath(receipts, applied.Receipt.Id));
        Assert.True(rolledBack.Success);
        Assert.False(File.Exists(target));
        Assert.Equal(original, File.ReadAllText(source));
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
