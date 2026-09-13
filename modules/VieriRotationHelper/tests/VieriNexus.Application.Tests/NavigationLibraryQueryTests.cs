using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationLibraryQueryTests
{
    [Fact]
    public void FilterSearchesMetadataAndReturnsStableNameOrder()
    {
        NavigationRouteSnapshot zeta = Route("Zeta Path", 101, notes: "Hunt train");
        NavigationRouteSnapshot alpha = Route("Alpha Path", 202, tags: "vendor");
        NavigationLibrarySnapshot snapshot = Snapshot([zeta, alpha]);

        IReadOnlyList<NavigationRouteSnapshot> all = NavigationLibraryQuery.Filter(snapshot, null);
        IReadOnlyList<NavigationRouteSnapshot> byTag = NavigationLibraryQuery.Filter(snapshot, "VENDOR");
        IReadOnlyList<NavigationRouteSnapshot> byTerritory = NavigationLibraryQuery.Filter(snapshot, "101");

        Assert.Equal([alpha.Id, zeta.Id], all.Select(route => route.Id));
        Assert.Equal(alpha.Id, Assert.Single(byTag).Id);
        Assert.Equal(zeta.Id, Assert.Single(byTerritory).Id);
    }

    [Fact]
    public void FindAcceptsExactNameOrIdButNeverFuzzyExecutes()
    {
        NavigationRouteSnapshot route = Route("Vendor Approach", 55);
        NavigationLibrarySnapshot snapshot = Snapshot([route]);

        Assert.Same(route, NavigationLibraryQuery.Find(snapshot, route.Id.ToString()));
        Assert.Same(route, NavigationLibraryQuery.Find(snapshot, "vendor approach"));
        Assert.Null(NavigationLibraryQuery.Find(snapshot, "Vendor"));
        Assert.Null(NavigationLibraryQuery.Find(snapshot, "   "));
    }

    private static NavigationLibrarySnapshot Snapshot(IReadOnlyList<NavigationRouteSnapshot> routes) =>
        new(1, 1, 1f, .75f, true, true, true, 355f, null, routes);

    private static NavigationRouteSnapshot Route(
        string name,
        uint territoryId,
        string notes = "",
        string tags = "") =>
        new(Guid.NewGuid(), name, territoryId, [new(1, 2, 3)], notes, tags, true, false,
            .75f, 3f, 0, 0, string.Empty, false, DateTime.UtcNow);
}
