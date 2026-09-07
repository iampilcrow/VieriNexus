using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using VieriNexus.Application;
using VieriNexus.Contracts;

namespace VieriNexus.Services;

internal sealed class NexusIpcProvider : IDisposable
{
    private readonly ICallGateProvider<NexusStatusDto> statusProvider;
    private readonly ICallGateProvider<DependencyDto[]> dependencyProvider;
    private readonly DependencyService dependencies;
    private readonly WorldStateStore world;

    internal NexusIpcProvider(IDalamudPluginInterface pluginInterface, DependencyService dependencies, WorldStateStore world)
    {
        this.dependencies = dependencies;
        this.world = world;
        statusProvider = pluginInterface.GetIpcProvider<NexusStatusDto>(NexusIpc.GetStatus);
        dependencyProvider = pluginInterface.GetIpcProvider<DependencyDto[]>(NexusIpc.GetDependencies);
        statusProvider.RegisterFunc(GetStatus);
        dependencyProvider.RegisterFunc(GetDependencies);
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

    public void Dispose()
    {
        statusProvider.UnregisterFunc();
        dependencyProvider.UnregisterFunc();
    }
}
