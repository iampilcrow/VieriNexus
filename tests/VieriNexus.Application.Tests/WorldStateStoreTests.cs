using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class WorldStateStoreTests
{
    [Fact]
    public void RejectsNonIncreasingRevision()
    {
        var store = new WorldStateStore();
        WorldSnapshot accepted = Snapshot(1);
        store.Publish(accepted);
        var changes = 0;
        store.Changed += _ => changes++;

        Assert.Throws<InvalidOperationException>(() => store.Publish(Snapshot(1)));
        Assert.Same(accepted, store.Current);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void PublishesImmutableLatestSnapshot()
    {
        var store = new WorldStateStore();
        WorldSnapshot? observed = null;
        store.Changed += value => observed = value;

        var expected = Snapshot(7);
        store.Publish(expected);

        Assert.Same(expected, store.Current);
        Assert.Same(expected, observed);
    }

    private static WorldSnapshot Snapshot(long revision) => new(
        revision,
        DateTimeOffset.UtcNow,
        new SessionSnapshot(true, false, false, true, 1),
        Observed<CharacterSnapshot>.Unknown(DateTimeOffset.UtcNow),
        new Dictionary<ProviderId, ProviderHealthSnapshot>());
}
