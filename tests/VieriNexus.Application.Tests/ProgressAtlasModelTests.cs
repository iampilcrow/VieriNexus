using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class ProgressAtlasModelTests
{
    [Fact]
    public void ComputesBoundedCategoryProgress()
    {
        ProgressAtlasCategorySnapshot category = ProgressAtlasModel.Category(
            ProgressAtlasCategoryId.AetherCurrents,
            "Aether Currents",
            7,
            10,
            true,
            "Live");

        Assert.Equal(3, category.Remaining);
        Assert.Equal(.7f, category.Completion, 3);
    }

    [Fact]
    public void AggregateIncludesOnlyLoadedCategories()
    {
        ProgressAtlasSnapshot snapshot = new(
            DateTimeOffset.UtcNow,
            true,
            [
                ProgressAtlasModel.Category(ProgressAtlasCategoryId.Aetherytes, "Aetherytes", 8, 10, true, "Live"),
                ProgressAtlasModel.Category(ProgressAtlasCategoryId.Achievements, "Achievements", 0, 100, false, "Loading"),
            ]);

        Assert.Equal(8, snapshot.Completed);
        Assert.Equal(10, snapshot.Total);
        Assert.Equal(2, snapshot.Remaining);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(11, 10)]
    [InlineData(1, -1)]
    public void RejectsImpossibleProgress(int completed, int total)
    {
        Assert.ThrowsAny<ArgumentException>(() => ProgressAtlasModel.Category(
            ProgressAtlasCategoryId.Achievements,
            "Achievements",
            completed,
            total,
            true,
            "Live"));
    }

    [Theory]
    [InlineData(-1, 5, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(3, 5, 3)]
    [InlineData(8, 5, 5)]
    [InlineData(2, 0, 0)]
    public void BoundsCompletedHuntingLogRanks(int currentRank, int rankCount, int expected)
    {
        Assert.Equal(expected, ProgressAtlasModel.CompletedHuntingLogRanks(currentRank, rankCount));
    }

    [Theory]
    [InlineData(2, 1, 0, 5, 5)]
    [InlineData(1, 2, 4, 5, 0)]
    [InlineData(2, 2, 3, 5, 3)]
    [InlineData(2, 2, 8, 5, 5)]
    [InlineData(2, 2, -1, 5, 0)]
    public void ResolvesExactHuntingLogKillProgress(
        int currentRank,
        int targetRank,
        int observed,
        int required,
        int expected)
    {
        Assert.Equal(expected, ProgressAtlasModel.HuntingLogKills(
            currentRank,
            targetRank,
            observed,
            required));
    }
}
