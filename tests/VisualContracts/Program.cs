using System.Numerics;
using VieriRotationHelper;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
// Actual action-to-button resolution, independent of Dalamud and job-specific IDs.
foreach (var (first, second, third) in new[] {
    (9u,15u,3539u), (31u,37u,42u), (75u,78u,84u),
    (2240u,2242u,2255u), (7477u,7478u,7481u), (34650u,34651u,34652u) })
{
    var consolidated = new ActionBinding[] { new(first, second, "1"), new(second, second, "2"), new(third, third, "3") };
    Check(HotbarKeySelection.Resolve(consolidated, second, first, true) == "1", "Consolidated lead uses its working anchor");
    Check(HotbarKeySelection.Resolve(consolidated, third, first, false, true) == "1", "Consolidated forecast keeps the entry-button hotkey");
    var separate = new ActionBinding[] { new(first, first, "1"), new(second, second, "2"), new(third, third, "3") };
    Check(HotbarKeySelection.Resolve(separate, second, first, true) == "2", "Disabling consolidation changes to the real second key");
    Check(HotbarKeySelection.Resolve(separate, third, first, false) == "3", "Third action uses its actual key without Wrath");
    Check(HotbarKeySelection.Resolve([new(first, first, "1")], second, first, false) == null, "Never invent a key for an unbound action");
    Check(HotbarKeySelection.Resolve([new(second, third, "2")], second, first, false) == null, "Raw ID match cannot use a button overwritten with a different action");
    Check(HotbarKeySelection.Resolve([new(first, third, "C4")], third, first, false) == "C4", "Native transforms retain actual modifier key");
    Check(HotbarKeySelection.Resolve([new(second, second, "S7")], second, first, false) == "S7", "Rebinding is reflected without a job change");
    Check(HotbarKeySelection.Resolve([], second, first, false) == null, "Logout/empty hotbar cannot reuse old character keys");
}
Check(SuggestionActionId.Item(1044110) == SuggestionActionId.Item(44110), "HQ item hotkeys match the base consumable suggestion");
Check(SuggestionActionId.ItemRow(2044110) == 44110, "Synthetic IDs must be decoded before native item queries");
Check(!SuggestionActionId.IsItem(2000000) && !SuggestionActionId.IsItem(3000000), "Sentinels are not real consumables");
var overlayGate = new GameplayOverlayGate();
Check(!overlayGate.Evaluate(false, false, false, false, 0), "Overlays stay hidden before login");
Check(!overlayGate.Evaluate(true, true, true, true, 10), "Overlays stay hidden between areas");
Check(!overlayGate.Evaluate(true, true, true, false, 20), "Overlays wait for the loaded world to settle");
Check(!overlayGate.Evaluate(true, true, true, false, 769), "Overlays do not appear early during the settle period");
Check(overlayGate.Evaluate(true, true, true, false, 770), "Overlays appear after the loaded world is stable");
Check(!overlayGate.Evaluate(true, true, true, true, 800), "A new loading transition immediately hides overlays");
Check(!overlayGate.Evaluate(true, true, true, false, 900), "A new area starts a fresh settle period");
Check(overlayGate.Evaluate(true, true, true, false, 1650), "Overlays return after the new area is stable");
Check(NativeUiOcclusionPolicy.Overlaps(new Vector2(100, 100), new Vector2(50, 50), new Vector2(120, 80), new Vector2(200, 130)),
    "Suggestion icons hide when a native FFXIV window overlaps them");
Check(!NativeUiOcclusionPolicy.Overlaps(new Vector2(100, 100), new Vector2(50, 50), new Vector2(150, 80), new Vector2(200, 100)),
    "Touching a native window edge without overlap does not hide the suggestion icon");
Check(!NativeUiOcclusionPolicy.Overlaps(new Vector2(100, 100), Vector2.Zero, new Vector2(90, 90), new Vector2(200, 200)),
    "Empty suggestion bounds never report native UI occlusion");
Check(HildaVisualStyle.Position(0, 65, 50, 3, true) == new Vector2(20, 20), "Hilda lead origin");
Check(HildaVisualStyle.Position(1, 65, 50, 3, true) == new Vector2(91, 27.5f), "Hilda second icon and vertical centering");
Check(HildaVisualStyle.Position(2, 65, 50, 3, true) == new Vector2(147, 27.5f), "Hilda third icon spacing");
for (var count = 1; count <= 10; count++)
    foreach (var main in new[] { 32f, 65f, 96f })
        foreach (var ratio in new[] { .45f, 50f / 65f, 1f })
        {
            var secondary = main * ratio;
            for (var i = 1; i < count; i++)
            {
                var prev = HildaVisualStyle.Position(i - 1, main, secondary, 3, true);
                var next = HildaVisualStyle.Position(i, main, secondary, 3, true);
                var prevSize = i == 1 ? main : secondary;
                Check(next.X >= prev.X + prevSize, "Icons must remain side by side without overlap");
                Check(Math.Abs(next.Y + secondary / 2 - (20 * main / 65 + main / 2)) < .001f, "Center alignment");
            }
        }
for (var step = 0; step <= 80; step++)
{
    var uv = HildaVisualStyle.RecastUv(step / 80f, 1);
    Check(uv.Min.X >= 0 && uv.Min.Y >= 0 && uv.Max.X <= 1 && uv.Max.Y <= 1, "Recast sprite within texture bounds");
    Check(uv.Max.X > uv.Min.X && uv.Max.Y > uv.Min.Y, "Recast sprite must have positive dimensions");
}
Check(HildaVisualStyle.FlankSymbol == (char)62263 && HildaVisualStyle.RearSymbol == (char)62217, "Exact Hilda positional glyphs");
foreach (var pair in new[] { ("§1", "S1"), ("ª2", "A2"), ("¢3", "C3"), ("¾4", "CA4"), ("½5", "CS5"), ("¼6", "AS6"), ("¶7", "CAS7"), ("F12", "F12"), ("", "") })
    Check(HotkeyLabel.Normalize(pair.Item1) == pair.Item2, "Hilda hotkey normalization");
Console.WriteLine($"{checks} Hilda visual-contract checks passed.");
Check(HildaHotkeyFamilies.Groups.Length == 167, "All Hilda keyboard action families");
foreach (var group in HildaHotkeyFamilies.Groups)
    foreach (var action in group)
        Check(HildaHotkeyFamilies.Related(action).Contains(action), "Family lookup includes the action itself");
Check(HildaHotkeyFamilies.Related(16479).Contains(75u), "Raiden Thrust resolves True Thrust hotkey");
Check(!HildaHotkeyFamilies.Related(uint.MaxValue).Any(), "Unknown actions have no invented hotkeys");
Console.WriteLine($"{checks} total visual/hotkey checks passed.");
