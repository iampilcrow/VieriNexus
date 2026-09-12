using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class SoloDutyCombatPolicyTests
{
    [Fact]
    public void SoloDutyEntryAlwaysStartsFreshRotationAutomationSession()
    {
        Assert.True(SoloDutyCombatPolicy.RequiresFreshRotationAutomationHandoff);
    }

    [Fact]
    public void RotationChoosesHostilesWithoutRewritingThePlayersHardTarget()
    {
        Assert.False(SoloDutyCombatPolicy.AllowsHardTargetMutation);
        Assert.Equal("SelectedTarget", SoloDutyCombatPolicy.PrimaryRotationTargetingMode);
        Assert.Equal("NearestHostile", SoloDutyCombatPolicy.FallbackRotationTargetingMode);
    }

    [Fact]
    public void NearestHostileModeOnlyActivatesAfterSustainedTargetAbsence()
    {
        Assert.False(SoloDutyCombatPolicy.ShouldEnableFallback(false, TimeSpan.FromMilliseconds(1999), true));
        Assert.True(SoloDutyCombatPolicy.ShouldEnableFallback(false, TimeSpan.FromSeconds(2), true));
        Assert.False(SoloDutyCombatPolicy.ShouldEnableFallback(true, TimeSpan.FromMinutes(1), true));
        Assert.False(SoloDutyCombatPolicy.ShouldEnableFallback(false, TimeSpan.FromMinutes(1), false));
    }

    [Fact]
    public void FallbackAlwaysYieldsAfterItsBoundedAssistWindow()
    {
        Assert.True(SoloDutyCombatPolicy.ShouldDisableFallback(true, TimeSpan.Zero));
        Assert.False(SoloDutyCombatPolicy.ShouldDisableFallback(false, TimeSpan.FromMilliseconds(1499)));
        Assert.True(SoloDutyCombatPolicy.ShouldDisableFallback(false, TimeSpan.FromMilliseconds(1500)));
    }

    [Fact]
    public void EncounterProviderRemainsTheOnlyMovementOwner()
    {
        Assert.Equal("EncounterProvider", SoloDutyCombatPolicy.MovementOwner);
    }

    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    public void NexusOnlyOwnsRotationForItsStockQuestionableSoloDuty(
        bool stockQuestionableSelected,
        bool nexusQuestActive,
        bool isInDuty,
        bool rotationProviderReady,
        bool expected)
    {
        Assert.Equal(expected, SoloDutyCombatPolicy.ShouldOwnRotation(
            stockQuestionableSelected,
            nexusQuestActive,
            isInDuty,
            rotationProviderReady));
    }
}
