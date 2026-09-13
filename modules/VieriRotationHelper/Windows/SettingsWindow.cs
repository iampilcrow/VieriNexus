using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace VieriRotationHelper.Windows;

internal sealed class SettingsWindow : Window
{
    private readonly Plugin plugin;
    private readonly WrathLiveProvider wrath;

    internal SettingsWindow(Plugin plugin, WrathLiveProvider wrath)
        : base("Nexus Combat · Rotation###VieriRotationHelperSettings")
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

    public override void Draw() => DrawNexusContents();

    internal void DrawNexusContents()
    {
        if (ImGui.CollapsingHeader("On-Screen Ability Suggestions###combat-ability-suggestions", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            DrawSuggestions();
            ImGui.Unindent();
        }
        if (ImGui.CollapsingHeader("Wrath Combo###combat-wrath-combo"))
        {
            ImGui.Indent();
            DrawRotationEngine();
            ImGui.Unindent();
        }
        if (ImGui.CollapsingHeader("Manual On-Screen Switch###combat-manual-switch"))
        {
            ImGui.Indent();
            DrawSwitch();
            ImGui.Unindent();
        }
    }

    private void DrawSuggestions()
    {
        var changed = false;
        ImGui.TextWrapped("Shows the next abilities chosen by Wrath Combo.");
        changed |= ImGui.Checkbox("Show ability suggestions", ref plugin.Configuration.Enabled);
        ImGui.Spacing();
        ImGui.TextUnformatted("Bars to show");
        changed |= ImGui.Checkbox("Single Target", ref plugin.Configuration.ShowSingleTarget);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("Area of Effect", ref plugin.Configuration.ShowAoe);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("Dynamic", ref plugin.Configuration.ShowDynamic);
        var aoeCount = plugin.Configuration.DynamicAoeTargetCount;
        if (plugin.Configuration.ShowDynamic && ImGui.SliderInt("Use Area of Effect at", ref aoeCount, 2, 8, "%d nearby enemies"))
        {
            plugin.Configuration.DynamicAoeTargetCount = aoeCount;
            changed = true;
        }
        ImGui.Spacing();
        ImGui.TextUnformatted("When to show");
        changed |= ImGui.Checkbox("Show while out of combat", ref plugin.Configuration.ShowOutOfCombat);
        changed |= ImGui.Checkbox("Show without a target", ref plugin.Configuration.ShowWithoutTarget);
        if (ImGui.TreeNode("Appearance"))
        {
            changed |= ImGui.Checkbox("Show assigned hotkeys", ref plugin.Configuration.ShowHotkeys);
            changed |= ImGui.Checkbox("Lock bars", ref plugin.Configuration.LockBars);
            changed |= ImGui.Checkbox("Horizontal layout", ref plugin.Configuration.Horizontal);
            var predictions = plugin.Configuration.PredictionCount;
            if (ImGui.SliderInt("Abilities shown", ref predictions, 1, 10))
            {
                plugin.Configuration.PredictionCount = predictions;
                changed = true;
            }
            changed |= ImGui.SliderFloat("First icon size", ref plugin.Configuration.IconSize, 32f, 96f, "%.0f px");
            changed |= ImGui.SliderFloat("Following icon size", ref plugin.Configuration.FutureIconScale, .45f, 1f, "%.2f");
            changed |= ImGui.SliderFloat("Spacing", ref plugin.Configuration.IconSpacing, 0f, 12f, "%.0f px");
            changed |= ImGui.SliderFloat("Opacity", ref plugin.Configuration.Opacity, .2f, 1f, "%.2f");
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("Extra indicators"))
        {
            changed |= ImGui.Checkbox("Cooldown", ref plugin.Configuration.ShowCooldownSweep);
            changed |= ImGui.Checkbox("Weave ability", ref plugin.Configuration.ShowWeaveIcon);
            changed |= ImGui.Checkbox("Position", ref plugin.Configuration.ShowPositionals);
            changed |= ImGui.Checkbox("Dim when out of range", ref plugin.Configuration.ShowRangeFade);
            changed |= ImGui.Checkbox("Nearby enemy count", ref plugin.Configuration.ShowEnemyCount);
            ImGui.TreePop();
        }
        if (changed) plugin.Save();
    }

    private void DrawRotationEngine()
    {
        var color = plugin.EmbeddedEngineActive ? new Vector4(.35f, 1f, .5f, 1f) : new Vector4(1f, .65f, .2f, 1f);
        ImGui.TextColored(color, plugin.EmbeddedEngineActive ? "Ready" : "Wrath Combo is not available");
        ImGui.TextWrapped("Wrath Combo handles rotations and job-specific behavior.");
        if (plugin.EmbeddedEngineActive && ImGui.Button("Open Wrath Combo settings", new Vector2(280, 34)))
            plugin.OpenEngineSettings();
    }

    private void DrawSwitch()
    {
        if (plugin.EmbeddedSwitchActive)
            plugin.DrawSwitchSettings();
        else
            ImGui.TextWrapped("The manual switch is temporarily unavailable.");
    }
}
