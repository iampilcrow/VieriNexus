using Dalamud.Plugin.Ipc;

namespace Avarice.Data;

internal sealed class RotationHelperWatcher
{
    private readonly ICallGateSubscriber<ulong, uint[]> subscriber =
        Svc.PluginInterface.GetIpcSubscriber<ulong, uint[]>("VieriRotationHelper.PositionalGuidance.V1");

    internal string Status { get; private set; } = "Waiting for positional guidance.";

    internal bool TryGet(ulong target, out uint side)
    {
        side = 0;
        try
        {
            var state = subscriber.InvokeFunc(target);
            if (state == null || state.Length != 6 || state[0] != 1 || state[1] != 1 || state[3] > 2)
            {
                Status = "Suite unavailable; standalone anticipation active.";
                return false;
            }
            side = state[3];
            Status = side == 0 ? "Integrated Wrath shared frame: no upcoming positional requirement."
                : $"Integrated Wrath shared frame: action {state[2]}, {(side == 1 ? "rear" : "flank")}, {state[4]} action(s) ahead.";
            return true;
        }
        catch
        {
            Status = "Suite unavailable; standalone anticipation active.";
            return false;
        }
    }
}
