using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationDiagnosticsMonitorTests
{
    [Fact]
    public void InitialObservationRecordsEachProviderOnce()
    {
        var monitor = new NavigationDiagnosticsMonitor();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        NavigationDiagnosticsSnapshot snapshot = monitor.Observe(
            [Provider("stop", NavigationDiagnosticState.Healthy, "ready")], now);

        Assert.Single(snapshot.Providers);
        NavigationAuditEntry entry = Assert.Single(snapshot.RecentTransitions);
        Assert.Equal("stop", entry.ProviderId);
        Assert.Equal(now, entry.ObservedAtUtc);
    }

    [Fact]
    public void RepeatedFrameDoesNotFloodTheAudit()
    {
        var monitor = new NavigationDiagnosticsMonitor();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        monitor.Observe([Provider("stop", NavigationDiagnosticState.Healthy, "ready")], now);

        NavigationDiagnosticsSnapshot snapshot = monitor.Observe(
            [Provider("stop", NavigationDiagnosticState.Healthy, "ready", "new detail")],
            now.AddSeconds(1));

        Assert.Single(snapshot.RecentTransitions);
        Assert.Equal("new detail", Assert.Single(snapshot.Providers).Detail);
    }

    [Fact]
    public void StateOrCodeChangeCreatesNewestFirstTransition()
    {
        var monitor = new NavigationDiagnosticsMonitor();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        monitor.Observe([Provider("stop", NavigationDiagnosticState.Healthy, "ready")], now);

        NavigationDiagnosticsSnapshot snapshot = monitor.Observe(
            [Provider("stop", NavigationDiagnosticState.Blocked, "missing")],
            now.AddSeconds(1));

        Assert.Equal(2, snapshot.RecentTransitions.Count);
        Assert.Equal("missing", snapshot.RecentTransitions[0].Code);
        Assert.Equal("ready", snapshot.RecentTransitions[1].Code);
    }

    [Fact]
    public void AuditCapacityIsBounded()
    {
        var monitor = new NavigationDiagnosticsMonitor(capacity: 2);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        monitor.Observe([Provider("stop", NavigationDiagnosticState.Healthy, "one")], now);
        monitor.Observe([Provider("stop", NavigationDiagnosticState.Attention, "two")], now.AddSeconds(1));

        NavigationDiagnosticsSnapshot snapshot = monitor.Observe(
            [Provider("stop", NavigationDiagnosticState.Blocked, "three")],
            now.AddSeconds(2));

        Assert.Equal(2, snapshot.RecentTransitions.Count);
        Assert.Equal(["three", "two"], snapshot.RecentTransitions.Select(entry => entry.Code));
    }

    [Fact]
    public void DuplicateProviderIdsAreRejected()
    {
        var monitor = new NavigationDiagnosticsMonitor();

        Assert.Throws<ArgumentException>(() => monitor.Observe(
            [
                Provider("stop", NavigationDiagnosticState.Healthy, "ready"),
                Provider("stop", NavigationDiagnosticState.Blocked, "missing"),
            ],
            DateTimeOffset.UtcNow));
    }

    private static NavigationProviderDiagnostic Provider(
        string id,
        NavigationDiagnosticState state,
        string code,
        string detail = "detail") =>
        new(id, id, state, null, code, detail);
}
