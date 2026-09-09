using VieriNexus.Domain;

namespace VieriNexus.Application;

public sealed record NavigationSimulationScenario(
    string Name,
    bool Passed,
    string Detail);

public sealed record NavigationSimulationReport(
    DateTimeOffset RanAtUtc,
    bool Passed,
    IReadOnlyList<NavigationSimulationScenario> Scenarios)
{
    public int PassedCount => Scenarios.Count(scenario => scenario.Passed);
}

/// <summary>
/// Exercises the production safety coordinators against isolated memory-only state.
/// The provider records Stop calls but has no movement operation, so this cannot move
/// the player, acquire a live plugin lease, or change the live execution journal.
/// </summary>
public sealed class NavigationSafetySimulator
{
    public NavigationSimulationReport Run(DateTimeOffset now)
    {
        NavigationSimulationScenario[] scenarios =
        [
            RunScenario("Guarded start and verified Stop", () => StartAndStop(now)),
            RunScenario("Manual movement takeover", () => ManualTakeover(now)),
            RunScenario("No-replay reload recovery", () => ReloadRecovery(now)),
            RunScenario("Source-owner return", () => SourceReturn(now)),
            RunScenario("Lease-expiry watchdog", () => LeaseExpiry(now)),
        ];
        return new NavigationSimulationReport(now, scenarios.All(scenario => scenario.Passed), scenarios);
    }

    private static string StartAndStop(DateTimeOffset now)
    {
        SimulationSetup setup = Create(now);
        Arm(setup, now, TimeSpan.FromMinutes(1));
        NavigationExecutionSafetyStatus stopped = setup.Safety.Shutdown(now.AddSeconds(1));
        bool acknowledged = setup.Safety.AcknowledgeInterruptedIntent(now.AddSeconds(2));
        Require(stopped.State == NavigationExecutionSafetyState.AwaitingExplicitResume,
            "verified Stop did not produce the no-replay checkpoint");
        Require(acknowledged, "the stopped checkpoint could not be acknowledged");
        Require(setup.Store.Current?.State == NavigationExecutionIntentState.Completed,
            "the isolated journal did not complete");
        Require(setup.Leases.Snapshot().Count == 0, "the isolated lease was not released");
        return "Start was journaled before movement, Stop was confirmed, and ownership was released.";
    }

    private static string ManualTakeover(DateTimeOffset now)
    {
        SimulationSetup setup = Create(now);
        Arm(setup, now, TimeSpan.FromMinutes(1));
        var manual = new ManualMovementSafetyCoordinator(setup.Stop);
        ManualMovementSafetyResult result = manual.Update(
            1_000,
            protectionEnabled: true,
            manualMovementInputActive: true,
            TimeSpan.FromMilliseconds(500));
        NavigationExecutionSafetyStatus reconciled = setup.Safety.Update(now.AddSeconds(1));
        Require(result.RequiresExplicitResume, "manual takeover did not latch explicit resume");
        Require(reconciled.State == NavigationExecutionSafetyState.AwaitingExplicitResume,
            "external Stop did not become a no-replay checkpoint");
        Require(setup.Leases.Snapshot().Count == 0, "manual takeover retained an inactive lease");
        return "Player input won, verified Stop completed, and automatic restart stayed blocked.";
    }

    private static string ReloadRecovery(DateTimeOffset now)
    {
        SimulationSetup first = Create(now);
        Arm(first, now, TimeSpan.FromMinutes(1));

        var reloadedProvider = new SimulationStopProvider();
        var reloadedLeases = new ResourceLeaseManager(() => now.AddSeconds(1));
        var reloadedStop = new NavigationStopCoordinator(reloadedProvider, TimeSpan.FromMinutes(1));
        var reloadedSafety = new NavigationExecutionSafetyCoordinator(
            reloadedLeases,
            reloadedStop,
            reloadedProvider,
            first.Store);
        NavigationExecutionSafetyStatus recovered = reloadedSafety.Update(now.AddSeconds(1));
        Require(recovered.State == NavigationExecutionSafetyState.AwaitingExplicitResume,
            "reload did not stop at an explicit checkpoint");
        Require(reloadedProvider.StopRequests == 1, "reload did not request Stop exactly once");
        Require(first.Store.Current?.State == NavigationExecutionIntentState.AwaitingExplicitResume,
            "reload changed or replayed the saved intent incorrectly");
        return "Reload requested Stop, confirmed inactivity, and never replayed saved intent.";
    }

    private static string SourceReturn(DateTimeOffset now)
    {
        SimulationSetup setup = Create(now);
        Arm(setup, now, TimeSpan.FromMinutes(1));
        setup.Prerequisites.Value = Ready() with { SourcePluginLoaded = true };
        NavigationAuthorityStatus status = setup.Authority.Update();
        Require(status.State == NavigationAuthorityState.Revoked, "source return did not revoke authority");
        Require(setup.Provider.StopRequests == 1, "source return did not request verified Stop");
        Require(setup.Leases.Snapshot().Count == 0, "source return did not release the stopped lease");
        return "Source reappearance revoked Nexus authority and completed verified Stop.";
    }

    private static string LeaseExpiry(DateTimeOffset now)
    {
        DateTimeOffset clock = now;
        SimulationSetup setup = Create(now, () => clock);
        Arm(setup, now, TimeSpan.FromSeconds(1));
        clock = now.AddSeconds(2);
        NavigationExecutionSafetyStatus status = setup.Safety.Update(clock);
        Require(status.State == NavigationExecutionSafetyState.AwaitingExplicitResume,
            "expired ownership did not end at a no-replay checkpoint");
        Require(status.Code == "expired-lease-stopped", "the expiry watchdog did not identify the transition");
        Require(setup.Provider.StopRequests == 1, "lease expiry did not request verified Stop");
        return "The missed heartbeat triggered Stop and blocked automatic restart.";
    }

    private static NavigationSimulationScenario RunScenario(string name, Func<string> scenario)
    {
        try
        {
            return new NavigationSimulationScenario(name, true, scenario());
        }
        catch (Exception ex)
        {
            return new NavigationSimulationScenario(name, false, ex.Message);
        }
    }

    private static SimulationSetup Create(
        DateTimeOffset now,
        Func<DateTimeOffset>? clock = null)
    {
        var leases = new ResourceLeaseManager(clock ?? (() => now));
        var provider = new SimulationStopProvider();
        var stop = new NavigationStopCoordinator(provider, TimeSpan.FromMinutes(1));
        var store = new SimulationIntentStore();
        var safety = new NavigationExecutionSafetyCoordinator(leases, stop, provider, store);
        safety.Update(now);
        var prerequisites = new PrerequisiteHolder { Value = Ready() };
        var authority = new NavigationAuthorityCoordinator(
            leases,
            stop,
            safety,
            () => prerequisites.Value);
        return new SimulationSetup(leases, provider, stop, store, safety, prerequisites, authority);
    }

    private static void Arm(SimulationSetup setup, DateTimeOffset now, TimeSpan lifetime)
    {
        setup.Authority.Update();
        Require(setup.Authority.Approve().IsActive, "isolated authority approval failed");
        NavigationExecutionStartResult started = setup.Authority.TryBeginExecution(
            Guid.NewGuid(),
            Owner(),
            lifetime,
            now);
        Require(started.Success, started.Message);
        Require(setup.Provider.StopRequests == 0, "the guarded start called a provider before movement existed");
    }

    private static NavigationAuthorityPrerequisites Ready() => new(
        HasVerifiedStagedLibrary: true,
        SourcePluginLoaded: false,
        RequiredDependenciesReady: true,
        StopAvailable: true,
        ManualMovementYieldingAvailable: true,
        ReloadReconciliationAvailable: true);

    private static LeaseOwner Owner() => new(
        GoalId.New(), TaskId.New(), AttemptId.New(), 0, "isolated non-moving safety simulation");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed record SimulationSetup(
        ResourceLeaseManager Leases,
        SimulationStopProvider Provider,
        NavigationStopCoordinator Stop,
        SimulationIntentStore Store,
        NavigationExecutionSafetyCoordinator Safety,
        PrerequisiteHolder Prerequisites,
        NavigationAuthorityCoordinator Authority);

    private sealed class PrerequisiteHolder
    {
        internal NavigationAuthorityPrerequisites Value { get; set; } = Ready();
    }

    private sealed class SimulationIntentStore : INavigationExecutionIntentStore
    {
        internal NavigationExecutionIntent? Current { get; private set; }

        public NavigationExecutionIntent? Load() => Current;

        public void Save(NavigationExecutionIntent intent) => Current = intent;
    }

    private sealed class SimulationStopProvider : INavigationStopProvider
    {
        internal int StopRequests { get; private set; }

        public bool IsAvailable => true;

        public void RequestStop() => StopRequests++;

        public bool? IsMovementActive() => false;
    }
}
