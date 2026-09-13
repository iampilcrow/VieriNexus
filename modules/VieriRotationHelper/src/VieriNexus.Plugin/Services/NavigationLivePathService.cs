using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using System.Numerics;
using VieriNexus.Application;

namespace VieriNexus.Services;

/// <summary>
/// Draws generated waypoints only while Nexus owns local execution or while a suite route
/// explicitly dispatched by this Nexus process remains active.
/// </summary>
internal sealed class NavigationLivePathService(
    IObjectTable objectTable,
    IGameGui gameGui,
    VnavmeshNavigationStopProvider navmesh)
{
    internal void Draw(bool enabled, bool showPointNumbers, bool nexusOwnsPath, bool suiteOwnsPath)
    {
        if (!enabled || (!nexusOwnsPath && !suiteOwnsPath) || objectTable.LocalPlayer is not { } player)
            return;

        IReadOnlyList<NavigationRoutePoint> waypoints = navmesh.GetActiveWaypoints();
        if (waypoints.Count == 0)
            return;

        var draw = ImGui.GetForegroundDrawList();
        uint firstLineColor = ImGui.GetColorU32(new Vector4(0.2f, 1f, 0.45f, 0.95f));
        uint lineColor = ImGui.GetColorU32(new Vector4(0.15f, 0.82f, 1f, 0.92f));
        uint pointColor = ImGui.GetColorU32(new Vector4(0.2f, 1f, 0.85f, 1f));
        Vector3 previousWorld = player.Position;

        for (int index = 0; index < waypoints.Count; index++)
        {
            NavigationRoutePoint waypoint = waypoints[index];
            Vector3 waypointWorld = new(waypoint.X, waypoint.Y, waypoint.Z);
            bool previousVisible = gameGui.WorldToScreen(previousWorld, out Vector2 previousScreen);
            bool waypointVisible = gameGui.WorldToScreen(waypointWorld, out Vector2 waypointScreen);
            if (previousVisible && waypointVisible)
                draw.AddLine(previousScreen, waypointScreen, index == 0 ? firstLineColor : lineColor, 3f);
            if (waypointVisible)
            {
                draw.AddCircleFilled(waypointScreen, index == waypoints.Count - 1 ? 7f : 5f, pointColor);
                if (showPointNumbers)
                    draw.AddText(waypointScreen + new Vector2(7f, -8f), pointColor, $"N{index + 1}");
            }
            previousWorld = waypointWorld;
        }
    }
}
