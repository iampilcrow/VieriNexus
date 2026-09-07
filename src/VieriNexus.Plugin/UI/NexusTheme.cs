using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VieriNexus.UI;

internal static class NexusTheme
{
    internal static readonly Vector4 Red = new(.92f, .16f, .19f, 1f);
    internal static readonly Vector4 RedMuted = new(.45f, .08f, .10f, 1f);
    internal static readonly Vector4 Gold = new(.94f, .70f, .30f, 1f);
    internal static readonly Vector4 Green = new(.29f, .86f, .43f, 1f);
    internal static readonly Vector4 Amber = new(1f, .72f, .22f, 1f);
    internal static readonly Vector4 Cyan = new(.25f, .78f, .88f, 1f);
    internal static readonly Vector4 Muted = new(.58f, .60f, .65f, 1f);
    internal static readonly Vector4 Panel = new(.065f, .070f, .082f, .98f);
    internal static readonly Vector4 PanelRaised = new(.085f, .092f, .108f, .98f);

    internal static void Push()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 7));

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.026f, .028f, .034f, .985f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(.045f, .048f, .058f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.28f, .11f, .12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.10f, .105f, .12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(.17f, .11f, .13f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(.24f, .09f, .11f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.055f, .025f, .030f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.16f, .035f, .045f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.12f, .125f, .145f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.29f, .08f, .10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.43f, .08f, .10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.27f, .065f, .08f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.37f, .075f, .09f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.47f, .08f, .10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(.98f, .31f, .32f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(.84f, .18f, .20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(1f, .30f, .31f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(.33f, .10f, .11f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, new Vector4(.09f, .095f, .11f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(.08f, .083f, .096f, .55f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.92f, .92f, .94f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
    }

    internal static void Pop()
    {
        ImGui.PopStyleColor(24);
        ImGui.PopStyleVar(7);
    }

    internal static void SectionTitle(string title, string? detail = null)
    {
        ImGui.TextColored(Gold, title.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(detail))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(detail);
        }
        ImGui.Separator();
    }

    internal static void StatusDot(Vector4 color, string text)
    {
        ImGui.TextColored(color, "●");
        ImGui.SameLine();
        ImGui.TextUnformatted(text);
    }
}
