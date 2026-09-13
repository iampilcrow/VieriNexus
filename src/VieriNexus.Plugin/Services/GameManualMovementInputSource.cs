using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace VieriNexus.Services;

internal sealed class GameManualMovementInputSource
{
    private static readonly InputId[] MovementInputs =
    [
        InputId.MOVE_FORE,
        InputId.MOVE_BACK,
        InputId.MOVE_LEFT,
        InputId.MOVE_RIGHT,
        InputId.MOVE_STRIFE_L,
        InputId.MOVE_STRIFE_R,
        InputId.MOVE_AND_STEER,
        InputId.MOVE_DESCENT,
        InputId.MOVE_ANGLE_RISING,
        InputId.MOVE_ANGLE_DESCENT,
        InputId.JUMP,
        InputId.AUTORUN_KEY,
        InputId.AUTORUN_PAD,
    ];

    internal unsafe bool IsAvailable => UIInputData.Instance() is not null;

    internal unsafe bool IsMovementInputActive()
    {
        UIInputData* input = UIInputData.Instance();
        if (input is null)
            return false;

        if (InputManager.IsAutoRunning())
            return true;

        return MovementInputs.Any(inputId => input->IsInputIdDown(inputId));
    }
}
