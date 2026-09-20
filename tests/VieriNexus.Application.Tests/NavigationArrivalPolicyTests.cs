using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationEndpointPolicyTests
{
    [Fact]
    public void ChoosesUnlockedEndpointClosestToActualDestination()
    {
        NavigationArrivalCandidate[] candidates =
        [
            C(1, 100, 10, true, null, true, 0, 0),
            C(2, 100, 10, false, "Markets", true, 100, 100),
            C(3, 100, 10, false, "Guild", true, 10, 10),
        ];

        NavigationArrivalSelection? selected = NavigationEndpointPolicy.Select(100, 95, 98, candidates);

        Assert.NotNull(selected);
        Assert.Equal(1u, selected.RootAetheryteId);
        Assert.Equal(2u, selected.ArrivalAetheryteId);
        Assert.Equal("Markets", selected.AethernetName);
    }

    [Fact]
    public void SkipsLockedEndpointAndUsesNearestUnlockedAlternative()
    {
        NavigationArrivalCandidate[] candidates =
        [
            C(1, 100, 10, true, null, true, 0, 0),
            C(2, 100, 10, false, "Locked", false, 100, 100),
            C(3, 100, 10, false, "Unlocked", true, 25, 30),
        ];

        NavigationArrivalSelection? selected = NavigationEndpointPolicy.Select(100, 100, 100, candidates);

        Assert.NotNull(selected);
        Assert.Equal(3u, selected.ArrivalAetheryteId);
        Assert.Equal("Unlocked", selected.AethernetName);
    }

    [Fact]
    public void ShardWithoutUnlockedRootCannotBeSelected()
    {
        NavigationArrivalCandidate[] candidates =
        [
            C(1, 99, 10, true, null, false, 0, 0),
            C(2, 100, 10, false, "Shard", true, 100, 100),
            C(3, 100, 20, true, null, true, 20, 20),
        ];

        NavigationArrivalSelection? selected = NavigationEndpointPolicy.Select(100, 100, 100, candidates);

        Assert.NotNull(selected);
        Assert.Equal(3u, selected.ArrivalAetheryteId);
        Assert.Null(selected.AethernetName);
    }

    [Fact]
    public void FallsBackDeterministicallyWhenPositionsAreUnavailable()
    {
        NavigationArrivalCandidate[] candidates =
        [
            C(8, 100, 0, true, null, true, null, null),
            C(7, 100, 0, true, null, true, null, null),
        ];

        NavigationArrivalSelection? selected = NavigationEndpointPolicy.Select(100, 50, 50, candidates);

        Assert.NotNull(selected);
        Assert.Equal(7u, selected.ArrivalAetheryteId);
    }

    private static NavigationArrivalCandidate C(
        uint id,
        uint territory,
        ushort group,
        bool main,
        string? name,
        bool unlocked,
        float? x,
        float? z) => new(id, territory, group, main, name, unlocked, x, z);
}
