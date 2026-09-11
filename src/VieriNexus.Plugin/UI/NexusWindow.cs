using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using VieriNexus.Application;
using VieriNexus.Domain;
using VieriNexus.Services;

namespace VieriNexus.UI;

internal sealed class NexusWindow : Window
{
    private static readonly Stack<bool> AutoPanelTableStack = new();
    private static readonly (string Group, string Id, string Label)[] Navigation =
    [
        ("OVERVIEW", "Home", "Home"),
        ("OVERVIEW", "Overview", "Control Center"),
        ("OVERVIEW", "Automation", "Automation"),
        ("OVERVIEW", "Progression", "Progression"),
        ("OVERVIEW", "Queue", "Queue"),
        ("MODULES", "Combat", "Combat"),
        ("MODULES", "Gear & Inventory", "Gear"),
        ("MODULES", "Routes & Navigation", "Routes"),
        ("MODULES", "Market", "Market"),
        ("MODULES", "Custom UI", "Custom UI"),
        ("MODULES", "Communications", "Communications"),
        ("SETUP", "Dependencies", "Dependencies"),
        ("SETUP", "Migration", "Migration"),
        ("SETUP", "Settings", "Settings"),
    ];

    private readonly Plugin plugin;
    private readonly DependencyService dependencies;
    private readonly LegacyConfigurationInventory legacyInventory;
    private readonly NavigationMigrationService navigationMigration;
    private readonly AutoDutyMigrationService autoDutyMigration;
    private readonly NavigationLibraryService navigationLibrary;
    private readonly NavigationActivationService navigationActivation;
    private readonly NavigationDiagnosticsService navigationDiagnostics;
    private readonly NavigationRouteRuntimeService navigationRuntime;
    private readonly ProgressionProviderService progressionProviders;
    private readonly ProgressionRuntimeService progressionRuntime;
    private readonly GearShoppingRuntimeService gearShoppingRuntime;
    private readonly NexusMaintenanceRuntimeService maintenanceRuntime;
    private readonly ModuleRegistry modules;
    private readonly WorldStateStore world;
    private readonly ISharedImmediateTexture logo;
    private string routeSearch = string.Empty;
    private Guid? selectedRouteId;
    private Guid? editingRouteId;
    private string routeNameEdit = string.Empty;
    private string routeTagsEdit = string.Empty;
    private string routeNotesEdit = string.Empty;
    private string routeOperationMessage = string.Empty;
    private int selectedRoutePoint = -1;
    private Guid? pendingDeleteRouteId;
    private Guid? pendingClearRouteId;
    private int selectedBuiltInRoute;
    private string progressionMessage = string.Empty;
    private GearUpgradePreview? gearUpgradePreview;
    private readonly HashSet<int> selectedGearUpgradeSlots = [];
    private string gearShoppingMessage = string.Empty;
    private string maintenanceMessage = string.Empty;
    private string migrationQuickStartMessage = string.Empty;

    internal NexusWindow(
        Plugin plugin,
        DependencyService dependencies,
        LegacyConfigurationInventory legacyInventory,
        NavigationMigrationService navigationMigration,
        AutoDutyMigrationService autoDutyMigration,
        NavigationLibraryService navigationLibrary,
        NavigationActivationService navigationActivation,
        NavigationDiagnosticsService navigationDiagnostics,
        NavigationRouteRuntimeService navigationRuntime,
        ProgressionProviderService progressionProviders,
        ProgressionRuntimeService progressionRuntime,
        GearShoppingRuntimeService gearShoppingRuntime,
        NexusMaintenanceRuntimeService maintenanceRuntime,
        ModuleRegistry modules,
        WorldStateStore world,
        ISharedImmediateTexture logo)
        : base("Vieri Nexus###VieriNexusMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.dependencies = dependencies;
        this.legacyInventory = legacyInventory;
        this.navigationMigration = navigationMigration;
        this.autoDutyMigration = autoDutyMigration;
        this.navigationLibrary = navigationLibrary;
        this.navigationActivation = navigationActivation;
        this.navigationDiagnostics = navigationDiagnostics;
        this.navigationRuntime = navigationRuntime;
        this.progressionProviders = progressionProviders;
        this.progressionRuntime = progressionRuntime;
        this.gearShoppingRuntime = gearShoppingRuntime;
        this.maintenanceRuntime = maintenanceRuntime;
        this.modules = modules;
        this.world = world;
        this.logo = logo;
        Size = new Vector2(1220, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(940, 580),
            MaximumSize = new Vector2(2200, 1400),
        };
    }

    public override void PreDraw()
    {
        NexusTheme.Push();
        var setupLocked = !plugin.Configuration.FirstRunComplete || !dependencies.RequiredReady;
        if (setupLocked && plugin.Configuration.SelectedPage is not ("Dependencies" or "Migration" or "Settings"))
            plugin.Configuration.SelectedPage = "Dependencies";
    }

    public override void PostDraw() => NexusTheme.Pop();

    public override void Draw()
    {
        ImGui.SetWindowFontScale(plugin.Configuration.UiScale);
        var navWidth = plugin.Configuration.CompactNavigation ? 72f : 205f;
        if (ImGui.BeginChild("###NexusNavigation", new Vector2(navWidth, 0), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            DrawNavigation(navWidth);
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild("###NexusContent", Vector2.Zero, true))
            DrawPage();
        ImGui.EndChild();
    }

    private void DrawNavigation(float width)
    {
        string? lastGroup = null;
        foreach (var item in Navigation)
        {
            if (!string.Equals(lastGroup, item.Group, StringComparison.Ordinal))
            {
                if (lastGroup != null)
                    ImGui.Spacing();
                if (!plugin.Configuration.CompactNavigation)
                    ImGui.TextColored(NexusTheme.Muted, item.Group);
                ImGui.Separator();
                lastGroup = item.Group;
            }

            var selected = plugin.Configuration.SelectedPage == item.Id;
            var setupLocked = !plugin.Configuration.FirstRunComplete || !dependencies.RequiredReady;
            var allowed = !setupLocked || item.Id is "Dependencies" or "Migration" or "Settings";
            if (!allowed)
                ImGui.BeginDisabled();
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.42f, .07f, .09f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.52f, .09f, .11f, 1f));
            }

            var label = plugin.Configuration.CompactNavigation ? item.Label[..1] : item.Label;
            if (ImGui.Button($"{label}##nav-{item.Id}", new Vector2(width - 18, 34)) && allowed)
            {
                plugin.Configuration.SelectedPage = item.Id;
                plugin.Save();
            }

            if (selected)
                ImGui.PopStyleColor(2);
            if (!allowed)
                ImGui.EndDisabled();
            if (plugin.Configuration.CompactNavigation && ImGui.IsItemHovered())
                ImGui.SetTooltip(item.Label);
        }
    }

    private void DrawPage()
    {
        switch (plugin.Configuration.SelectedPage)
        {
            case "Home": DrawHome(); break;
            case "Overview": DrawOverview(); break;
            case "Progression": DrawProgression(); break;
            case "Gear & Inventory": DrawGearAndInventory(); break;
            case "Routes & Navigation": DrawRoutesAndNavigation(); break;
            case "Dependencies": DrawDependencies(); break;
            case "Migration": DrawMigration(); break;
            case "Settings": DrawSettings(); break;
            default: DrawModulePage(plugin.Configuration.SelectedPage); break;
        }
    }

    private void DrawHome()
    {
        var available = ImGui.GetContentRegionAvail();
        var heroHeight = Math.Min(470f, Math.Max(360f, available.Y * .64f));
        if (ImGui.BeginChild("###NexusHomeHero", new Vector2(0, heroHeight), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var draw = ImGui.GetWindowDrawList();
            var position = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            draw.AddRectFilledMultiColor(position, position + size,
                0xFF08090C, 0xFF100A0D, 0xFF19070A, 0xFF08090C);
            draw.AddRect(position, position + size, ImGui.GetColorU32(NexusTheme.Red), 8f,
                ImDrawFlags.None, 1.5f);

            var wrap = logo.GetWrapOrEmpty();
            var imageWidth = Math.Clamp(size.X * .56f, 390f, 620f);
            var imageSize = new Vector2(imageWidth, imageWidth / 1.5f);
            ImGui.SetCursorPosX((size.X - imageSize.X) * .5f);
            ImGui.SetCursorPosY(Math.Max(18f, (size.Y - imageSize.Y - 54f) * .42f));
            ImGui.Image(wrap.Handle, imageSize);

            var state = dependencies.RequiredReady
                ? "All required services are ready"
                : "Dependency setup requires attention";
            ImGui.SetCursorPosX((size.X - ImGui.CalcTextSize(state).X) * .5f);
            NexusTheme.StatusDot(dependencies.RequiredReady ? NexusTheme.Green : NexusTheme.Amber, state);
            var subtitle = "One home for the complete Vieri experience";
            ImGui.SetCursorPosX((size.X - ImGui.CalcTextSize(subtitle).X) * .5f);
            ImGui.TextColored(NexusTheme.Gold, subtitle);
        }
        ImGui.EndChild();

        ImGui.Spacing();
        if (ImGui.BeginTable("###HomeStatus", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            StatusCard("ROUTES", "Live", "Author, travel, play, and stop", NexusTheme.Green);
            ImGui.TableNextColumn();
            StatusCard("GEAR", "Approval live", "Preview exact upgrades before shopping", NexusTheme.Green);
            ImGui.TableNextColumn();
            StatusCard("PROGRESSION", "Duty lane live", "One verified run at a time", NexusTheme.Green);
            ImGui.EndTable();
        }
    }

    private void DrawOverview()
    {
        PageHeading("Control Center", "One place to understand, direct, and safely stop every Nexus activity.");

        if (ImGui.BeginTable("###OverviewStatus", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            ProgressionGoalState? progression = progressionRuntime.State;
            StatusCard("AUTOMATION",
                progression?.Goal.Status.ToString() ?? "Idle",
                progression?.Goal.Title ?? "No active level goal",
                progression?.Goal.Status == GoalStatus.Active ? NexusTheme.Green : NexusTheme.Muted);
            ImGui.TableNextColumn();
            StatusCard("DEPENDENCIES", dependencies.RequiredReady ? "Ready" : "Attention", "Required services", dependencies.RequiredReady ? NexusTheme.Green : NexusTheme.Amber);
            ImGui.TableNextColumn();
            var snapshot = world.Current;
            var character = snapshot.Character.Value;
            StatusCard("CHARACTER", character?.Name ?? "Waiting", character is null ? "No character snapshot" : $"Level {character.Level} • Job {character.ClassJobId}", character is null ? NexusTheme.Muted : NexusTheme.Cyan);
            ImGui.EndTable();
        }

        ImGui.Spacing();
        NexusTheme.SectionTitle("Modules", "Nine current products, migrating behind one control plane");
        DrawModuleGrid();

        ImGui.Spacing();
        if (ImGui.BeginTable("###OverviewBottom", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            BeginPanel("CURRENT ACTIVITY");
            NexusTheme.StatusDot(NexusTheme.Green, "Nexus foundation running");
            ImGui.TextWrapped("Routes are live. Gear & Inventory owns exact shopping approval. Progression owns a durable level goal and one verified duty at a time.");
            EndPanel();
            ImGui.TableNextColumn();
            BeginPanel("SAFETY STATE");
            ImGui.TextUnformatted("Route control: Explicit Stop");
            ImGui.TextUnformatted("Gear shopping: Exact single-use approval");
            ImGui.TextUnformatted("Progression execution: Bounded duty lane");
            ImGui.TextUnformatted("Provider selection: Capability checked");
            ImGui.TextColored(NexusTheme.Green, "Nexus owns goal scheduling; the current duty provider owns only its bounded run.");
            EndPanel();
            ImGui.EndTable();
        }
    }

    private void DrawDependencies()
    {
        PageHeading("Dependencies", "Required services unlock Nexus. Recommended integrations add optional automation and convenience features.");
        var statuses = dependencies.Snapshot();
        var required = statuses.Where(x => x.Definition.Required).ToArray();
        var recommended = statuses.Where(x => !x.Definition.Required).ToArray();
        var readyCount = required.Count(x => x.IsReady);
        ImGui.ProgressBar((float)readyCount / required.Length, new Vector2(-1, 22),
            $"{readyCount} of {required.Length} required services ready");
        ImGui.TextDisabled("Migration-source Vieri products are not global dependencies. Progression checks Vieri and stock provider contracts separately without requiring both implementations at once.");
        ImGui.Spacing();

        NexusTheme.SectionTitle("Required", "Core navigation, travel, quest, duty, and market providers");
        DrawDependencyGrid(required);

        ImGui.Spacing();
        NexusTheme.SectionTitle("Recommended", "Optional integrations discovered across every current Vieri product");
        DrawDependencyGrid(recommended);

        var allReady = required.All(x => x.IsReady);
        ImGui.Spacing();
        if (!allReady)
            ImGui.BeginDisabled();
        if (ImGui.Button("Continue to Vieri Nexus", new Vector2(230, 38)) && allReady)
        {
            plugin.Configuration.FirstRunComplete = true;
            plugin.Configuration.SelectedPage = "Home";
            plugin.Save();
        }
        if (!allReady)
            ImGui.EndDisabled();
        if (!allReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Install and enable every required dependency first.");
    }

    private void DrawDependencyGrid(IReadOnlyList<DependencyStatus> statuses)
    {
        if (!ImGui.BeginTable($"###DependencyGrid-{(statuses.FirstOrDefault()?.Definition.Required == true ? "required" : "recommended")}",
                2, ImGuiTableFlags.SizingStretchSame))
            return;

        foreach (var status in statuses)
        {
            ImGui.TableNextColumn();
            BeginPanel(status.Definition.DisplayName.ToUpperInvariant(), 132);
            var color = status.Health switch
            {
                DependencyHealth.Healthy => NexusTheme.Green,
                DependencyHealth.Disabled => NexusTheme.Amber,
                _ => NexusTheme.Red,
            };
            NexusTheme.StatusDot(color, status.Health.ToString());
            ImGui.SameLine();
            ImGui.TextColored(NexusTheme.Gold, status.Definition.Capability);
            ImGui.TextWrapped(status.Definition.Description);
            if (!string.IsNullOrWhiteSpace(status.Version))
                ImGui.TextDisabled($"Version {status.Version}");
            var action = status.Health switch
            {
                DependencyHealth.Missing => "Install",
                DependencyHealth.Disabled => "Enable",
                _ => "Manage",
            };
            if (ImGui.Button($"{action}##dependency-{status.Definition.Id}", new Vector2(112, 0)))
                dependencies.OpenInstaller(status);
            if (status.Health == DependencyHealth.Missing && !string.IsNullOrWhiteSpace(status.Definition.RepositoryUrl))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("External repository");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(status.Definition.RepositoryUrl);
            }
            EndPanel();
        }

        ImGui.EndTable();
    }

    private void DrawMigration()
    {
        PageHeading("Migration", "Protect every setting while each product moves into Nexus.");
        ImGui.TextWrapped("Discovery is read-only. A supported import first creates a timestamped source backup, validates into staging, then commits Nexus-owned data atomically. Existing plugins remain installed and authoritative.");
        ImGui.Spacing();

        DrawMigrationQuickStart();

        foreach (var source in legacyInventory.Scan())
        {
            if (source.Id == "navplotter")
            {
                DrawNavigationMigration();
                continue;
            }
            if (source.Id == "autoduty")
            {
                DrawAutoDutyMigration();
                continue;
            }
            BeginPanel(source.DisplayName.ToUpperInvariant());
            NexusTheme.StatusDot(source.Found ? NexusTheme.Green : NexusTheme.Muted,
                source.Found ? "Configuration located" : "Not found on this computer");
            ImGui.TextColored(NexusTheme.Gold, $"Destination: {source.Destination}");
            ImGui.TextDisabled(source.Found
                ? source.ContainsProtectedValues
                    ? "Protected values detected by file presence only • contents have not been opened"
                    : "Preserved in place • import adapter pending validation"
                : "A different user will be detected from their own local settings.");
            EndPanel();
        }

        ImGui.Spacing();
        BeginPanel("CREDENTIAL SAFETY");
        ImGui.TextColored(NexusTheme.Green, "Discord credentials and channel identifiers have not been touched.");
        ImGui.TextWrapped("The Communications migration will copy encrypted values transactionally on the same computer, verify them, and retain the original VieriLink configuration as rollback data. Each user imports only their own local configuration.");
        EndPanel();
    }

    private void DrawMigrationQuickStart()
    {
        NavigationMigrationStatus navigationStatus = navigationMigration.Status();
        AutoDutyMigrationStatus operationsStatus = autoDutyMigration.Status();
        bool navigationReady = navigationStatus.Preview?.CanImport == true;
        bool operationsReady = operationsStatus.Preview?.CanImport == true;
        bool navigationPrepared = navigationStatus.LastReceipt is not null && navigationLibrary.HasWorkingLibrary;
        bool operationsPrepared = operationsStatus.LastReceipt is not null && autoDutyMigration.HasWorkingProfiles;
        bool needsNavigation = navigationReady && !navigationPrepared;
        bool needsOperations = operationsReady && !operationsPrepared;
        bool blocked = (navigationStatus.SourceFound && !navigationReady && !navigationPrepared) ||
                       (operationsStatus.SourceFound && !operationsReady && !operationsPrepared);
        bool foundAnything = navigationStatus.SourceFound || operationsStatus.SourceFound ||
                             navigationPrepared || operationsPrepared;

        BeginAutoPanel("SET UP THIS COMPUTER");
        if (blocked && !needsNavigation && !needsOperations)
        {
            NexusTheme.StatusDot(NexusTheme.Red,
                "A detected settings source needs attention in its detailed Migration card");
        }
        else if (!needsNavigation && !needsOperations)
        {
            NexusTheme.StatusDot(foundAnything ? NexusTheme.Green : NexusTheme.Amber,
                foundAnything
                    ? "Every currently supported Vieri settings source on this computer is prepared"
                    : "No currently supported Vieri settings source was detected on this computer");
        }
        else
        {
            NexusTheme.StatusDot(NexusTheme.Cyan,
                $"Detected {(needsNavigation ? 1 : 0) + (needsOperations ? 1 : 0)} supported settings source(s) ready to prepare");
            TextWrapped(NexusTheme.Muted,
                "This uses only this player's local Dalamud settings, keeps timestamped backups, verifies the imported data, and creates Nexus working copies. Leave the old Vieri products installed until their replacement cards are green.");
            if (ImGui.Button("Back up and prepare detected settings", new Vector2(ButtonWidth("Back up and prepare detected settings"), 0)))
            {
                List<string> results = [];
                if (needsNavigation)
                {
                    if (navigationStatus.LastReceipt is not null)
                    {
                        results.Add(navigationLibrary.CreateWorkingCopy().Message);
                    }
                    else
                    {
                        MigrationWriteResult imported = navigationMigration.Import();
                        results.Add(imported.Message);
                        if (imported.Success && imported.Receipt is { } receipt)
                        {
                            LegacyImportState state = plugin.Configuration.ForLegacyImport("navplotter");
                            state.Reviewed = state.Imported = true;
                            state.SourceVersion = "routes-v1";
                            state.ImportedAt = receipt.CreatedAtUtc;
                            state.ReceiptId = receipt.Id;
                            state.ImportedItemCount = navigationStatus.Preview!.Snapshot!.Routes.Count;
                            if (!navigationLibrary.HasWorkingLibrary)
                                results.Add(navigationLibrary.CreateWorkingCopy().Message);
                        }
                    }
                }
                if (needsOperations)
                {
                    MigrationWriteResult imported = autoDutyMigration.Import();
                    results.Add(imported.Message);
                    if (imported.Success && imported.Receipt is { } receipt)
                    {
                        LegacyImportState state = plugin.Configuration.ForLegacyImport("autoduty");
                        state.Reviewed = state.Imported = true;
                        state.SourceVersion = "operations-v1";
                        state.ImportedAt = receipt.CreatedAtUtc;
                        state.ReceiptId = receipt.Id;
                        state.ImportedItemCount = operationsStatus.Preview!.Snapshot!.Profiles.Count;
                    }
                }
                plugin.Save();
                plugin.ApplyPendingOperationsProfile();
                migrationQuickStartMessage = string.Join(" ", results);
            }
        }
        if (!string.IsNullOrWhiteSpace(migrationQuickStartMessage))
            TextWrapped(NexusTheme.Green, migrationQuickStartMessage);
        EndAutoPanel();
    }

    private void DrawAutoDutyMigration()
    {
        const string importLabel = "Create backup and import operations";
        const string rollbackLabel = "Rollback operations import";
        AutoDutyMigrationStatus status = autoDutyMigration.Status();
        LegacyImportState state = plugin.Configuration.ForLegacyImport("autoduty");
        bool staged = status.LastReceipt is not null;
        string operationMessage = status.Message;

        BeginPanel("DUTIES, GEAR & INVENTORY");
        NexusTheme.StatusDot(status.SourceFound ? NexusTheme.Green : NexusTheme.Muted,
            status.SourceFound ? "VieriAutoDuty configuration located" : "Not found on this computer");
        ImGui.TextColored(NexusTheme.Gold, "Destination: Nexus Operations and stock-compatible Duties");
        if (status.Preview?.Snapshot is { } snapshot)
        {
            int assignments = snapshot.Profiles.Sum(profile => profile.CharacterIds.Count);
            ImGui.TextUnformatted($"{snapshot.Profiles.Count} profile(s) • {assignments} character assignment(s) • {snapshot.RetiredEquipmentTransfersJson.Count} pending retired-item transfer(s)");
            TextWrapped(NexusTheme.Muted,
                "Overlay layout, gear, repair, extraction, coffers, desynthesis, Grand Company turn-ins, selling, registration, thresholds, preferred vendors, and safe in-duty maintenance rules are mapped together.");
            foreach (MigrationIssue issue in status.Preview.Issues.Take(2))
            {
                Vector4 color = issue.Severity == MigrationIssueSeverity.Error ? NexusTheme.Red :
                    issue.Severity == MigrationIssueSeverity.Warning ? NexusTheme.Amber : NexusTheme.Muted;
                TextWrapped(color, $"• {issue.Message}");
            }
        }
        else
        {
            TextWrapped(NexusTheme.Muted, status.Message);
        }

        float availableButtonWidth = ImGui.GetContentRegionAvail().X;
        float importButtonWidth = ButtonWidth(importLabel);
        float rollbackButtonWidth = ButtonWidth(rollbackLabel);
        bool canImport = status.Preview?.CanImport == true;
        if (!canImport)
            ImGui.BeginDisabled();
        if (ImGui.Button(importLabel, new Vector2(importButtonWidth, 0)) && canImport)
        {
            MigrationWriteResult result = autoDutyMigration.Import();
            operationMessage = result.Message;
            if (result.Success && result.Receipt is { } receipt)
            {
                staged = true;
                state.Reviewed = true;
                state.Imported = true;
                state.SourceVersion = "operations-v1";
                state.ImportedAt = receipt.CreatedAtUtc;
                state.ReceiptId = receipt.Id;
                state.ImportedItemCount = status.Preview!.Snapshot!.Profiles.Count;
                state.ReadyForActivation = false;
                state.Activated = false;
                plugin.Save();
                plugin.ApplyPendingOperationsProfile();
            }
        }
        if (!canImport)
            ImGui.EndDisabled();

        if (staged && status.LastReceipt is { } savedReceipt)
        {
            if (availableButtonWidth >= importButtonWidth + ImGui.GetStyle().ItemSpacing.X + rollbackButtonWidth)
                ImGui.SameLine();
            if (ImGui.Button(rollbackLabel, new Vector2(rollbackButtonWidth, 0)))
            {
                MigrationWriteResult result = autoDutyMigration.Rollback(savedReceipt.Id);
                operationMessage = result.Message;
                if (result.Success)
                {
                    staged = false;
                    state.Imported = false;
                    state.ImportedAt = null;
                    state.ReceiptId = null;
                    state.ImportedItemCount = 0;
                    state.ReadyForActivation = false;
                    state.Activated = false;
                    plugin.Save();
                }
            }
        }

        TextWrapped(staged ? NexusTheme.Green : NexusTheme.Muted, operationMessage);
        TextWrapped(NexusTheme.Muted, staged
            ? "Verified staging plus a separate Nexus working copy • source configuration remains unchanged • only native Nexus actions can use it"
            : "Import creates an exact source backup and does not enable duplicate automation.");
        EndPanel();
    }

    private void DrawRoutesAndNavigation()
    {
        PageHeading("Routes & Navigation", "Create, edit, and run your saved routes.");

        NavigationLibrarySnapshot? snapshot = navigationLibrary.Current;
        if (snapshot is null)
        {
            BeginPanel("ROUTE LIBRARY", 180);
            NexusTheme.StatusDot(NexusTheme.Muted, "No verified route library");
            ImGui.TextWrapped("Import VieriNavPlotter on the Migration page before Nexus can show its staged settings and personal routes here.");
            TextWrapped(NexusTheme.Muted, "VieriNavPlotter remains unchanged and authoritative.");
            if (ImGui.Button("Open Migration", new Vector2(ButtonWidth("Open Migration"), 0)))
            {
                plugin.Configuration.SelectedPage = "Migration";
                plugin.Save();
            }
            EndPanel();
            return;
        }

        if (!navigationLibrary.HasWorkingLibrary)
            DrawNavigationWorkingLibrary();
        DrawCompactNavigationStatus();
        ImGui.Spacing();

        if (ImGui.CollapsingHeader("Add a built-in vendor route"))
            DrawBuiltInVendorTemplates();

        if (snapshot.Routes.Count == 0)
        {
            BeginPanel("PERSONAL ROUTES", navigationLibrary.HasWorkingLibrary ? 205 : 150);
            ImGui.TextUnformatted("No personal routes have been created yet.");
            ImGui.TextWrapped("Your recording, display, pane, selection, tolerance, and navigation preferences are present in the library.");
            if (navigationLibrary.HasWorkingLibrary)
            {
                TextWrapped(NexusTheme.Muted,
                    "Create a route at your current position, move to the next desired waypoint, and add another point.");
                if (ImGui.Button("Create route at current position", new Vector2(ButtonWidth("Create route at current position"), 0)))
                    CreateRouteAtCurrentPosition();
                if (ImGui.Button("Import route JSON", new Vector2(ButtonWidth("Import route JSON"), 0)))
                {
                    NavigationLibraryWriteResult result = navigationLibrary.ImportRoute(ImGui.GetClipboardText());
                    routeOperationMessage = result.Message;
                    if (result.Success)
                    {
                        selectedRouteId = result.Snapshot?.SelectedRouteId;
                        selectedRoutePoint = -1;
                        editingRouteId = null;
                    }
                }
                DrawRouteOperationMessage();
            }
            else
            {
                TextWrapped(NexusTheme.Muted,
                    "Create a separate Nexus working copy before adding or changing routes.");
            }
            EndPanel();
            if (!navigationLibrary.HasWorkingLibrary)
                DrawStagedNavigationSettings(snapshot);
            return;
        }

        ImGui.SetNextItemWidth(Math.Min(420f, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint("###RouteSearch", "Search name, tags, notes, target, or territory", ref routeSearch, 256);
        if (navigationLibrary.HasWorkingLibrary &&
            ImGui.Button("Create route at current position", new Vector2(ButtonWidth("Create route at current position"), 0)))
        {
            CreateRouteAtCurrentPosition();
        }
        NavigationRouteRecordingStatus recordingStatus = navigationRuntime.RecordingStatus;
        if (recordingStatus.IsRecording)
        {
            ImGui.TextColored(NexusTheme.Green,
                $"● RECORDING • {recordingStatus.CapturedPointCount} captured this session");
        }
        IReadOnlyList<NavigationRouteSnapshot> filtered = NavigationLibraryQuery.Filter(snapshot, routeSearch);

        float routeTableWidth = ImGui.GetContentRegionAvail().X;
        float maximumRouteListWidth = Math.Max(240f, Math.Min(520f, routeTableWidth - 390f));
        if (!ImGui.BeginTable("###RouteLibrary", 2,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Routes", ImGuiTableColumnFlags.WidthFixed,
            Math.Clamp(snapshot.LibraryPaneWidth, 240f, maximumRouteListWidth));
        ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn();
        if (filtered.Count == 0)
        {
            ImGui.TextDisabled("No routes match this search.");
        }
        else
        {
            bool selectionVisible = selectedRouteId is { } currentId && filtered.Any(route => route.Id == currentId);
            if (!selectionVisible)
            {
                Guid? importedSelection = snapshot.SelectedRouteId;
                selectedRouteId = filtered.FirstOrDefault(route => route.Id == importedSelection)?.Id ?? filtered[0].Id;
            }

            foreach (NavigationRouteSnapshot route in filtered)
            {
                bool selected = selectedRouteId == route.Id;
                if (ImGui.Selectable($"{route.Name}##route-{route.Id:N}", selected))
                {
                    selectedRouteId = route.Id;
                    selectedRoutePoint = -1;
                    editingRouteId = null;
                    navigationLibrary.SelectRoute(route.Id);
                }
                ImGui.TextDisabled($"Territory {route.TerritoryId} • {route.Points.Count} point(s)");
                if (navigationRuntime.IsRecording(route.Id))
                    ImGui.TextColored(NexusTheme.Green, "● Timed recording active");
                if (route.OverrideEnabled)
                    ImGui.TextColored(NexusTheme.Amber, "Nexus gear-vendor override enabled");
                ImGui.Spacing();
            }
        }

        ImGui.TableNextColumn();
        NavigationRouteSnapshot? selectedRoute = selectedRouteId is { } id
            ? filtered.FirstOrDefault(route => route.Id == id)
            : null;
        DrawRouteDetails(selectedRoute, snapshot.ShowPointNumbers);
        ImGui.EndTable();

        if (ImGui.CollapsingHeader("Troubleshooting"))
            DrawNavigationDiagnostics();
    }

    private void DrawCompactNavigationStatus()
    {
        NavigationActivationAssessment assessment = navigationActivation.Assess();
        NavigationAuthorityStatus authority = navigationActivation.AuthorityStatus;
        if (authority.IsActive)
        {
            NexusTheme.StatusDot(NexusTheme.Green, "Ready — Nexus handles routes while VieriNavPlotter is off");
            return;
        }

        if (assessment.IsSourcePluginLoaded)
        {
            NexusTheme.StatusDot(NexusTheme.Amber, "Paused — disable VieriNavPlotter to use Nexus routes");
            return;
        }

        NexusTheme.StatusDot(NexusTheme.Amber, authority.Message);
    }

    private void DrawRouteDetails(NavigationRouteSnapshot? route, bool showPointNumbers)
    {
        if (route is null)
        {
            ImGui.TextDisabled("Select a route to review its staged details.");
        }
        else
        {
            PrepareRouteEditor(route);
            if (navigationLibrary.HasWorkingLibrary)
            {
                float saveNameWidth = ButtonWidth("Save name");
                ImGui.SetNextItemWidth(Math.Max(140f, ImGui.GetContentRegionAvail().X - saveNameWidth - ImGui.GetStyle().ItemSpacing.X));
                ImGui.InputText("###NexusRouteName", ref routeNameEdit, 120);
                ImGui.SameLine();
                if (ImGui.Button("Save name"))
                {
                    routeOperationMessage = navigationLibrary.UpdateRoute(route with
                    {
                        Name = routeNameEdit,
                        Tags = routeTagsEdit,
                        Notes = routeNotesEdit,
                    }).Message;
                }
            }
            else
            {
                ImGui.TextColored(NexusTheme.Gold, route.Name);
            }
            ImGui.Separator();
            ImGui.TextDisabled($"Territory {route.TerritoryId} • {route.Points.Count} point{(route.Points.Count == 1 ? string.Empty : "s")}");
            DrawRoutePlaybackControls(route, showPointNumbers);

            if (ImGui.CollapsingHeader("Route details & automation"))
            {
                if (navigationLibrary.HasWorkingLibrary)
                {
                    bool mesh = route.UseMesh;
                    bool flight = route.UseFlight;
                    float tolerance = route.Tolerance;
                    float finalTolerance = route.LastPointTolerance;
                    bool changed = ImGui.Checkbox("Mesh-assisted", ref mesh);
                    ImGui.SameLine();
                    changed |= ImGui.Checkbox("Allow flight", ref flight);
                    ImGui.SetNextItemWidth(180);
                    changed |= ImGui.SliderFloat("Route tolerance", ref tolerance, 0.1f, 20f, "%.2f");
                    ImGui.SetNextItemWidth(180);
                    changed |= ImGui.SliderFloat("Final tolerance", ref finalTolerance, 0.1f, 30f, "%.2f");
                    if (changed)
                    {
                        routeOperationMessage = navigationLibrary.UpdateRoute(route with
                        {
                            UseMesh = mesh,
                            UseFlight = flight,
                            Tolerance = tolerance,
                            LastPointTolerance = finalTolerance,
                        }).Message;
                    }
                }
                string binding = route.BindingKind == 1 ? "Gear vendor" : "None";
                ImGui.TextUnformatted($"Binding: {binding}");
                if (navigationLibrary.HasWorkingLibrary)
                {
                    int bindingKind = route.BindingKind;
                    ImGui.SetNextItemWidth(180);
                    if (ImGui.Combo("Use this route for", ref bindingKind, "No override\0Gear vendor\0"))
                        routeOperationMessage = navigationLibrary.SetBinding(route.Id, bindingKind).Message;
                    if (bindingKind == 1)
                    {
                        if (ImGui.Button("Bind current target"))
                        {
                            if (Plugin.TargetManager.Target is not { } target)
                            {
                                routeOperationMessage = "Target the vendor or object first.";
                            }
                            else
                            {
                                routeOperationMessage = navigationLibrary.BindCurrentTarget(
                                    route.Id,
                                    Plugin.ClientState.TerritoryType,
                                    target.BaseId,
                                    target.Name.ToString()).Message;
                            }
                        }
                        TextWrapped(NexusTheme.Muted,
                            "Changing the target turns its override off until you enable it again.");
                    }
                }
                if (route.TargetDataId != 0 || !string.IsNullOrWhiteSpace(route.TargetLabel))
                    ImGui.TextUnformatted($"Target: {route.TargetLabel} ({route.TargetDataId})");
                if (navigationLibrary.HasWorkingLibrary && route.BindingKind == 1 && route.TargetDataId != 0)
                {
                    bool enabled = route.OverrideEnabled;
                    if (ImGui.Checkbox("Use as gear vendor override", ref enabled))
                        routeOperationMessage = navigationLibrary.SetOverride(route.Id, enabled).Message;
                }
                if (navigationLibrary.HasWorkingLibrary)
                {
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputTextWithHint("###NexusRouteTags", "Tags", ref routeTagsEdit, 240);
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputTextMultiline("###NexusRouteNotes", ref routeNotesEdit, 600, new Vector2(-1, 46));
                    if (ImGui.Button("Save route details"))
                    {
                        NavigationLibraryWriteResult result = navigationLibrary.UpdateRoute(route with
                        {
                            Name = routeNameEdit,
                            Tags = routeTagsEdit,
                            Notes = routeNotesEdit,
                        });
                        routeOperationMessage = result.Message;
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(route.Tags))
                        ImGui.TextWrapped($"Tags: {route.Tags}");
                    if (!string.IsNullOrWhiteSpace(route.Notes))
                        ImGui.TextWrapped($"Notes: {route.Notes}");
                }
                ImGui.TextDisabled($"Updated {route.UpdatedAtUtc.ToLocalTime():g}");
            }

            if (navigationLibrary.HasWorkingLibrary &&
                ImGui.CollapsingHeader("Record & edit route", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool routeExecutionActive = navigationRuntime.Status.IsActive;
                bool anotherRouteRecording = navigationRuntime.RecordingStatus is
                { IsRecording: true, RouteId: { } recordingRouteId } && recordingRouteId != route.Id;
                bool thisRouteRecording = navigationRuntime.IsRecording(route.Id);
                if (routeExecutionActive || anotherRouteRecording)
                    ImGui.BeginDisabled();
                if (thisRouteRecording)
                {
                    if (ImGui.Button("Stop timed recording"))
                        routeOperationMessage = navigationRuntime.StopRecording().Message;
                }
                else if (ImGui.Button("Start timed recording"))
                {
                    routeOperationMessage = navigationRuntime.StartRecording(route, Environment.TickCount64).Message;
                    navigationRuntime.RefreshPreview(route.Id);
                }
                if (routeExecutionActive || anotherRouteRecording)
                    ImGui.EndDisabled();

                ImGui.Spacing();
                if (routeExecutionActive || thisRouteRecording)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Add current position"))
                {
                    AddCurrentPosition(route.Id);
                    navigationRuntime.RefreshPreview(route.Id);
                }
                if (ImGui.Button("Undo last point"))
                {
                    routeOperationMessage = navigationLibrary.RemoveLastPoint(route.Id).Message;
                    selectedRoutePoint = Math.Min(selectedRoutePoint, Math.Max(-1, route.Points.Count - 2));
                    navigationRuntime.RefreshPreview(route.Id);
                }
                if (ImGui.Button("Reverse points"))
                {
                    routeOperationMessage = navigationLibrary.Reverse(route.Id).Message;
                    selectedRoutePoint = selectedRoutePoint < 0
                        ? -1
                        : route.Points.Count - 1 - selectedRoutePoint;
                    navigationRuntime.RefreshPreview(route.Id);
                }
                if (routeExecutionActive || thisRouteRecording)
                    ImGui.EndDisabled();

                NavigationRouteRecordingStatus recording = navigationRuntime.RecordingStatus;
                if (thisRouteRecording || recording.RouteId == route.Id)
                    TextWrapped(thisRouteRecording ? NexusTheme.Green : NexusTheme.Muted, recording.Message);

                DrawInlineRecordingAndDisplaySettings();
            }
            DrawRouteOperationMessage();

            ImGui.Spacing();
            NexusTheme.SectionTitle(navigationLibrary.HasWorkingLibrary ? "Route points" : "Staged points");
            if (ImGui.BeginTable("###RoutePoints", 4,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableSetupColumn("#");
                ImGui.TableSetupColumn("X");
                ImGui.TableSetupColumn("Y");
                ImGui.TableSetupColumn("Z");
                ImGui.TableHeadersRow();
                for (int index = 0; index < route.Points.Count; index++)
                {
                    NavigationRoutePoint point = route.Points[index];
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (navigationLibrary.HasWorkingLibrary)
                    {
                        if (ImGui.Selectable($"{index + 1}##route-point-{route.Id:N}-{index}", selectedRoutePoint == index,
                                ImGuiSelectableFlags.SpanAllColumns))
                            selectedRoutePoint = index;
                    }
                    else
                    {
                        ImGui.TextUnformatted((index + 1).ToString());
                    }
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{point.X:0.###}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{point.Y:0.###}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{point.Z:0.###}");
                }
                ImGui.EndTable();
            }

            if (navigationLibrary.HasWorkingLibrary &&
                selectedRoutePoint >= 0 && selectedRoutePoint < route.Points.Count)
            {
                bool editingBlocked = navigationRuntime.Status.IsActive || navigationRuntime.IsRecording(route.Id);
                if (editingBlocked)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Replace selected with current"))
                {
                    ReplaceSelectedPoint(route.Id);
                    navigationRuntime.RefreshPreview(route.Id);
                }
                bool canMoveUp = selectedRoutePoint > 0;
                if (!canMoveUp)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Move up") && canMoveUp)
                {
                    NavigationLibraryWriteResult result = navigationLibrary.MovePoint(route.Id, selectedRoutePoint, -1);
                    routeOperationMessage = result.Message;
                    if (result.Success)
                        selectedRoutePoint--;
                    navigationRuntime.RefreshPreview(route.Id);
                }
                if (!canMoveUp)
                    ImGui.EndDisabled();
                ImGui.SameLine();
                bool canMoveDown = selectedRoutePoint < route.Points.Count - 1;
                if (!canMoveDown)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Move down") && canMoveDown)
                {
                    NavigationLibraryWriteResult result = navigationLibrary.MovePoint(route.Id, selectedRoutePoint, 1);
                    routeOperationMessage = result.Message;
                    if (result.Success)
                        selectedRoutePoint++;
                    navigationRuntime.RefreshPreview(route.Id);
                }
                if (!canMoveDown)
                    ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Remove selected point"))
                {
                    NavigationLibraryWriteResult result = navigationLibrary.RemovePoint(route.Id, selectedRoutePoint);
                    routeOperationMessage = result.Message;
                    if (result.Success)
                        selectedRoutePoint = Math.Min(selectedRoutePoint, route.Points.Count - 2);
                    navigationRuntime.RefreshPreview(route.Id);
                }
                if (editingBlocked)
                    ImGui.EndDisabled();
            }

            if (navigationLibrary.HasWorkingLibrary && ImGui.CollapsingHeader("More route actions"))
            {
                ImGui.Spacing();
                bool routeExecutionActive = navigationRuntime.Status.IsActive;
                bool routeRecordingActive = navigationRuntime.IsRecording(route.Id);
                if (routeExecutionActive || routeRecordingActive)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Duplicate route"))
                {
                    NavigationLibraryWriteResult result = navigationLibrary.DuplicateRoute(route.Id);
                    routeOperationMessage = result.Message;
                    selectedRouteId = result.Snapshot?.SelectedRouteId;
                    selectedRoutePoint = -1;
                    editingRouteId = null;
                }
                ImGui.SameLine();
                if (ImGui.Button("Copy route JSON"))
                {
                    string json = navigationLibrary.ExportRoute(route.Id);
                    if (string.IsNullOrWhiteSpace(json))
                        routeOperationMessage = "The selected route could not be exported.";
                    else
                    {
                        ImGui.SetClipboardText(json);
                        routeOperationMessage = $"Copied {route.Name} to the clipboard.";
                    }
                }
                if (ImGui.Button("Import route JSON"))
                {
                    NavigationLibraryWriteResult result = navigationLibrary.ImportRoute(ImGui.GetClipboardText());
                    routeOperationMessage = result.Message;
                    if (result.Success)
                    {
                        selectedRouteId = result.Snapshot?.SelectedRouteId;
                        selectedRoutePoint = -1;
                        editingRouteId = null;
                    }
                }
                if (ImGui.Button("Clear all points…"))
                {
                    pendingClearRouteId = route.Id;
                    ImGui.OpenPopup("Clear Nexus route points?###ClearNexusRoutePoints");
                }
                ImGui.SameLine();
                if (ImGui.Button("Delete route"))
                {
                    pendingDeleteRouteId = route.Id;
                    ImGui.OpenPopup("Delete Nexus route?###DeleteNexusRoute");
                }
                if (routeExecutionActive || routeRecordingActive)
                    ImGui.EndDisabled();
            }
        }

        // Popups must be drawn in the same ImGui ID scope as the buttons that open them.
        DrawDeleteRouteConfirmation();
        DrawClearPointsConfirmation();
    }

    private void DrawRoutePlaybackControls(NavigationRouteSnapshot route, bool showPointNumbers)
    {
        ImGui.Spacing();
        NavigationRouteExecutionStatus executionStatus = navigationRuntime.Status;
        if (executionStatus.CanStop)
        {
            if (ImGui.Button("Stop"))
                routeOperationMessage = navigationRuntime.Stop().Message;
        }
        else
        {
            NavigationRoutePlan playbackPlan = navigationRuntime.Plan(route, NavigationRoutePlanKind.Playback);
            bool canExecute = navigationLibrary.HasWorkingLibrary &&
                              !navigationRuntime.Status.IsActive &&
                              !navigationRuntime.RecordingStatus.IsRecording;
            bool playbackAvailable = playbackPlan.IsValid &&
                                     (playbackPlan.IsExecutable || navigationRuntime.CanDispatchCrossZone);
            if (!canExecute || !playbackAvailable)
                ImGui.BeginDisabled();
            if (ImGui.Button("Play route") && canExecute && playbackAvailable)
                routeOperationMessage = navigationRuntime.Start(route, NavigationRoutePlanKind.Playback).Message;
            if (!canExecute || !playbackAvailable)
                ImGui.EndDisabled();

            ImGui.SameLine();
            NavigationRoutePlan travelPlan = navigationRuntime.Plan(route, NavigationRoutePlanKind.TravelToStart);
            bool travelAvailable = travelPlan.IsValid &&
                                   (travelPlan.IsExecutable || navigationRuntime.CanDispatchCrossZone);
            if (!canExecute || !travelAvailable)
                ImGui.BeginDisabled();
            if (ImGui.Button("Travel to start") && canExecute && travelAvailable)
                routeOperationMessage = navigationRuntime.Start(route, NavigationRoutePlanKind.TravelToStart).Message;
            if (!canExecute || !travelAvailable)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button(navigationRuntime.IsPreviewing(route.Id) ? "Hide route" : "Show route"))
            {
                if (!navigationRuntime.IsPreviewing(route.Id) && !navigationLibrary.Current!.ShowWorldPreview)
                {
                    NavigationLibrarySnapshot preferences = navigationLibrary.Current!;
                    navigationLibrary.UpdatePreferences(
                        preferences.RecordingIntervalSeconds,
                        preferences.MinimumPointDistance,
                        showWorldPreview: true,
                        preferences.ShowPointNumbers,
                        preferences.ShowLiveNavigationPath);
                }
                routeOperationMessage = navigationRuntime.TogglePreview(route, showPointNumbers).Message;
            }
        }

        if (executionStatus.RouteId == route.Id && executionStatus.State != NavigationRouteExecutionState.Idle)
        {
            Vector4 executionColor = executionStatus.State switch
            {
                NavigationRouteExecutionState.Running => NexusTheme.Green,
                NavigationRouteExecutionState.Completed => NexusTheme.Green,
                NavigationRouteExecutionState.Blocked => NexusTheme.Amber,
                NavigationRouteExecutionState.AwaitingAcknowledgement => NexusTheme.Amber,
                _ => NexusTheme.Red,
            };
            TextWrapped(executionColor, executionStatus.Message);
        }
    }

    private void DrawInlineRecordingAndDisplaySettings()
    {
        if (navigationLibrary.Current is not { } snapshot)
            return;

        if (ImGui.CollapsingHeader("Recording options"))
        {
            float interval = snapshot.RecordingIntervalSeconds;
            float spacing = snapshot.MinimumPointDistance;
            bool changed = false;
            ImGui.SetNextItemWidth(220);
            changed |= ImGui.SliderFloat("Capture interval", ref interval, 0.2f, 5f, "%.1f sec");
            ImGui.SetNextItemWidth(220);
            changed |= ImGui.SliderFloat("Minimum point spacing", ref spacing, 0.1f, 10f, "%.1f y");
            if (changed)
                routeOperationMessage = navigationLibrary.UpdatePreferences(
                    interval, spacing, snapshot.ShowWorldPreview,
                    snapshot.ShowPointNumbers, snapshot.ShowLiveNavigationPath).Message;
        }

        if (ImGui.CollapsingHeader("Display options"))
        {
            bool worldPreview = snapshot.ShowWorldPreview;
            bool pointNumbers = snapshot.ShowPointNumbers;
            bool liveNavigationPath = snapshot.ShowLiveNavigationPath;
            bool changed = ImGui.Checkbox("Show saved route", ref worldPreview);
            changed |= ImGui.Checkbox("Show point numbers", ref pointNumbers);
            changed |= ImGui.Checkbox("Show live navigation path", ref liveNavigationPath);
            if (changed)
            {
                routeOperationMessage = navigationLibrary.UpdatePreferences(
                    snapshot.RecordingIntervalSeconds, snapshot.MinimumPointDistance,
                    worldPreview, pointNumbers, liveNavigationPath).Message;
                if (!worldPreview)
                    navigationRuntime.ClearPreview();
                else if (selectedRouteId is { } routeId)
                    navigationRuntime.RefreshPreview(routeId);
            }
        }
    }

    private void DrawBuiltInVendorTemplates()
    {
        IReadOnlyList<NavigationRouteSnapshot> routes = NavigationBuiltInRouteCatalog.Routes;
        selectedBuiltInRoute = Math.Clamp(selectedBuiltInRoute, 0, routes.Count - 1);
        NavigationRouteSnapshot selected = routes[selectedBuiltInRoute];

        float height = MathF.Ceiling(
            (ImGui.GetTextLineHeightWithSpacing() * 9f) +
            (ImGui.GetStyle().WindowPadding.Y * 2f) + 8f);
        BeginPanel("BUILT-IN VENDOR TEMPLATES", height);
        ImGui.TextWrapped("Verified VieriAutoDuty standing points are available as immutable templates. Add one to the personal library before editing or enabling its target override.");
        ImGui.SetNextItemWidth(Math.Min(520f, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##BuiltInVendorTemplate", selected.Name))
        {
            for (int index = 0; index < routes.Count; index++)
            {
                NavigationRouteSnapshot route = routes[index];
                bool active = index == selectedBuiltInRoute;
                if (ImGui.Selectable($"{route.Name}##built-in-{route.TargetDataId}", active))
                    selectedBuiltInRoute = index;
                if (active)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        selected = routes[selectedBuiltInRoute];
        ImGui.TextDisabled($"Territory {selected.TerritoryId} • target {selected.TargetDataId} • {selected.Points.Count} verified point(s)");
        if (!navigationLibrary.HasWorkingLibrary)
            ImGui.BeginDisabled();
        if (ImGui.Button("Add template to personal routes"))
        {
            NavigationLibraryWriteResult result = navigationLibrary.AddBuiltInTemplate(selected);
            routeOperationMessage = result.Message;
            if (result.Success)
            {
                selectedRouteId = result.Snapshot?.SelectedRouteId;
                selectedRoutePoint = -1;
                editingRouteId = null;
            }
        }
        if (!navigationLibrary.HasWorkingLibrary)
            ImGui.EndDisabled();
        TextWrapped(NexusTheme.Muted, navigationLibrary.HasWorkingLibrary
            ? "Added routes start with the override disabled."
            : "Create the Nexus working library first.");
        EndPanel();
    }

    private void DrawNavigationWorkingLibrary()
    {
        NavigationLibraryStatus status = navigationLibrary.Status;
        float height = navigationLibrary.HasWorkingLibrary ? 115 : 175;
        BeginPanel("NEXUS WORKING LIBRARY", height);
        NexusTheme.StatusDot(navigationLibrary.HasWorkingLibrary ? NexusTheme.Green : NexusTheme.Amber,
            navigationLibrary.HasWorkingLibrary ? "Editable Nexus copy active" : "Verified staging remains immutable");
        TextWrapped(NexusTheme.Muted, status.Message);
        if (!navigationLibrary.HasWorkingLibrary)
        {
            if (ImGui.Button("Create Nexus working copy", new Vector2(ButtonWidth("Create Nexus working copy"), 0)))
            {
                NavigationLibraryWriteResult result = navigationLibrary.CreateWorkingCopy();
                routeOperationMessage = result.Message;
            }
            ImGui.TextDisabled("Does not modify VieriNavPlotter or its migration receipt");
        }
        EndPanel();
    }

    private void PrepareRouteEditor(NavigationRouteSnapshot route)
    {
        if (editingRouteId == route.Id)
            return;
        editingRouteId = route.Id;
        selectedRoutePoint = -1;
        routeNameEdit = route.Name;
        routeTagsEdit = route.Tags;
        routeNotesEdit = route.Notes;
    }

    private void CreateRouteAtCurrentPosition()
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } player)
        {
            routeOperationMessage = "The current character position is unavailable.";
            return;
        }
        NavigationLibraryWriteResult result = navigationLibrary.CreateRoute(
            Plugin.ClientState.TerritoryType, player.Position);
        routeOperationMessage = result.Message;
        selectedRouteId = result.Snapshot?.SelectedRouteId;
        editingRouteId = null;
    }

    private void AddCurrentPosition(Guid routeId)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } player)
        {
            routeOperationMessage = "The current character position is unavailable.";
            return;
        }
        routeOperationMessage = navigationLibrary.AddCurrentPoint(
            routeId, Plugin.ClientState.TerritoryType, player.Position).Message;
    }

    private void ReplaceSelectedPoint(Guid routeId)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } player)
        {
            routeOperationMessage = "The current character position is unavailable.";
            return;
        }
        routeOperationMessage = navigationLibrary.ReplacePoint(
            routeId,
            selectedRoutePoint,
            Plugin.ClientState.TerritoryType,
            player.Position).Message;
    }

    private void DrawRouteOperationMessage()
    {
        if (!string.IsNullOrWhiteSpace(routeOperationMessage))
            TextWrapped(NexusTheme.Muted, routeOperationMessage);
    }

    private void DrawDeleteRouteConfirmation()
    {
        if (!ImGui.BeginPopupModal("Delete Nexus route?###DeleteNexusRoute", ImGuiWindowFlags.AlwaysAutoResize))
            return;

        NavigationRouteSnapshot? route = pendingDeleteRouteId is { } id
            ? navigationLibrary.Current?.Routes.FirstOrDefault(item => item.Id == id)
            : null;
        ImGui.TextUnformatted(route is null
            ? "The selected route no longer exists."
            : $"Delete '{route.Name}' from the Nexus working library?");
        ImGui.TextDisabled("The previous complete working-library file is retained on disk.");
        if (route is not null && ImGui.Button("Delete route"))
        {
            navigationRuntime.ClearPreview(route.Id);
            routeOperationMessage = navigationLibrary.DeleteRoute(route.Id).Message;
            selectedRouteId = navigationLibrary.Current?.SelectedRouteId;
            editingRouteId = null;
            pendingDeleteRouteId = null;
            ImGui.CloseCurrentPopup();
        }
        if (route is not null)
            ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            pendingDeleteRouteId = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawClearPointsConfirmation()
    {
        if (!ImGui.BeginPopupModal("Clear Nexus route points?###ClearNexusRoutePoints",
                ImGuiWindowFlags.AlwaysAutoResize))
            return;

        NavigationRouteSnapshot? route = pendingClearRouteId is { } id
            ? navigationLibrary.Current?.Routes.FirstOrDefault(item => item.Id == id)
            : null;
        ImGui.TextUnformatted(route is null
            ? "The selected route no longer exists."
            : $"Remove all {route.Points.Count} point(s) from '{route.Name}'?");
        ImGui.TextDisabled("The previous complete working-library file is retained on disk.");
        if (route is not null && ImGui.Button("Clear all points"))
        {
            routeOperationMessage = navigationLibrary.ClearPoints(route.Id).Message;
            navigationRuntime.RefreshPreview(route.Id);
            selectedRoutePoint = -1;
            pendingClearRouteId = null;
            ImGui.CloseCurrentPopup();
        }
        if (route is not null)
            ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            pendingClearRouteId = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawStagedNavigationSettings(NavigationLibrarySnapshot snapshot)
    {
        bool editable = navigationLibrary.HasWorkingLibrary;
        float settingsHeight = MathF.Ceiling(
            (ImGui.GetTextLineHeightWithSpacing() * (editable ? 12f : 6f)) +
            (ImGui.GetStyle().WindowPadding.Y * 2f) +
            ImGui.GetStyle().ItemSpacing.Y +
            8f);
        BeginPanel(editable ? "ROUTE RECORDING & DISPLAY" : "STAGED SETTINGS", settingsHeight);
        if (!editable)
        {
            ImGui.TextUnformatted($"Recording interval: {snapshot.RecordingIntervalSeconds:0.##} seconds");
            ImGui.TextUnformatted($"Minimum point distance: {snapshot.MinimumPointDistance:0.##}");
            ImGui.TextUnformatted($"World preview: {(snapshot.ShowWorldPreview ? "Shown" : "Hidden")} • Point numbers: {(snapshot.ShowPointNumbers ? "Shown" : "Hidden")}");
            ImGui.TextUnformatted($"Live navigation path: {(snapshot.ShowLiveNavigationPath ? "Shown" : "Hidden")}");
            EndPanel();
            return;
        }

        float interval = snapshot.RecordingIntervalSeconds;
        float spacing = snapshot.MinimumPointDistance;
        bool worldPreview = snapshot.ShowWorldPreview;
        bool pointNumbers = snapshot.ShowPointNumbers;
        bool liveNavigationPath = snapshot.ShowLiveNavigationPath;
        bool changed = false;
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Capture interval", ref interval, 0.2f, 5f, "%.1f sec");
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Minimum point spacing", ref spacing, 0.1f, 10f, "%.1f y");
        changed |= ImGui.Checkbox("Show connected route in the world", ref worldPreview);
        changed |= ImGui.Checkbox("Point numbers", ref pointNumbers);
        changed |= ImGui.Checkbox("Show live generated navigation waypoints", ref liveNavigationPath);
        if (changed)
        {
            NavigationLibraryWriteResult result = navigationLibrary.UpdatePreferences(
                interval,
                spacing,
                worldPreview,
                pointNumbers,
                liveNavigationPath);
            routeOperationMessage = result.Message;
            if (!worldPreview)
                navigationRuntime.ClearPreview();
            else if (selectedRouteId is { } routeId)
                navigationRuntime.RefreshPreview(routeId);
        }
        TextWrapped(NexusTheme.Muted,
            "Timed recording observes your movement only; it never acquires navigation ownership or moves the character.");
        TextWrapped(NexusTheme.Muted,
            "Live generated waypoints appear only for a route Nexus started locally or explicitly delegated to suite travel. Unrelated provider movement remains hidden.");
        EndPanel();
    }

    private void DrawNavigationDiagnostics()
    {
        NavigationDiagnosticsSnapshot diagnostics = navigationDiagnostics.Current;
        float providerHeight = MathF.Ceiling(
            (ImGui.GetTextLineHeightWithSpacing() * (diagnostics.Providers.Count + 6f)) +
            (ImGui.GetStyle().WindowPadding.Y * 2f) + 18f);
        BeginPanel("PROVIDER HEALTH", providerHeight);
        TextWrapped(NexusTheme.Muted,
            "Live, read-only observations used by the navigation safety gate. Audit entries are added only when state changes.");
        foreach (NavigationProviderDiagnostic provider in diagnostics.Providers)
        {
            Vector4 color = provider.State switch
            {
                NavigationDiagnosticState.Healthy => NexusTheme.Green,
                NavigationDiagnosticState.Attention => NexusTheme.Amber,
                _ => NexusTheme.Red,
            };
            string version = string.IsNullOrWhiteSpace(provider.Version) ? string.Empty : $" • {provider.Version}";
            string state = provider.State switch
            {
                NavigationDiagnosticState.Healthy => "Ready",
                NavigationDiagnosticState.Attention => "Review",
                _ => "Blocked",
            };
            NexusTheme.StatusDot(color, $"{provider.DisplayName}: {state}{version}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(provider.Detail);
        }

        ImGui.Spacing();
        if (ImGui.Button("Run isolated non-moving safety simulation", new Vector2(-1, 0)))
            navigationDiagnostics.RunSimulation();
        TextWrapped(NexusTheme.Muted,
            "Uses isolated memory-only state. It cannot move the character, call live movement, change live ownership, or modify the live safety journal.");
        EndPanel();

        NavigationSimulationReport? simulation = navigationDiagnostics.LastSimulation;
        NavigationAuditEntry[] transitions = diagnostics.RecentTransitions.Take(6).ToArray();
        int simulationLines = simulation is null ? 0 : (simulation.Scenarios.Count * 2) + 2;
        float auditHeight = MathF.Ceiling(
            (ImGui.GetTextLineHeightWithSpacing() * (transitions.Length + simulationLines + 4f)) +
            (ImGui.GetStyle().WindowPadding.Y * 2f) + 16f);
        BeginPanel("SAFETY AUDIT", auditHeight);
        if (transitions.Length == 0)
        {
            TextWrapped(NexusTheme.Muted, "No provider or safety transitions have been observed this session.");
        }
        else
        {
            foreach (NavigationAuditEntry entry in transitions)
            {
                Vector4 color = entry.State switch
                {
                    NavigationDiagnosticState.Healthy => NexusTheme.Green,
                    NavigationDiagnosticState.Attention => NexusTheme.Amber,
                    _ => NexusTheme.Red,
                };
                TextWrapped(color,
                    $"{entry.ObservedAtUtc.ToLocalTime():T} • {entry.DisplayName}: {entry.Code}");
            }
        }

        if (simulation is not null)
        {
            ImGui.Spacing();
            NexusTheme.SectionTitle(simulation.Passed
                ? $"Simulation passed {simulation.PassedCount}/{simulation.Scenarios.Count}"
                : $"Simulation needs attention {simulation.PassedCount}/{simulation.Scenarios.Count}");
            foreach (NavigationSimulationScenario scenario in simulation.Scenarios)
            {
                TextWrapped(scenario.Passed ? NexusTheme.Green : NexusTheme.Red,
                    $"{(scenario.Passed ? "✓" : "!")} {scenario.Name}: {scenario.Detail}");
            }
        }
        EndPanel();
    }

    private void DrawNavigationMigration()
    {
        const string importLabel = "Create backup and import to staging";
        const string rollbackLabel = "Rollback staged import";
        NavigationMigrationStatus status = navigationMigration.Status();
        LegacyImportState state = plugin.Configuration.ForLegacyImport("navplotter");
        string operationMessage = status.Message;
        bool staged = status.LastReceipt is not null;
        BeginPanel("ROUTES & NAVIGATION", 320f * Math.Max(1f, plugin.Configuration.UiScale));
        NexusTheme.StatusDot(status.SourceFound ? NexusTheme.Green : NexusTheme.Muted,
            status.SourceFound ? "Configuration located" : "Not found on this computer");
        ImGui.TextColored(NexusTheme.Gold, "Destination: Routes and Navigation");

        if (status.Preview?.Snapshot is { } snapshot)
        {
            string enabled = $"{snapshot.Routes.Count(route => route.OverrideEnabled)} enabled override(s)";
            ImGui.TextUnformatted($"{snapshot.Routes.Count} personal route(s) • {enabled}");
            TextWrapped(NexusTheme.Muted,
                "Recording, display, pane, selection, route, point, binding, tolerance, and override settings mapped.");
            foreach (MigrationIssue issue in status.Preview.Issues.Take(2))
            {
                Vector4 color = issue.Severity == MigrationIssueSeverity.Error ? NexusTheme.Red :
                    issue.Severity == MigrationIssueSeverity.Warning ? NexusTheme.Amber : NexusTheme.Muted;
                TextWrapped(color, $"• {issue.Message}");
            }
        }
        else
        {
            TextWrapped(NexusTheme.Muted, status.Message);
        }

        float availableButtonWidth = ImGui.GetContentRegionAvail().X;
        float importButtonWidth = ButtonWidth(importLabel);
        float rollbackButtonWidth = ButtonWidth(rollbackLabel);
        bool canImport = status.Preview?.CanImport == true;
        if (!canImport)
            ImGui.BeginDisabled();
        if (ImGui.Button(importLabel, new Vector2(importButtonWidth, 0)) && canImport)
        {
            MigrationWriteResult result = navigationMigration.Import();
            operationMessage = result.Message;
            if (result.Success && result.Receipt is { } receipt)
            {
                staged = true;
                state.Reviewed = true;
                state.Imported = true;
                state.SourceVersion = status.Preview!.Snapshot!.SourceConfigurationVersion.ToString();
                state.ImportedAt = receipt.CreatedAtUtc;
                state.ReceiptId = receipt.Id;
                state.ImportedItemCount = status.Preview.Snapshot.Routes.Count;
                state.ReadyForActivation = true;
                state.Activated = false;
                plugin.Save();
            }
        }
        if (!canImport)
            ImGui.EndDisabled();

        if (staged && status.LastReceipt is { } savedReceipt)
        {
            if (availableButtonWidth >= importButtonWidth + ImGui.GetStyle().ItemSpacing.X + rollbackButtonWidth)
                ImGui.SameLine();
            if (ImGui.Button(rollbackLabel, new Vector2(rollbackButtonWidth, 0)))
            {
                MigrationWriteResult result = navigationMigration.Rollback(savedReceipt.Id);
                operationMessage = result.Message;
                if (result.Success)
                {
                    staged = false;
                    state.Imported = false;
                    state.ImportedAt = null;
                    state.ReceiptId = null;
                    state.ImportedItemCount = 0;
                    state.ReadyForActivation = false;
                    state.Activated = false;
                    plugin.Save();
                }
            }
        }

        TextWrapped(staged ? NexusTheme.Green : NexusTheme.Muted, operationMessage);
        if (staged)
            TextWrapped(NexusTheme.Muted,
                "Staged only • standalone VieriNavPlotter remains authoritative • no duplicate route execution");
        EndPanel();
    }

    private void DrawGearAndInventory()
    {
        PageHeading("Gear & Inventory", "Review exact upgrades before Nexus allows any purchase.");

        CharacterSnapshot? character = world.Current.Character.Value;
        if (character is null || !character.Key.IsKnown)
        {
            BeginAutoPanel("SHOP FOR UPGRADES");
            NexusTheme.StatusDot(NexusTheme.Muted, "Waiting for the current character");
            EndAutoPanel();
            return;
        }

        CharacterConfiguration characterConfiguration = plugin.Configuration.ForCharacter(character.Key.ToString());
        ProgressionCharacterMetrics metrics = progressionProviders.CharacterMetrics();
        ProgressionGearProviderObservation observation = progressionProviders.ObserveGearReadiness();
        ManualGearShoppingStatus shoppingStatus = gearShoppingRuntime.Status;
        bool ownedBusy = shoppingStatus.IsActive;
        bool busy = ownedBusy || observation.IsBusy == true;

        BeginAutoPanel("SHOP FOR UPGRADES");
        NexusTheme.StatusDot(
            busy ? NexusTheme.Cyan : progressionProviders.IsGearShoppingPreviewReady ? NexusTheme.Green : NexusTheme.Amber,
            busy
                ? "Shopping and equipment verification are running"
                : progressionProviders.IsGearShoppingPreviewReady
                    ? $"Ready • {metrics.Gil:N0} gil • item level {metrics.ItemLevel}"
                    : "Log into a character to inspect gear upgrades");
        TextWrapped(NexusTheme.Muted,
            progressionProviders.IsGearShoppingExecutionReady
                ? "Nexus owns this entire transaction: live vendor lookup, route travel, exact purchases, verified equipping, gearset update, and displaced-item cleanup."
                : "Nexus reads the live equipment and vendor catalogs directly. Shopping actions require a logged-in character and vnavmesh.");
        if (!shoppingStatus.IsActive && shoppingStatus.State is not ManualGearShoppingState.Idle)
            TextWrapped(shoppingStatus.State == ManualGearShoppingState.Completed ? NexusTheme.Green : NexusTheme.Red,
                shoppingStatus.Message);

        int reserve = characterConfiguration.Progression.MinimumGilReserve;
        if (ImGui.InputInt("Minimum gil to keep", ref reserve, 10_000, 100_000))
        {
            characterConfiguration.Progression.MinimumGilReserve = Math.Clamp(reserve, 0, 999_999_999);
            plugin.Save();
        }

        if (busy)
        {
            TextWrapped(NexusTheme.Cyan, ownedBusy ? shoppingStatus.Message : observation.Detail);
            if (ownedBusy && ImGui.Button("Stop shopping"))
            {
                gearShoppingMessage = gearShoppingRuntime.Stop().Message;
            }
        }
        else
        {
            if (!progressionProviders.IsGearShoppingPreviewReady)
                ImGui.BeginDisabled();
            if (ImGui.Button(gearUpgradePreview is null ? "Check for upgrades" : "Refresh upgrades"))
            {
                if (progressionProviders.TryGetGearUpgradePreview(out GearUpgradePreview? preview, out string message))
                {
                    gearUpgradePreview = preview;
                    selectedGearUpgradeSlots.Clear();
                    if (preview is not null)
                    {
                        foreach (GearUpgradeSlot slot in preview.Slots)
                        {
                            if (slot.Recommended && !slot.ActiveExperienceBonus && slot.Replacement is not null)
                                selectedGearUpgradeSlots.Add(slot.SlotKey);
                        }
                    }
                }
                gearShoppingMessage = message;
            }
            if (!progressionProviders.IsGearShoppingPreviewReady)
                ImGui.EndDisabled();
        }

        if (!string.IsNullOrWhiteSpace(gearShoppingMessage))
            TextWrapped(gearShoppingMessage.Contains("could not", StringComparison.OrdinalIgnoreCase) ||
                        gearShoppingMessage.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
                ? NexusTheme.Red
                : NexusTheme.Cyan, gearShoppingMessage);
        EndAutoPanel();

        DrawNativeMaintenance();

        if (gearUpgradePreview is not { } currentPreview)
            return;

        BeginAutoPanel("UPGRADE PLAN");
        ImGui.TextUnformatted($"{currentPreview.Job} level {currentPreview.Level} • vendor band {currentPreview.VendorLevel}");
        if (!string.IsNullOrWhiteSpace(currentPreview.UnavailableReason))
        {
            TextWrapped(NexusTheme.Amber, currentPreview.UnavailableReason);
            EndAutoPanel();
            return;
        }

        if (ImGui.Button("Recommended"))
        {
            selectedGearUpgradeSlots.Clear();
            foreach (GearUpgradeSlot slot in currentPreview.Slots)
            {
                if (slot.Recommended && !slot.ActiveExperienceBonus && slot.Replacement is not null)
                    selectedGearUpgradeSlots.Add(slot.SlotKey);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Select all upgrades"))
        {
            selectedGearUpgradeSlots.Clear();
            foreach (GearUpgradeSlot slot in currentPreview.Slots)
            {
                if (!slot.ActiveExperienceBonus && slot.Replacement is not null)
                    selectedGearUpgradeSlots.Add(slot.SlotKey);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear selection"))
            selectedGearUpgradeSlots.Clear();

        const ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
                                           ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable("###NexusGearUpgradePlan", 4, tableFlags))
        {
            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 150f);
            ImGui.TableSetupColumn("Current", ImGuiTableColumnFlags.WidthStretch, 1.2f);
            ImGui.TableSetupColumn("Approved replacement", ImGuiTableColumnFlags.WidthStretch, 1.6f);
            ImGui.TableSetupColumn("Cost", ImGuiTableColumnFlags.WidthFixed, 130f);
            ImGui.TableHeadersRow();
            foreach (GearUpgradeSlot slot in currentPreview.Slots)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                bool selected = selectedGearUpgradeSlots.Contains(slot.SlotKey);
                bool selectable = !slot.ActiveExperienceBonus && slot.Replacement is not null && !busy;
                if (!selectable)
                    ImGui.BeginDisabled();
                if (ImGui.Checkbox($"{slot.Name}###gear-slot-{slot.SlotKey}", ref selected))
                {
                    if (selected)
                        selectedGearUpgradeSlots.Add(slot.SlotKey);
                    else
                        selectedGearUpgradeSlots.Remove(slot.SlotKey);
                }
                if (!selectable)
                    ImGui.EndDisabled();

                ImGui.TableNextColumn();
                ImGui.TextWrapped(slot.CurrentEquipment);
                if (slot.ActiveExperienceBonus)
                    TextWrapped(NexusTheme.Amber, "Protected EXP item");

                ImGui.TableNextColumn();
                if (slot.Replacement is { } replacement)
                {
                    TextWrapped(slot.Recommended ? NexusTheme.Green : NexusTheme.Cyan,
                        $"{replacement.Name} • iLvl {replacement.ItemLevel}");
                    TextWrapped(NexusTheme.Muted, replacement.Vendor);
                }
                else
                {
                    ImGui.TextDisabled("No verified upgrade");
                }

                ImGui.TableNextColumn();
                if (slot.Replacement is { } priced)
                {
                    ImGui.TextUnformatted(priced.Quantity > 0
                        ? $"{priced.Quantity} × {priced.UnitPrice:N0}"
                        : "Already owned");
                }
                else
                {
                    ImGui.TextDisabled("—");
                }
            }
            ImGui.EndTable();
        }

        GearShoppingApprovalResult approval = GearShoppingApprovalPolicy.Build(
            currentPreview,
            selectedGearUpgradeSlots,
            metrics.Gil,
            characterConfiguration.Progression.MinimumGilReserve);
        if (approval.Success)
            TextWrapped(NexusTheme.Green,
                $"Selected: {approval.Approval!.Lines.Count} slot(s) • estimated purchase {approval.EstimatedCost:N0} gil • " +
                $"keep at least {approval.Approval.MinimumGilReserve:N0} gil");
        else
            TextWrapped(NexusTheme.Muted, approval.Message);

        bool shoppingAllowed = approval.Success && !busy && characterConfiguration.AllowAutomation &&
                               progressionProviders.IsGearShoppingExecutionReady;
        if (!shoppingAllowed)
            ImGui.BeginDisabled();
        if (ImGui.Button("Approve selected upgrades and shop", new Vector2(-1, 0)) && approval.Approval is not null)
        {
            ProgressionActionResult result = gearShoppingRuntime.Start(approval.Approval);
            gearShoppingMessage = result.Message;
            if (result.Success)
            {
                gearUpgradePreview = null;
                selectedGearUpgradeSlots.Clear();
            }
        }
        if (!shoppingAllowed)
            ImGui.EndDisabled();
        TextWrapped(NexusTheme.Muted,
            "Approval is single-use. Any character, job, equipment, item, quantity, or price change requires a fresh preview.");
        EndAutoPanel();
    }

    private void DrawNativeMaintenance()
    {
        NexusMaintenanceStatus status = maintenanceRuntime.Status;
        AutoDutyProfileSnapshot? profile = maintenanceRuntime.CurrentProfile;
        BeginAutoPanel("MAINTENANCE");
        NexusTheme.StatusDot(
            status.IsActive ? NexusTheme.Cyan : profile is not null ? NexusTheme.Green : NexusTheme.Amber,
            status.IsActive
                ? status.Message
                : profile is not null
                    ? $"Nexus profile: {profile.Name}"
                    : "Import operations settings on the Migration page");
        TextWrapped(NexusTheme.Muted,
            "Nexus now runs self-repair, materia extraction, card/minion/orchestrion registration, and eligible coffers directly. AutoDuty is not called for these actions.");

        if (status.IsActive)
        {
            if (ImGui.Button("Stop maintenance"))
                maintenanceRuntime.Stop(out maintenanceMessage);
        }
        else if (profile is not null)
        {
            if (ImGui.Button("Run enabled maintenance"))
                maintenanceRuntime.StartConfigured(out maintenanceMessage);
            SameLineIfFits("Self-repair");
            if (ImGui.Button("Self-repair"))
                maintenanceRuntime.Start(NexusMaintenanceOperation.Repair, out maintenanceMessage);
            SameLineIfFits("Extract materia");
            if (ImGui.Button("Extract materia"))
                maintenanceRuntime.Start(NexusMaintenanceOperation.ExtractMateria, out maintenanceMessage);

            if (ImGui.Button("Register collectibles"))
                maintenanceRuntime.StartRegistrations(out maintenanceMessage);
            SameLineIfFits("Open coffers");
            if (ImGui.Button("Open coffers"))
                maintenanceRuntime.Start(NexusMaintenanceOperation.OpenCoffers, out maintenanceMessage);
        }
        if (!string.IsNullOrWhiteSpace(maintenanceMessage))
            TextWrapped(NexusTheme.Cyan, maintenanceMessage);
        TextWrapped(NexusTheme.Muted,
            "Selling, desynthesis, Grand Company turn-ins, and storage remain blocked until their destructive-item review and verification layer is native.");
        EndAutoPanel();
    }

    private void DrawProgression()
    {
        PageHeading("Progression", "Set a level goal once; Nexus plans the work and delegates only bounded provider tasks.");

        CharacterSnapshot? character = world.Current.Character.Value;
        ProgressionProviderSnapshot providers = progressionProviders.Snapshot();
        if (character is null || !character.Key.IsKnown)
        {
            BeginAutoPanel("CURRENT JOB");
            NexusTheme.StatusDot(NexusTheme.Muted, "Waiting for the current character");
            TextWrapped(NexusTheme.Muted,
                "The progression planner becomes available after the character and permanent job level are known.");
            EndAutoPanel();
            DrawProgressionProviders(providers);
            return;
        }

        CharacterConfiguration characterConfiguration = plugin.Configuration.ForCharacter(character.Key.ToString());
        ProgressionDraftConfiguration draftConfiguration = characterConfiguration.Progression;
        if (draftConfiguration.TargetLevel == 0)
        {
            draftConfiguration.TargetLevel = Math.Min(
                ReachJobLevelPlanner.MaximumSupportedLevel,
                Math.Max(1, character.Level + 2));
            plugin.Save();
        }

        BeginAutoPanel("REACH JOB LEVEL");
        NexusTheme.StatusDot(NexusTheme.Cyan,
            $"{character.Name} • Job {character.ClassJobId} • Level {character.Level}");
        TextWrapped(NexusTheme.Muted,
            "This first goal follows the job you are currently playing. Job switching joins the queue after its ownership contract is migrated.");

        bool changed = false;
        int targetLevel = draftConfiguration.TargetLevel;
        if (ImGui.SliderInt("Target level", ref targetLevel, 1, ReachJobLevelPlanner.MaximumSupportedLevel))
        {
            draftConfiguration.TargetLevel = targetLevel;
            changed = true;
        }

        ImGui.Spacing();
        ImGui.TextColored(NexusTheme.Gold, "Allowed leveling methods");
        bool jobQuests = draftConfiguration.AllowJobQuests;
        if (ImGui.Checkbox("Job quests", ref jobQuests))
        {
            draftConfiguration.AllowJobQuests = jobQuests;
            changed = true;
        }
        ImGui.SameLine();
        bool huntingLog = draftConfiguration.AllowHuntingLog;
        if (ImGui.Checkbox("Hunting Log", ref huntingLog))
        {
            draftConfiguration.AllowHuntingLog = huntingLog;
            changed = true;
        }
        ImGui.SameLine();
        bool sideQuests = draftConfiguration.AllowSideQuests;
        if (ImGui.Checkbox("Side quests", ref sideQuests))
        {
            draftConfiguration.AllowSideQuests = sideQuests;
            changed = true;
        }
        ImGui.SameLine();
        bool duties = draftConfiguration.AllowDuties;
        if (ImGui.Checkbox("Duties", ref duties))
        {
            draftConfiguration.AllowDuties = duties;
            changed = true;
        }

        int gilReserve = draftConfiguration.MinimumGilReserve;
        if (ImGui.InputInt("Minimum gil to keep", ref gilReserve, 10_000, 100_000))
        {
            draftConfiguration.MinimumGilReserve = Math.Clamp(gilReserve, 0, 999_999_999);
            changed = true;
        }
        TextWrapped(NexusTheme.Muted,
            "Nexus-owned gear planning will treat this as a hard spending floor. Providers will not make that policy decision.");
        if (changed)
        {
            progressionMessage = "Progression draft saved for this character.";
            plugin.Save();
        }
        if (!string.IsNullOrWhiteSpace(progressionMessage))
            TextWrapped(NexusTheme.Green, progressionMessage);
        EndAutoPanel();

        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            new ReachJobLevelGoalDraft(
                character.Key,
                character.ClassJobId,
                character.Level,
                draftConfiguration.TargetLevel,
                draftConfiguration.AllowJobQuests,
                draftConfiguration.AllowHuntingLog,
                draftConfiguration.AllowSideQuests,
                draftConfiguration.AllowDuties,
                draftConfiguration.MinimumGilReserve,
                progressionRuntime.CurrentMetrics.ItemLevel,
                progressionRuntime.CurrentMetrics.Gil),
            providers.Questing,
            providers.Duties);
        DrawProgressionRuntime(plan, character, draftConfiguration);
        if (ImGui.CollapsingHeader("Provider details###ProgressionProviders"))
            DrawProgressionProviders(providers);
        if (ImGui.CollapsingHeader("Plan details###ProgressionPlan"))
            DrawProgressionPlan(plan);
    }

    private void DrawProgressionRuntime(
        ReachJobLevelPlan plan,
        CharacterSnapshot character,
        ProgressionDraftConfiguration draft)
    {
        ProgressionGoalState? state = progressionRuntime.State;
        NexusTheme.SectionTitle("Goal control");
        if (!string.IsNullOrWhiteSpace(progressionRuntime.LoadError))
        {
            BeginAutoPanel("SAVED GOAL");
            NexusTheme.StatusDot(NexusTheme.Red, progressionRuntime.LoadError);
            TextWrapped(NexusTheme.Muted, "The saved file was left untouched for diagnosis and recovery.");
            EndAutoPanel();
            return;
        }

        if (state is null || state.Goal.Status is GoalStatus.Satisfied or GoalStatus.Cancelled)
        {
            int eligible = progressionRuntime.EligibleDuties(character.Level).Count;
            BeginAutoPanel("START");
            bool hasExecutableStart = plan.IsExecutionConnected && progressionRuntime.IsGearReadinessReady;
            NexusTheme.StatusDot(hasExecutableStart ? NexusTheme.Green : NexusTheme.Amber,
                hasExecutableStart
                    ? $"Ready to verify gear, then run one duty at a time • {eligible} currently eligible"
                    : plan.IsExecutionConnected
                    ? "Nexus gear shopping is unavailable"
                        : "No executable duty lane is ready");
            TextWrapped(NexusTheme.Muted,
                "Nexus—not the provider—owns the level target, task history, Stop, Last Run, verification, and decision to schedule another duty.");
            bool canStart = plan.IsValid && !plan.IsSatisfied && hasExecutableStart &&
                plugin.Configuration.ForCharacter(character.Key.ToString()).AllowAutomation;
            if (!canStart)
                ImGui.BeginDisabled();
            if (ImGui.Button("Start level goal", new Vector2(-1, 0)))
            {
                ReachJobLevelGoalDraft goalDraft = new(
                    character.Key,
                    character.ClassJobId,
                    character.Level,
                    draft.TargetLevel,
                    draft.AllowJobQuests,
                    draft.AllowHuntingLog,
                    draft.AllowSideQuests,
                    draft.AllowDuties,
                    draft.MinimumGilReserve,
                    progressionRuntime.CurrentMetrics.ItemLevel,
                    progressionRuntime.CurrentMetrics.Gil);
                ProgressionActionResult result = progressionRuntime.Start(goalDraft, plan);
                progressionMessage = result.Message;
            }
            if (!canStart)
                ImGui.EndDisabled();
            if (state?.Goal.Status is GoalStatus.Satisfied or GoalStatus.Cancelled)
                TextWrapped(state.Goal.Status == GoalStatus.Satisfied ? NexusTheme.Green : NexusTheme.Muted,
                    state.Goal.StatusDetail ?? state.Goal.Status.ToString());
            EndAutoPanel();
            return;
        }

        NexusTask? activeTask = state.ActiveTask;
        BeginAutoPanel("ACTIVE GOAL");
        Vector4 statusColor = state.Goal.Status switch
        {
            GoalStatus.Active => NexusTheme.Green,
            GoalStatus.Paused => NexusTheme.Amber,
            GoalStatus.Blocked => NexusTheme.Red,
            _ => NexusTheme.Muted,
        };
        NexusTheme.StatusDot(statusColor, $"{state.Goal.Status}: {state.Goal.Title}");
        TextWrapped(NexusTheme.Muted, state.Goal.StatusDetail ?? "No status detail is available.");
        int completedDuties = state.Tasks.Count(task =>
            task.Status == NexusTaskStatus.Succeeded && task.Kind.Value == "vieri.duties.run-one/v1");
        ImGui.TextUnformatted($"Plan revision: {state.Goal.PlanRevision} • Bounded duties completed: {completedDuties}");
        if (activeTask is not null)
        {
            ImGui.TextColored(NexusTheme.Gold, activeTask.Title);
            TextWrapped(NexusTheme.Muted, activeTask.StatusDetail ?? activeTask.Reason);
            ImGui.TextUnformatted($"Task state: {activeTask.Status} • Provider: {activeTask.Provider?.Value ?? "Nexus"}");
        }

        if (state.Goal.Status == GoalStatus.Active)
        {
            bool disableLastRun = state.StopAfterCurrentDuty || activeTask is null;
            if (disableLastRun)
                ImGui.BeginDisabled();
            if (ImGui.Button(state.StopAfterCurrentDuty ? "Last Run armed" : "Stop after this duty"))
                progressionMessage = progressionRuntime.StopAfterCurrentDuty().Message;
            if (disableLastRun)
                ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Stop now"))
                progressionMessage = progressionRuntime.StopNow().Message;
        }
        else if (state.Goal.Status is GoalStatus.Paused or GoalStatus.Blocked)
        {
            if (activeTask is not null)
                ImGui.BeginDisabled();
            if (ImGui.Button("Resume with a fresh plan"))
                progressionMessage = progressionRuntime.Resume().Message;
            if (activeTask is not null)
                ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Cancel goal"))
                progressionMessage = progressionRuntime.StopNow().Message;
        }

        if (!string.IsNullOrWhiteSpace(progressionMessage))
            TextWrapped(NexusTheme.Cyan, progressionMessage);
        EndAutoPanel();
    }

    private void DrawProgressionProviders(ProgressionProviderSnapshot providers)
    {
        NexusTheme.SectionTitle("Providers");
        TextWrapped(NexusTheme.Muted,
            "Vieri entries are temporary migration providers. Stock Questionable and AutoDuty are the long-term targets.");
        if (!ImGui.BeginTable("###ProgressionProviders", 2, ImGuiTableFlags.SizingStretchSame))
            return;

        ImGui.TableNextColumn();
        DrawProgressionProvider("QUESTING", providers.Questing);
        ImGui.TableNextColumn();
        DrawProgressionProvider("DUTIES", providers.Duties);
        ImGui.EndTable();
    }

    private void DrawProgressionProvider(string title, ProgressionProviderSelection selection)
    {
        BeginAutoPanel(title);
        Vector4 selectionColor = selection.Readiness switch
        {
            ProgressionProviderReadiness.Ready => NexusTheme.Green,
            ProgressionProviderReadiness.Disabled => NexusTheme.Amber,
            ProgressionProviderReadiness.Missing => NexusTheme.Muted,
            _ => NexusTheme.Red,
        };
        NexusTheme.StatusDot(selectionColor, selection.IsReady
            ? selection.Selected!.Flavor == ProgressionProviderFlavor.Stock
                ? $"Target provider active: {selection.Selected.DisplayName}"
                : $"Current migration provider: {selection.Selected.DisplayName}"
            : selection.Readiness.ToString());
        foreach (ProgressionProviderCandidate candidate in selection.Candidates)
        {
            Vector4 candidateColor = candidate.Readiness switch
            {
                ProgressionProviderReadiness.Ready => NexusTheme.Green,
                ProgressionProviderReadiness.Disabled => NexusTheme.Amber,
                ProgressionProviderReadiness.Missing => NexusTheme.Muted,
                _ => NexusTheme.Red,
            };
            string version = string.IsNullOrWhiteSpace(candidate.Version) ? string.Empty : $" • {candidate.Version}";
            string flavor = candidate.Flavor == ProgressionProviderFlavor.Stock ? "target" : "migration";
            TextWrapped(candidateColor,
                $"• {candidate.DisplayName}: {candidate.Readiness} • {flavor}{version}");
            if (candidate.Readiness == ProgressionProviderReadiness.Incompatible)
                TextWrapped(NexusTheme.Muted, $"  {candidate.Detail}");
        }
        TextWrapped(NexusTheme.Muted, selection.Detail);
        EndAutoPanel();
    }

    private void DrawProgressionPlan(ReachJobLevelPlan plan)
    {
        BeginAutoPanel("PLAN PREVIEW");
        Vector4 summaryColor = plan.IsSatisfied
            ? NexusTheme.Green
            : plan.IsValid ? NexusTheme.Cyan : NexusTheme.Red;
        NexusTheme.StatusDot(summaryColor, plan.Summary);

        foreach (ProgressionPlanStep step in plan.Steps)
        {
            string provider = step.Provider is null ? "Nexus" : step.Provider.Value.Value;
            ImGui.TextColored(NexusTheme.Gold, step.Title);
            TextWrapped(NexusTheme.Muted, $"{step.Reason} Owner: {provider}.");
        }

        foreach (ProgressionPlanIssue issue in plan.Issues)
        {
            Vector4 color = issue.Severity switch
            {
                ProgressionPlanIssueSeverity.Information => NexusTheme.Muted,
                ProgressionPlanIssueSeverity.Warning => NexusTheme.Amber,
                _ => NexusTheme.Red,
            };
            TextWrapped(color, $"• {issue.Message}");
        }

        if (plan.IsValid && !plan.IsSatisfied)
            TextWrapped(plan.IsExecutionConnected ? NexusTheme.Green : NexusTheme.Amber,
                plan.IsExecutionConnected
                    ? "Gear readiness and the duty lane are connected as separate verified tasks. Quest execution remains planning-only until its Nexus policy is migrated."
                    : "Planning is live, but no bounded provider lane is ready to execute.");
        EndAutoPanel();
    }

    private void DrawSettings()
    {
        PageHeading("Settings", "Global presentation and character-specific safety controls.");

        NexusTheme.SectionTitle("Appearance");
        var scale = plugin.Configuration.UiScale;
        if (ImGui.SliderFloat("Interface scale", ref scale, .8f, 1.5f, "%.2f"))
        {
            plugin.Configuration.UiScale = scale;
            plugin.Save();
        }
        var openOnLogin = plugin.Configuration.OpenOnLogin;
        if (ImGui.Checkbox("Open the Vieri Nexus Home page after entering the world", ref openOnLogin))
        {
            plugin.Configuration.OpenOnLogin = openOnLogin;
            plugin.Save();
        }
        var compact = plugin.Configuration.CompactNavigation;
        if (ImGui.Checkbox("Compact navigation", ref compact))
        {
            plugin.Configuration.CompactNavigation = compact;
            plugin.Save();
        }

        ImGui.Spacing();
        NexusTheme.SectionTitle("Operations overlay");
        bool showOperations = plugin.Configuration.ShowOperationsOverlay;
        if (ImGui.Checkbox("Show compact Nexus operations overlay", ref showOperations))
        {
            plugin.Configuration.ShowOperationsOverlay = showOperations;
            plugin.Save();
        }
        bool lockOperations = plugin.Configuration.LockOperationsOverlay;
        if (ImGui.Checkbox("Lock overlay position", ref lockOperations))
        {
            plugin.Configuration.LockOperationsOverlay = lockOperations;
            plugin.Save();
        }
        bool transparentOperations = plugin.Configuration.OperationsOverlayTransparent;
        if (ImGui.Checkbox("Transparent overlay background", ref transparentOperations))
        {
            plugin.Configuration.OperationsOverlayTransparent = transparentOperations;
            plugin.Save();
        }
        bool showOperationsStatus = plugin.Configuration.ShowOperationsStatus;
        if (ImGui.Checkbox("Show current action below the overlay", ref showOperationsStatus))
        {
            plugin.Configuration.ShowOperationsStatus = showOperationsStatus;
            plugin.Save();
        }

        ImGui.Spacing();
        NexusTheme.SectionTitle("Current character");
        var character = world.Current.Character.Value;
        if (character is null || !character.Key.IsKnown)
        {
            ImGui.TextDisabled("Character settings become available after the character snapshot is ready.");
            return;
        }

        var characterConfig = plugin.Configuration.ForCharacter(character.Key.ToString());
        ImGui.TextColored(NexusTheme.Gold, character.Name);
        ImGui.TextDisabled($"Character scope {character.Key}");
        var automation = characterConfig.AllowAutomation;
        if (ImGui.Checkbox("Allow automation for this character", ref automation))
        {
            characterConfig.AllowAutomation = automation;
            plugin.Save();
        }
        var manualTarget = characterConfig.PauseOnManualTarget;
        if (ImGui.Checkbox("Pause automated targeting when I change targets", ref manualTarget))
        {
            characterConfig.PauseOnManualTarget = manualTarget;
            plugin.Save();
        }
    }

    private void DrawModulePage(string page)
    {
        PageHeading(page, "This module is registered in the Nexus shell and awaiting its parity migration.");
        BeginPanel("MIGRATION STATUS");
        NexusTheme.StatusDot(NexusTheme.Amber, "Staged — existing plugin remains authoritative");
        ImGui.TextWrapped("Nexus will not enable this module until its settings importer, compatibility endpoints, resource ownership, recovery behavior, and regression suite pass validation.");
        EndPanel();
        ImGui.Spacing();
        BeginPanel("WHY THIS IS SAFE");
        ImGui.TextWrapped("Explicit Routes, approved gear shopping, and bounded Progression duty tasks are live. Other automation, retainers, market work, HUD changes, and Discord actions remain inactive.");
        EndPanel();
    }

    private void DrawModuleGrid()
    {
        if (!ImGui.BeginTable("###ModuleGrid", 2, ImGuiTableFlags.SizingStretchSame))
            return;
        foreach (var module in modules.Modules.OrderBy(x => x.Descriptor.DisplayName))
        {
            ImGui.TableNextColumn();
            BeginPanel(module.Descriptor.DisplayName.ToUpperInvariant());
            (Vector4 color, string status) = module.Descriptor.Id switch
            {
                "navigation" => (NexusTheme.Green, "Live"),
                "progression" => (NexusTheme.Green, "Bounded duty execution"),
                "gear" => (NexusTheme.Green, "Gear transactions and safe maintenance live"),
                _ => (NexusTheme.Amber, "Migration staged"),
            };
            NexusTheme.StatusDot(color, status);
            ImGui.TextWrapped(module.Descriptor.Description);
            ImGui.TextColored(NexusTheme.Gold, module.Descriptor.Category);
            EndPanel();
        }
        ImGui.EndTable();
    }

    private static void PageHeading(string title, string subtitle)
    {
        ImGui.TextColored(new Vector4(1f, .86f, .86f, 1f), title.ToUpperInvariant());
        ImGui.TextDisabled(subtitle);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void StatusCard(string label, string value, string detail, Vector4 color)
    {
        BeginPanel(label);
        ImGui.TextColored(color, value);
        ImGui.TextDisabled(detail);
        EndPanel();
    }

    private static void BeginPanel(string title, float height = 105)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, NexusTheme.PanelRaised);
        ImGui.BeginChild($"###panel-{title}-{ImGui.GetCursorPosY()}", new Vector2(0, height), true,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.TextColored(NexusTheme.Gold, title);
        ImGui.Separator();
    }

    private static void EndPanel()
    {
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    private static void BeginAutoPanel(string title)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8f, 8f));
        bool beganTable = ImGui.BeginTable($"###auto-panel-{title}", 1,
            ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp);
        AutoPanelTableStack.Push(beganTable);
        if (beganTable)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg,
                ImGui.ColorConvertFloat4ToU32(NexusTheme.PanelRaised));
        }
        else
        {
            ImGui.BeginGroup();
        }
        ImGui.TextColored(NexusTheme.Gold, title);
        ImGui.Separator();
    }

    private static void EndAutoPanel()
    {
        if (AutoPanelTableStack.Pop())
            ImGui.EndTable();
        else
            ImGui.EndGroup();
        ImGui.PopStyleVar();
        ImGui.Spacing();
    }

    private static float ButtonWidth(string label) =>
        MathF.Ceiling(ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f) + 2f);

    private static void SameLineIfFits(string nextLabel)
    {
        if (ImGui.GetContentRegionAvail().X >= ButtonWidth(nextLabel) + ImGui.GetStyle().ItemSpacing.X)
            ImGui.SameLine();
    }

    private static void TextWrapped(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
}
