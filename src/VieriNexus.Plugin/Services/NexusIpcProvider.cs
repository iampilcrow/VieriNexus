using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using VieriNexus.Application;
using VieriNexus.Contracts;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class NexusIpcProvider : IDisposable
{
    private readonly ICallGateProvider<NexusStatusDto> statusProvider;
    private readonly ICallGateProvider<DependencyDto[]> dependencyProvider;
    private readonly ICallGateProvider<int> navigationVersionProvider;
    private readonly ICallGateProvider<NavigationLibraryStatusDto> navigationStatusProvider;
    private readonly ICallGateProvider<string> navigationListProvider;
    private readonly ICallGateProvider<string, string?> navigationGetProvider;
    private readonly ICallGateProvider<uint, uint, string> navigationResolveVendorProvider;
    private readonly ICallGateProvider<NavigationActivationStatusDto> navigationActivationProvider;
    private readonly DependencyService dependencies;
    private readonly NavigationLibraryService navigation;
    private readonly NavigationActivationService navigationActivation;
    private readonly ProgressionRuntimeService progression;
    private readonly WorldStateStore world;

    internal NexusIpcProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies,
        NavigationLibraryService navigation,
        NavigationActivationService navigationActivation,
        ProgressionRuntimeService progression,
        WorldStateStore world)
    {
        this.dependencies = dependencies;
        this.navigation = navigation;
        this.navigationActivation = navigationActivation;
        this.progression = progression;
        this.world = world;
        statusProvider = pluginInterface.GetIpcProvider<NexusStatusDto>(NexusIpc.GetStatus);
        dependencyProvider = pluginInterface.GetIpcProvider<DependencyDto[]>(NexusIpc.GetDependencies);
        navigationVersionProvider = pluginInterface.GetIpcProvider<int>(NexusIpc.GetNavigationApiVersion);
        navigationStatusProvider = pluginInterface.GetIpcProvider<NavigationLibraryStatusDto>(NexusIpc.GetNavigationStatus);
        navigationListProvider = pluginInterface.GetIpcProvider<string>(NexusIpc.ListNavigationRoutes);
        navigationGetProvider = pluginInterface.GetIpcProvider<string, string?>(NexusIpc.GetNavigationRoute);
        navigationResolveVendorProvider = pluginInterface.GetIpcProvider<uint, uint, string>(NexusIpc.ResolveGearVendorOverride);
        navigationActivationProvider = pluginInterface.GetIpcProvider<NavigationActivationStatusDto>(NexusIpc.GetNavigationActivationStatus);
        statusProvider.RegisterFunc(GetStatus);
        dependencyProvider.RegisterFunc(GetDependencies);
        navigationVersionProvider.RegisterFunc(() => NexusIpc.CurrentVersion);
        navigationStatusProvider.RegisterFunc(GetNavigationStatus);
        navigationListProvider.RegisterFunc(ListNavigationRoutes);
        navigationGetProvider.RegisterFunc(GetNavigationRoute);
        navigationResolveVendorProvider.RegisterFunc(ResolveGearVendorOverride);
        navigationActivationProvider.RegisterFunc(GetNavigationActivationStatus);
    }

    private NexusStatusDto GetStatus()
    {
        var snapshot = world.Current;
        var ready = dependencies.RequiredReady && snapshot.Session.IsLoggedIn && snapshot.Session.IsPlayerAvailable;
        ProgressionGoalState? progressionState = progression.State;
        NexusTask? activeTask = progressionState?.ActiveTask;
        bool paused = progressionState?.Goal.Status == GoalStatus.Paused;
        string state = progressionState?.Goal.Status.ToString() ?? (ready ? "Idle" : "SetupRequired");
        return new NexusStatusDto(
            NexusIpc.CurrentVersion,
            ready,
            paused,
            state,
            progressionState?.Goal.Title,
            activeTask?.Title,
            progressionState?.Goal.StatusDetail ??
                (ready ? "No active goal." : "Waiting for required dependencies and a ready character."),
            activeTask?.Provider?.Value,
            progressionState?.Goal.Status == GoalStatus.Active
                ? "Verify this bounded duty, then replan from current character state."
                : null,
            progressionState?.Goal.Status == GoalStatus.Blocked
                ? progressionState.Goal.StatusDetail
                : ready ? null : "Complete the Dependencies page.",
            snapshot.Revision);
    }

    private DependencyDto[] GetDependencies() => dependencies.Snapshot().Select(status => new DependencyDto(
        status.Definition.Id,
        status.Definition.DisplayName,
        status.Health.ToString(),
        status.Version,
        status.Definition.Required,
        status.Definition.Capability,
        status.Definition.Description)).ToArray();

    private NavigationLibraryStatusDto GetNavigationStatus()
    {
        NavigationLibrarySnapshot? snapshot = navigation.Current;
        NavigationActivationAssessment activation = navigationActivation.Assess();
        return new NavigationLibraryStatusDto(
            NexusIpc.CurrentVersion,
            snapshot is not null,
            activation.State == NavigationActivationState.Active,
            activation.IsSourcePluginAuthoritative,
            snapshot?.Routes.Count ?? 0,
            snapshot?.Routes.Count(route => route.OverrideEnabled) ?? 0,
            snapshot is null ? "NotStaged" : navigation.HasWorkingLibrary ? "NexusWorkingLibrary" : "ReadOnlyStaged",
            snapshot is null
                ? "No verified staged route library is available."
                : navigation.HasWorkingLibrary
                    ? "The Nexus working route library is available."
                    : activation.IsSourcePluginAuthoritative
                    ? "Verified staged route data is available read-only; VieriNavPlotter remains authoritative."
                    : "Verified staged route data is available read-only; Nexus navigation execution is disabled.");
    }

    private string ListNavigationRoutes()
    {
        NavigationLibrarySnapshot? snapshot = navigation.Current;
        if (snapshot is null)
            return "[]";

        NavigationRouteListEntryDto[] routes = snapshot.Routes.Select(route => new NavigationRouteListEntryDto(
            route.Id, route.Name, route.TerritoryId, route.Points.Count, route.Notes, route.Tags)).ToArray();
        return NavigationContractJson.SerializeRouteList(routes);
    }

    private string? GetNavigationRoute(string nameOrId)
    {
        NavigationLibrarySnapshot? snapshot = navigation.Current;
        NavigationRouteSnapshot? route = snapshot is null
            ? null
            : NavigationLibraryQuery.Find(snapshot, nameOrId);
        if (route is null)
            return null;

        return NavigationContractJson.SerializeRoute(ToDto(route));
    }

    private string ResolveGearVendorOverride(uint territoryId, uint targetDataId)
    {
        NavigationActivationAssessment activation = navigationActivation.Assess();
        if (activation.State != NavigationActivationState.Active)
        {
            return NavigationContractJson.SerializeRouteResolution(new NavigationRouteResolutionDto(
                false,
                "navigation-not-authoritative",
                "Nexus route overrides are unavailable until Nexus owns navigation for this session.",
                null));
        }
        NavigationRouteOverrideResolution result = NavigationRouteOverrideResolver.ResolveGearVendor(
            navigation.Current, territoryId, targetDataId);
        return NavigationContractJson.SerializeRouteResolution(new NavigationRouteResolutionDto(
            result.Success,
            result.Code,
            result.Message,
            result.Route is null ? null : ToDto(result.Route)));
    }

    private static NavigationRouteDto ToDto(NavigationRouteSnapshot route) => new(
            route.Id,
            route.Name,
            route.TerritoryId,
            route.Points.Select(point => new NavigationRoutePointDto(point.X, point.Y, point.Z)).ToArray(),
            route.Notes,
            route.Tags,
            route.UseMesh,
            route.UseFlight,
            route.Tolerance,
            route.LastPointTolerance,
            route.BindingKind,
            route.TargetDataId,
            route.TargetLabel,
            route.OverrideEnabled,
            route.UpdatedAtUtc);

    private NavigationActivationStatusDto GetNavigationActivationStatus()
    {
        NavigationActivationAssessment assessment = navigationActivation.Assess();
        bool executionEnabled = assessment.State == NavigationActivationState.Active;
        return new NavigationActivationStatusDto(
            NexusIpc.CurrentVersion,
            assessment.State.ToString(),
            assessment.CanActivate,
            executionEnabled,
            assessment.IsSourcePluginInstalled,
            assessment.IsSourcePluginLoaded,
            assessment.IsSourcePluginAuthoritative,
            assessment.Blockers.Select(blocker =>
                new NavigationActivationBlockerDto(blocker.Code, blocker.Message)).ToArray());
    }

    public void Dispose()
    {
        navigationActivationProvider.UnregisterFunc();
        navigationResolveVendorProvider.UnregisterFunc();
        navigationGetProvider.UnregisterFunc();
        navigationListProvider.UnregisterFunc();
        navigationStatusProvider.UnregisterFunc();
        navigationVersionProvider.UnregisterFunc();
        statusProvider.UnregisterFunc();
        dependencyProvider.UnregisterFunc();
    }
}
