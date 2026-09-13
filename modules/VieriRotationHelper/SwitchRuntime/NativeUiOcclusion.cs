using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WrathSwitch;

internal static class NativeUiOcclusion
{
    public static unsafe bool IsCovered(Vector2 windowPosition, Vector2 windowSize)
    {
        if (windowSize.X <= 0 || windowSize.Y <= 0)
            return false;

        var windowMaximum = windowPosition + windowSize;
        var stage = AtkStage.Instance();
        if (stage is null)
            return false;
        var manager = stage->RaptureAtkUnitManager;
        if (manager is null)
            return false;

        var units = &manager->AtkUnitManager.AllLoadedUnitsList;
        for (var index = 0; index < units->Count; index++)
        {
            try
            {
                var addon = *(AtkUnitBase**)Unsafe.AsPointer(ref units->Entries[index]);
                if (addon is null || addon->RootNode is null || addon->WindowNode is null ||
                    !addon->IsVisible || addon->Scale <= 0 || !addon->WindowNode->IsVisible())
                    continue;

                // Match the native-window bounds used by DelvUI's game-window clipping.
                var margin = 5f * addon->Scale;
                var bottomMargin = 13f * addon->Scale;
                var addonMinimum = new Vector2(
                    addon->RootNode->X + margin,
                    addon->RootNode->Y + margin);
                var addonMaximum = addonMinimum + new Vector2(
                    addon->RootNode->Width * addon->Scale - margin,
                    addon->RootNode->Height * addon->Scale - bottomMargin);

                if (addonMaximum.X <= addonMinimum.X || addonMaximum.Y <= addonMinimum.Y)
                    continue;

                if (windowPosition.X < addonMaximum.X && windowMaximum.X > addonMinimum.X &&
                    windowPosition.Y < addonMaximum.Y && windowMaximum.Y > addonMinimum.Y)
                    return true;
            }
            catch
            {
                // Native addon lists can change during a frame; skip entries that disappear.
            }
        }

        return false;
    }
}

