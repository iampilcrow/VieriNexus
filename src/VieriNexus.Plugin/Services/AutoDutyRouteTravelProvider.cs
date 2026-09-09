using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using VieriNexus.Application;

namespace VieriNexus.Services;

/// <summary>
/// Narrow adapter over VieriAutoDuty's public suite-travel contract. Nexus never reaches into
/// AutoDuty internals, so provider updates are isolated to capability compatibility at this seam.
/// </summary>
internal sealed class AutoDutyRouteTravelProvider : INavigationSuiteTravelProvider
{
    private readonly DependencyService dependencies;
    private readonly ICallGateSubscriber<string, string> travelRoute;
    private readonly ICallGateSubscriber<bool> stopRoute;
    private readonly ICallGateSubscriber<bool> isVisualizationActive;

    internal AutoDutyRouteTravelProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies)
    {
        this.dependencies = dependencies;
        travelRoute = pluginInterface.GetIpcSubscriber<string, string>("AutoDuty.TravelVieriRoute");
        stopRoute = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.StopVieriRouteTravel");
        isVisualizationActive = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsNavPlotterVisualizationActive");
    }

    public bool IsAvailable => dependencies.FindPlugin("AutoDuty").IsLoaded &&
                               travelRoute.HasFunction &&
                               stopRoute.HasFunction &&
                               isVisualizationActive.HasFunction;

    public SuiteRouteDispatchResult Dispatch(string requestJson)
    {
        if (!IsAvailable)
            return new(false, "Cross-zone route travel requires VieriAutoDuty to be loaded.");
        try
        {
            string message = travelRoute.InvokeFunc(requestJson);
            return new(message.StartsWith("Started ", StringComparison.OrdinalIgnoreCase), message);
        }
        catch
        {
            return new(false, "VieriAutoDuty's route-travel capability is unavailable or incompatible.");
        }
    }

    public bool Stop()
    {
        if (!IsAvailable)
            return false;
        try { return stopRoute.InvokeFunc(); }
        catch { return false; }
    }

    public bool IsRouteVisualizationActive()
    {
        if (!IsAvailable)
            return false;
        try { return isVisualizationActive.InvokeFunc(); }
        catch { return false; }
    }
}
