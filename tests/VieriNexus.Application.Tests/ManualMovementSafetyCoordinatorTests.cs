using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ManualMovementSafetyCoordinatorTests
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(1500);

    [Fact]
    public void ActiveManualInputBlocksANewExecutionUntilTheQuietPeriodEnds()
    {
        var fixture = new Fixture();

        ManualMovementSafetyResult active = fixture.Manual.Update(1000, true, true, QuietPeriod);
        ManualMovementSafetyResult quiet = fixture.Manual.Update(2000, true, false, QuietPeriod);
        ManualMovementSafetyResult ready = fixture.Manual.Update(2500, true, false, QuietPeriod);

        Assert.Equal(ManualMovementSafetyState.ManualControlDetected, active.State);
        Assert.Equal(ManualMovementSafetyState.ManualControlQuietPeriod, quiet.State);
        Assert.Equal(ManualMovementSafetyState.Monitoring, ready.State);
        Assert.False(active.CanStartExecution);
        Assert.False(quiet.CanStartExecution);
        Assert.True(ready.CanStartExecution);
        Assert.Equal(0, fixture.Provider.StopRequests);
    }

    [Fact]
    public void ManualTakeoverStopsTrackedNavigationAndRequiresExplicitResume()
    {
        var fixture = new Fixture { Provider = { MovementActive = false } };
        fixture.TrackNavigation();

        ManualMovementSafetyResult result = fixture.Manual.Update(1000, true, true, QuietPeriod);

        Assert.Equal(ManualMovementSafetyState.AwaitingExplicitResume, result.State);
        Assert.True(result.StopResult?.IsStopConfirmed);
        Assert.True(result.RequiresExplicitResume);
        Assert.True(result.IsExecutionBlocked);
        Assert.False(result.CanStartExecution);
        Assert.True(fixture.Manual.IsYieldLatched);
        Assert.Equal(1, fixture.Provider.StopRequests);
        AssertNavigationCanBeAcquired(fixture.Leases);
    }

    [Fact]
    public void UnconfirmedStopKeepsOwnershipAndRetriesWhileYieldIsLatched()
    {
        var fixture = new Fixture { Provider = { MovementActive = true } };
        fixture.TrackNavigation();

        ManualMovementSafetyResult first = fixture.Manual.Update(1000, true, true, QuietPeriod);
        ManualMovementSafetyResult second = fixture.Manual.Update(1100, true, false, QuietPeriod);

        Assert.Equal(ManualMovementSafetyState.StoppingForManualControl, first.State);
        Assert.Equal(ManualMovementSafetyState.StoppingForManualControl, second.State);
        Assert.True(first.StopResult?.NavigationLeaseRetained);
        Assert.True(second.StopResult?.NavigationLeaseRetained);
        Assert.Equal(2, fixture.Provider.StopRequests);
        AssertNavigationBlocked(fixture.Leases);
    }

    [Fact]
    public void LaterInactiveConfirmationReleasesOwnershipButDoesNotAutoResume()
    {
        var fixture = new Fixture { Provider = { MovementActive = true } };
        fixture.TrackNavigation();
        fixture.Manual.Update(1000, true, true, QuietPeriod);
        fixture.Provider.MovementActive = false;

        ManualMovementSafetyResult stopped = fixture.Manual.Update(1100, true, false, QuietPeriod);
        ManualMovementSafetyResult afterQuiet = fixture.Manual.Update(2600, true, false, QuietPeriod);

        Assert.Equal(ManualMovementSafetyState.AwaitingExplicitResume, stopped.State);
        Assert.Equal(ManualMovementSafetyState.AwaitingExplicitResume, afterQuiet.State);
        Assert.True(stopped.StopResult?.NavigationLeaseReleased);
        Assert.False(afterQuiet.CanStartExecution);
        Assert.True(afterQuiet.RequiresExplicitResume);
        AssertNavigationCanBeAcquired(fixture.Leases);
    }

    [Fact]
    public void ExplicitResumeAcknowledgementRequiresStopAndQuietPeriod()
    {
        var fixture = new Fixture { Provider = { MovementActive = false } };
        fixture.TrackNavigation();
        fixture.Manual.Update(1000, true, true, QuietPeriod);

        Assert.False(fixture.Manual.TryAcknowledgeResume(2000, QuietPeriod));
        Assert.True(fixture.Manual.TryAcknowledgeResume(2500, QuietPeriod));

        ManualMovementSafetyResult resumed = fixture.Manual.Update(2501, true, false, QuietPeriod);
        Assert.Equal(ManualMovementSafetyState.Monitoring, resumed.State);
        Assert.True(resumed.CanStartExecution);
    }

    [Fact]
    public void DisablingProtectionDuringTrackedNavigationFailsClosedAndStops()
    {
        var fixture = new Fixture { Provider = { MovementActive = false } };
        fixture.TrackNavigation();

        ManualMovementSafetyResult result = fixture.Manual.Update(1000, false, false, QuietPeriod);

        Assert.Equal(ManualMovementSafetyState.AwaitingExplicitResume, result.State);
        Assert.True(result.IsExecutionBlocked);
        Assert.True(result.StopResult?.IsStopConfirmed);
        Assert.True(fixture.Manual.IsYieldLatched);
    }

    [Fact]
    public void DisabledProtectionWithoutExecutionBlocksActivationWithoutCallingStop()
    {
        var fixture = new Fixture();

        ManualMovementSafetyResult result = fixture.Manual.Update(1000, false, false, QuietPeriod);

        Assert.Equal(ManualMovementSafetyState.Disabled, result.State);
        Assert.False(result.CanStartExecution);
        Assert.Equal(0, fixture.Provider.StopRequests);
    }

    [Fact]
    public void ObservationTimeMustBeMonotonic()
    {
        var fixture = new Fixture();
        fixture.Manual.Update(1000, true, false, QuietPeriod);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.Manual.Update(999, true, false, QuietPeriod));
    }

    private static void AssertNavigationBlocked(ResourceLeaseManager leases) =>
        Assert.False(leases.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out _, out _));

    private static void AssertNavigationCanBeAcquired(ResourceLeaseManager leases)
    {
        Assert.True(leases.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var next, out _));
        next!.Dispose();
    }

    private static LeaseOwner Owner() => new(
        GoalId.New(),
        TaskId.New(),
        AttemptId.New(),
        0,
        "manual-movement-test");

    private sealed class Fixture
    {
        internal ResourceLeaseManager Leases { get; } = new();
        internal FakeProvider Provider { get; } = new();
        internal NavigationStopCoordinator Stop { get; }
        internal ManualMovementSafetyCoordinator Manual { get; }

        internal Fixture()
        {
            Stop = new NavigationStopCoordinator(Provider, TimeSpan.FromMinutes(1));
            Manual = new ManualMovementSafetyCoordinator(Stop);
        }

        internal void TrackNavigation()
        {
            Assert.True(Leases.TryAcquire(Owner(), [ResourceKind.Navigation], TimeSpan.FromMinutes(1), out var lease, out _));
            Stop.TrackExecution(lease!);
        }
    }

    private sealed class FakeProvider : INavigationStopProvider
    {
        internal bool? MovementActive { get; set; }
        internal int StopRequests { get; private set; }

        public bool IsAvailable => true;

        public void RequestStop() => StopRequests++;

        public bool? IsMovementActive() => MovementActive;
    }
}
