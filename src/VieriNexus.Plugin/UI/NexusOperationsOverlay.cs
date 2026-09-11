using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using VieriNexus.Application;
using VieriNexus.Services;

namespace VieriNexus.UI;

internal sealed class NexusOperationsOverlay : Window
{
    private readonly Plugin plugin;
    private readonly NavigationRouteRuntimeService navigation;
    private readonly GearShoppingRuntimeService gear;
    private readonly ProgressionRuntimeService progression;
    private readonly NexusMaintenanceRuntimeService maintenance;
    private readonly StrikingDummyTravelService strikingDummies;
    private readonly Action<string> openPage;
    private string message = string.Empty;

    internal NexusOperationsOverlay(
        Plugin plugin,
        NavigationRouteRuntimeService navigation,
        GearShoppingRuntimeService gear,
        ProgressionRuntimeService progression,
        NexusMaintenanceRuntimeService maintenance,
        StrikingDummyTravelService strikingDummies,
        Action<string> openPage)
        : base("Nexus Operations###VieriNexusOperations",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        this.navigation = navigation;
        this.gear = gear;
        this.progression = progression;
        this.maintenance = maintenance;
        this.strikingDummies = strikingDummies;
        this.openPage = openPage;
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public override bool DrawConditions() => plugin.Configuration.ShowOperationsOverlay;

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize;
        if (plugin.Configuration.LockOperationsOverlay)
            Flags |= ImGuiWindowFlags.NoMove;
        if (plugin.Configuration.OperationsOverlayTransparent)
            Flags |= ImGuiWindowFlags.NoBackground;
        NexusTheme.Push();
    }

    public override void PostDraw() => NexusTheme.Pop();

    public override void Draw()
    {
        bool navigationActive = navigation.Status.State is not NavigationRouteExecutionState.Idle and
            not NavigationRouteExecutionState.Completed and not NavigationRouteExecutionState.Failed;
        bool gearActive = gear.Status.IsActive;
        bool progressionActive = progression.State?.Goal.Status == VieriNexus.Domain.GoalStatus.Active;
        bool maintenanceActive = maintenance.Status.IsActive;
        bool dummyTravelActive = strikingDummies.Status.IsActive;
        bool anyActive = navigationActive || gearActive || progressionActive || maintenanceActive || dummyTravelActive;

        if (anyActive)
        {
            if (ImGui.Button("Stop"))
            {
                navigation.Stop();
                strikingDummies.Stop(out _);
                gear.Stop();
                maintenance.Stop(out _);
                progression.StopNow();
                message = "Stop requested for every Nexus-owned operation.";
            }
            ImGui.SameLine();
        }

        CategoryButton("Goto", "NexusGoto");
        if (ImGui.BeginPopup("NexusGoto"))
        {
            if (ImGui.Selectable("Saved routes")) Open("Routes & Navigation");
            if (ImGui.BeginMenu("Striking dummies"))
            {
                foreach (IGrouping<string, StrikingDummyDestination> expansion in
                         StrikingDummyCatalog.Destinations.GroupBy(item => item.Expansion))
                {
                    if (!ImGui.BeginMenu(expansion.Key))
                        continue;
                    foreach (StrikingDummyDestination destination in expansion)
                    {
                        if (ImGui.Selectable($"{destination.DisplayLocation} — Lv. {destination.Levels}"))
                        {
                            ImGui.CloseCurrentPopup();
                            strikingDummies.Start(destination, out message);
                        }
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenu();
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        CategoryButton("Gear", "NexusGear");
        if (ImGui.BeginPopup("NexusGear"))
        {
            if (ImGui.Selectable("Shop for upgrades")) Open("Gear & Inventory");
            if (gearActive && ImGui.Selectable("Stop gear operation"))
                message = gear.Stop().Message;
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        CategoryButton("Inventory", "NexusInventory");
        if (ImGui.BeginPopup("NexusInventory"))
        {
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Run enabled maintenance"))
            {
                ImGui.CloseCurrentPopup();
                maintenance.StartConfigured(out message);
            }
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Self-repair"))
            {
                ImGui.CloseCurrentPopup();
                maintenance.Start(NexusMaintenanceOperation.Repair, out message);
            }
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Extract materia"))
            {
                ImGui.CloseCurrentPopup();
                maintenance.Start(NexusMaintenanceOperation.ExtractMateria, out message);
            }
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Register collectibles"))
            {
                ImGui.CloseCurrentPopup();
                maintenance.StartRegistrations(out message);
            }
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Open coffers"))
            {
                ImGui.CloseCurrentPopup();
                maintenance.Start(NexusMaintenanceOperation.OpenCoffers, out message);
            }
            if (ImGui.Selectable("Gear & Inventory page")) Open("Gear & Inventory");
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        CategoryButton("Extras", "NexusExtras");
        if (ImGui.BeginPopup("NexusExtras"))
        {
            if (ImGui.Selectable("Progression")) Open("Progression");
            if (progressionActive && ImGui.Selectable("Finish current duty, then stop"))
                message = progression.StopAfterCurrentDuty().Message;
            if (ImGui.Selectable("Control Center")) Open("Overview");
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("×###CloseNexusOperations"))
        {
            plugin.Configuration.ShowOperationsOverlay = false;
            plugin.Save();
        }

        if (!plugin.Configuration.ShowOperationsStatus)
            return;

        string status = gearActive ? gear.Status.Message :
            maintenanceActive ? maintenance.Status.Message :
            dummyTravelActive ? strikingDummies.Status.Message :
            navigationActive ? navigation.Status.Message :
            progressionActive ? progression.State!.ActiveTask?.StatusDetail ?? progression.State.ActiveTask?.Title ?? progression.State.Goal.Title :
            message;
        if (!string.IsNullOrWhiteSpace(status))
        {
            ImGui.Separator();
            ImGui.TextColored(anyActive ? NexusTheme.Green : NexusTheme.Muted, status);
        }
    }

    private void Open(string page)
    {
        ImGui.CloseCurrentPopup();
        openPage(page);
    }

    private static void CategoryButton(string label, string popup)
    {
        if (ImGui.Button(label))
            ImGui.OpenPopup(popup);
    }

}
