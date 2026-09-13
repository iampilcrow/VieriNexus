using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Ipc;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
    private readonly NexusControlService control;
    private readonly Action<string> openPage;
    private readonly ICallGateSubscriber<string, object> lifestreamCommand;
    private readonly ICallGateSubscriber<bool> lifestreamBusy;
    private readonly ICallGateSubscriber<object> lifestreamAbort;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloor;
    private string message = string.Empty;
    private bool quickTravelRequested;
    private bool quickTravelObservedBusy;
    private string quickTravelDestination = string.Empty;
    private DateTimeOffset quickTravelStartedAt;

    private static readonly QuickPoint[] SummoningBells =
    [
        new("Limsa Lominsa", 129, new(-123.88806f, 17.990356f, 21.469421f)),
        new("Old Gridania", 133, new(171.00781f, 15.487854f, -101.487854f)),
        new("Ul'dah", 131, new(148.91272f, 3.982544f, -44.205383f)),
        new("The Pillars", 419, new(-151.1712f, -12.64978f, -11.7647705f)),
        new("Rhalgr's Reach", 635, new(-57.63336f, -0.015319824f, 49.30188f)),
        new("Kugane", 628, new(19.394226f, 4.043579f, 53.025024f)),
        new("The Doman Enclave", 759, new(60.56299f, -0.015319824f, -3.982666f)),
        new("The Crystarium", 819, new(-69.840576f, -7.7058716f, 123.49121f)),
        new("Eulmore", 820, new(7.1869507f, 83.17688f, 31.448853f)),
        new("Old Sharlayan", 962, new(42.09961f, 2.517002f, -39.414062f)),
        new("Radz-at-Han", 963, new(26.749023f, -0.015319824f, -53.696533f)),
        new("Tuliyollal", 1185, new(18.57019f, -14.023071f, 120.408936f)),
        new("Nexus Arcade", 1186, new(-151.59845f, 0.59503174f, -15.304871f)),
    ];

    internal NexusOperationsOverlay(
        Plugin plugin,
        NavigationRouteRuntimeService navigation,
        GearShoppingRuntimeService gear,
        ProgressionRuntimeService progression,
        NexusMaintenanceRuntimeService maintenance,
        StrikingDummyTravelService strikingDummies,
        NexusControlService control,
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
        this.control = control;
        this.openPage = openPage;
        lifestreamCommand = Plugin.PluginInterface.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand");
        lifestreamBusy = Plugin.PluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        lifestreamAbort = Plugin.PluginInterface.GetIpcSubscriber<object>("Lifestream.Abort");
        pointOnFloor = Plugin.PluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>(
            "vnavmesh.Query.Mesh.PointOnFloor");
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
        UpdateQuickTravel();
        bool navigationActive = navigation.Status.State is not NavigationRouteExecutionState.Idle and
            not NavigationRouteExecutionState.Completed and not NavigationRouteExecutionState.Failed;
        bool gearActive = gear.Status.IsActive;
        bool progressionActive = progression.State?.Goal.Status == VieriNexus.Domain.GoalStatus.Active;
        bool maintenanceActive = maintenance.Status.IsActive;
        bool dummyTravelActive = strikingDummies.Status.IsActive;
        bool anyActive = navigationActive || gearActive || progressionActive || maintenanceActive || dummyTravelActive ||
                         quickTravelRequested;

        if (anyActive)
        {
            if (ImGui.Button("Stop"))
            {
                bool quickStopped = StopQuickTravel();
                VieriNexus.Contracts.NexusCommandResultDto stopped = control.StopAll();
                message = quickStopped && !stopped.Accepted
                    ? "Stopped the Nexus travel request."
                    : stopped.Message;
            }
            ImGui.SameLine();
        }

        CategoryButton("Goto", "NexusGoto");
        if (ImGui.BeginPopup("NexusGoto"))
        {
            if (ImGui.Selectable("Saved routes")) Open("Routes & Navigation");
            if (ImGui.Selectable("Inn")) StartLifestream("inn", "inn");
            if (ImGui.Selectable("Grand Company headquarters")) StartLifestream("gc", "Grand Company headquarters");
            if (ImGui.Selectable("Grand Company supply counter")) StartGrandCompanyPoint(barracks: false);
            if (ImGui.Selectable("Grand Company barracks entrance")) StartGrandCompanyPoint(barracks: true);
            if (ImGui.Selectable("Flag marker")) StartFlagMarker();
            if (ImGui.Selectable("Apartment")) StartLifestream("apartment", "apartment");
            if (ImGui.Selectable("Personal estate")) StartLifestream("home", "personal estate");
            if (ImGui.Selectable("Free Company estate")) StartLifestream("fc", "Free Company estate");
            if (ImGui.Selectable("Market board")) StartLifestream("mb", "market board");
            if (ImGui.BeginMenu("Summoning bells"))
            {
                foreach (QuickPoint bell in SummoningBells)
                    if (ImGui.Selectable(bell.Name))
                        StartPoint($"Summoning bell — {bell.Name}", bell.TerritoryId, bell.Position, 4f);
                ImGui.EndMenu();
            }
            if (ImGui.Selectable("Triple Triad trader"))
                StartPoint("Triple Triad trader", 144, new(-56.1f, 1.6f, 16.6f), 4f);
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
            if (ImGui.Selectable("Review and shop for upgrades")) Open("Gear & Inventory");
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Repair gear"))
                maintenance.Start(NexusMaintenanceOperation.Repair, out message);
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Extract materia"))
                maintenance.Start(NexusMaintenanceOperation.ExtractMateria, out message);
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Desynthesize eligible items"))
                maintenance.Start(NexusMaintenanceOperation.Desynthesize, out message);
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
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Open coffers"))
            {
                ImGui.CloseCurrentPopup();
                maintenance.Start(NexusMaintenanceOperation.OpenCoffers, out message);
            }
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Grand Company turn-ins"))
            {
                ImGui.CloseCurrentPopup();
                maintenance.Start(NexusMaintenanceOperation.GrandCompanyTurnIn, out message);
            }
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Entrust eligible Armoire items"))
                maintenance.Start(NexusMaintenanceOperation.EntrustArmoire, out message);
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Entrust eligible Glamour Dresser items"))
                maintenance.Start(NexusMaintenanceOperation.EntrustGlamourChest, out message);
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Review protected selling"))
                Open("Gear & Inventory");
            if (ImGui.Selectable("Gear & Inventory page")) Open("Gear & Inventory");
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        CategoryButton("Duty", "NexusDuty");
        if (ImGui.BeginPopup("NexusDuty"))
        {
            ProgressionGoalState? goal = progression.State;
            if (goal is null || goal.Goal.Status is VieriNexus.Domain.GoalStatus.Satisfied or VieriNexus.Domain.GoalStatus.Cancelled)
            {
                if (ImGui.Selectable("Start configured level goal"))
                    message = control.ExecuteLocal("progression.start").Message;
            }
            else if (goal.Goal.Status is VieriNexus.Domain.GoalStatus.Paused or VieriNexus.Domain.GoalStatus.Blocked)
            {
                if (ImGui.Selectable("Resume with a fresh plan"))
                    message = control.ExecuteLocal("progression.resume").Message;
            }
            if (progressionActive && ImGui.Selectable("Finish current duty, then stop"))
                message = progression.StopAfterCurrentDuty().Message;
            if (ImGui.Selectable("Progression details")) Open("Progression");
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        CategoryButton("Extras", "NexusExtras");
        if (ImGui.BeginPopup("NexusExtras"))
        {
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Register Triple Triad cards"))
                maintenance.Start(NexusMaintenanceOperation.RegisterTripleTriadCards, out message);
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Register minions"))
                maintenance.Start(NexusMaintenanceOperation.RegisterMinions, out message);
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Register orchestrion rolls"))
                maintenance.Start(NexusMaintenanceOperation.RegisterOrchestrionRolls, out message);
            if (maintenance.HasWorkingProfile && ImGui.Selectable("Run all enabled maintenance"))
                maintenance.StartConfigured(out message);
            ImGui.Separator();
            if (ImGui.Selectable("Control Center")) Open("Overview");
            if (ImGui.Selectable("Nexus settings")) Open("Settings");
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

        VieriNexus.Contracts.NexusOperationsStatusDto operations = control.Status();
        string? status = anyActive
            ? operations.Detail ?? operations.Activity ?? message
            : string.IsNullOrWhiteSpace(message) ? null : message;
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

    private void StartLifestream(string command, string destination)
    {
        ImGui.CloseCurrentPopup();
        if (!lifestreamCommand.HasAction || !lifestreamBusy.HasFunction || !lifestreamAbort.HasAction)
        {
            message = "Lifestream is not loaded or does not expose the required travel contract.";
            return;
        }
        try
        {
            if (lifestreamBusy.InvokeFunc())
            {
                message = "Lifestream is already handling another trip.";
                return;
            }
            lifestreamCommand.InvokeAction(command);
            quickTravelRequested = true;
            quickTravelObservedBusy = false;
            quickTravelDestination = destination;
            quickTravelStartedAt = DateTimeOffset.UtcNow;
            message = $"Nexus asked Lifestream to travel to the {destination}.";
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Nexus could not start the {Destination} shortcut.", destination);
            message = $"Lifestream did not accept the {destination} shortcut.";
        }
    }

    private void UpdateQuickTravel()
    {
        if (!quickTravelRequested)
            return;
        try
        {
            bool busy = lifestreamBusy.HasFunction && lifestreamBusy.InvokeFunc();
            quickTravelObservedBusy |= busy;
            TimeSpan elapsed = DateTimeOffset.UtcNow - quickTravelStartedAt;
            if (!busy && (quickTravelObservedBusy || elapsed >= TimeSpan.FromSeconds(4)))
            {
                quickTravelRequested = false;
                message = $"Lifestream finished the {quickTravelDestination} travel request.";
            }
            else if (elapsed >= TimeSpan.FromMinutes(15))
            {
                StopQuickTravel();
                message = $"The {quickTravelDestination} travel request timed out and was stopped.";
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Nexus could not observe Lifestream quick travel.");
            quickTravelRequested = false;
            message = "Nexus could not verify the Lifestream travel request.";
        }
    }

    private bool StopQuickTravel()
    {
        if (!quickTravelRequested)
            return false;
        try
        {
            if (lifestreamAbort.HasAction)
                lifestreamAbort.InvokeAction();
        }
        catch (Exception exception)
        {
            Plugin.Log.Debug(exception, "Lifestream could not be aborted from the Nexus overlay.");
        }
        quickTravelRequested = false;
        return true;
    }

    private unsafe void StartGrandCompanyPoint(bool barracks)
    {
        byte grandCompany = PlayerState.Instance()->GrandCompany;
        QuickPoint destination = (grandCompany, barracks) switch
        {
            (1, false) => new("Maelstrom supply counter", 128, new(94.02183f, 40.27537f, 74.475525f)),
            (2, false) => new("Twin Adder supply counter", 132, new(-68.678566f, -0.5015295f, -8.470145f)),
            (_, false) => new("Immortal Flames supply counter", 130, new(-142.82619f, 4.0999994f, -106.31349f)),
            (1, true) => new("Maelstrom barracks entrance", 128, new(98.00867f, 41.275635f, 62.790894f)),
            (2, true) => new("Twin Adder barracks entrance", 132, new(-80.216736f, 0.47296143f, -7.0039062f)),
            _ => new("Immortal Flames barracks entrance", 130, new(-153.30743f, 5.2338257f, -98.039246f)),
        };
        StartPoint(destination.Name, destination.TerritoryId, destination.Position, barracks ? 2f : 3f);
    }

    private unsafe void StartFlagMarker()
    {
        ImGui.CloseCurrentPopup();
        AgentMap* map = AgentMap.Instance();
        if (map is null || map->FlagMarkerCount == 0)
        {
            message = "Set a flag marker on the map first.";
            return;
        }
        FlagMapMarker marker = map->FlagMapMarkers[0];
        if (marker.TerritoryId != Plugin.ClientState.TerritoryType)
        {
            message = "Travel to the flag marker's territory first, then choose Flag marker again.";
            return;
        }
        Vector3 approximate = new(marker.XFloat, 1024f, marker.YFloat);
        Vector3? floor = pointOnFloor.HasFunction ? pointOnFloor.InvokeFunc(approximate, false, 10f) : null;
        if (floor is null)
        {
            message = "vnavmesh could not locate walkable ground at the flag marker.";
            return;
        }
        StartPoint("Flag marker", marker.TerritoryId, floor.Value, 2f);
    }

    private void StartPoint(string name, uint territoryId, Vector3 position, float tolerance)
    {
        ImGui.CloseCurrentPopup();
        var route = new NavigationRouteSnapshot(
            Guid.NewGuid(), name, territoryId,
            [new(position.X, position.Y, position.Z)],
            "Temporary Nexus overlay destination.", "built-in, overlay",
            true, false, tolerance, tolerance, 0, 0, string.Empty, false, DateTime.UtcNow);
        NavigationRouteExecutionStatus started = navigation.Start(route, NavigationRoutePlanKind.Playback);
        message = started.Message;
    }

    private sealed record QuickPoint(string Name, uint TerritoryId, Vector3 Position);

}
