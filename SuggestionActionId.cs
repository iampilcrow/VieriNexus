namespace VieriRotationHelper;

internal static class SuggestionActionId
{
    // Upstream Wrath's item suggestion namespace; never passed to ActionManager.
    internal const uint ItemBase = 2_000_000;
    internal static bool IsItem(uint id) => id > ItemBase && id < 3_000_000;
    internal static uint Item(uint id) => ItemBase + id % 1_000_000;
    internal static uint ItemRow(uint id) => id - ItemBase;
}
