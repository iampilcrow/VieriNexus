using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationBuiltInRouteCatalogTests
{
    [Fact]
    public void CatalogContainsEveryVerifiedVendorAsDisabledImmutableTemplate()
    {
        IReadOnlyList<NavigationRouteSnapshot> routes = NavigationBuiltInRouteCatalog.Routes;

        Assert.Equal(27, routes.Count);
        Assert.Equal(routes.Count, routes.Select(route => route.Id).Distinct().Count());
        Assert.Equal(routes.Count, routes.Select(route => (route.TerritoryId, route.TargetDataId)).Distinct().Count());
        Assert.All(routes, route =>
        {
            Assert.Equal(1, route.BindingKind);
            Assert.NotEqual(0u, route.TerritoryId);
            Assert.NotEqual(0u, route.TargetDataId);
            Assert.NotEmpty(route.TargetLabel);
            Assert.NotEmpty(route.Points);
            Assert.True(route.UseMesh);
            Assert.True(route.UseFlight);
            Assert.False(route.OverrideEnabled);
            Assert.Equal(.75f, route.Tolerance);
            Assert.Equal(.75f, route.LastPointTolerance);
            Assert.Equal(DateTime.UnixEpoch, route.UpdatedAtUtc);
        });
    }

    [Fact]
    public void DomitienPreservesTheAuthoredTwoPointApproach()
    {
        NavigationRouteSnapshot route = Assert.IsType<NavigationRouteSnapshot>(
            NavigationBuiltInRouteCatalog.Find(133, 1000215));

        Assert.Equal([
            new NavigationRoutePoint(164.4264f, 15.5000f, -75.7035f),
            new NavigationRoutePoint(157.5930f, 15.7000f, -69.3316f),
        ], route.Points);
    }

    [Theory]
    [InlineData(129, 1001203, -155.3658f, 18.2000f, 23.3950f)]
    [InlineData(133, 1000217, 168.4092f, 15.6999f, -73.9508f)]
    [InlineData(962, 1037049, 43.2774f, 5.1500f, -74.5438f)]
    [InlineData(1191, 1049486, -210.7973f, 31.0000f, 129.5844f)]
    public void CriticalStandingPointsRemainExact(
        uint territoryId, uint targetDataId, float x, float y, float z)
    {
        NavigationRouteSnapshot route = Assert.IsType<NavigationRouteSnapshot>(
            NavigationBuiltInRouteCatalog.Find(territoryId, targetDataId));

        Assert.Equal(new NavigationRoutePoint(x, y, z), Assert.Single(route.Points));
    }
}
