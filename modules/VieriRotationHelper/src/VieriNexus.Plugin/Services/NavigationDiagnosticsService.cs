using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class NavigationDiagnosticsService(
    DependencyService dependencies,
    ResourceLeaseManager leases,
    NavigationStopCoordinator stop,
    ManualMovementSafetyService manualMovement,
    NavigationExecutionSafetyCoordinator executionSafety,
    NavigationAuthorityCoordinator authority,
    NavigationDiagnosticsMonitor monitor,
    NavigationSafetySimulator simulator)
{
    internal NavigationDiagnosticsSnapshot Current => monitor.Current;

    internal NavigationSimulationReport? LastSimulation { get; private set; }

    internal NavigationDiagnosticsSnapshot Update(DateTimeOffset now)
    {
        PluginPresence source = dependencies.FindPlugin("VieriNavPlotter");
        PluginPresence vnavmesh = dependencies.FindPlugin("vnavmesh");
        NavigationExecutionSafetyStatus reload = executionSafety.Status;
        NavigationAuthorityStatus authorityStatus = authority.Status;
        Guid? trackedLeaseId = executionSafety.TrackedLeaseId;
        ResourceLeaseSnapshot? conflict = leases.Snapshot().FirstOrDefault(lease =>
            lease.LeaseId != trackedLeaseId &&
            (lease.Resources.Contains(ResourceKind.Navigation) ||
             lease.Resources.Contains(ResourceKind.Movement)));

        NavigationProviderDiagnostic[] providers =
        [
            new(
                "vnavmesh",
                "vnavmesh Stop availability",
                vnavmesh.IsLoaded && stop.IsProviderAvailable
                    ? NavigationDiagnosticState.Healthy
                    : NavigationDiagnosticState.Blocked,
                vnavmesh.Version,
                vnavmesh.IsLoaded && stop.IsProviderAvailable ? "stop-provider-ready" : "stop-provider-unavailable",
                vnavmesh.IsLoaded && stop.IsProviderAvailable
                    ? "The plugin and Stop adapter are loaded. This read-only check does not invoke Stop or movement-state IPC."
                    : vnavmesh.IsInstalled
                        ? "Installed but not available to the verified Stop contract."
                        : "Not installed; navigation execution cannot be made safe."),
            new(
                "explicit-route-stop",
                "Route Stop policy",
                manualMovement.IsReadyForActivation
                    ? NavigationDiagnosticState.Healthy
                    : manualMovement.Current.RequiresExplicitResume
                        ? NavigationDiagnosticState.Attention
                        : NavigationDiagnosticState.Blocked,
                null,
                manualMovement.Current.Code,
                manualMovement.Current.Message),
            new(
                "reload-watchdog",
                "Reload and lease watchdog",
                reload.IsReadyForActivation
                    ? NavigationDiagnosticState.Healthy
                    : reload.State == NavigationExecutionSafetyState.AwaitingExplicitResume
                        ? NavigationDiagnosticState.Attention
                        : NavigationDiagnosticState.Blocked,
                null,
                reload.Code,
                reload.Message),
            new(
                "source-owner",
                "VieriNavPlotter ownership",
                source.IsLoaded ? NavigationDiagnosticState.Attention : NavigationDiagnosticState.Healthy,
                source.Version,
                source.IsLoaded ? "source-owner-loaded" : "source-owner-unloaded",
                source.IsLoaded
                    ? "Loaded and authoritative; Nexus route actions are paused."
                    : source.IsInstalled
                        ? "Installed but unloaded; Nexus routes become ready automatically when every gate is healthy."
                        : "Not installed; no predecessor navigation owner is loaded."),
            new(
                "navigation-resources",
                "Navigation and Movement ownership",
                conflict is null ? NavigationDiagnosticState.Healthy : NavigationDiagnosticState.Blocked,
                null,
                conflict is null
                    ? trackedLeaseId is null ? "resources-free" : "resources-owned-by-nexus"
                    : "resources-owned-elsewhere",
                conflict is null
                    ? trackedLeaseId is null
                        ? "The atomic Navigation/Movement bundle is free."
                        : "A tracked Nexus safety lease owns the atomic bundle."
                    : $"Another task owns the bundle: {conflict.Owner.Reason}."),
            new(
                "nexus-authority",
                "Nexus navigation authority",
                authorityStatus.IsActive
                    ? NavigationDiagnosticState.Healthy
                    : authorityStatus.State == NavigationAuthorityState.Revoked
                        ? NavigationDiagnosticState.Blocked
                        : NavigationDiagnosticState.Attention,
                null,
                authorityStatus.Code,
                authorityStatus.Message),
        ];

        return monitor.Observe(providers, now);
    }

    internal NavigationSimulationReport RunSimulation()
    {
        LastSimulation = simulator.Run(DateTimeOffset.UtcNow);
        return LastSimulation;
    }

    internal IReadOnlyDictionary<ProviderId, ProviderHealthSnapshot> ProviderHealth()
    {
        return Current.Providers.ToDictionary(
            provider => new ProviderId(provider.Id),
            provider => new ProviderHealthSnapshot(
                new ProviderId(provider.Id),
                provider.DisplayName,
                provider.State.ToString(),
                provider.Version,
                provider.Detail));
    }
}
