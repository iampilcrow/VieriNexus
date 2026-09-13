using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Avarice;

internal static class NativeUiOcclusion
{
    public static bool IsWorldPointCovered(Vector3 worldPosition, float radius)
    {
        if (!P.config.HideBehindNativeUi ||
            !Svc.GameGui.WorldToScreen(worldPosition, out var screenPosition))
            return false;

        var safeRadius = MathF.Max(1f, radius);
        var bounds = new Vector2(safeRadius * 2f);
        return IsCovered(screenPosition - new Vector2(safeRadius), bounds);
    }

    public static unsafe bool IsCovered(Vector2 position, Vector2 size)
    {
        if (size.X <= 0 || size.Y <= 0)
            return false;

        var maximum = position + size;
        var stage = AtkStage.Instance();
        if (stage == null)
            return false;

        var manager = stage->RaptureAtkUnitManager;
        if (manager == null)
            return false;

        var units = &manager->AtkUnitManager.AllLoadedUnitsList;
        for (var index = 0; index < units->Count; index++)
        {
            try
            {
                var addon = *(AtkUnitBase**)Unsafe.AsPointer(ref units->Entries[index]);
                if (addon == null || addon->RootNode == null || addon->WindowNode == null ||
                    !addon->IsVisible || addon->Scale <= 0 || !addon->WindowNode->IsVisible())
                    continue;

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

                if (position.X < addonMaximum.X && maximum.X > addonMinimum.X &&
                    position.Y < addonMaximum.Y && maximum.Y > addonMinimum.Y)
                    return true;
            }
            catch
            {
                // Native addon lists can change while the UI frame is being drawn.
            }
        }

        return false;
    }
}
