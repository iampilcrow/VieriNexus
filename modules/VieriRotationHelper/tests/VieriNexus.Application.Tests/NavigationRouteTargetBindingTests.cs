using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRouteTargetBindingTests
{
    [Fact]
    public void CapturingTargetNeverEnablesOverride()
    {
        NavigationRouteSnapshot route = Route() with { BindingKind = 1, OverrideEnabled = true };

        NavigationRouteBindingUpdate result = NavigationRouteTargetBinding.BindCurrentTarget(
            route, 129, 1001205, "Faezghim", DateTime.UnixEpoch.AddDays(1));

        Assert.True(result.Success);
        Assert.Equal(1001205u, result.Route!.TargetDataId);
        Assert.Equal("Faezghim", result.Route.TargetLabel);
        Assert.False(result.Route.OverrideEnabled);
    }

    [Fact]
    public void ExistingPointsPreventCrossTerritoryTargetBinding()
    {
        NavigationRouteSnapshot route = Route() with { BindingKind = 1 };

        NavigationRouteBindingUpdate result = NavigationRouteTargetBinding.BindCurrentTarget(
            route, 133, 1000215, "Domitien", DateTime.UnixEpoch);

        Assert.False(result.Success);
        Assert.Equal("route-target-territory-mismatch", result.Code);
    }

    [Fact]
    public void RemovingBindingClearsTargetAndApproval()
    {
        NavigationRouteSnapshot route = Route() with
        {
            BindingKind = 1,
            TargetDataId = 1001205,
            TargetLabel = "Faezghim",
            OverrideEnabled = true,
        };

        NavigationRouteBindingUpdate result = NavigationRouteTargetBinding.SetKind(
            route, 0, DateTime.UnixEpoch.AddDays(1));

        Assert.True(result.Success);
        Assert.Equal(0, result.Route!.BindingKind);
        Assert.Equal(0u, result.Route.TargetDataId);
        Assert.Equal(string.Empty, result.Route.TargetLabel);
        Assert.False(result.Route.OverrideEnabled);
    }

    private static NavigationRouteSnapshot Route() => new(
        Guid.NewGuid(), "Test", 129, [new(1, 2, 3)],
        string.Empty, string.Empty, true, false, 0.75f, 3f,
        0, 0, string.Empty, false, DateTime.UnixEpoch);
}
