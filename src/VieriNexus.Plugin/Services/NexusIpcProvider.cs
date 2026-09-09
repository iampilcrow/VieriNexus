using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System.Text.Json;
using VieriNexus.Application;
using VieriNexus.Contracts;

namespace VieriNexus.Services;

internal sealed class NexusIpcProvider : IDisposable
{
    private readonly ICallGateProvider<NexusStatusDto> statusProvider;
    private readonly ICallGateProvider<DependencyDto[]> dependencyProvider;
    private readonly ICallGateProvider<int> navigationVersionProvider;
    private readonly ICallGateProvider<NavigationLibraryStatusDto> navigationStatusProvider;
    private readonly ICallGateProvider<string> navigationListProvider;
    private readonly ICallGateProvider<string, string?> navigationGetProvider;
    private readonly DependencyService dependencies;
    private readonly NavigationMigrationService navigation;
    private readonly WorldStateStore world;

    internal NexusIpcProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies,
        NavigationMigrationService navigation,
        WorldStateStore world)
    {
        this.dependencies = dependencies;
        this.navigation = navigation;
        this.world = world;
        statusProvider = pluginInterface.GetIpcProvider<NexusStatusDto>(NexusIpc.GetStatus);
        dependencyProvider = pluginInterface.GetIpcProvider<DependencyDto[]>(NexusIpc.GetDependencies);
        navigationVersionProvider = pluginInterface.GetIpcProvider<int>(NexusIpc.GetNavigationApiVersion);
        navigationStatusProvider = pluginInterface.GetIpcProvider<NavigationLibraryStatusDto>(NexusIpc.GetNavigationStatus);
        navigationListProvider = pluginInterface.GetIpcProvider<string>(NexusIpc.ListNavigationRoutes);
        navigationGetProvider = pluginInterface.GetIpcProvider<string, string?>(NexusIpc.GetNavigationRoute);
        statusProvider.RegisterFunc(GetStatus);
        dependencyProvider.RegisterFunc(GetDependencies);
        navigationVersionProvider.RegisterFunc(() => NexusIpc.CurrentVersion);
        navigationStatusProvider.RegisterFunc(GetNavigationStatus);
        navigationListProvider.RegisterFunc(ListNavigationRoutes);
        navigationGetProvider.RegisterFunc(GetNavigationRoute);
    }

    private NexusStatusDto GetStatus()
    {
        var snapshot = world.Current;
        var ready = dependencies.RequiredReady && snapshot.Session.IsLoggedIn && snapshot.Session.IsPlayerAvailable;
        return new NexusStatusDto(
            NexusIpc.CurrentVersion,
            ready,
            false,
            ready ? "Idle" : "SetupRequired",
            null,
            null,
            ready ? "No active goal." : "Waiting for required dependencies and a ready character.",
            null,
            null,
            ready ? null : "Complete the Dependencies page.",
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
        NavigationLibrarySnapshot? snapshot = navigation.StagedSnapshot;
        return new NavigationLibraryStatusDto(
            NexusIpc.CurrentVersion,
            snapshot is not null,
            false,
            true,
            snapshot?.Routes.Count ?? 0,
            snapshot?.Routes.Count(route => route.OverrideEnabled) ?? 0,
            snapshot is null ? "NotStaged" : "ReadOnlyStaged",
            snapshot is null
                ? "No verified staged route library is available."
                : "Verified staged route data is available read-only; VieriNavPlotter remains authoritative.");
    }

    private string ListNavigationRoutes()
    {
        NavigationLibrarySnapshot? snapshot = navigation.StagedSnapshot;
        if (snapshot is null)
            return "[]";

        NavigationRouteListEntryDto[] routes = snapshot.Routes.Select(route => new NavigationRouteListEntryDto(
            route.Id, route.Name, route.TerritoryId, route.Points.Count, route.Notes, route.Tags)).ToArray();
        return JsonSerializer.Serialize(routes);
    }

    private string? GetNavigationRoute(string nameOrId)
    {
        NavigationLibrarySnapshot? snapshot = navigation.StagedSnapshot;
        NavigationRouteSnapshot? route = snapshot is null
            ? null
            : NavigationLibraryQuery.Find(snapshot, nameOrId);
        if (route is null)
            return null;

        return JsonSerializer.Serialize(new NavigationRouteDto(
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
            route.UpdatedAtUtc));
    }

    public void Dispose()
    {
        navigationGetProvider.UnregisterFunc();
        navigationListProvider.UnregisterFunc();
        navigationStatusProvider.UnregisterFunc();
        navigationVersionProvider.UnregisterFunc();
        statusProvider.UnregisterFunc();
        dependencyProvider.UnregisterFunc();
    }
}
