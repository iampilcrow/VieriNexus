using VieriNexus.Domain;

namespace VieriNexus.Application;

public sealed record LeaseOwner(
    GoalId GoalId,
    TaskId TaskId,
    AttemptId AttemptId,
    int Priority,
    string Reason);

public sealed record ResourceLeaseSnapshot(
    Guid LeaseId,
    LeaseOwner Owner,
    IReadOnlySet<ResourceKind> Resources,
    DateTimeOffset AcquiredAt,
    DateTimeOffset ExpiresAt);

public sealed class ResourceLeaseManager
{
    private readonly object sync = new();
    private readonly Dictionary<ResourceKind, Lease> owners = [];
    private readonly Dictionary<Guid, ResourceLeaseSnapshot> expiredAwaitingWatchdog = [];
    private readonly Func<DateTimeOffset> utcNow;

    public ResourceLeaseManager(Func<DateTimeOffset>? utcNow = null)
    {
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public bool TryAcquire(
        LeaseOwner owner,
        IEnumerable<ResourceKind> requestedResources,
        TimeSpan lifetime,
        out ResourceLeaseHandle? handle,
        out ResourceLeaseSnapshot? blockingLease)
    {
        ArgumentNullException.ThrowIfNull(requestedResources);
        var requested = Expand(requestedResources).Distinct().Order().ToArray();
        if (requested.Length == 0)
            throw new ArgumentException("At least one resource is required.", nameof(requestedResources));
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        lock (sync)
        {
            DateTimeOffset now = utcNow();
            ExpireDeadLeases(now);
            foreach (var resource in requested)
            {
                if (!owners.TryGetValue(resource, out var current))
                    continue;

                handle = null;
                blockingLease = current.Snapshot;
                return false;
            }

            var lease = new Lease(Guid.NewGuid(), owner, requested.ToHashSet(), now, lifetime);
            foreach (var resource in requested)
                owners.Add(resource, lease);

            handle = new ResourceLeaseHandle(this, lease.Id);
            blockingLease = null;
            return true;
        }
    }

    public IReadOnlyList<ResourceLeaseSnapshot> Snapshot()
    {
        lock (sync)
        {
            ExpireDeadLeases(utcNow());
            return owners.Values.DistinctBy(x => x.Id).Select(x => x.Snapshot).ToArray();
        }
    }

    /// <summary>
    /// Actively expires dead leases and returns every expiration that has not yet been
    /// observed by the watchdog. Expirations discovered by acquisition, snapshots, or a
    /// late heartbeat are retained here so a safety response cannot be skipped.
    /// </summary>
    public IReadOnlyList<ResourceLeaseSnapshot> SweepExpired(DateTimeOffset now)
    {
        lock (sync)
        {
            ExpireDeadLeases(now);
            ResourceLeaseSnapshot[] expired = expiredAwaitingWatchdog.Values.ToArray();
            expiredAwaitingWatchdog.Clear();
            return expired;
        }
    }

    public bool LeaseOwns(Guid leaseId, params ResourceKind[] requiredResources)
    {
        ArgumentNullException.ThrowIfNull(requiredResources);
        if (leaseId == Guid.Empty || requiredResources.Length == 0)
            return false;

        lock (sync)
        {
            ExpireDeadLeases(utcNow());
            Lease? lease = owners.Values.FirstOrDefault(x => x.Id == leaseId);
            return lease is not null && requiredResources.All(lease.Resources.Contains);
        }
    }

    internal bool Heartbeat(Guid leaseId, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
            return false;

        lock (sync)
        {
            ExpireDeadLeases(utcNow());
            var lease = owners.Values.FirstOrDefault(x => x.Id == leaseId);
            if (lease is null)
                return false;
            lease.ExpiresAt = utcNow() + lifetime;
            return true;
        }
    }

    internal void Release(Guid leaseId)
    {
        lock (sync)
        {
            foreach (var resource in owners.Where(x => x.Value.Id == leaseId).Select(x => x.Key).ToArray())
                owners.Remove(resource);
        }
    }

    private void ExpireDeadLeases(DateTimeOffset now)
    {
        Lease[] expired = owners.Values
            .DistinctBy(x => x.Id)
            .Where(x => x.ExpiresAt <= now)
            .ToArray();
        foreach (Lease lease in expired)
        {
            foreach (ResourceKind resource in owners
                         .Where(x => x.Value.Id == lease.Id)
                         .Select(x => x.Key)
                         .ToArray())
            {
                owners.Remove(resource);
            }

            expiredAwaitingWatchdog.TryAdd(lease.Id, lease.Snapshot);
        }
    }

    private static IEnumerable<ResourceKind> Expand(IEnumerable<ResourceKind> requested)
    {
        foreach (var resource in requested)
        {
            yield return resource;
            switch (resource)
            {
                case ResourceKind.Navigation:
                    yield return ResourceKind.Movement;
                    break;
                case ResourceKind.Teleport:
                    yield return ResourceKind.Movement;
                    yield return ResourceKind.Navigation;
                    break;
                case ResourceKind.Combat:
                    yield return ResourceKind.Targeting;
                    break;
                case ResourceKind.Rotation:
                    yield return ResourceKind.Combat;
                    yield return ResourceKind.Targeting;
                    break;
                case ResourceKind.Retainer:
                    yield return ResourceKind.UiInteraction;
                    break;
                case ResourceKind.Market:
                    yield return ResourceKind.Retainer;
                    yield return ResourceKind.UiInteraction;
                    yield return ResourceKind.InventoryMutation;
                    break;
            }
        }
    }

    private sealed class Lease(Guid id, LeaseOwner owner, HashSet<ResourceKind> resources, DateTimeOffset acquiredAt, TimeSpan lifetime)
    {
        internal Guid Id { get; } = id;
        internal LeaseOwner Owner { get; } = owner;
        internal HashSet<ResourceKind> Resources { get; } = resources;
        internal DateTimeOffset AcquiredAt { get; } = acquiredAt;
        internal DateTimeOffset ExpiresAt { get; set; } = acquiredAt + lifetime;
        internal ResourceLeaseSnapshot Snapshot => new(Id, Owner, Resources, AcquiredAt, ExpiresAt);
    }
}

public sealed class ResourceLeaseHandle : IDisposable
{
    private readonly ResourceLeaseManager manager;
    private readonly Guid leaseId;
    private int disposed;

    internal ResourceLeaseHandle(ResourceLeaseManager manager, Guid leaseId)
    {
        this.manager = manager;
        this.leaseId = leaseId;
    }

    public Guid LeaseId => leaseId;

    public bool Heartbeat(TimeSpan lifetime) => Volatile.Read(ref disposed) == 0 && manager.Heartbeat(leaseId, lifetime);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            manager.Release(leaseId);
    }
}
