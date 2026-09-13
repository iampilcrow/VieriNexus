using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace WrathSwitch.Windows;

internal sealed class RotationWindow : Window
{
    private readonly Plugin plugin;
    private bool applyPosition = true;
    private bool lastLockState;
    private Vector2 currentSize;

    public RotationWindow(Plugin plugin)
        : base(plugin.IsEmbedded ? "VieriRotationHelper Switch###WrathSwitchStatus" : "VieriWrathSwitch###WrathSwitchStatus")
    {
        this.plugin = plugin;
        lastLockState = plugin.Configuration.LockWindow;
        RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        var cfg = plugin.Configuration;

        if (cfg.LockWindow != lastLockState)
        {
            if (cfg.WindowPositionInitialized)
            {
                var titleBarHeight = ImGui.GetFrameHeight();
                cfg.WindowPosition = new Vector2(
                    cfg.WindowPosition.X,
                    cfg.WindowPosition.Y + (cfg.LockWindow ? titleBarHeight : -titleBarHeight));
                applyPosition = true;
                plugin.MarkDirty();
            }

            lastLockState = cfg.LockWindow;
        }

        Flags = ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse |
                ImGuiWindowFlags.NoCollapse |
                (cfg.LockWindow ? ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar : ImGuiWindowFlags.None);
        ImGui.SetNextWindowBgAlpha(cfg.Opacity);

        if (applyPosition)
        {
            var viewport = ImGui.GetMainViewport();
            var position = cfg.WindowPositionInitialized
                ? cfg.WindowPosition
                : viewport.Pos + new Vector2(viewport.Size.X * .5f - 95, viewport.Size.Y * .68f);
            ImGui.SetNextWindowPos(position, ImGuiCond.Always);
            applyPosition = false;
        }
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var position = ImGui.GetWindowPos();
        currentSize = ImGui.GetWindowSize();
        if (!cfg.WindowPositionInitialized || Vector2.DistanceSquared(position, cfg.WindowPosition) > .25f)
        {
            cfg.WindowPosition = position;
            cfg.WindowPositionInitialized = true;
            plugin.MarkDirty();
        }

        var available = plugin.WrathAvailable;
        var enabled = plugin.AutoRotationEnabled;
        var color = !available ? cfg.UnavailableColor : enabled ? cfg.EnabledColor : cfg.DisabledColor;
        var hover = Vector4.Lerp(color, Vector4.One, .13f);
        var active = Vector4.Lerp(color, Vector4.Zero, .18f);

        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.One);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        if (!available) ImGui.BeginDisabled();
        var label = (!available ? "N/A" : enabled ? "ON" : "OFF") + "###WrathSwitchToggle";
        ImGui.SetWindowFontScale(Math.Clamp(1.4f * cfg.Scale, 1f, 2.5f));
        if (ImGui.Button(label, new Vector2(184, 54) * cfg.Scale))
            plugin.ToggleRotation();
        ImGui.SetWindowFontScale(1f);
        if (!available) ImGui.EndDisabled();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(available
                ? $"Click to turn Wrath Combo Auto-Rotation {(enabled ? "off" : "on")}."
                : "Wrath Combo is not loaded or its IPC is not ready.");

        var manualMovement = cfg.BlockAutomatedMovement;
        var movementColor = manualMovement ? cfg.EnabledColor : cfg.DisabledColor;
        ImGui.PushStyleColor(ImGuiCol.Button, movementColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Lerp(movementColor, Vector4.One, .13f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Lerp(movementColor, Vector4.Zero, .18f));
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.One);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        var manualControlText = $"MANUAL CONTROL: {(manualMovement ? "ON" : "OFF")}";
        var manualControlWidth = ImGui.CalcTextSize(manualControlText).X;
        var manualControlFontScale = Math.Min(1f, 174f * cfg.Scale / Math.Max(1f, manualControlWidth));
        ImGui.SetWindowFontScale(manualControlFontScale);
        if (ImGui.Button($"{manualControlText}###WrathSwitchMovementSafety",
                new Vector2(184, 30) * cfg.Scale))
            plugin.ToggleMovementSafety();
        var manualButtonMin = ImGui.GetItemRectMin();
        var manualButtonMax = ImGui.GetItemRectMax();
        ImGui.SetWindowFontScale(1f);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        if (manualMovement && cfg.AnimateManualControlAlert)
            DrawActiveModeAlert(manualButtonMin, manualButtonMax, cfg.Scale);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(manualMovement
                ? "Manual Movement / Targeting Only is ON. Click to resume paused automation immediately."
                : "Click to pause automated movement, targeting, and camera steering while keeping Wrath rotations active for your target.");

        var combatOnly = cfg.CombatOnlyRotation;
        var combatOnlyColor = combatOnly ? cfg.EnabledColor : cfg.DisabledColor;
        ImGui.PushStyleColor(ImGuiCol.Button, combatOnlyColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Lerp(combatOnlyColor, Vector4.One, .13f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Lerp(combatOnlyColor, Vector4.Zero, .18f));
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.One);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        var combatOnlyText = $"IN COMBAT ONLY: {(combatOnly ? "ON" : "OFF")}";
        ImGui.SetWindowFontScale(manualControlFontScale);
        if (ImGui.Button($"{combatOnlyText}###WrathSwitchCombatOnly", new Vector2(184, 30) * cfg.Scale))
            plugin.ToggleCombatOnly();
        var combatOnlyButtonMin = ImGui.GetItemRectMin();
        var combatOnlyButtonMax = ImGui.GetItemRectMax();
        ImGui.SetWindowFontScale(1f);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        if (combatOnly && cfg.AnimateManualControlAlert)
            DrawActiveModeAlert(combatOnlyButtonMin, combatOnlyButtonMax, cfg.Scale);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(combatOnly
                ? "In Combat Only is ON. Wrath stays idle outside combat and begins rotating when combat starts."
                : "Click to prevent Wrath from using any rotation actions while outside combat.");

        if (cfg.ShowHotkeyText)
        {
            var hotkey = plugin.HotkeyName;
            var width = ImGui.CalcTextSize(hotkey).X;
            ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), (ImGui.GetWindowWidth() - width) / 2));
            ImGui.TextDisabled(hotkey);
        }

        if (ImGui.BeginPopupContextWindow("###WrathSwitchContext", ImGuiPopupFlags.MouseButtonRight))
        {
            if (ImGui.MenuItem("Configure")) plugin.OpenSettings();
            if (ImGui.MenuItem(cfg.LockWindow ? "Unlock window" : "Lock window"))
            {
                cfg.LockWindow = !cfg.LockWindow;
                plugin.SaveNow();
            }
            if (ImGui.MenuItem("Manual Movement / Targeting Only", string.Empty, cfg.BlockAutomatedMovement))
                plugin.ToggleMovementSafety();
            if (ImGui.MenuItem("In Combat Only", string.Empty, cfg.CombatOnlyRotation))
                plugin.ToggleCombatOnly();
            if (ImGui.MenuItem("Animate active-mode alerts", string.Empty, cfg.AnimateManualControlAlert))
            {
                cfg.AnimateManualControlAlert = !cfg.AnimateManualControlAlert;
                plugin.SaveNow();
            }
            if (ImGui.MenuItem("Hide switch"))
            {
                cfg.ShowWindow = false;
                plugin.SaveNow();
            }
            ImGui.EndPopup();
        }
    }

    public override void OnClose()
    {
        plugin.Configuration.ShowWindow = false;
        plugin.SaveNow();
    }

    public void ApplyDefaultPosition() => applyPosition = true;

    private static void DrawActiveModeAlert(Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pulse = .72f + .28f * MathF.Sin((float)ImGui.GetTime() * 6.5f);
        var padding = Math.Max(2f, 3f * scale);
        var rounding = Math.Max(5f, 5f * scale);

        // A warm pulsing halo keeps enabled safety modes visually distinct from the
        // green rotation state without changing the switch's dimensions or hit target.
        drawList.AddRect(
            min - new Vector2(padding),
            max + new Vector2(padding),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, .52f, .05f, .26f * pulse)),
            rounding,
            ImDrawFlags.None,
            Math.Max(4f, 6f * scale));
        drawList.AddRect(
            min - Vector2.One,
            max + Vector2.One,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, .82f, .22f, .95f)),
            rounding,
            ImDrawFlags.None,
            Math.Max(1.5f, 2f * scale));

        // Marching white-gold dashes echo FFXIV's animated combo-ready border.
        var dash = Math.Max(6f, 8f * scale);
        var gap = Math.Max(4f, 6f * scale);
        var phase = (float)(ImGui.GetTime() * 46f * scale) % (dash + gap);
        var trailColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, .98f, .78f, 1f));
        var thickness = Math.Max(1.5f, 2f * scale);
        DrawDashedEdge(drawList, min, new Vector2(max.X, min.Y), phase, dash, gap, trailColor, thickness);
        DrawDashedEdge(drawList, new Vector2(max.X, min.Y), max, phase, dash, gap, trailColor, thickness);
        DrawDashedEdge(drawList, max, new Vector2(min.X, max.Y), phase, dash, gap, trailColor, thickness);
        DrawDashedEdge(drawList, new Vector2(min.X, max.Y), min, phase, dash, gap, trailColor, thickness);
    }

    private static void DrawDashedEdge(
        ImDrawListPtr drawList,
        Vector2 start,
        Vector2 end,
        float phase,
        float dash,
        float gap,
        uint color,
        float thickness)
    {
        var edge = end - start;
        var length = edge.Length();
        if (length <= 0f)
            return;

        var direction = edge / length;
        for (var distance = -phase; distance < length; distance += dash + gap)
        {
            var segmentStart = Math.Clamp(distance, 0f, length);
            var segmentEnd = Math.Clamp(distance + dash, 0f, length);
            if (segmentEnd > segmentStart)
                drawList.AddLine(start + direction * segmentStart, start + direction * segmentEnd, color, thickness);
        }
    }

    public override bool DrawConditions()
    {
        if (!plugin.Configuration.WindowPositionInitialized)
            return true;

        var size = currentSize.X > 0 && currentSize.Y > 0
            ? currentSize
            : new Vector2(200, plugin.Configuration.ShowHotkeyText ? 126 : 110) * plugin.Configuration.Scale;
        return !NativeUiOcclusion.IsCovered(plugin.Configuration.WindowPosition, size);
    }
}

