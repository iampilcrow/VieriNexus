using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using System.Numerics;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed class NavigationRoutePreviewService(
    IClientState clientState,
    IGameGui gameGui)
{
    private NavigationRoutePlan? plan;
    private bool showPointNumbers;

    internal Guid? RouteId => plan?.RouteId;

    internal void Set(NavigationRoutePlan? value, bool numbers = true)
    {
        plan = value;
        showPointNumbers = numbers;
    }

    internal void Draw()
    {
        NavigationRoutePlan? current = plan;
        if (current is null || !current.IsValid || current.TerritoryId != clientState.TerritoryType)
            return;

        var draw = ImGui.GetForegroundDrawList();
        uint lineColor = ImGui.GetColorU32(new Vector4(0.95f, 0.18f, 0.18f, 0.9f));
        uint pointColor = ImGui.GetColorU32(new Vector4(1f, 0.75f, 0.15f, 1f));
        Vector2? previous = null;
        for (int index = 0; index < current.Points.Count; index++)
        {
            NavigationRoutePoint point = current.Points[index];
            if (!gameGui.WorldToScreen(new Vector3(point.X, point.Y, point.Z), out Vector2 screen))
            {
                previous = null;
                continue;
            }

            if (previous is { } prior)
                draw.AddLine(prior, screen, lineColor, 3f);
            draw.AddCircleFilled(screen, 6f, pointColor);
            if (showPointNumbers)
                draw.AddText(screen + new Vector2(8f, -10f), pointColor, (index + 1).ToString());
            previous = screen;
        }
    }
}
