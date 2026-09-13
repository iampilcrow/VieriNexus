using Dalamud.Plugin.Services;

namespace VieriRotationHelper;

internal sealed class ActionDisplay(IDataManager dataManager)
{
    internal ActionInfo Get(uint actionId)
    {
        if (SuggestionActionId.IsItem(actionId))
        {
            var item = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(SuggestionActionId.ItemRow(actionId));
            return item.HasValue ? new(item.Value.Name.ToString(), item.Value.Icon, 0, 0) : new("Unknown item", 0, 0, 0);
        }
        var row = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(actionId);
        if (!row.HasValue)
            return new ActionInfo($"Action {actionId}", 0, 0, 0);
        return new ActionInfo(row.Value.Name.ToString(), row.Value.Icon, row.Value.Range, row.Value.EffectRange);
    }
}
