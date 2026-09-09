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

        while (true)
        {
            WorldSnapshot previous = Volatile.Read(ref current);
            if (snapshot.Revision <= previous.Revision && !ReferenceEquals(previous, WorldSnapshot.Empty))
                throw new InvalidOperationException("World snapshot revisions must increase monotonically.");
            if (ReferenceEquals(Interlocked.CompareExchange(ref current, snapshot, previous), previous))
                break;
        }

        Changed?.Invoke(snapshot);
    }
}
