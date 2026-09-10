using System.Text.Json;
using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationSuiteRouteRequestTests
{
    [Fact]
    public void VendorCatalogKeepsNpcFallbackSeparateForEveryBuiltInRoute()
    {
        Assert.Equal(27, NavigationVendorTargetCatalog.Targets.Count);
        Assert.Equal(27, NavigationVendorTargetCatalog.Targets
            .Select(target => (target.TerritoryId, target.TargetDataId)).Distinct().Count());
        foreach (NavigationRouteSnapshot route in NavigationBuiltInRouteCatalog.Routes)
        {
            NavigationVendorTarget target = NavigationVendorTargetCatalog.Find(
                route.TerritoryId, route.TargetDataId)!;
            Assert.NotNull(target);
            Assert.DoesNotContain(route.Points, point => point == target.Position);
        }
    }

    [Fact]
    public void PlaybackIncludesEveryPointAndKnownVendorFallback()
    {
        NavigationRouteSnapshot route = NavigationBuiltInRouteCatalog.Find(133, 1000215)!;

        using JsonDocument document = JsonDocument.Parse(
            NavigationSuiteRouteRequest.Create(route, NavigationRoutePlanKind.Playback));
        JsonElement root = document.RootElement;

        Assert.Equal("play", root.GetProperty("Mode").GetString());
        Assert.Equal(2, root.GetProperty("Points").GetArrayLength());
        Assert.Equal(1000215u, root.GetProperty("VendorTargetDataId").GetUInt32());
        Assert.Equal(152.8512f, root.GetProperty("VendorPosition").GetProperty("X").GetSingle());
    }

    [Fact]
    public void TravelToStartOfMultiPointVendorNeverClaimsVendorArrival()
    {
        NavigationRouteSnapshot route = NavigationBuiltInRouteCatalog.Find(133, 1000215)!;

        using JsonDocument document = JsonDocument.Parse(
            NavigationSuiteRouteRequest.Create(route, NavigationRoutePlanKind.TravelToStart));
        JsonElement root = document.RootElement;

        Assert.Equal("travel", root.GetProperty("Mode").GetString());
        Assert.Equal(1, root.GetProperty("Points").GetArrayLength());
        Assert.Equal(0u, root.GetProperty("VendorTargetDataId").GetUInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("VendorPosition").ValueKind);
    }

    [Fact]
    public void ArbitraryUnboundRouteCannotClaimVendorArrival()
    {
        NavigationRouteSnapshot route = new(
            Guid.NewGuid(), "Personal", 500, [new(1, 2, 3), new(4, 5, 6)],
            string.Empty, string.Empty, true, true, 0.75f, 3f,
            0, 0, string.Empty, false, DateTime.UnixEpoch);

        using JsonDocument document = JsonDocument.Parse(
            NavigationSuiteRouteRequest.Create(route, NavigationRoutePlanKind.Playback));

        Assert.Equal(0u, document.RootElement.GetProperty("VendorTargetDataId").GetUInt32());
    }

    [Fact]
    public void CreatedRequestRoundTripsIntoNexusOwnedPlaybackContract()
    {
        NavigationRouteSnapshot route = NavigationBuiltInRouteCatalog.Find(133, 1000215)!;

        NavigationSuiteRouteRequest.PlaybackRequest parsed = NavigationSuiteRouteRequest.Parse(
            NavigationSuiteRouteRequest.Create(route, NavigationRoutePlanKind.Playback))!;

        Assert.Equal(route.TerritoryId, parsed.TerritoryId);
        Assert.Equal(route.Points, parsed.Points);
        Assert.Equal(route.UseMesh, parsed.UseMesh);
        Assert.Equal(route.UseFlight, parsed.UseFlight);
        Assert.Equal(route.Tolerance, parsed.Tolerance);
        Assert.Equal(route.LastPointTolerance, parsed.LastPointTolerance);
        Assert.False(parsed.TravelOnly);
        Assert.Equal(route.TargetDataId, parsed.VendorTargetDataId);
        Assert.NotNull(parsed.VendorPosition);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"TerritoryId\":0,\"Points\":[]}")]
    [InlineData("{\"TerritoryId\":100,\"Points\":[]}")]
    public void InvalidRequestIsRejected(string json)
    {
        Assert.Null(NavigationSuiteRouteRequest.Parse(json));
    }
}
