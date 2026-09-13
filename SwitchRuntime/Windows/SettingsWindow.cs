using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace WrathSwitch.Windows;

internal sealed class SettingsWindow : Window
{
    private readonly Plugin plugin;

    public SettingsWindow(Plugin plugin)
        : base(plugin.IsEmbedded ? "VieriRotationHelper · Switch###WrathSwitchSettings" : "VieriWrathSwitch Settings###WrathSwitchSettings", ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        Size = new Vector2(570, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 450),
            MaximumSize = new Vector2(900, 900),
        };
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var stateColor = !plugin.WrathAvailable
            ? cfg.UnavailableColor
            : plugin.AutoRotationEnabled ? cfg.EnabledColor : cfg.DisabledColor;
        ImGui.TextColored(stateColor, plugin.WrathAvailable
            ? $"Wrath Combo: Auto-Rotation {(plugin.AutoRotationEnabled ? "ON" : "OFF")}" 
            : "Wrath Combo: unavailable");
        ImGui.SameLine();
        if (ImGui.Button("Open Wrath Auto Settings"))
            Plugin.CommandManager.ProcessCommand("/wrath autosettings");
        ImGui.Separator();

        ImGui.TextUnformatted("Switch window");
        var show = cfg.ShowWindow;
        if (ImGui.Checkbox("Show the movable switch", ref show)) { cfg.ShowWindow = show; plugin.SaveNow(); }
        var locked = cfg.LockWindow;
        if (ImGui.Checkbox("Lock position and hide title bar", ref locked)) { cfg.LockWindow = locked; plugin.SaveNow(); }
        var hotkeyText = cfg.ShowHotkeyText;
        if (ImGui.Checkbox("Show assigned keybind below the button", ref hotkeyText)) { cfg.ShowHotkeyText = hotkeyText; plugin.SaveNow(); }
        var hideUi = cfg.HideWhenGameUiHidden;
        if (ImGui.Checkbox("Hide with the FFXIV UI", ref hideUi)) { cfg.HideWhenGameUiHidden = hideUi; plugin.SaveNow(); }

        var scale = cfg.Scale;
        ImGui.SetNextItemWidth(250);
        if (ImGui.SliderFloat("Scale", ref scale, .65f, 2f, "%.2fx")) { cfg.Scale = scale; plugin.MarkDirty(); }
        var opacity = cfg.Opacity;
        ImGui.SetNextItemWidth(250);
        if (ImGui.SliderFloat("Window opacity", ref opacity, .25f, 1f, "%.2f")) { cfg.Opacity = opacity; plugin.MarkDirty(); }
        if (ImGui.Button("Reset switch position")) plugin.ResetWindowPosition();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Manual control");
        var blockMovement = cfg.BlockAutomatedMovement;
        if (ImGui.Checkbox("Manual Movement / Targeting Only", ref blockMovement))
            plugin.SetMovementSafety(blockMovement);
        ImGui.TextWrapped("When ON, VieriCodex, AutoDuty, BossMod, and navmesh movement, targeting, and automatic camera steering are paused. Wrath Auto-Rotation stays available for the target you select. Turn it OFF to let the paused task continue immediately.");
        var combatOnly = cfg.CombatOnlyRotation;
        if (ImGui.Checkbox("In Combat Only", ref combatOnly))
            plugin.SetCombatOnly(combatOnly);
        ImGui.TextWrapped("When ON, Wrath performs no rotation actions outside combat. It begins rotating normally as soon as combat starts. Travel and other automation are not paused.");
        var animateManualControlAlert = cfg.AnimateManualControlAlert;
        if (ImGui.Checkbox("Animate active-mode warnings", ref animateManualControlAlert))
        {
            cfg.AnimateManualControlAlert = animateManualControlAlert;
            plugin.SaveNow();
        }
        ImGui.TextDisabled("Shows the same pulsing gold glow and animated combo-style border while Manual Control or In Combat Only is ON.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Keybind");
        ImGui.TextWrapped("The keybind is read directly from FFXIV's key-state buffer and works while the game is focused. Choose an unused combination; VieriWrathSwitch does not consume the key, so FFXIV can still respond to the same bind.");
        ImGui.TextUnformatted($"Current: {plugin.HotkeyName}");

        if (plugin.IsCapturingHotkey)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.65f, .34f, .08f, 1));
            if (ImGui.Button("Press a key combination... (Esc cancels)", new Vector2(300, 32)))
                plugin.CancelHotkeyCapture();
            ImGui.PopStyleColor();
        }
        else if (ImGui.Button("Record keybind", new Vector2(150, 30)))
        {
            plugin.StartHotkeyCapture();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear")) plugin.ClearHotkey();

        var hotkeyEnabled = cfg.HotkeyEnabled;
        if (ImGui.Checkbox("Enable keybind", ref hotkeyEnabled)) { cfg.HotkeyEnabled = hotkeyEnabled; plugin.SaveNow(); }
        var exact = cfg.ExactModifiers;
        if (ImGui.Checkbox("Require exact modifier combination", ref exact)) { cfg.ExactModifiers = exact; plugin.SaveNow(); }
        if (cfg.Hotkey != Dalamud.Game.ClientState.Keys.VirtualKey.NO_KEY &&
            !cfg.HotkeyControl && !cfg.HotkeyShift && !cfg.HotkeyAlt)
            ImGui.TextColored(new Vector4(1f, .7f, .2f, 1), "Tip: a Ctrl, Shift, or Alt combination avoids triggering while typing in chat.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Colors");
        var enabledColor = cfg.EnabledColor;
        if (ImGui.ColorEdit4("Rotation on", ref enabledColor)) { cfg.EnabledColor = enabledColor; plugin.MarkDirty(); }
        var disabledColor = cfg.DisabledColor;
        if (ImGui.ColorEdit4("Rotation off", ref disabledColor)) { cfg.DisabledColor = disabledColor; plugin.MarkDirty(); }
        var unavailableColor = cfg.UnavailableColor;
        if (ImGui.ColorEdit4("Wrath unavailable", ref unavailableColor)) { cfg.UnavailableColor = unavailableColor; plugin.MarkDirty(); }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextWrapped("To remove Wrath Combo's original entry: open Dalamud Settings → Server Info Bar and disable the “Wrath Combo” entry. VieriWrathSwitch does not take ownership of another plugin's server-bar item.");
        ImGui.TextDisabled("Commands: /ws, /ws toggle, /ws on, /ws off, /ws safe, /ws combat, /ws bind, /ws lock, /ws unlock");
    }
}

