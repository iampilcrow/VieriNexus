using VieriNexus.Domain;

namespace VieriNexus.Application;

public sealed class WorldStateStore
{
    private WorldSnapshot current = WorldSnapshot.Empty;

    public WorldSnapshot Current => Volatile.Read(ref current);

    public event Action<WorldSnapshot>? Changed;

    public void Publish(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var previous = Interlocked.Exchange(ref current, snapshot);
        if (snapshot.Revision <= previous.Revision && previous != WorldSnapshot.Empty)
            throw new InvalidOperationException("World snapshot revisions must increase monotonically.");
        Changed?.Invoke(snapshot);
    }
}
