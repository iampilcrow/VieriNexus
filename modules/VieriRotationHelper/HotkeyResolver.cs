using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VieriRotationHelper;

// Hilda's keyboard hotbar-label lookup, with bounds/type checks and live
// adjusted-action matching for Wrath's replacement buttons.
internal sealed class HotkeyResolver(IGameGui gameGui)
{
    private readonly List<(uint Action, string Key)> bindings = [];
    private long nextRefresh;
    private ulong character;
    private uint job;

    internal unsafe string? Resolve(uint action, uint anchor, bool liveLead, bool forceAnchor = false)
    {
        Refresh();
        var manager = ActionManager.Instance();
        if (manager == null) return null;
        var current = new List<ActionBinding>(bindings.Count);
        foreach (var binding in bindings)
            current.Add(new(binding.Action, SuggestionActionId.IsItem(binding.Action)
                ? binding.Action : manager->GetAdjustedActionId(binding.Action), binding.Key));
        return HotbarKeySelection.Resolve(current, action, anchor, liveLead, forceAnchor);
    }

    private unsafe void Refresh()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            bindings.Clear();
            character = 0;
            return;
        }
        if (Environment.TickCount64 < nextRefresh && character == player.GameObjectId && job == player.ClassJob.RowId)
            return;
        nextRefresh = Environment.TickCount64 + 100;
        character = player.GameObjectId;
        job = player.ClassJob.RowId;
        bindings.Clear();
        var ui = UIModule.Instance();
        if (ui == null)
            return;
        var module = ui->GetRaptureHotbarModule();
        if (module == null)
            return;
        for (var bar = 0; bar < 10; bar++)
        {
            var name = bar == 0 ? "_ActionBar" : $"_ActionBar{bar:00}";
            var addon = (AtkUnitBase*)gameGui.GetAddonByName(name).Address;
            if (addon == null)
                continue;
            if (addon->UldManager.NodeList == null || addon->UldManager.NodeListCount <= 20)
                continue;
            // The displayed first hotbar may be paged to a different backing bar.
            var hotbarId = ((AddonActionBarBase*)addon)->RaptureHotbarId;
            if (hotbarId < 0 || hotbarId >= module->StandardHotbars.Length)
                continue;
            for (var slotIndex = 0; slotIndex < 12; slotIndex++)
            {
                var slot = module->StandardHotbars[(int)hotbarId].Slots[slotIndex];
                if (slot.CommandType is not (RaptureHotbarModule.HotbarSlotType.Action or RaptureHotbarModule.HotbarSlotType.Item) || slot.CommandId == 0)
                    continue;
                var node = addon->UldManager.NodeList[20 - slotIndex];
                if (node == null || (int)node->Type < 1000)
                    continue;
                var component = ((AtkComponentNode*)node)->Component;
                if (component == null || component->UldManager.NodeList == null || component->UldManager.NodeListCount < 2)
                    continue;
                var textNode = component->UldManager.NodeList[1];
                if (textNode == null || textNode->Type != NodeType.Text)
                    continue;
                var label = ((AtkTextNode*)textNode)->NodeText.ToString();
                if (!string.IsNullOrWhiteSpace(label))
                    bindings.Add((slot.CommandType == RaptureHotbarModule.HotbarSlotType.Item
                        ? SuggestionActionId.Item(slot.CommandId) : slot.CommandId, HotkeyLabel.Normalize(label)));
            }
        }
    }

}
