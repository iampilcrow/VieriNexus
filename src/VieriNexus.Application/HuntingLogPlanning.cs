namespace VieriNexus.Application;

public sealed record HuntingLogLocation(
    uint TerritoryId,
    uint MapId,
    uint DutyTerritoryId,
    float MapX,
    float MapY)
{
    public bool IsOpenWorld => DutyTerritoryId == 0 && TerritoryId != 0;
}

public sealed record HuntingLogTargetProgress(
    uint LogKey,
    string LogName,
    int Rank,
    int TaskIndex,
    int MonsterIndex,
    uint NameId,
    string TargetName,
    int Killed,
    int Required,
    bool IsCurrentRank,
    IReadOnlyList<HuntingLogLocation> Locations)
{
    public bool IsComplete => Killed >= Required;
    public bool HasOpenWorldLocation => Locations.Any(location => location.IsOpenWorld);
}

public static class HuntingLogCandidatePolicy
{
    public static HuntingLogTargetProgress? SelectNext(
        IEnumerable<HuntingLogTargetProgress> targets,
        uint currentTerritoryId,
        IReadOnlySet<(uint LogKey, int Rank, int TaskIndex, int MonsterIndex)> attempted)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(attempted);

        return targets
            .Where(target => target.IsCurrentRank && !target.IsComplete && target.HasOpenWorldLocation)
            .Where(target => !attempted.Contains((target.LogKey, target.Rank, target.TaskIndex, target.MonsterIndex)))
            .OrderByDescending(target => target.Locations.Any(location =>
                location.IsOpenWorld && location.TerritoryId == currentTerritoryId))
            .ThenBy(target => target.LogKey >= 10_000 ? 1 : 0)
            .ThenBy(target => target.Rank)
            .ThenBy(target => target.TaskIndex)
            .ThenBy(target => target.MonsterIndex)
            .FirstOrDefault();
    }
}
