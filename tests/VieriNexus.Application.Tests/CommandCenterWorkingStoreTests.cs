using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class CommandCenterWorkingStoreTests
{
    [Fact]
    public void SaveLoadAndReceiptRollbackPreservePriorWorkingCopy()
    {
        string root = Path.Combine(Path.GetTempPath(), "nexus-command-center-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "working.json");
        try
        {
            CommandCenterWorkingStore store = new(path);
            Guid firstReceipt = Guid.NewGuid();
            Guid secondReceipt = Guid.NewGuid();
            store.Save(new(1, firstReceipt, Snapshot(["AutoDuty"])));
            store.SaveImported(new(1, secondReceipt, Snapshot(["AutoDuty", "DelvUI"])));
            store.Save(new(1, secondReceipt, Snapshot(["AutoDuty", "DelvUI", "Lifestream"])));

            Assert.Equal(3, store.Load()!.Snapshot.Favorites.Count);
            Assert.True(store.Rollback(secondReceipt));
            Assert.Equal(["AutoDuty"], store.Load()!.Snapshot.Favorites);
            Assert.False(store.Rollback(secondReceipt));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CommandCenterSnapshot Snapshot(IReadOnlyList<string> favorites) => new(
        1, 3, true, false, false, false, false, string.Empty,
        455, 650, 680, 1, false, 0, 0, true, 123, false, false, false, true,
        favorites, [], new Dictionary<string, string>(),
        new Dictionary<string, IReadOnlyList<CommandCenterCustomCommand>>());
}
