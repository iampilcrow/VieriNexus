namespace VieriNexus.Application;

public sealed record NavigationArrivalCandidate(
    uint AetheryteId,
    uint TerritoryId,
    ushort AethernetGroup,
    bool IsMainAetheryte,
    string? AethernetName,
    bool IsUnlocked,
    float? X,
    float? Z);

public sealed record NavigationArrivalSelection(
    uint RootAetheryteId,
    uint RootTerritoryId,
    string? AethernetName,
    uint ArrivalAetheryteId);

/// <summary>
/// Chooses the unlocked arrival endpoint closest to the actual authored destination.
/// A city shard is useful only when its network has an unlocked main Aetheryte to teleport to.
/// </summary>
public static class NavigationEndpointPolicy
{
    public static NavigationArrivalSelection? Select(
        uint territoryId,
        float targetX,
        float targetZ,
        IReadOnlyCollection<NavigationArrivalCandidate> candidates)
    {
        if (territoryId == 0 || !float.IsFinite(targetX) || !float.IsFinite(targetZ))
            return null;

        Dictionary<ushort, NavigationArrivalCandidate> roots = candidates
            .Where(candidate => candidate.IsUnlocked && candidate.IsMainAetheryte && candidate.AethernetGroup != 0)
            .GroupBy(candidate => candidate.AethernetGroup)
            .ToDictionary(group => group.Key, group => group.OrderBy(candidate => candidate.AetheryteId).First());

        return candidates
            .Where(candidate => candidate.IsUnlocked && candidate.TerritoryId == territoryId)
            .Select(candidate => ToOption(candidate, roots, targetX, targetZ))
            .Where(option => option is not null)
            .Select(option => option!)
            .OrderBy(option => option.HasPosition ? 0 : 1)
            .ThenBy(option => option.DistanceSquared)
            .ThenBy(option => option.Selection.ArrivalAetheryteId)
            .Select(option => option.Selection)
            .FirstOrDefault();
    }

    private static ArrivalOption? ToOption(
        NavigationArrivalCandidate candidate,
        IReadOnlyDictionary<ushort, NavigationArrivalCandidate> roots,
        float targetX,
        float targetZ)
    {
        NavigationArrivalSelection selection;
        if (candidate.IsMainAetheryte)
        {
            selection = new NavigationArrivalSelection(
                candidate.AetheryteId,
                candidate.TerritoryId,
                null,
                candidate.AetheryteId);
        }
        else
        {
            if (candidate.AethernetGroup == 0 || string.IsNullOrWhiteSpace(candidate.AethernetName) ||
                !roots.TryGetValue(candidate.AethernetGroup, out NavigationArrivalCandidate? root))
                return null;
            selection = new NavigationArrivalSelection(
                root.AetheryteId,
                root.TerritoryId,
                candidate.AethernetName,
                candidate.AetheryteId);
        }

        bool hasPosition = candidate.X is not null && candidate.Z is not null;
        float distanceSquared = hasPosition
            ? Square(candidate.X!.Value - targetX) + Square(candidate.Z!.Value - targetZ)
            : float.MaxValue;
        return new ArrivalOption(selection, hasPosition, distanceSquared);
    }

    private static float Square(float value) => value * value;

    private sealed record ArrivalOption(
        NavigationArrivalSelection Selection,
        bool HasPosition,
        float DistanceSquared);
}
