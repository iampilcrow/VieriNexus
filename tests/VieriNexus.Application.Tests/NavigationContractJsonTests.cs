using System.Text.Json;
using VieriNexus.Contracts;

namespace VieriNexus.Application.Tests;

public sealed class NavigationContractJsonTests
{
    [Fact]
    public void ReadOnlyRoutePayloadPreservesPointsAndDisabledOverride()
    {
        Guid id = Guid.Parse("5c1793cb-2590-4b40-8b86-f6dfa9aca755");
        NavigationRouteDto route = new(
            id,
            "Safe vendor hall",
            133,
            [new(1.1f, 2.2f, 3.3f), new(4.4f, 5.5f, 6.6f)],
            "Keep this note",
            "vendor,manual",
            true,
            false,
            .75f,
            1.25f,
            1,
            1000215,
            "Domitien",
            false,
            DateTime.Parse("2026-09-07T21:00:00Z").ToUniversalTime());

        using JsonDocument document = JsonDocument.Parse(NavigationContractJson.SerializeRoute(route));
        JsonElement root = document.RootElement;

        Assert.Equal(id, root.GetProperty("Id").GetGuid());
        Assert.Equal("Safe vendor hall", root.GetProperty("Name").GetString());
        Assert.Equal(2, root.GetProperty("Points").GetArrayLength());
        Assert.False(root.GetProperty("OverrideEnabled").GetBoolean());
        Assert.Equal("Domitien", root.GetProperty("TargetLabel").GetString());
    }

    [Fact]
    public void EmptyRouteListUsesStableArrayPayload()
    {
        Assert.Equal("[]", NavigationContractJson.SerializeRouteList([]));
    }

    [Fact]
    public void OverrideResolutionPayloadKeepsFailureCodeAndOptionalRoute()
    {
        NavigationRouteResolutionDto resolution = new(
            false, "route-override-not-found", "No enabled route.", null);

        using JsonDocument document = JsonDocument.Parse(
            NavigationContractJson.SerializeRouteResolution(resolution));

        Assert.False(document.RootElement.GetProperty("Success").GetBoolean());
        Assert.Equal("route-override-not-found", document.RootElement.GetProperty("Code").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("Route").ValueKind);
    }
}
