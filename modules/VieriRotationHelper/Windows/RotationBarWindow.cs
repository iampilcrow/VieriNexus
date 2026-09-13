using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace VieriRotationHelper.Windows;

internal sealed class RotationBarWindow : Window
{
    private readonly Plugin plugin;
    private readonly RotationCoordinator coordinator;
    private readonly ActionDisplay display;
    private readonly RotationMode mode;
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;

    internal RotationBarWindow(Plugin plugin, RotationCoordinator coordinator, ActionDisplay display, RotationMode mode)
        : base($"###VieriRotation{mode}", BaseFlags)
    {
        this.plugin = plugin;
        this.coordinator = coordinator;
        this.display = display;
        this.mode = mode;
        IsOpen = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        Position = new Vector2(420, mode == RotationMode.SingleTarget ? 260 : mode == RotationMode.Aoe ? 390 : 520);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions() => plugin.ShouldShow(mode);

    public override void PreDraw()
    {
        Flags = BaseFlags;
        if (plugin.Configuration.LockBars)
            Flags |= ImGuiWindowFlags.NoMove | (plugin.SettingsOpen ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, plugin.Configuration.Opacity);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
    }

    public override void PostDraw() => ImGui.PopStyleVar(3);

    public override void Draw()
    {
        var frame = coordinator.Evaluate(mode);
        if (frame.Lead == null)
            return;
        var config = plugin.Configuration;
        var forecast = frame.Forecast.Take(Math.Max(0, Math.Clamp(config.PredictionCount, 1, 10) - 1)).ToArray();
        var origin = ImGui.GetCursorScreenPos();
        var main = Math.Clamp(config.IconSize, 32f, 96f);
        var secondary = main * Math.Clamp(config.FutureIconScale, .45f, 1f);
        var count = 1 + forecast.Length;
        var end = HildaVisualStyle.Position(count - 1, main, secondary, config.IconSpacing, config.Horizontal);
        var lastSize = count == 1 ? main : secondary;
        var bounds = end + new Vector2(lastSize + 20f, lastSize + 24f);
        // Exactly one layout item: overlays can never move the next icon.
        ImGui.Dummy(bounds);
        DrawAction(frame.Lead, frame, origin + HildaVisualStyle.Position(0, main, secondary, config.IconSpacing, config.Horizontal), main, main / 65f, true);
        for (var i = 0; i < forecast.Length; i++)
            DrawAction(forecast[i], frame, origin + HildaVisualStyle.Position(i + 1, main, secondary, config.IconSpacing, config.Horizontal), secondary, main / 65f, false);
    }

    private unsafe void DrawAction(RotationSuggestion suggestion, RotationFrame frame, Vector2 pos, float size, float scale, bool lead)
    {
        if (NativeUiOcclusion.IsCovered(pos, new Vector2(size + scale, size + 4f * scale)))
            return;

        var info = display.Get(suggestion.ActionId);
        if (info.Icon == 0)
            return;
        var draw = ImGui.GetWindowDrawList();
        var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(info.Icon)).GetWrapOrEmpty();
        var inset = new Vector2(1.6f * scale);
        var tint = Color(1, 1, 1, 1);
        draw.AddImage(icon.Handle, pos + inset, pos + inset + new Vector2(size * .95f), Vector2.Zero, Vector2.One, tint);
        var border = Plugin.TextureProvider.GetFromGame(HildaVisualStyle.FrameTexture).GetWrapOrEmpty();
        var clipShadow = !plugin.Configuration.Horizontal && plugin.Configuration.IconSpacing < 9f;
        draw.AddImage(border.Handle, pos, pos + new Vector2(size + scale, size + (clipShadow ? 1 : 4) * scale),
            new Vector2(.007f, .014f), new Vector2(.1061f, .3333f - (clipShadow ? .03f : .01f)), tint);
        if (plugin.Configuration.ShowRangeFade && info.Range > 0 && Plugin.TargetManager.Target is { } target && target.CurrentDistance > info.Range)
            draw.AddRectFilled(pos, pos + new Vector2(size), Color(0, 0, 0, .5f));
        var row = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(suggestion.ActionId);
        var isWeave = SuggestionActionId.IsItem(suggestion.ActionId) ||
            row?.ActionCategory.RowId == 4 && suggestion.ActionId is not (2259 or 2261 or 2263);
        DrawRecast(suggestion.ActionId, frame, pos, size, scale, isWeave);
        if (plugin.Configuration.ShowHotkeys && frame.Anchor is { } anchor)
        {
            var sourceAction = frame.EffectiveMode == RotationMode.Aoe ? anchor.AoeAction : anchor.SingleTargetAction;
            var key = plugin.Hotkeys.Resolve(suggestion.ActionId, sourceAction,
                lead && frame.WrathLoaded, suggestion.UsesEntryButton);
            if (key != null)
                Text(key, pos + new Vector2(-2f, -6f), 26f * .7f * scale, 0xFFFFFFFF);
        }
        if (isWeave && plugin.Configuration.ShowWeaveIcon)
            Symbol(HildaVisualStyle.WeaveSymbol, pos + new Vector2(-2, size - 14 * scale), scale, 0xFF0080FF);
        if (lead && plugin.Configuration.ShowEnemyCount && frame.ActionEnemyCount > 1)
        {
            var at = pos + new Vector2(size - 16 * scale, size - 68 * scale);
            Symbol(HildaVisualStyle.EnemySymbol, at, scale, 0xFFFFFFFF);
            Text(frame.ActionEnemyCount.ToString(), at + new Vector2(2.3f, .4f) * scale, 26f * .6f * scale, 0xFF000000, false);
        }
        if (plugin.Configuration.ShowPositionals)
        {
            var symbol = PositionalCatalog.Get(suggestion.ActionId) switch
            {
                PositionalKind.Flank => HildaVisualStyle.FlankSymbol,
                PositionalKind.Rear => HildaVisualStyle.RearSymbol,
                _ => '\0',
            };
            if (symbol != '\0')
                Symbol(symbol, pos + new Vector2(size - 12 * scale, size - 32 * scale), scale, 0xFF00FF00);
        }
        if (plugin.SettingsOpen && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(size)))
        {
            var details = plugin.Configuration.DebugMode ? $"\n{frame.Status}\n{suggestion.Reason}" : string.Empty;
            ImGui.SetTooltip($"{mode}: {info.Name}{details}");
        }
    }

    private unsafe void DrawRecast(uint actionId, RotationFrame frame, Vector2 pos, float size, float scale, bool weave)
    {
        var manager = ActionManager.Instance();
        if (manager == null)
            return;
        var item = SuggestionActionId.IsItem(actionId);
        var type = item ? ActionType.Item : ActionType.Action;
        var nativeId = item ? SuggestionActionId.ItemRow(actionId) : actionId;
        var total = manager->GetRecastTime(type, nativeId);
        var elapsed = manager->GetRecastTimeElapsed(type, nativeId);
        var charges = item ? 0 : manager->GetCurrentCharges(nativeId);
        var maxCharges = item ? 0 : ActionManager.GetMaxCharges(nativeId, Plugin.ObjectTable.LocalPlayer?.Level ?? 0);
        if (maxCharges > 1 && charges > 0)
            Text(charges.ToString(), pos + new Vector2(size - 14 * scale, size - 14 * scale), 26f * .8f * scale, 0xFFFFFFFF, true, 0x800000BF);
        if (!plugin.Configuration.ShowCooldownSweep)
            return;
        if (total <= 0 && !weave && frame.Anchor is { } anchor)
        {
            total = manager->GetRecastTime(ActionType.Action, anchor.SingleTargetAction);
            elapsed = manager->GetRecastTimeElapsed(ActionType.Action, anchor.SingleTargetAction);
        }
        if (total <= 0 || elapsed <= 0 || elapsed >= total)
            return;
        if (!weave || total <= 2.5f)
        {
            var uv = HildaVisualStyle.RecastUv(elapsed, total);
            var texture = Plugin.TextureProvider.GetFromGame(HildaVisualStyle.RecastTexture).GetWrapOrEmpty();
            ImGui.GetWindowDrawList().AddImage(texture.Handle, pos, pos + new Vector2(size + scale, size - 1.1f * scale), uv.Min, uv.Max, Color(1, 1, 1, 1));
        }
        else if (charges == 0)
        {
            var remaining = maxCharges > 1 ? (total - elapsed) % (total / maxCharges) : total - elapsed;
            if (remaining <= 0) remaining = total / Math.Max(1, (int)maxCharges);
            var label = Math.Round(remaining).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var offset = new Vector2(-26f / (label.Length > 1 ? 1.8f : 2.8f), -7) * scale;
            Text(label, pos + new Vector2(size / 2) + offset, 26f * scale, 0xFFFFFFFF);
        }
    }

    private uint Color(float r, float g, float b, float a) => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a * plugin.Configuration.Opacity));

    private void Symbol(char symbol, Vector2 pos, float scale, uint color) =>
        Outlined(UiBuilder.IconFont, UiBuilder.IconFont.FontSize * scale, pos, color, symbol.ToString(), true, 0xFF000000);

    private void Text(string text, Vector2 pos, float size, uint color, bool outline = true, uint outlineColor = 0xFF000000)
    {
        using var font = plugin.Fonts.Miedinger.Push();
        Outlined(ImGui.GetFont(), size, pos, color, text, outline, outlineColor);
    }

    private void Outlined(ImFontPtr font, float size, Vector2 pos, uint color, string text, bool outline, uint outlineColor)
    {
        var draw = ImGui.GetWindowDrawList();
        uint Fade(uint value) => (value & 0x00FFFFFF) | ((uint)((value >> 24) * plugin.Configuration.Opacity) << 24);
        if (outline)
            for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                    if (x != 0 || y != 0)
                        draw.AddText(font, size, pos + new Vector2(x, y), Fade(outlineColor), text);
        draw.AddText(font, size, pos, Fade(color), text);
    }
}
