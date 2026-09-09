using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed class VnavmeshNavigationStopProvider : INavigationStopProvider
{
    private readonly DependencyService dependencies;
    private readonly ICallGateSubscriber<object> stop;
    private readonly ICallGateSubscriber<bool> isRunning;

    internal VnavmeshNavigationStopProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies)
    {
        this.dependencies = dependencies;
        stop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        isRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
    }

    public bool IsAvailable => dependencies.FindPlugin("vnavmesh").IsLoaded;

    public void RequestStop() => stop.InvokeAction();

    public bool? IsMovementActive() => isRunning.InvokeFunc();
}
