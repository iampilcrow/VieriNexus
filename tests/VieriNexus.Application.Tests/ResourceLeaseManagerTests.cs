using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ResourceLeaseManagerTests
{
    [Fact]
    public void NavigationAlsoOwnsMovement()
    {
        var manager = new ResourceLeaseManager();
        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));

        Assert.False(manager.TryAcquire(Owner(), [ResourceKind.Movement], TimeSpan.FromMinutes(1), out _, out var blocker));
        Assert.NotNull(blocker);

        lease!.Dispose();
        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Movement], TimeSpan.FromMinutes(1), out var next, out _));
        next!.Dispose();
    }

    [Fact]
    public void FailedBundleAcquisitionOwnsNothing()
    {
        var manager = new ResourceLeaseManager();
        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Movement], TimeSpan.FromMinutes(1), out var movement, out _));

        Assert.False(manager.TryAcquire(Owner(), [ResourceKind.Movement, ResourceKind.Market], TimeSpan.FromMinutes(1), out _, out _));
        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Market], TimeSpan.FromMinutes(1), out var market, out _));

        movement!.Dispose();
        market!.Dispose();
    }

    [Fact]
    public void MarketOwnsRetainerUiAndInventoryMutation()
    {
        var manager = new ResourceLeaseManager();
        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Market], TimeSpan.FromMinutes(1), out var lease, out _));

        var resources = Assert.Single(manager.Snapshot()).Resources;
        Assert.Contains(ResourceKind.Market, resources);
        Assert.Contains(ResourceKind.Retainer, resources);
        Assert.Contains(ResourceKind.UiInteraction, resources);
        Assert.Contains(ResourceKind.InventoryMutation, resources);
        lease!.Dispose();
    }

    [Fact]
    public void WatchdogReceivesExpiredLeaseExactlyOnce()
    {
        var manager = new ResourceLeaseManager();
        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));

        ResourceLeaseSnapshot expired = Assert.Single(
            manager.SweepExpired(DateTimeOffset.UtcNow.AddMinutes(2)));

        Assert.Equal(lease!.LeaseId, expired.LeaseId);
        Assert.Contains(ResourceKind.Navigation, expired.Resources);
        Assert.Contains(ResourceKind.Movement, expired.Resources);
        Assert.Empty(manager.SweepExpired(DateTimeOffset.UtcNow.AddMinutes(3)));
        Assert.False(lease.Heartbeat(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void ExpirationFoundDuringAcquireIsRetainedForTheWatchdog()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var manager = new ResourceLeaseManager(() => now);
        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Targeting], TimeSpan.FromMilliseconds(1), out _, out _));
        now = now.AddSeconds(1);

        Assert.True(manager.TryAcquire(Owner(), [ResourceKind.Targeting], TimeSpan.FromMinutes(1), out var next, out _));
        ResourceLeaseSnapshot expired = Assert.Single(manager.SweepExpired(now));

        Assert.Contains(ResourceKind.Targeting, expired.Resources);
        next!.Dispose();
    }

    private static LeaseOwner Owner() => new(
        GoalId.New(),
        TaskId.New(),
        AttemptId.New(),
        0,
        "test");
}
