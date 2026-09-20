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
    private Vector2 lastPosition;
    private int priorLineCount = 1;
    private int currentLineCount = 1;
    private GearUpgradePreview? manualShoppingPlan;
    private readonly HashSet<int> manualShoppingSlots = [];
    private string manualShoppingMessage = string.Empty;
    private bool openManualShoppingPopup;

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
        if (!progressionActive && !progressionPaused)
        {
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
                if (ImGui.Selectable(VieriAutoDutyOverlayContract.ShopForUpgrades))
                    PrepareManualGearShopping();
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

        if (openManualShoppingPopup)
        {
            ImGui.OpenPopup("Shop For Upgrades###NexusManualGearShopping");
            openManualShoppingPopup = false;
        }
        DrawManualGearShoppingPopup();
    }

    private void PrepareManualGearShopping()
    {
        ImGui.CloseCurrentPopup();
        RefreshManualGearShopping();
        openManualShoppingPopup = true;
    }

    private void RefreshManualGearShopping()
    {
        manualShoppingSlots.Clear();
        manualShoppingPlan = null;
        if (progressionProviders.TryGetGearUpgradePreview(out GearUpgradePreview? preview, out string result))
        {
            manualShoppingPlan = preview;
            if (preview is not null)
            {
                foreach (GearUpgradeSlot slot in preview.Slots)
                {
                    if (slot.Recommended && !slot.ActiveExperienceBonus && slot.Replacement is not null)
                        manualShoppingSlots.Add(slot.SlotKey);
                }
            }
        }
        manualShoppingMessage = result;
    }

    private void DrawManualGearShoppingPopup()
    {
        if (!ImGui.IsPopupOpen("Shop For Upgrades###NexusManualGearShopping"))
            return;

        ImGui.SetNextWindowSize(new Vector2(650, 540), ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Vector2(520, 400), new Vector2(float.MaxValue, float.MaxValue));
        if (!ImGui.BeginPopupModal("Shop For Upgrades###NexusManualGearShopping"))
            return;

        if (manualShoppingPlan is not { } preview)
        {
            ImGui.TextWrapped(string.IsNullOrWhiteSpace(manualShoppingMessage)
                ? "Equipment information is unavailable."
                : manualShoppingMessage);
        }
        else if (!string.IsNullOrWhiteSpace(preview.UnavailableReason))
        {
            ImGui.TextWrapped(preview.UnavailableReason);
        }
        else
        {
            ImGui.TextUnformatted($"{preview.Job} — Level {preview.Level}");
            ImGui.TextDisabled($"Current vendor band: level {preview.VendorLevel}");
            ImGui.Spacing();
            ImGui.TextWrapped("Review the replacements below, then select the upgrades you want. Only the displayed items may be purchased; unavailable items will not be substituted. Active EXP items stay protected.");
            ImGui.Spacing();

            if (ImGui.Button("Recommended"))
            {
                manualShoppingSlots.Clear();
                foreach (GearUpgradeSlot slot in preview.Slots)
                {
                    if (slot.Recommended && !slot.ActiveExperienceBonus && slot.Replacement is not null)
                        manualShoppingSlots.Add(slot.SlotKey);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Select All"))
            {
                manualShoppingSlots.Clear();
                foreach (GearUpgradeSlot slot in preview.Slots)
                {
                    if (!slot.ActiveExperienceBonus && slot.Replacement is not null)
                        manualShoppingSlots.Add(slot.SlotKey);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
                manualShoppingSlots.Clear();
            ImGui.SameLine();
            if (ImGui.Button("Refresh"))
                RefreshManualGearShopping();

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
            GearShoppingApprovalResult approval = GearShoppingApprovalPolicy.Build(
                preview,
                manualShoppingSlots,
                metrics.Gil,
                minimumGilReserve);
            ImGui.TextWrapped(approval.Success
                ? $"Selected purchase estimate: {approval.EstimatedCost:N0} gil. Keep at least {minimumGilReserve:N0} gil."
                : approval.Message);

            ImGui.Separator();
            float slotListHeight = MathF.Max(140f, ImGui.GetContentRegionAvail().Y - 72f);
            if (ImGui.BeginChild("NexusManualGearShoppingSlots", new Vector2(0, slotListHeight), true))
            {
                const ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
                if (ImGui.BeginTable("NexusManualGearShoppingSlotTable", 2, tableFlags))
                {
                    ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 145f);
                    ImGui.TableSetupColumn("Current equipment / replacement", ImGuiTableColumnFlags.WidthStretch);
                    foreach (GearUpgradeSlot slot in preview.Slots)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        bool selected = manualShoppingSlots.Contains(slot.SlotKey);
                        bool selectable = !slot.ActiveExperienceBonus && slot.Replacement is not null;
                        if (!selectable)
                            ImGui.BeginDisabled();
                        if (ImGui.Checkbox($"{slot.Name}##NexusManualGearSlot{slot.SlotKey}", ref selected))
                        {
                            if (selected)
                                manualShoppingSlots.Add(slot.SlotKey);
                            else
                                manualShoppingSlots.Remove(slot.SlotKey);
                        }
                        if (!selectable)
                            ImGui.EndDisabled();

                        ImGui.TableNextColumn();
                        ImGui.TextWrapped($"Current: {slot.CurrentEquipment}");
                        if (slot.ActiveExperienceBonus)
                            ImGui.TextColored(new Vector4(1f, .82f, .25f, 1f), "Protected EXP item");
                        else if (slot.Replacement is { } replacement)
                        {
                            if (slot.Recommended)
                                ImGui.TextColored(new Vector4(.3f, .9f, .4f, 1f), "Upgrade recommended");
                            ImGui.TextWrapped($"Replacement: {replacement.Name} (iLvl {replacement.ItemLevel}, requires level {replacement.EquipLevel})");
                            ImGui.TextWrapped(replacement.Quantity > 0
                                ? $"Buy {replacement.Quantity} × {replacement.UnitPrice:N0} gil each — {replacement.Vendor}"
                                : "Already owned — no purchase needed; the equipment pass will check it.");
                        }
                        else
                        {
                            ImGui.TextWrapped("No verified gil-vendor upgrade in this vendor band.");
                        }
                    }
                    ImGui.EndTable();
                }
            }
            ImGui.EndChild();

            if (!string.IsNullOrWhiteSpace(manualShoppingMessage))
                ImGui.TextWrapped(manualShoppingMessage);

            bool canStart = approval.Success && approval.Approval is not null && !gear.Status.IsActive &&
                            progressionProviders.IsGearShoppingExecutionReady;
            if (!canStart)
                ImGui.BeginDisabled();
            if (ImGui.Button("Start Shopping") && approval.Approval is not null)
            {
                ProgressionActionResult started = gear.Start(approval.Approval);
                manualShoppingMessage = started.Message;
                message = started.Message;
                if (started.Success)
                    ImGui.CloseCurrentPopup();
            }
            if (!canStart)
                ImGui.EndDisabled();
            ImGui.SameLine();
        }

        if (ImGui.Button("Cancel"))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
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
