using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationExecutionIntentStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"VieriNexus-tests-{Guid.NewGuid():N}");

    [Fact]
    public void MissingFileLoadsAsNoExecutionIntent()
    {
        var store = new FileNavigationExecutionIntentStore(Path.Combine(directory, "intent.json"));

        Assert.Null(store.Load());
    }

    [Fact]
    public void IntentRoundTripsWithExplicitState()
    {
        string path = Path.Combine(directory, "intent.json");
        var store = new FileNavigationExecutionIntentStore(path);
        var expected = new NavigationExecutionIntent(
            NavigationExecutionIntent.CurrentSchemaVersion,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            NavigationExecutionIntentState.StopPending,
            DateTimeOffset.UtcNow);

        store.Save(expected);

        Assert.Equal(expected, store.Load());
        Assert.DoesNotContain(Directory.GetFiles(directory), file => file.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedSchemaIsRejected()
    {
        string path = Path.Combine(directory, "intent.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path,
            "{\"SchemaVersion\":99,\"ExecutionId\":\"11111111-1111-1111-1111-111111111111\",\"RouteId\":\"22222222-2222-2222-2222-222222222222\",\"LeaseId\":\"33333333-3333-3333-3333-333333333333\",\"State\":\"Running\",\"UpdatedAtUtc\":\"2026-09-08T00:00:00Z\"}");
        var store = new FileNavigationExecutionIntentStore(path);

        Assert.Throws<InvalidDataException>(() => store.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
