using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationLibraryStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"vieri-nexus-library-{Guid.NewGuid():N}");

    [Fact]
    public void WorkingLibraryRoundTripsSeparately()
    {
        string path = Path.Combine(root, "navigation-library.v1.json");
        var store = new FileNavigationLibraryStore(path);
        NavigationLibrarySnapshot expected = Snapshot("First");

        store.Save(expected);
        NavigationLibrarySnapshot? actual = store.Load();

        Assert.NotNull(actual);
        Assert.Equal(expected.Routes[0].Id, actual.Routes[0].Id);
        Assert.Equal("First", actual.Routes[0].Name);
    }

    [Fact]
    public void ReplacementRetainsPreviousWorkingCopy()
    {
        string path = Path.Combine(root, "navigation-library.v1.json");
        var store = new FileNavigationLibraryStore(path);
        store.Save(Snapshot("First"));

        store.Save(Snapshot("Second"));

        Assert.True(File.Exists(path + ".previous"));
        Assert.Contains("First", File.ReadAllText(path + ".previous"));
        Assert.Equal("Second", store.Load()!.Routes[0].Name);
    }

    [Fact]
    public void DuplicateIdsAreRejectedBeforeWrite()
    {
        string path = Path.Combine(root, "navigation-library.v1.json");
        var store = new FileNavigationLibraryStore(path);
        NavigationRouteSnapshot route = Snapshot("Duplicate").Routes[0];
        NavigationLibrarySnapshot invalid = Snapshot("Unused") with { Routes = [route, route] };

        Assert.Throws<InvalidDataException>(() => store.Save(invalid));
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static NavigationLibrarySnapshot Snapshot(string name) => new(
        1, 1, 1f, 0.75f, true, true, true, 355f, null,
        [new(Guid.NewGuid(), name, 100, [new(1, 2, 3)], string.Empty, string.Empty,
            true, false, 0.75f, 3f, 0, 0, string.Empty, false, DateTime.UtcNow)]);
}
