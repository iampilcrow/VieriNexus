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

    private static LeaseOwner Owner() => new(
        GoalId.New(),
        TaskId.New(),
        AttemptId.New(),
        0,
        "test");
}
