using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace WrathCombo;

/// <summary>
/// Provides canonical equivalents for permanent trait-upgraded actions. The
/// forecast first preserves the displayed action and only uses this alternative
/// when the rotation's next result proves that its rules expect the canonical
/// combo state. Temporary stateful replacements such as Viper's changing combo
/// buttons are excluded.
/// </summary>
internal static class PredictedComboState
{
    private static IReadOnlyDictionary<uint, uint> recordedActions =
        new Dictionary<uint, uint>();

    internal static void LoadFromGameData()
    {
        var replacements = new List<TraitReplacementMap.Replacement>();
        foreach (var group in Svc.Data.GetSubrowExcelSheet<ReplaceAction>())
        foreach (var row in group)
        {
            Add(replacements, row.Action.RowId, row.ReplaceActions[0].RowId, row.Type1);
            Add(replacements, row.Action.RowId, row.ReplaceActions[1].RowId, row.Type2);
            Add(replacements, row.Action.RowId, row.ReplaceActions[2].RowId, row.Type3);
            Add(replacements, row.Action.RowId, row.ReplaceActions[3].RowId, row.Type4);
        }

        recordedActions = TraitReplacementMap.Build(replacements);
    }

    private static void Add(List<TraitReplacementMap.Replacement> replacements, uint baseAction,
        uint displayAction, sbyte type)
    {
        if (baseAction != 0 && displayAction != 0)
            replacements.Add(new(baseAction, displayAction, type));
    }

    internal static uint CanonicalAction(uint displayedAction) =>
        recordedActions.TryGetValue(displayedAction, out var recorded)
            ? recorded
            : displayedAction;
}
