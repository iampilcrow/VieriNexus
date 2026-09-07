using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace VieriNexus.UI;

internal sealed class SplashWindow(ISharedImmediateTexture logo) : Window(
    "###VieriNexusSplash",
    ImGuiWindowFlags.NoDecoration |
    ImGuiWindowFlags.NoMove |
    ImGuiWindowFlags.NoResize |
    ImGuiWindowFlags.NoInputs |
    ImGuiWindowFlags.NoNav |
    ImGuiWindowFlags.NoSavedSettings)
{
    private double openedAt = -1;

    internal void Show()
    {
        openedAt = -1;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        if (openedAt < 0)
            openedAt = ImGui.GetTime();

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(.5f));
        ImGui.SetNextWindowSize(new Vector2(520, 385), ImGuiCond.Always);
        NexusTheme.Push();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(18));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        NexusTheme.Pop();
    }

    public override void Draw()
    {
        var elapsed = ImGui.GetTime() - openedAt;
        if (elapsed >= 3.4)
        {
            IsOpen = false;
            return;
        }

        var alpha = (float)Math.Clamp(Math.Min(elapsed / .35, (3.4 - elapsed) / .45), 0, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, alpha);

        var draw = ImGui.GetWindowDrawList();
        var position = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        draw.AddRectFilledMultiColor(position, position + size,
            0xFF08090C, 0xFF0F0A0C, 0xFF160609, 0xFF08090C);
        draw.AddRect(position, position + size, ImGui.GetColorU32(NexusTheme.Red), 8f, ImDrawFlags.None, 1.5f);

        var wrap = logo.GetWrapOrEmpty();
        var imageSize = new Vector2(450, 300);
        ImGui.SetCursorPosX((size.X - imageSize.X) * .5f);
        ImGui.Image(wrap.Handle, imageSize);
        ImGui.SetCursorPosY(324);
        var subtitle = "THE COMPLETE VIERI EXPERIENCE";
        ImGui.SetCursorPosX((size.X - ImGui.CalcTextSize(subtitle).X) * .5f);
        ImGui.TextColored(NexusTheme.Gold, subtitle);
        ImGui.SetCursorPosY(350);
        var state = "Initializing modules and validating dependencies…";
        ImGui.SetCursorPosX((size.X - ImGui.CalcTextSize(state).X) * .5f);
        ImGui.TextDisabled(state);
        ImGui.PopStyleVar();
    }
}
