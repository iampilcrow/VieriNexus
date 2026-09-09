using System.Text.Json;

namespace VieriNexus.Application;

public sealed record NavigationVendorTarget(
    uint TerritoryId,
    uint TargetDataId,
    NavigationRoutePoint Position);

/// <summary>
/// NPC object positions used only for interaction fallback. They are deliberately separate from
/// authored standing-point routes and may never replace a route point.
/// </summary>
public static class NavigationVendorTargetCatalog
{
    public static IReadOnlyList<NavigationVendorTarget> Targets { get; } = Array.AsReadOnly<NavigationVendorTarget>(
    [
        V(129, 1001203, -156.6034f, 18.2000f, 20.9200f),
        V(129, 1001205, -236.1034f, 16.0000f, 36.9200f),
        V(129, 1001202, -136.1034f, 18.2000f, 12.4200f),
        V(133, 1000215, 152.8512f, 15.5000f, -71.9293f),
        V(133, 1000217, 167.8366f, 15.5000f, -76.9244f),
        V(419, 1011200, -188.3116f, -12.5349f, -42.7100f),
        V(419, 1011203, -214.3844f, -16.0349f, -62.4175f),
        V(419, 1011204, -203.5784f, -16.0349f, -53.2282f),
        V(628, 1018988, 29.8923f, 4.7760f, 49.2442f),
        V(628, 1018989, 35.1788f, 4.7760f, 49.2578f),
        V(628, 1018990, 40.0461f, 4.8365f, 49.0844f),
        V(614, 1019296, -284.4060f, 17.31996f, 490.3871f),
        V(614, 1019269, 169.5713f, 5.16971f, -421.7089f),
        V(620, 1020866, -247.1199f, 257.5265f, 751.4304f),
        V(819, 1027242, -121.5391f, -1.1096f, 129.6337f),
        V(819, 1027243, -132.8298f, -1.0798f, 112.6268f),
        V(819, 1027991, -126.2379f, -1.0834f, 96.3301f),
        V(962, 1037049, 42.9011f, 5.1500f, -77.0043f),
        V(958, 1037720, -429.1346f, 22.4812f, 450.3930f),
        V(959, 1037791, -19.8631f, -132.9519f, -461.3871f),
        V(961, 1037907, 140.5236f, 10.3859f, 164.8957f),
        V(960, 1038003, 467.0165f, 437.0017f, 327.2120f),
        V(1185, 1048377, -33.0111f, -10.0000f, 79.7725f),
        V(1188, 1048851, -449.6504f, 122.1928f, 274.0082f),
        V(1189, 1048971, 626.9987f, -137.1328f, 517.8016f),
        V(1190, 1049371, -282.5980f, 18.9704f, -96.5870f),
        V(1191, 1049486, -209.3354f, 31.0000f, 129.8653f),
    ]);

    public static NavigationVendorTarget? Find(uint territoryId, uint targetDataId) =>
        Targets.SingleOrDefault(target =>
            target.TerritoryId == territoryId && target.TargetDataId == targetDataId);

    private static NavigationVendorTarget V(uint territoryId, uint targetDataId, float x, float y, float z) =>
        new(territoryId, targetDataId, new(x, y, z));
}

/// <summary>
/// Compatibility-shaped request for the suite travel provider. JSON keeps this boundary additive
/// and allows the external provider to evolve without coupling Nexus to its internal types.
/// </summary>
public static class NavigationSuiteRouteRequest
{
    public static string Create(NavigationRouteSnapshot route, NavigationRoutePlanKind kind)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (kind == NavigationRoutePlanKind.Review)
            throw new ArgumentException("A review plan cannot be dispatched.", nameof(kind));
        if (route.TerritoryId == 0 || route.Points.Count == 0)
            throw new ArgumentException("A territory and at least one route point are required.", nameof(route));

        IReadOnlyList<NavigationRoutePoint> points = kind == NavigationRoutePlanKind.TravelToStart
            ? [route.Points[0]]
            : route.Points.ToArray();
        bool includeVendor = route.BindingKind == 1 && route.TargetDataId != 0 &&
                             (kind != NavigationRoutePlanKind.TravelToStart || route.Points.Count == 1);
        NavigationRoutePoint? vendorPosition = includeVendor
            ? NavigationVendorTargetCatalog.Find(route.TerritoryId, route.TargetDataId)?.Position
            : null;

        return JsonSerializer.Serialize(new
        {
            route.TerritoryId,
            Points = points,
            route.UseFlight,
            route.UseMesh,
            route.Tolerance,
            route.LastPointTolerance,
            Mode = kind == NavigationRoutePlanKind.TravelToStart ? "travel" : "play",
            VendorTargetDataId = includeVendor ? route.TargetDataId : 0,
            VendorPosition = vendorPosition,
        });
    }
}
