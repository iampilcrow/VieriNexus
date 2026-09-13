using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace WrathSwitch;

internal sealed class WrathBridge
{
    private readonly ICallGateSubscriber<bool> getAutoRotationState;

    public WrathBridge(IDalamudPluginInterface pluginInterface)
    {
        getAutoRotationState = pluginInterface.GetIpcSubscriber<bool>("WrathCombo.GetAutoRotationState");
    }

    public bool TryGetAutoRotationState(out bool enabled)
    {
        try
        {
            enabled = getAutoRotationState.InvokeFunc();
            return true;
        }
        catch
        {
            enabled = false;
            return false;
        }
    }
}

