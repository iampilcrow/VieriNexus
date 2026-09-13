using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Keys;

namespace VieriRotationHelper.Windows;

internal sealed class SettingsWindow : Window
{
    private readonly Plugin plugin;
    private readonly WrathLiveProvider wrath;

    internal SettingsWindow(Plugin plugin, WrathLiveProvider wrath)
        : base("VieriRotationHelper Suite###VieriRotationHelperSettings")
    {
        this.plugin = plugin;
        this.wrath = wrath;
        Size = new Vector2(680, 720);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(570, 520),
            MaximumSize = new Vector2(1100, 1000),
        };
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("###VieriCombatSuiteTabs")) return;
        if (ImGui.BeginTabItem("Suggestions")) { DrawSuggestions(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Rotation Engine")) { DrawRotationEngine(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Switch")) { DrawSwitch(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Integrations")) { DrawIntegrations(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Keybinds")) { DrawKeybinds(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    private void DrawKeybinds()
    {
        var cfg = plugin.Configuration;
        var hotkey = plugin.WindowHotkey;
        ImGui.TextUnformatted("Suite window keybind");
        ImGui.TextWrapped("Record a key combination to open or close this settings window while FFXIV is focused. This does not toggle rotation, suggestions, Manual Control, or In Combat Only.");
        ImGui.TextUnformatted($"Current: {hotkey.Name}");
        if (hotkey.IsCapturing)
        {
            if (ImGui.Button("Press a key combination... (Esc cancels)")) hotkey.CancelCapture();
        }
        else if (ImGui.Button("Record keybind")) hotkey.StartCapture();
        ImGui.SameLine();
        if (ImGui.Button("Clear")) hotkey.Clear();
        var changed = ImGui.Checkbox("Enable keybind", ref cfg.WindowHotkeyEnabled);
        changed |= ImGui.Checkbox("Require exact modifier combination", ref cfg.WindowHotkeyExactModifiers);
        if (changed) plugin.Save();
        ImGui.TextWrapped("Use an unused Ctrl, Shift, or Alt combination. Existing game and other plugin bindings are not replaced or blocked.");
        if (cfg.WindowHotkey != VirtualKey.NO_KEY && !cfg.WindowHotkeyControl && !cfg.WindowHotkeyShift && !cfg.WindowHotkeyAlt)
            ImGui.TextWrapped("Warning: an unmodified key can also trigger while typing in chat.");
        ImGui.Separator();
        ImGui.TextWrapped("Rotation ON/OFF (F1 by default) and the other switch shortcuts remain in Switch settings.");
        if (ImGui.Button("Open Switch keybind settings")) plugin.OpenSwitchSettings();
    }

    private void DrawSuggestions()
    {
        var changed = false;
        changed |= ImGui.Checkbox("Enable rotation suggestions", ref plugin.Configuration.Enabled);
        ImGui.Separator();
        changed |= ImGui.Checkbox("Show Single Target", ref plugin.Configuration.ShowSingleTarget);
        changed |= ImGui.Checkbox("Show AoE", ref plugin.Configuration.ShowAoe);
        changed |= ImGui.Checkbox("Show Dynamic (switches by nearby targets)", ref plugin.Configuration.ShowDynamic);
        var aoeCount = plugin.Configuration.DynamicAoeTargetCount;
        if (ImGui.SliderInt("Dynamic AoE at target count", ref aoeCount, 2, 8))
        {
            plugin.Configuration.DynamicAoeTargetCount = aoeCount;
            changed = true;
        }
        changed |= ImGui.Checkbox("Show while out of combat", ref plugin.Configuration.ShowOutOfCombat);
        changed |= ImGui.Checkbox("Show without a target", ref plugin.Configuration.ShowWithoutTarget);
        changed |= ImGui.Checkbox("Show hotkeys", ref plugin.Configuration.ShowHotkeys);
        changed |= ImGui.Checkbox("Lock bars", ref plugin.Configuration.LockBars);
        changed |= ImGui.Checkbox("Horizontal layout", ref plugin.Configuration.Horizontal);
        var predictions = plugin.Configuration.PredictionCount;
        if (ImGui.SliderInt("Actions shown", ref predictions, 1, 10))
        {
            plugin.Configuration.PredictionCount = predictions;
            changed = true;
        }
        changed |= ImGui.SliderFloat("Lead icon size", ref plugin.Configuration.IconSize, 32f, 96f, "%.0f px");
        changed |= ImGui.SliderFloat("Future icon scale", ref plugin.Configuration.FutureIconScale, .45f, 1f, "%.2f");
        changed |= ImGui.SliderFloat("Icon spacing", ref plugin.Configuration.IconSpacing, 0f, 12f, "%.0f");
        changed |= ImGui.SliderFloat("Opacity", ref plugin.Configuration.Opacity, .2f, 1f, "%.2f");
        ImGui.Separator();
        changed |= ImGui.Checkbox("Show cooldown sweep", ref plugin.Configuration.ShowCooldownSweep);
        changed |= ImGui.Checkbox("Show weave icon", ref plugin.Configuration.ShowWeaveIcon);
        changed |= ImGui.Checkbox("Show positional cues", ref plugin.Configuration.ShowPositionals);
        changed |= ImGui.Checkbox("Dim actions when target is out of range", ref plugin.Configuration.ShowRangeFade);
        changed |= ImGui.Checkbox("Show nearby enemy count", ref plugin.Configuration.ShowEnemyCount);
        changed |= ImGui.Checkbox("Debug state and parity", ref plugin.Configuration.DebugMode);
        ImGui.TextWrapped("Hilda-style game icon frames, hotkeys, positional symbols, cooldowns, and multi-action prediction are driven by the same integrated Wrath rules that execute the rotation.");
        if (changed) plugin.Save();
    }

    private void DrawRotationEngine()
    {
        var color = plugin.EmbeddedEngineActive ? new Vector4(.35f, 1f, .5f, 1f) : new Vector4(1f, .65f, .2f, 1f);
        ImGui.TextColored(color, plugin.EmbeddedEngineActive ? "Integrated Wrath engine: ACTIVE" : "Integrated Wrath engine: WAITING");
        ImGui.TextWrapped(plugin.EngineStatus);
        ImGui.Spacing();
        if (plugin.EmbeddedEngineActive && ImGui.Button("Open full Rotation Engine settings", new Vector2(280, 34)))
            plugin.OpenEngineSettings();
        ImGui.TextWrapped("All Wrath job presets, advanced options, action replacement, Auto-Rotation, targeting, opener logic, and IPC controls now run inside VieriRotationHelper. Disable the separate Wrath Combo plugin to prevent duplicate action hooks.");
        ImGui.TextDisabled("Legacy command aliases remain available: /wrath and /wrathcombo");
    }

    private void DrawSwitch()
    {
        var cfg = plugin.Configuration.Switch;
        ImGui.TextColored(plugin.EmbeddedSwitchActive ? new Vector4(.35f, 1f, .5f, 1f) : new Vector4(1f, .65f, .2f, 1f),
            plugin.EmbeddedSwitchActive ? "Integrated switch: ACTIVE" : "Integrated switch: waiting for the separate VieriWrathSwitch plugin to be disabled and plugins reloaded");
        var show = cfg.ShowWindow;
        if (ImGui.Checkbox("Show the movable switch", ref show)) { cfg.ShowWindow = show; plugin.Save(); }
        ImGui.TextWrapped(cfg.BlockAutomatedMovement ? "Manual Movement / Targeting Only is ON." : "Manual Movement / Targeting Only is OFF.");
        ImGui.TextWrapped(cfg.CombatOnlyRotation ? "In Combat Only is ON." : "In Combat Only is OFF.");
        if (ImGui.Button("Open full Switch settings", new Vector2(240, 34))) plugin.OpenSwitchSettings();
        ImGui.TextDisabled("Legacy commands remain available: /wrathswitch and /ws");
    }

    private static void DrawIntegrations()
    {
        ImGui.TextColored(new Vector4(.35f, 1f, .5f, 1f), "Compatibility interfaces are enabled");
        ImGui.BulletText("WrathCombo.* IPC contracts for AutoDuty, BossMod, Avarice, and other clients");
        ImGui.BulletText("WrathSwitch.BeginAutomation for VieriCodex and VieriAutoDuty");
        ImGui.BulletText("Existing Wrath commands and automation lease behavior");
        ImGui.Spacing();
        ImGui.TextWrapped("VieriRotationHelper is now the provider. After installing it, disable the separate Wrath Combo and VieriWrathSwitch plugins, then reload plugins once so only one action hook and one switch provider exist.");
    }
}
