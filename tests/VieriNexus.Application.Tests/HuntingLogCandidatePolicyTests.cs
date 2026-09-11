using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class HuntingLogCandidatePolicyTests
{
    [Fact]
    public void PrefersIncompleteCurrentRankTargetInCurrentTerritory()
    {
        HuntingLogTargetProgress remote = Target(1, 0, 0, 0, 140);
        HuntingLogTargetProgress local = Target(1, 0, 1, 0, 141);

        HuntingLogTargetProgress? selected = HuntingLogCandidatePolicy.SelectNext(
            [remote, local],
            141,
            new HashSet<(uint, int, int, int)>());

        Assert.Same(local, selected);
    }

    [Fact]
    public void SkipsCompleteDutyFutureRankAndAttemptedTargets()
    {
        HuntingLogTargetProgress complete = Target(1, 0, 0, 0, 140) with { Killed = 3 };
        HuntingLogTargetProgress duty = Target(1, 0, 1, 0, 0) with
        {
            Locations = [new HuntingLogLocation(0, 0, 100, 0, 0)],
        };
        HuntingLogTargetProgress future = Target(1, 1, 2, 0, 141) with { IsCurrentRank = false };
        HuntingLogTargetProgress attempted = Target(1, 0, 3, 0, 142);

        HuntingLogTargetProgress? selected = HuntingLogCandidatePolicy.SelectNext(
            [complete, duty, future, attempted],
            142,
            new HashSet<(uint, int, int, int)> { (1, 0, 3, 0) });

        Assert.Null(selected);
    }

    private static HuntingLogTargetProgress Target(
        uint logKey,
        int rank,
        int task,
        int monster,
        uint territory) => new(
        logKey,
        "Class Hunting Log",
        rank,
        task,
        monster,
        100 + (uint)task,
        $"Target {task}",
        0,
        3,
        true,
        [new HuntingLogLocation(territory, 1, 0, 20, 20)]);
}
