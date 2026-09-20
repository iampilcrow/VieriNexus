using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationFlightPreparationPolicyTests
{
    [Theory]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, true)]
    public void UsesGroundWhenFlightIsUnavailableOrPreparationTimesOut(
        bool requested,
        bool unlocked,
        bool mounted,
        bool inFlight,
        bool timedOut)
    {
        Assert.Equal(
            NavigationFlightPreparationAction.UseGroundPath,
            NavigationFlightPreparationPolicy.Decide(requested, unlocked, mounted, inFlight, timedOut));
    }

    [Fact]
    public void MountsBeforeAttemptingTakeoff()
    {
        Assert.Equal(
            NavigationFlightPreparationAction.Mount,
            NavigationFlightPreparationPolicy.Decide(true, true, false, false, false));
    }

    [Fact]
    public void TakesOffAfterMounting()
    {
        Assert.Equal(
            NavigationFlightPreparationAction.TakeOff,
            NavigationFlightPreparationPolicy.Decide(true, true, true, false, false));
    }

    [Fact]
    public void UsesFlightOnlyAfterFlightIsConfirmed()
    {
        Assert.Equal(
            NavigationFlightPreparationAction.UseFlightPath,
            NavigationFlightPreparationPolicy.Decide(true, true, true, true, false));
    }
}
