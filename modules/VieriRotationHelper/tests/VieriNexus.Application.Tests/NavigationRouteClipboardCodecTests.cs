using System.Text.Json;
using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRouteClipboardCodecTests
{
    [Fact]
    public void RoundTripPreservesRouteButRegeneratesIdentityAndDisablesOverride()
    {
        NavigationRouteSnapshot source = Route() with { OverrideEnabled = true };

        string json = NavigationRouteClipboardCodec.Write(source);
        NavigationRouteClipboardReadResult result = NavigationRouteClipboardCodec.Read(
            json, new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(result.Success);
        Assert.NotNull(result.Route);
        Assert.NotEqual(source.Id, result.Route.Id);
        Assert.Equal(source.Name, result.Route.Name);
        Assert.Equal(source.Points, result.Route.Points);
        Assert.Equal(source.BindingKind, result.Route.BindingKind);
        Assert.Equal(source.TargetDataId, result.Route.TargetDataId);
        Assert.False(result.Route.OverrideEnabled);
    }

    [Fact]
    public void LegacyNavPlotterShapeWithoutSchemaIsAccepted()
    {
        const string json = """
            {
              "Id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "Name": "Legacy path",
              "TerritoryId": 100,
              "Points": [{ "X": 1, "Y": 2, "Z": 3 }],
              "Notes": "note",
              "Tags": "tag",
              "UseMesh": true,
              "UseFlight": false,
              "Tolerance": 0.75,
              "LastPointTolerance": 3,
              "BindingKind": 0,
              "TargetDataId": 0,
              "TargetLabel": "",
              "OverrideEnabled": false
            }
            """;

        NavigationRouteClipboardReadResult result = NavigationRouteClipboardCodec.Read(json, DateTime.UtcNow);

        Assert.True(result.Success);
        Assert.Equal("Legacy path", result.Route!.Name);
        Assert.Single(result.Route.Points);
    }

    [Fact]
    public void NewerSchemaIsRejected()
    {
        NavigationRouteClipboardData data = Data() with { SchemaVersion = 2 };

        NavigationRouteClipboardReadResult result = NavigationRouteClipboardCodec.Read(
            JsonSerializer.Serialize(data), DateTime.UtcNow);

        Assert.False(result.Success);
        Assert.Contains("not supported", result.Message);
    }

    [Fact]
    public void MissingPointCollectionIsRejected()
    {
        NavigationRouteClipboardData data = Data() with { Points = null };

        NavigationRouteClipboardReadResult result = NavigationRouteClipboardCodec.Read(
            JsonSerializer.Serialize(data), DateTime.UtcNow);

        Assert.False(result.Success);
        Assert.Contains("point collection", result.Message);
    }

    [Fact]
    public void ImportedToleranceIsClampedAndTextIsBounded()
    {
        NavigationRouteClipboardData data = Data() with
        {
            Name = new string('N', 140),
            Tolerance = -4,
            LastPointTolerance = 99,
        };

        NavigationRouteClipboardReadResult result = NavigationRouteClipboardCodec.Read(
            JsonSerializer.Serialize(data), DateTime.UtcNow);

        Assert.True(result.Success);
        Assert.Equal(120, result.Route!.Name.Length);
        Assert.Equal(0.1f, result.Route.Tolerance);
        Assert.Equal(30f, result.Route.LastPointTolerance);
    }

    private static NavigationRouteSnapshot Route() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Test route",
        100,
        [new(1, 2, 3), new(4, 5, 6)],
        "Notes",
        "Tags",
        true,
        false,
        0.75f,
        3f,
        1,
        1234,
        "Vendor",
        false,
        DateTime.UtcNow);

    private static NavigationRouteClipboardData Data() => new(
        1,
        Guid.NewGuid(),
        "Route",
        100,
        [new(1, 2, 3)],
        string.Empty,
        string.Empty,
        true,
        false,
        0.75f,
        3f,
        0,
        0,
        string.Empty,
        false,
        DateTime.UtcNow);
}
