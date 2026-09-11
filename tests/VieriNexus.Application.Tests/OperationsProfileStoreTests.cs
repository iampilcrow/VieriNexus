using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class OperationsProfileStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"vieri-nexus-operations-{Guid.NewGuid():N}");

    [Fact]
    public void WorkingProfilesRoundTripAndRetainPreviousCopy()
    {
        string path = Path.Combine(root, "operations-profiles.v1.json");
        var store = new FileOperationsProfileStore(path);
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        store.Save(new(1, first, Snapshot("First", 10)));
        store.Save(new(1, second, Snapshot("Second", 20)));

        Assert.Equal("Second", store.Load()!.Snapshot.DefaultProfileName);
        Assert.True(File.Exists(path + ".previous"));
        Assert.Contains("First", File.ReadAllText(path + ".previous"));
        Assert.True(store.Rollback(second));
        Assert.Equal("First", store.Load()!.Snapshot.DefaultProfileName);
    }

    [Fact]
    public void AssignedCharacterWinsThenDefaultThenFirst()
    {
        AutoDutyMigrationSnapshot snapshot = Snapshot("Default", 10) with
        {
            Profiles = [Profile("First", 1), Profile("Default", 2), Profile("Assigned", 77)],
        };

        Assert.Equal("Assigned", OperationsProfilePolicy.Resolve(snapshot, 77)!.Name);
        Assert.Equal("Default", OperationsProfilePolicy.Resolve(snapshot, 99)!.Name);
        Assert.Equal("First", OperationsProfilePolicy.Resolve(snapshot with { DefaultProfileName = "Missing" }, 99)!.Name);
    }

    [Fact]
    public void DuplicateCharacterAssignmentsAreRejected()
    {
        AutoDutyMigrationSnapshot snapshot = Snapshot("First", 10) with
        {
            Profiles = [Profile("First", 10), Profile("Second", 10)],
        };
        var store = new FileOperationsProfileStore(Path.Combine(root, "operations-profiles.v1.json"));

        Assert.Throws<InvalidDataException>(() => store.Save(new(1, Guid.NewGuid(), snapshot)));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static AutoDutyMigrationSnapshot Snapshot(string name, ulong id) =>
        new(1, name, [Profile(name, id)], []);

    private static AutoDutyProfileSnapshot Profile(string name, ulong id) => new(
        name, [id],
        new(true, false, false, false, false, true, true, true, true, true, true, true, true, true, true, true),
        new(false, 1_000_000, false, false, 50, true, null, false, false, false, null, false,
            new Dictionary<uint, string>(), false, false, 50, false, true, 1, false, false, 5, false,
            false, false, false, false, false, false, 1, 1, false, "UnneededEquipment", true, 120,
            true, 85, true, null, false, true, 20, true, false, false, false));
}
