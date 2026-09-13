namespace VieriNexus.Application;

/// <summary>
/// Immutable, reviewable route templates migrated from VieriAutoDuty's verified gear-vendor approaches.
/// NPC object positions remain outside this catalog; every point is an authored walkable movement point.
/// </summary>
public static class NavigationBuiltInRouteCatalog
{
    public static IReadOnlyList<NavigationRouteSnapshot> Routes { get; } = Array.AsReadOnly<NavigationRouteSnapshot>(
    [
        Vendor(1001203, "Iron Thunder — armor", 129, [P(-155.3658f, 18.2000f, 23.3950f)]),
        Vendor(1001205, "Faezghim — weapons", 129, [P(-236.5439f, 16.2000f, 40.3006f)]),
        Vendor(1001202, "Sorcha — accessories", 129, [P(-135.1727f, 18.2000f, 14.8682f)]),
        Vendor(1000215, "Domitien — magical armor", 133,
            [P(164.4264f, 15.5000f, -75.7035f), P(157.5930f, 15.7000f, -69.3316f)]),
        Vendor(1000217, "Geraint — weapons", 133, [P(168.4092f, 15.6999f, -73.9508f)]),
        Vendor(1011200, "Seghuie — accessories", 419, [P(-189.1842f, -12.6349f, -40.0551f)]),
        Vendor(1011203, "Elbert — weapons", 419, [P(-216.0509f, -16.1262f, -60.4229f)]),
        Vendor(1011204, "Norlaise — armor", 419, [P(-205.2957f, -16.1349f, -51.2569f)]),
        Vendor(1018988, "Kugane accessories vendor", 628, [P(29.9279f, 4.0000f, 52.4925f)]),
        Vendor(1018989, "Kugane weapons vendor", 628, [P(35.2371f, 4.0000f, 52.5185f)]),
        Vendor(1018990, "Kugane armor vendor", 628, [P(40.1606f, 4.0000f, 52.5056f)]),
        Vendor(1019296, "Level 64 accessories vendor", 614, [P(-283.8307f, 17.3200f, 492.3687f)]),
        Vendor(1019269, "Level 66 accessories vendor", 614, [P(171.1704f, 5.1697f, -421.6375f)]),
        Vendor(1020866, "Level 68 accessories vendor", 620, [P(-249.5169f, 257.5265f, 750.1727f)]),
        Vendor(1027242, "Crystarium accessories vendor", 819, [P(-120.8424f, -1.0766f, 126.7847f)]),
        Vendor(1027243, "Crystarium gear vendor — first counter", 819, [P(-129.5804f, -1.0767f, 112.0974f)]),
        Vendor(1027991, "Crystarium gear vendor — second counter", 819, [P(-122.9644f, -1.0765f, 99.1908f)]),
        Vendor(1037049, "Old Sharlayan gear vendor", 962, [P(43.2774f, 5.1500f, -74.5438f)]),
        Vendor(1037720, "Level 82 gear vendor", 958, [P(-425.7329f, 22.4297f, 450.5089f)]),
        Vendor(1037791, "Level 84 gear vendor", 959, [P(-21.4712f, -132.9464f, -462.4854f)]),
        Vendor(1037907, "Level 86 gear vendor", 961, [P(140.9911f, 10.4610f, 163.3107f)]),
        Vendor(1038003, "Level 88 gear vendor", 960, [P(468.3042f, 437.0017f, 327.8175f)]),
        Vendor(1048377, "Level 90 gear vendor", 1185, [P(-30.9625f, -10.0000f, 82.3698f)]),
        Vendor(1048851, "Level 92 gear vendor", 1188, [P(-450.8014f, 121.6334f, 276.1090f)]),
        Vendor(1048971, "Level 94 gear vendor", 1189, [P(627.0523f, -137.1266f, 514.2490f)]),
        Vendor(1049371, "Level 96 gear vendor", 1190, [P(-285.4235f, 18.9721f, -96.3331f)]),
        Vendor(1049486, "Level 98 gear vendor", 1191, [P(-210.7973f, 31.0000f, 129.5844f)]),
    ]);

    public static NavigationRouteSnapshot? Find(uint territoryId, uint targetDataId) =>
        Routes.SingleOrDefault(route =>
            route.TerritoryId == territoryId && route.TargetDataId == targetDataId);

    private static NavigationRouteSnapshot Vendor(
        uint targetDataId,
        string name,
        uint territoryId,
        IReadOnlyList<NavigationRoutePoint> points) => new(
        StableId(targetDataId),
        name,
        territoryId,
        Array.AsReadOnly(points.ToArray()),
        "Verified VieriAutoDuty gear-vendor standing-point template. NPC object coordinates remain separate and are not movement points.",
        "built-in, gear vendor, verified",
        UseMesh: true,
        UseFlight: true,
        Tolerance: 0.75f,
        LastPointTolerance: 0.75f,
        BindingKind: 1,
        TargetDataId: targetDataId,
        TargetLabel: name,
        OverrideEnabled: false,
        UpdatedAtUtc: DateTime.UnixEpoch);

    private static Guid StableId(uint targetDataId) =>
        Guid.Parse($"00000000-0000-0000-0000-{targetDataId:D12}");

    private static NavigationRoutePoint P(float x, float y, float z) => new(x, y, z);
}
