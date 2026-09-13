using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRouteOverrideResolverTests
{
    [Fact]
    public void ResolvesOnlyTheEnabledExactTarget()
    {
        NavigationRouteSnapshot disabled = Route(133, 1000215, false, "Disabled");
        NavigationRouteSnapshot enabled = Route(133, 1000215, true, "Enabled");
        NavigationRouteSnapshot otherTerritory = Route(129, 1000215, true, "Other territory");
        NavigationLibrarySnapshot library = Library([disabled, enabled, otherTerritory]);

        NavigationRouteOverrideResolution result = NavigationRouteOverrideResolver.ResolveGearVendor(
            library, 133, 1000215);

        Assert.True(result.Success);
        Assert.Equal("route-override-resolved", result.Code);
        Assert.Equal(enabled.Id, result.Route?.Id);
    }

    [Fact]
    public void DisabledOrMismatchedRoutesDoNotResolve()
    {
        NavigationLibrarySnapshot library = Library([Route(133, 1000215, false, "Disabled")]);

        NavigationRouteOverrideResolution disabled = NavigationRouteOverrideResolver.ResolveGearVendor(
            library, 133, 1000215);
        NavigationRouteOverrideResolution wrongTarget = NavigationRouteOverrideResolver.ResolveGearVendor(
            library, 133, 1000217);

        Assert.False(disabled.Success);
        Assert.Equal("route-override-not-found", disabled.Code);
        Assert.False(wrongTarget.Success);
        Assert.Equal("route-override-not-found", wrongTarget.Code);
    }

    [Fact]
    public void AmbiguousEnabledRoutesFailClosed()
    {
        NavigationLibrarySnapshot library = Library([
            Route(133, 1000215, true, "First"),
            Route(133, 1000215, true, "Second"),
        ]);

        NavigationRouteOverrideResolution result = NavigationRouteOverrideResolver.ResolveGearVendor(
            library, 133, 1000215);

        Assert.False(result.Success);
        Assert.Equal("route-override-ambiguous", result.Code);
        Assert.Null(result.Route);
    }

    [Fact]
    public void InvalidTargetAndInvalidRouteFailClosed()
    {
        NavigationRouteOverrideResolution invalidTarget = NavigationRouteOverrideResolver.ResolveGearVendor(
            Library([]), 0, 1000215);
        NavigationRouteSnapshot noPoints = Route(133, 1000215, true, "No points") with { Points = [] };
        NavigationRouteOverrideResolution invalidRoute = NavigationRouteOverrideResolver.ResolveGearVendor(
            Library([noPoints]), 133, 1000215);

        Assert.Equal("route-target-invalid", invalidTarget.Code);
        Assert.Equal("route-override-invalid", invalidRoute.Code);
    }

    [Fact]
    public void EnablingRouteAtomicallyDisablesOnlyItsSameTargetCompetitor()
    {
        DateTime now = DateTime.Parse("2026-09-09T12:00:00Z").ToUniversalTime();
        NavigationRouteSnapshot selected = Route(133, 1000215, false, "Selected");
        NavigationRouteSnapshot competitor = Route(133, 1000215, true, "Competitor");
        NavigationRouteSnapshot otherTarget = Route(133, 1000217, true, "Other target");

        NavigationRouteOverrideUpdate result = NavigationRouteOverrideResolver.SetExclusive(
            Library([selected, competitor, otherTarget]), selected.Id, true, now);

        Assert.True(result.Success);
        Assert.Equal(selected.Id, result.Library?.SelectedRouteId);
        Assert.True(result.Library?.Routes.Single(route => route.Id == selected.Id).OverrideEnabled);
        Assert.False(result.Library?.Routes.Single(route => route.Id == competitor.Id).OverrideEnabled);
        Assert.True(result.Library?.Routes.Single(route => route.Id == otherTarget.Id).OverrideEnabled);
        Assert.Equal(now, result.Library?.Routes.Single(route => route.Id == selected.Id).UpdatedAtUtc);
    }

    [Fact]
    public void InvalidAssignmentCannotBeEnabled()
    {
        NavigationRouteSnapshot unbound = Route(133, 1000215, false, "Unbound") with
        {
            BindingKind = 0,
            TargetDataId = 0,
        };
        NavigationRouteSnapshot empty = Route(133, 1000215, false, "Empty") with { Points = [] };

        NavigationRouteOverrideUpdate unboundResult = NavigationRouteOverrideResolver.SetExclusive(
            Library([unbound]), unbound.Id, true, DateTime.UtcNow);
        NavigationRouteOverrideUpdate emptyResult = NavigationRouteOverrideResolver.SetExclusive(
            Library([empty]), empty.Id, true, DateTime.UtcNow);

        Assert.Equal("route-binding-required", unboundResult.Code);
        Assert.Equal("route-points-required", emptyResult.Code);
        Assert.Null(unboundResult.Library);
        Assert.Null(emptyResult.Library);
    }

    private static NavigationRouteSnapshot Route(
        uint territoryId, uint targetDataId, bool enabled, string name) => new(
        Guid.NewGuid(), name, territoryId, [new(1, 2, 3)], string.Empty, "vendor",
        true, true, .75f, .75f, 1, targetDataId, name, enabled, DateTime.UtcNow);

    private static NavigationLibrarySnapshot Library(IReadOnlyList<NavigationRouteSnapshot> routes) => new(
        1, 1, 1, .75f, true, true, true, 320, null, routes);
}
