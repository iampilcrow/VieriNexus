using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Ipc;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using VieriNexus.Application;
using VieriNexus.Services;

namespace VieriNexus.UI;

internal sealed class NexusOperationsOverlay : Window
{
    private static readonly IReadOnlyDictionary<uint, (string Name, Vector3 Position)> SummoningBells =
        new Dictionary<uint, (string, Vector3)>
        {
            [129] = ("Limsa Lominsa Lower Decks", new(-123.88806f, 17.990356f, 21.469421f)),
            [133] = ("Old Gridania", new(171.00781f, 15.487854f, -101.487854f)),
            [131] = ("Ul'dah — Steps of Thal", new(148.91272f, 3.982544f, -44.205383f)),
            [419] = ("The Pillars", new(-151.1712f, -12.64978f, -11.7647705f)),
            [635] = ("Rhalgr's Reach", new(-57.63336f, -0.015319824f, 49.30188f)),
            [628] = ("Kugane", new(19.394226f, 4.043579f, 53.025024f)),
            [759] = ("The Doman Enclave", new(60.56299f, -0.015319824f, -3.982666f)),
            [819] = ("The Crystarium", new(-69.840576f, -7.7058716f, 123.49121f)),
            [820] = ("Eulmore", new(7.1869507f, 83.17688f, 31.448853f)),
            [962] = ("Old Sharlayan", new(42.09961f, 2.517002f, -39.414062f)),
            [963] = ("Radz-at-Han", new(26.749023f, -0.015319824f, -53.696533f)),
            [1185] = ("Tuliyollal", new(18.57019f, -14.023071f, 120.408936f)),
            [1186] = ("Solution Nine", new(-151.59845f, 0.59503174f, -15.304871f)),
        };
    private readonly Plugin plugin;
    private readonly NavigationRouteRuntimeService navigation;
    private readonly MapClickNavigationService mapClickNavigation;
    private readonly GearShoppingRuntimeService gear;
    private readonly ProgressionRuntimeService progression;
    private readonly NexusMaintenanceRuntimeService maintenance;
    private readonly StrikingDummyTravelService strikingDummies;
    private readonly NexusControlService control;
    private readonly Action<string> openPage;
    private readonly ICallGateSubscriber<string, object> lifestreamCommand;
    private readonly ICallGateSubscriber<int?, object> enqueueInnShortcut;
    private readonly ICallGateSubscriber<bool> lifestreamBusy;
    private readonly ICallGateSubscriber<object> lifestreamAbort;
    private string message = string.Empty;
    private bool quickTravelRequested;
    private bool quickTravelObservedBusy;
    private string quickTravelDestination = string.Empty;
    private DateTimeOffset quickTravelStartedAt;
    private Vector2 lastPosition;
    private int priorLineCount = 1;
    private int currentLineCount = 1;

    internal NexusOperationsOverlay(
        Plugin plugin,
        NavigationRouteRuntimeService navigation,
        MapClickNavigationService mapClickNavigation,
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
        this.mapClickNavigation = mapClickNavigation;
        this.gear = gear;
        this.progression = progression;
        this.maintenance = maintenance;
        this.strikingDummies = strikingDummies;
        this.control = control;
        this.openPage = openPage;
        lifestreamCommand = Plugin.PluginInterface.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand");
        enqueueInnShortcut = Plugin.PluginInterface.GetIpcSubscriber<int?, object>("Lifestream.EnqueueInnShortcut");
        lifestreamBusy = Plugin.PluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        lifestreamAbort = Plugin.PluginInterface.GetIpcSubscriber<object>("Lifestream.Abort");
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public override bool DrawConditions() => plugin.Configuration.ShowOperationsOverlay &&
        (!plugin.Configuration.HideOperationsOverlayWhenStopped || IsAnyOperationActive());

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize;
        if (plugin.Configuration.LockOperationsOverlay)
            Flags |= ImGuiWindowFlags.NoMove;
        if (plugin.Configuration.OperationsOverlayTransparent)
            Flags |= ImGuiWindowFlags.NoBackground;
        int heightDifference = currentLineCount - priorLineCount;
        if (plugin.Configuration.OperationsOverlayAnchorBottom && heightDifference != 0)
        {
            Position ??= lastPosition;
            Position -= new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * heightDifference);
        }
        else
        {
            Position = null;
        }
        priorLineCount = currentLineCount;
    }

    public override void Draw()
    {
        lastPosition = ImGui.GetWindowPos();
        UpdateQuickTravel();
        bool navigationActive = navigation.Status.State is not NavigationRouteExecutionState.Idle and
            not NavigationRouteExecutionState.Completed and not NavigationRouteExecutionState.Failed;
        bool gearActive = gear.Status.IsActive;
        bool progressionActive = progression.State?.Goal.Status == VieriNexus.Domain.GoalStatus.Active;
        bool progressionPaused = progression.State?.Goal.Status == VieriNexus.Domain.GoalStatus.Paused;
        bool maintenanceActive = maintenance.Status.IsActive;
        bool dummyTravelActive = strikingDummies.Status.IsActive;
        AutoDutyOverlayPreferences? overlay = maintenance.CurrentProfile?.Overlay;
        bool anyActive = navigationActive || gearActive || progressionActive || progressionPaused || maintenanceActive ||
                         dummyTravelActive || quickTravelRequested;
        currentLineCount = plugin.Configuration.ShowOperationsStatus &&
                           anyActive ? 2 : 1;

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
            ImGui.SameLine(0, 5);
        }

        if (progressionActive)
        {
            if (ImGui.Button("Pause"))
                message = progression.PauseNow().Message;
            ImGui.SameLine(0, 5);
            bool lastRunArmed = progression.State?.StopAfterCurrentDuty == true;
            if (lastRunArmed)
                ImGui.BeginDisabled();
            if (ImGui.Button(lastRunArmed ? "Last Run Set" : "Last Run"))
                message = progression.StopAfterCurrentDuty().Message;
            if (lastRunArmed)
                ImGui.EndDisabled();
            ImGui.SameLine(0, 5);
        }
        else if (progressionPaused)
        {
            if (ImGui.Button("Resume"))
                message = progression.Resume().Message;
            ImGui.SameLine(0, 5);
        }

        bool controlsEnabled = !anyActive;
        CategoryButton("Goto", "NexusGoto", controlsEnabled && overlay?.ShowGoto != false);
        if (ImGui.BeginPopup("NexusGoto"))
        {
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.Barracks)) StartGrandCompanyPoint(barracks: true);
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.Inn)) StartInn();
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.GrandCompanySupply)) StartGrandCompanyPoint(barracks: false);
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.FlagMarker)) StartFlagMarker();
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.SummoningBell)) StartPreferredSummoningBell();
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.Apartment)) StartLifestream("apartment", "apartment");
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.PersonalHome)) StartLifestream("home", "personal home");
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.FreeCompanyEstate)) StartLifestream("fc", "Free Company estate");
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.TripleTriadTrader))
                StartPoint("Triple Triad trader", 144, new(-56.1f, 1.6f, 16.6f), 4f);
            if (ImGui.BeginMenu(VieriAutoDutyOverlayContract.StrikingDummies))
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
        ImGui.SameLine(0, 5);

        CategoryButton("Gear", "NexusGear", controlsEnabled);
        if (ImGui.BeginPopup("NexusGear"))
        {
            if (ImGui.Selectable(VieriAutoDutyOverlayContract.ShopForUpgrades)) Open("Gear & Inventory");
            if (Selectable(VieriAutoDutyOverlayContract.Equip, overlay?.ShowGear != false))
            {
                ImGui.CloseCurrentPopup();
                message = Plugin.CommandManager.ProcessCommand("/ad autoequip")
                    ? "AutoDuty is equipping its recommended gear."
                    : "AutoDuty is not ready to equip recommended gear.";
            }
            if (Selectable(VieriAutoDutyOverlayContract.Repair, maintenance.HasWorkingProfile && overlay?.ShowRepair != false))
                maintenance.Start(NexusMaintenanceOperation.Repair, out message);
            if (Selectable(VieriAutoDutyOverlayContract.ExtractMateria, maintenance.HasWorkingProfile && overlay?.ShowExtract != false))
                maintenance.Start(NexusMaintenanceOperation.ExtractMateria, out message);
            if (Selectable(VieriAutoDutyOverlayContract.Desynth, maintenance.HasWorkingProfile && overlay?.ShowDesynth != false))
                maintenance.Start(NexusMaintenanceOperation.Desynthesize, out message);
            ImGui.EndPopup();
        }
        ImGui.SameLine(0, 5);

        CategoryButton("Inventory", "NexusInventory", controlsEnabled);
        if (ImGui.BeginPopup("NexusInventory"))
        {
            if (Selectable(VieriAutoDutyOverlayContract.SellInventory, maintenance.HasWorkingProfile && overlay?.ShowSell != false))
            {
                ImGui.CloseCurrentPopup();
                maintenance.StartProtectedSelling(out message);
            }
            if (Selectable(VieriAutoDutyOverlayContract.TurnIn, maintenance.HasWorkingProfile && overlay?.ShowTurnIn != false))
            {
                ImGui.CloseCurrentPopup();
                maintenance.Start(NexusMaintenanceOperation.GrandCompanyTurnIn, out message);
            }
            if (Selectable(VieriAutoDutyOverlayContract.Coffers, maintenance.HasWorkingProfile && overlay?.ShowCoffers != false))
            {
                ImGui.CloseCurrentPopup();
                maintenance.Start(NexusMaintenanceOperation.OpenCoffers, out message);
            }
            if (Selectable(VieriAutoDutyOverlayContract.Armoire, maintenance.HasWorkingProfile))
                maintenance.Start(NexusMaintenanceOperation.EntrustArmoire, out message);
            ImGui.EndPopup();
        }

        ImGui.SameLine(0, 5);
        CategoryButton("Extras", "NexusExtras", controlsEnabled);
        if (ImGui.BeginPopup("NexusExtras"))
        {
            bool tripleTriadEnabled = maintenance.HasWorkingProfile && overlay?.ShowTripleTriad != false;
            if (!tripleTriadEnabled)
                ImGui.BeginDisabled();
            if (ImGui.BeginMenu(VieriAutoDutyOverlayContract.TripleTriad))
            {
                if (ImGui.Selectable(VieriAutoDutyOverlayContract.RegisterTripleTriadCards))
                    maintenance.Start(NexusMaintenanceOperation.RegisterTripleTriadCards, out message);
                if (ImGui.Selectable(VieriAutoDutyOverlayContract.SellTripleTriadCards))
                {
                    ImGui.CloseCurrentPopup();
                    message = Plugin.CommandManager.ProcessCommand("/ad ttsell")
                        ? "AutoDuty is selling duplicate Triple Triad cards."
                        : "AutoDuty is not ready to sell Triple Triad cards.";
                }
                ImGui.EndMenu();
            }
            if (!tripleTriadEnabled)
                ImGui.EndDisabled();
            ImGui.EndPopup();
        }

        ImGui.SameLine(0, 5);
        using (Plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            if (ImGui.Button($"{FontAwesomeIcon.Cog.ToIconString()}###NexusOperationsSettings"))
                Open("Automation");
            ImGui.SameLine(0, 5);
            if (ImGui.Button($"{FontAwesomeIcon.WindowClose.ToIconString()}###CloseNexusOperations"))
            {
                plugin.Configuration.ShowOperationsOverlay = false;
                plugin.Save();
            }
        }

        if (!plugin.Configuration.ShowOperationsStatus)
            return;

        string? status = OverlayActionText(anyActive);
        if (!string.IsNullOrWhiteSpace(status))
        {
            ImGui.NewLine();
            ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), Truncate(status, 40));
        }
    }

    private void Open(string page)
    {
        ImGui.CloseCurrentPopup();
        openPage(page);
    }

    private static void CategoryButton(string label, string popup, bool enabled)
    {
        if (!enabled)
            ImGui.BeginDisabled();
        if (ImGui.Button(label))
            ImGui.OpenPopup(popup);
        if (!enabled)
            ImGui.EndDisabled();
    }

    private static bool Selectable(string label, bool enabled)
    {
        if (!enabled)
            ImGui.BeginDisabled();
        bool selected = ImGui.Selectable(label);
        if (!enabled)
            ImGui.EndDisabled();
        return selected && enabled;
    }

    private string? OverlayActionText(bool anyActive)
    {
        if (!anyActive)
            return null;
        if (maintenance.Status.IsActive)
            return maintenance.Status.Message;
        if (gear.Status.IsActive)
            return gear.Status.Message;
        if (strikingDummies.Status.IsActive)
            return strikingDummies.Status.Message;
        if (quickTravelRequested)
            return $"Traveling to {quickTravelDestination}.";
        if (navigation.Status.State is not NavigationRouteExecutionState.Idle and
            not NavigationRouteExecutionState.Completed and not NavigationRouteExecutionState.Failed)
            return navigation.Status.Message;
        if (progression.State?.Goal.Status is VieriNexus.Domain.GoalStatus.Active or VieriNexus.Domain.GoalStatus.Paused)
            return progression.State.Goal.Status == VieriNexus.Domain.GoalStatus.Paused
                ? "Automation paused."
                : "Running automation.";
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : $"{value[..(maximum - 3)]}...";

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

    private void StartInn()
    {
        ImGui.CloseCurrentPopup();
        if (!enqueueInnShortcut.HasAction || !lifestreamBusy.HasFunction || !lifestreamAbort.HasAction)
        {
            message = "Lifestream is not loaded or does not expose its inn travel contract.";
            return;
        }
        try
        {
            if (lifestreamBusy.InvokeFunc())
            {
                message = "Lifestream is already handling another trip.";
                return;
            }
            // A null selection deliberately means the current character's Grand Company,
            // matching AutoDuty's proven Goto Inn behavior (including Twin Adder/Gridania).
            enqueueInnShortcut.InvokeAction(null);
            quickTravelRequested = true;
            quickTravelObservedBusy = false;
            quickTravelDestination = "Grand Company inn";
            quickTravelStartedAt = DateTimeOffset.UtcNow;
            message = "Nexus asked Lifestream to travel to your Grand Company inn.";
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Nexus could not start Grand Company inn travel.");
            message = "Lifestream did not accept the Grand Company inn request.";
        }
    }

    private void StartPreferredSummoningBell()
    {
        uint destination = maintenance.CurrentProfile?.PreferredSummoningBell ?? 0;
        switch (destination)
        {
            case 0:
                StartInn();
                return;
            case 1:
                StartLifestream("apartment", "apartment summoning bell");
                return;
            case 2:
                StartLifestream("home", "personal-home summoning bell");
                return;
            case 3:
                StartLifestream("fc", "Free Company summoning bell");
                return;
        }

        if (SummoningBells.TryGetValue(destination, out var bell))
        {
            StartPoint($"{bell.Name} summoning bell", destination, bell.Position, 4f);
            return;
        }
        StartInn();
    }

    private bool IsAnyOperationActive() =>
        navigation.Status.State is not NavigationRouteExecutionState.Idle and
            not NavigationRouteExecutionState.Completed and not NavigationRouteExecutionState.Failed ||
        gear.Status.IsActive ||
        progression.State?.Goal.Status is VieriNexus.Domain.GoalStatus.Active or VieriNexus.Domain.GoalStatus.Paused ||
        maintenance.Status.IsActive || strikingDummies.Status.IsActive || quickTravelRequested;

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
                message = string.Empty;
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

    private void StartFlagMarker()
    {
        ImGui.CloseCurrentPopup();
        mapClickNavigation.NavigateToCurrentFlag(out message);
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
