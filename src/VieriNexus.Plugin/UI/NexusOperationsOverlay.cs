using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Plugin.Ipc;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
    private readonly ProgressionProviderService progressionProviders;
    private readonly GearShoppingRuntimeService gear;
    private readonly ProgressionRuntimeService progression;
    private readonly NexusMaintenanceRuntimeService maintenance;
    private readonly StrikingDummyTravelService strikingDummies;
    private readonly NexusControlService control;
    private readonly WorldStateStore world;
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
    private GrandCompanyOverlayDestination? barracksDestination;
    private DateTimeOffset barracksStartedAt;
    private DateTimeOffset nextBarracksActionAt;
    private Vector2 lastPosition;
    private int priorLineCount = 1;
    private int currentLineCount = 1;

    internal NexusOperationsOverlay(
        Plugin plugin,
        NavigationRouteRuntimeService navigation,
        MapClickNavigationService mapClickNavigation,
        ProgressionProviderService progressionProviders,
        GearShoppingRuntimeService gear,
        ProgressionRuntimeService progression,
        NexusMaintenanceRuntimeService maintenance,
        StrikingDummyTravelService strikingDummies,
        NexusControlService control,
        WorldStateStore world,
        Action<string> openPage)
        : base("Nexus Operations###VieriNexusOperations",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        this.navigation = navigation;
        this.mapClickNavigation = mapClickNavigation;
        this.progressionProviders = progressionProviders;
        this.gear = gear;
        this.progression = progression;
        this.maintenance = maintenance;
        this.strikingDummies = strikingDummies;
        this.control = control;
        this.world = world;
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
        bool navigationActive = navigation.Status.State is not NavigationRouteExecutionState.Idle and
            not NavigationRouteExecutionState.Completed and not NavigationRouteExecutionState.Failed;
        bool gearActive = gear.Status.IsActive;
        bool progressionActive = progression.State?.Goal.Status == VieriNexus.Domain.GoalStatus.Active;
        bool progressionPaused = progression.State?.Goal.Status == VieriNexus.Domain.GoalStatus.Paused;
        bool maintenanceActive = maintenance.Status.IsActive;
        bool dummyTravelActive = strikingDummies.Status.IsActive;
        AutoDutyOverlayPreferences? overlay = maintenance.CurrentProfile?.Overlay;
        AutoDutyMaintenancePolicy? maintenancePolicy = maintenance.CurrentProfile?.Maintenance;
        VieriAutoDutyOverlayButtonState buttonState = VieriAutoDutyOverlayButtonPolicy.Evaluate(
            overlay, maintenancePolicy, maintenance.HasWorkingProfile);
        bool anyActive = navigationActive || gearActive || progressionActive || progressionPaused || maintenanceActive ||
                         dummyTravelActive || quickTravelRequested || barracksDestination is not null;
        string? dutyStatus = plugin.Configuration.ShowOperationsDutyStatus
            ? OverlayDutyText()
            : null;
        string? actionStatus = plugin.Configuration.ShowOperationsActionStatus
            ? OverlayActionText(anyActive)
            : null;
        currentLineCount = 1 + (string.IsNullOrWhiteSpace(dutyStatus) ? 0 : 1) +
                           (string.IsNullOrWhiteSpace(actionStatus) ? 0 : 1);

        if (anyActive)
        {
            if (ImGui.Button("Stop"))
            {
                bool quickStopped = StopQuickTravel();
                bool barracksStopped = StopBarracksTravel();
                VieriNexus.Contracts.NexusCommandResultDto stopped = control.StopAll();
                message = (quickStopped || barracksStopped) && !stopped.Accepted
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
        if (!progressionActive && !progressionPaused)
        {
            CategoryButton("Goto", "NexusGoto", controlsEnabled && buttonState.Goto);
            if (ImGui.BeginPopup("NexusGoto"))
            {
                if (ImGui.Selectable(VieriAutoDutyOverlayContract.Barracks)) StartBarracks();
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
                if (ImGui.Selectable(VieriAutoDutyOverlayContract.ShopForUpgrades))
                    StartRecommendedGearShopping();
                if (Selectable(VieriAutoDutyOverlayContract.Equip, buttonState.Equip))
                {
                    ImGui.CloseCurrentPopup();
                    message = Plugin.CommandManager.ProcessCommand("/ad autoequip")
                        ? "AutoDuty is equipping its recommended gear."
                        : "AutoDuty is not ready to equip recommended gear.";
                }
                if (Selectable(VieriAutoDutyOverlayContract.Repair, buttonState.Repair))
                    maintenance.Start(NexusMaintenanceOperation.Repair, out message);
                if (Selectable(VieriAutoDutyOverlayContract.ExtractMateria, buttonState.Extract))
                    maintenance.Start(NexusMaintenanceOperation.ExtractMateria, out message);
                if (Selectable(VieriAutoDutyOverlayContract.Desynth, buttonState.Desynth))
                    maintenance.Start(NexusMaintenanceOperation.Desynthesize, out message);
                ImGui.EndPopup();
            }
            ImGui.SameLine(0, 5);

            CategoryButton("Inventory", "NexusInventory", controlsEnabled);
            if (ImGui.BeginPopup("NexusInventory"))
            {
                if (Selectable(VieriAutoDutyOverlayContract.SellInventory, buttonState.Sell))
                {
                    ImGui.CloseCurrentPopup();
                    maintenance.StartProtectedSelling(out message);
                }
                if (Selectable(VieriAutoDutyOverlayContract.TurnIn, buttonState.TurnIn))
                {
                    ImGui.CloseCurrentPopup();
                    maintenance.Start(NexusMaintenanceOperation.GrandCompanyTurnIn, out message);
                }
                if (Selectable(VieriAutoDutyOverlayContract.Coffers, buttonState.Coffers))
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
                bool tripleTriadEnabled = buttonState.TripleTriad;
                if (!tripleTriadEnabled)
                    ImGui.BeginDisabled();
                if (ImGui.BeginMenu(VieriAutoDutyOverlayContract.TripleTriad))
                {
                    if (ImGui.Selectable(VieriAutoDutyOverlayContract.RegisterTripleTriadCards))
                        maintenance.Start(NexusMaintenanceOperation.RegisterTripleTriadCards, out message);
                    if (ImGui.Selectable(VieriAutoDutyOverlayContract.SellTripleTriadCards))
                    {
                        ImGui.CloseCurrentPopup();
                        maintenance.Start(NexusMaintenanceOperation.SellTripleTriadCards, out message);
                    }
                    ImGui.EndMenu();
                }
                if (!tripleTriadEnabled)
                    ImGui.EndDisabled();
                ImGui.EndPopup();
            }
            ImGui.SameLine(0, 5);
        }

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

        if (!string.IsNullOrWhiteSpace(dutyStatus))
        {
            ImGui.NewLine();
            ImGui.TextColored(new Vector4(93 / 255f, 226 / 255f, 231 / 255f, 1f), Truncate(dutyStatus, 40));
        }
        if (!string.IsNullOrWhiteSpace(actionStatus))
        {
            ImGui.NewLine();
            ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), Truncate(actionStatus, 40));
        }

    }

    private void StartRecommendedGearShopping()
    {
        ImGui.CloseCurrentPopup();
        if (!progressionProviders.TryGetGearUpgradePreview(out GearUpgradePreview? preview, out string result) ||
            preview is null)
        {
            message = result;
            return;
        }
        AutoDutyProfileSnapshot? operationsProfile = maintenance.CurrentProfile;
        int minimumGilReserve = operationsProfile is not null
            ? checked((int)Math.Min(operationsProfile.Maintenance.MinimumGilReserve, int.MaxValue))
            : 0;
        if (operationsProfile is null &&
            world.Current.Character.Value is { Key.IsKnown: true } currentCharacter)
        {
            minimumGilReserve = plugin.Configuration
                .ForCharacter(currentCharacter.Key.ToString())
                .Progression.MinimumGilReserve;
        }

        ProgressionCharacterMetrics metrics = progressionProviders.CharacterMetrics();
        GearReadinessDecision decision = GearReadinessDecisionPolicy.Build(
            preview,
            metrics.Gil,
            minimumGilReserve);
        if (!decision.Success || !decision.RequiresShopping || decision.Approval is null)
        {
            message = decision.Message;
            return;
        }

        ProgressionActionResult started = gear.Start(decision.Approval);
        message = started.Message;
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
        if (barracksDestination is not null)
            return message;
        if (navigation.Status.State is not NavigationRouteExecutionState.Idle and
            not NavigationRouteExecutionState.Completed and not NavigationRouteExecutionState.Failed)
            return navigation.Status.Message;
        if (progression.State is { } state &&
            state.Goal.Status is VieriNexus.Domain.GoalStatus.Active or VieriNexus.Domain.GoalStatus.Paused)
        {
            if (state.Goal.Status == VieriNexus.Domain.GoalStatus.Paused)
                return "Automation paused.";
            return state.ActiveTask?.Kind.Value switch
            {
                "vieri.gear.ensure-readiness/v1" => "Checking and equipping gear.",
                "vieri.inventory.run-between-duty-maintenance/v1" => "Running inventory maintenance.",
                "vieri.quest.run-one/v1" => "Completing the current quest.",
                "vieri.hunting-log.complete-target/v1" => "Completing the current Hunting Log target.",
                "vieri.duties.run-one/v1" => "Running the current duty.",
                _ => "Preparing the next automation step.",
            };
        }
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private string? OverlayDutyText()
    {
        VieriNexus.Application.ProgressionGoalState? state = progression.State;
        if (state?.ActiveTask is not { Kind.Value: "vieri.duties.run-one/v1" } task)
            return null;
        try
        {
            VieriNexus.Application.ProgressionDutyTaskPayload? payload =
                System.Text.Json.JsonSerializer.Deserialize<VieriNexus.Application.ProgressionDutyTaskPayload>(task.PayloadJson);
            if (payload is null)
                return null;
            string suffix = state.StopAfterCurrentDuty ? " · Last Run" : string.Empty;
            return $"{payload.DutyName}{suffix}";
        }
        catch (System.Text.Json.JsonException)
        {
            return "Current duty";
        }
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

    private unsafe void StartInn()
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
            PlayerState* playerState = PlayerState.Instance();
            byte grandCompany = playerState is null ? (byte)0 : playerState->GrandCompany;
            GrandCompanyOverlayDestination destination =
                VieriAutoDutyGrandCompanyContract.Resolve(grandCompany);
            enqueueInnShortcut.InvokeAction(destination.InnShortcutIndex);
            quickTravelRequested = true;
            quickTravelObservedBusy = false;
            quickTravelDestination = $"{destination.CompanyName} inn";
            quickTravelStartedAt = DateTimeOffset.UtcNow;
            message = $"Traveling to the {destination.CompanyName} inn.";
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
        maintenance.Status.IsActive || strikingDummies.Status.IsActive || quickTravelRequested ||
        barracksDestination is not null;

    internal void Update(DateTimeOffset now)
    {
        UpdateQuickTravel();
        UpdateBarracksTravel(now);
    }

    internal void Shutdown()
    {
        StopQuickTravel();
        StopBarracksTravel();
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

    private unsafe void StartBarracks()
    {
        ImGui.CloseCurrentPopup();
        PlayerState* playerState = PlayerState.Instance();
        byte grandCompany = playerState is null ? (byte)0 : playerState->GrandCompany;
        GrandCompanyOverlayDestination destination =
            VieriAutoDutyGrandCompanyContract.Resolve(grandCompany);
        if (Plugin.ClientState.TerritoryType == destination.BarracksTerritoryId)
        {
            message = $"Already inside the {destination.CompanyName} barracks.";
            return;
        }

        var route = new NavigationRouteSnapshot(
            Guid.NewGuid(), $"{destination.CompanyName} barracks", destination.HeadquartersTerritoryId,
            [new(destination.BarracksX, destination.BarracksY, destination.BarracksZ)],
            "Exact VieriAutoDuty Grand Company barracks entrance.", "built-in, overlay, vieriautoduty",
            true, false, 0.25f, 2f, 0, 0, string.Empty, false, DateTime.UtcNow);
        NavigationRouteExecutionStatus started = navigation.Start(route, NavigationRoutePlanKind.Playback);
        message = started.Message;
        if (started.State is NavigationRouteExecutionState.Running)
        {
            barracksDestination = destination;
            barracksStartedAt = DateTimeOffset.UtcNow;
            nextBarracksActionAt = DateTimeOffset.UtcNow;
        }
    }

    private unsafe void UpdateBarracksTravel(DateTimeOffset now)
    {
        if (barracksDestination is not { } destination)
            return;
        if (Plugin.ClientState.TerritoryType == destination.BarracksTerritoryId)
        {
            barracksDestination = null;
            message = string.Empty;
            return;
        }
        if (now - barracksStartedAt >= TimeSpan.FromMinutes(10))
        {
            navigation.Stop();
            barracksDestination = null;
            message = $"Travel to the {destination.CompanyName} barracks timed out and was stopped.";
            return;
        }
        if (Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
            Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
            return;

        NavigationRouteExecutionStatus status = navigation.Status;
        if (status.IsActive)
        {
            message = $"Traveling to the {destination.CompanyName} barracks.";
            return;
        }
        if (status.State is NavigationRouteExecutionState.Failed or NavigationRouteExecutionState.Blocked or
            NavigationRouteExecutionState.AwaitingAcknowledgement)
        {
            barracksDestination = null;
            message = status.Message;
            return;
        }
        if (Plugin.ClientState.TerritoryType != destination.HeadquartersTerritoryId)
        {
            barracksDestination = null;
            message = $"Nexus stopped because it did not reach the {destination.CompanyName} barracks entrance.";
            return;
        }

        nint confirmationAddress = Plugin.GameGui.GetAddonByName("SelectYesno", 1);
        AddonSelectYesno* confirmation = (AddonSelectYesno*)confirmationAddress;
        if (confirmation is not null && confirmation->AtkUnitBase.IsReady && confirmation->AtkUnitBase.IsVisible)
        {
            ClickButton(confirmation->YesButton, (AtkUnitBase*)confirmation);
            message = $"Entering the {destination.CompanyName} barracks.";
            nextBarracksActionAt = now.AddMilliseconds(750);
            return;
        }
        if (now < nextBarracksActionAt)
            return;

        IGameObject? door = Plugin.ObjectTable
            .Where(candidate => candidate.BaseId == destination.BarracksDoorDataId)
            .OrderBy(candidate => Plugin.ObjectTable.LocalPlayer is { } player
                ? Vector3.DistanceSquared(player.Position, candidate.Position)
                : float.MaxValue)
            .FirstOrDefault();
        if (door is not { IsTargetable: true } || Plugin.ObjectTable.LocalPlayer is not { } localPlayer ||
            Vector3.Distance(localPlayer.Position, door.Position) > 4f)
        {
            message = $"Waiting for the {destination.CompanyName} barracks entrance.";
            nextBarracksActionAt = now.AddSeconds(1);
            return;
        }

        TargetSystem* targets = TargetSystem.Instance();
        if (targets is not null)
            targets->InteractWithObject((GameObject*)door.Address, false);
        message = $"Entering the {destination.CompanyName} barracks.";
        nextBarracksActionAt = now.AddSeconds(1);
    }

    private bool StopBarracksTravel()
    {
        if (barracksDestination is null)
            return false;
        navigation.Stop();
        barracksDestination = null;
        return true;
    }

    private static unsafe void ClickButton(AtkComponentButton* button, AtkUnitBase* addon)
    {
        if (button is null || !button->IsEnabled)
            return;
        AtkComponentNode* node = button->AtkComponentBase.OwnerNode;
        if (node is null)
            return;
        AtkEvent* evt = (AtkEvent*)node->AtkEventManager.Event;
        if (evt is not null)
            addon->ReceiveEvent(evt->State.EventType, checked((int)evt->Param), evt);
    }

    private unsafe void StartGrandCompanyPoint(bool barracks)
    {
        byte grandCompany = PlayerState.Instance()->GrandCompany;
        GrandCompanyOverlayDestination company = VieriAutoDutyGrandCompanyContract.Resolve(grandCompany);
        QuickPoint destination = barracks
            ? new($"{company.CompanyName} barracks entrance", company.HeadquartersTerritoryId,
                new(company.BarracksX, company.BarracksY, company.BarracksZ))
            : new($"{company.CompanyName} supply counter", company.HeadquartersTerritoryId,
                new(company.SupplyX, company.SupplyY, company.SupplyZ));
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
