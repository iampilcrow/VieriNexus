using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VieriRotationHelper;

internal static class NativeUiOcclusion
{
    internal static unsafe bool IsCovered(Vector2 position, Vector2 size)
    {
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
                var minimum = new Vector2(addon->RootNode->X + margin, addon->RootNode->Y + margin);
                var maximum = minimum + new Vector2(
                    addon->RootNode->Width * addon->Scale - margin,
                    addon->RootNode->Height * addon->Scale - bottomMargin);

                if (NativeUiOcclusionPolicy.Overlaps(position, size, minimum, maximum))
                    return true;
            }
            catch
            {
                // Native addon lists can change while Dalamud is drawing this frame.
            }
        }

        return false;
    }
}
