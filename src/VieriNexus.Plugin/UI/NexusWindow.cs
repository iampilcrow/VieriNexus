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
    private static readonly (string Group, string Id, string Label)[] Navigation =
    [
        ("OVERVIEW", "Home", "Home"),
        ("OVERVIEW", "Overview", "Control Center"),
        ("OVERVIEW", "Automation", "Automation"),
        ("OVERVIEW", "Progression", "Progression"),
        ("OVERVIEW", "Queue", "Queue"),
        ("MODULES", "Combat", "Combat"),
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
    private readonly NavigationLibraryService navigationLibrary;
    private readonly NavigationActivationService navigationActivation;
    private readonly NavigationDiagnosticsService navigationDiagnostics;
    private readonly NavigationRecoveryService navigationRecovery;
    private readonly NavigationRouteRuntimeService navigationRuntime;
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

    internal NexusWindow(
        Plugin plugin,
        DependencyService dependencies,
        LegacyConfigurationInventory legacyInventory,
        NavigationMigrationService navigationMigration,
        NavigationLibraryService navigationLibrary,
        NavigationActivationService navigationActivation,
        NavigationDiagnosticsService navigationDiagnostics,
        NavigationRecoveryService navigationRecovery,
        NavigationRouteRuntimeService navigationRuntime,
        ModuleRegistry modules,
        WorldStateStore world,
        ISharedImmediateTexture logo)
        : base("Vieri Nexus###VieriNexusMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.dependencies = dependencies;
        this.legacyInventory = legacyInventory;
        this.navigationMigration = navigationMigration;
        this.navigationLibrary = navigationLibrary;
        this.navigationActivation = navigationActivation;
        this.navigationDiagnostics = navigationDiagnostics;
        this.navigationRecovery = navigationRecovery;
        this.navigationRuntime = navigationRuntime;
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
            StatusCard("FOUNDATION", "Online", "Safe migration shell", NexusTheme.Green);
            ImGui.TableNextColumn();
            StatusCard("AUTOMATION", "Staged", "Existing Vieri products remain authoritative", NexusTheme.Amber);
            ImGui.TableNextColumn();
            StatusCard("NEXT", "Migration", "Module parity before replacement", NexusTheme.Cyan);
            ImGui.EndTable();
        }
    }

    private void DrawOverview()
    {
        PageHeading("Control Center", "One place to understand, direct, and safely stop every Nexus activity.");

        if (ImGui.BeginTable("###OverviewStatus", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            StatusCard("AUTOMATION", "Idle", "No active goal", NexusTheme.Muted);
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
            ImGui.TextWrapped("No automation has been enabled. Existing Vieri plugins remain authoritative while migration is staged safely.");
            EndPanel();
            ImGui.TableNextColumn();
            BeginPanel("SAFETY STATE");
            ImGui.TextUnformatted("Resource owners: 0");
            ImGui.TextUnformatted("Active goals: 0");
            ImGui.TextUnformatted("Pending migrations: 9");
            ImGui.TextColored(NexusTheme.Green, "No live behavior has been replaced.");
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
        ImGui.TextDisabled("VieriCodex and the other current Vieri products are migration sources, not third-party dependencies. Questionable is incorporated through VieriCodex and is intentionally not listed separately.");
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

        foreach (var source in legacyInventory.Scan())
        {
            if (source.Id == "navplotter")
            {
                DrawNavigationMigration();
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

    private void DrawRoutesAndNavigation()
    {
        PageHeading("Routes & Navigation", "Build, preview, and safely run Nexus-owned routes through one guarded navigation authority.");

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
            DrawNavigationActivationSafety();
            DrawNavigationRecovery();
            DrawNavigationDiagnostics();
            return;
        }

        DrawNavigationWorkingLibrary();

        NexusTheme.StatusDot(NexusTheme.Green, navigationLibrary.HasWorkingLibrary
            ? "Nexus working library available"
            : "Verified staged library available");
        ImGui.SameLine();
        ImGui.TextDisabled(navigationLibrary.HasWorkingLibrary
            ? "Nexus-owned • atomic saves • previous working copy retained"
            : "Read-only staging • VieriNavPlotter remains authoritative");
        ImGui.Spacing();

        if (ImGui.BeginTable("###RouteSummary", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            StatusCard("PERSONAL ROUTES", snapshot.Routes.Count.ToString(),
                navigationLibrary.HasWorkingLibrary ? "Nexus working library" : "Imported into Nexus staging", NexusTheme.Cyan);
            ImGui.TableNextColumn();
            int enabledOverrides = snapshot.Routes.Count(route => route.OverrideEnabled);
            StatusCard("ENABLED OVERRIDES", enabledOverrides.ToString(), "Visible here; not active in Nexus", NexusTheme.Amber);
            ImGui.TableNextColumn();
            StatusCard("SOURCE CONFIG", $"Version {snapshot.SourceConfigurationVersion}", "Verified migration snapshot", NexusTheme.Green);
            ImGui.EndTable();
        }

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
            DrawStagedNavigationSettings(snapshot);
            DrawNavigationActivationSafety();
            DrawNavigationRecovery();
            DrawNavigationDiagnostics();
            return;
        }

        DrawStagedNavigationSettings(snapshot);

        ImGui.SetNextItemWidth(Math.Min(420f, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint("###RouteSearch", "Search name, tags, notes, target, or territory", ref routeSearch, 256);
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
        if (ImGui.BeginChild("###RouteList", new Vector2(0, 650), true))
        {
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
                        ImGui.TextColored(NexusTheme.Amber, "Override enabled in source settings");
                    ImGui.Spacing();
                }
            }
        }
        ImGui.EndChild();

        ImGui.TableNextColumn();
        NavigationRouteSnapshot? selectedRoute = selectedRouteId is { } id
            ? filtered.FirstOrDefault(route => route.Id == id)
            : null;
        DrawRouteDetails(selectedRoute, snapshot.ShowPointNumbers);
        ImGui.EndTable();

        DrawNavigationActivationSafety();
        DrawNavigationRecovery();
        DrawNavigationDiagnostics();
        DrawDeleteRouteConfirmation();
        DrawClearPointsConfirmation();
    }

    private void DrawRouteDetails(NavigationRouteSnapshot? route, bool showPointNumbers)
    {
        if (ImGui.BeginChild("###RouteDetails", new Vector2(0, 650), true))
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
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("###NexusRouteName", ref routeNameEdit, 120);
                }
                else
                {
                    ImGui.TextColored(NexusTheme.Gold, route.Name);
                }
                ImGui.Separator();
                ImGui.TextUnformatted($"Territory: {route.TerritoryId}");
                ImGui.TextUnformatted($"Points: {route.Points.Count}");
                ImGui.TextUnformatted($"Movement: {(route.UseMesh ? "Mesh" : "Direct")} • {(route.UseFlight ? "Flight allowed" : "Ground only")}");
                ImGui.TextUnformatted($"Tolerance: {route.Tolerance:0.##} • Final point: {route.LastPointTolerance:0.##}");
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
                if (route.TargetDataId != 0 || !string.IsNullOrWhiteSpace(route.TargetLabel))
                    ImGui.TextUnformatted($"Target: {route.TargetLabel} ({route.TargetDataId})");
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

                ImGui.Spacing();
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
                    NavigationRoutePlan preview = navigationRuntime.TogglePreview(route, showPointNumbers);
                    routeOperationMessage = preview.Message;
                }
                NavigationRoutePlan travelPlan = navigationRuntime.Plan(route, NavigationRoutePlanKind.TravelToStart);
                bool canExecute = navigationLibrary.HasWorkingLibrary &&
                                  navigationActivation.AuthorityStatus.IsActive &&
                                  !navigationRuntime.Status.IsActive &&
                                  !navigationRuntime.RecordingStatus.IsRecording;
                if (!canExecute || !travelPlan.IsExecutable)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Travel to start") && canExecute && travelPlan.IsExecutable)
                    routeOperationMessage = navigationRuntime.Start(route, NavigationRoutePlanKind.TravelToStart).Message;
                if (!canExecute || !travelPlan.IsExecutable)
                    ImGui.EndDisabled();
                NavigationRoutePlan playbackPlan = navigationRuntime.Plan(route, NavigationRoutePlanKind.Playback);
                if (!canExecute || !playbackPlan.IsExecutable)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Play route") && canExecute && playbackPlan.IsExecutable)
                    routeOperationMessage = navigationRuntime.Start(route, NavigationRoutePlanKind.Playback).Message;
                if (!canExecute || !playbackPlan.IsExecutable)
                    ImGui.EndDisabled();
                if (navigationRuntime.Status.CanStop)
                {
                    if (ImGui.Button("Stop playback"))
                        routeOperationMessage = navigationRuntime.Stop().Message;
                }
                TextWrapped(NexusTheme.Muted,
                    $"Plan: {playbackPlan.Points.Count} point(s) • {playbackPlan.TotalDistance:0.0} yalms • {playbackPlan.Message}");
                NavigationRouteExecutionStatus executionStatus = navigationRuntime.Status;
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

                if (navigationLibrary.HasWorkingLibrary)
                {
                    ImGui.Spacing();
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

                if (navigationLibrary.HasWorkingLibrary)
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
        }
        ImGui.EndChild();
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
            (ImGui.GetTextLineHeightWithSpacing() * (editable ? 11f : 6f)) +
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
        bool changed = false;
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Capture interval", ref interval, 0.2f, 5f, "%.1f sec");
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Minimum point spacing", ref spacing, 0.1f, 10f, "%.1f y");
        changed |= ImGui.Checkbox("Show connected route in the world", ref worldPreview);
        changed |= ImGui.Checkbox("Point numbers", ref pointNumbers);
        if (changed)
        {
            NavigationLibraryWriteResult result = navigationLibrary.UpdatePreferences(
                interval,
                spacing,
                worldPreview,
                pointNumbers,
                snapshot.ShowLiveNavigationPath);
            routeOperationMessage = result.Message;
            if (!worldPreview)
                navigationRuntime.ClearPreview();
            else if (selectedRouteId is { } routeId)
                navigationRuntime.RefreshPreview(routeId);
        }
        TextWrapped(NexusTheme.Muted,
            "Timed recording observes your movement only; it never acquires navigation ownership or moves the character.");
        TextWrapped(NexusTheme.Muted,
            $"Live generated navigation waypoints: {(snapshot.ShowLiveNavigationPath ? "saved as shown" : "saved as hidden")} • rendering remains disabled until its ownership filter is migrated.");
        EndPanel();
    }

    private void DrawNavigationActivationSafety()
    {
        NavigationActivationAssessment assessment = navigationActivation.Assess();
        NavigationAuthorityStatus authority = navigationActivation.AuthorityStatus;
        NavigationActivationBlocker[] displayedBlockers = assessment.Blockers
            .Where(blocker => blocker.Code != "source-plugin-loaded")
            .ToArray();
        int readinessLines = (assessment.IsStopAvailable ? 1 : 0) +
                             (assessment.IsManualOverrideAvailable ? 1 : 0) +
                             (assessment.IsReloadReconciliationAvailable ? 1 : 0);
        float height = MathF.Ceiling(
            (ImGui.GetTextLineHeightWithSpacing() * ((displayedBlockers.Length * 2f) + 9f + (readinessLines * 2f))) +
            (ImGui.GetStyle().WindowPadding.Y * 2f) + 12f);
        BeginPanel("ACTIVATION SAFETY", height);
        NexusTheme.StatusDot(authority.IsActive ? NexusTheme.Green : NexusTheme.Amber,
            authority.IsActive
                ? "Nexus ownership active — guarded route controls available"
                : "Staging only — execution blocked");
        TextWrapped(NexusTheme.Muted,
            "Static preview is non-moving. Travel and playback require every safety gate and explicit Nexus ownership.");
        string sourceState = assessment.IsSourcePluginLoaded
            ? "VieriNavPlotter is loaded and is the current navigation owner."
            : assessment.IsSourcePluginInstalled
                ? "VieriNavPlotter is installed but not loaded; Nexus may own navigation after explicit approval."
                : "VieriNavPlotter is not installed; Nexus may own navigation after explicit approval.";
        TextWrapped(NexusTheme.Muted, sourceState);
        if (assessment.IsStopAvailable)
            TextWrapped(NexusTheme.Green,
                "• Verified Stop is connected; ownership releases only after movement is confirmed inactive.");
        if (assessment.IsManualOverrideAvailable)
            TextWrapped(NexusTheme.Green,
                "• Manual movement yielding is connected; player movement input always takes priority.");
        if (assessment.IsReloadReconciliationAvailable)
            TextWrapped(NexusTheme.Green,
                "• Reload recovery and the lease watchdog are connected; stale movement intent is stopped, never replayed.");
        foreach (NavigationActivationBlocker blocker in displayedBlockers)
            TextWrapped(NexusTheme.Muted, $"• {blocker.Message}");

        ImGui.Spacing();
        TextWrapped(authority.IsActive ? NexusTheme.Green : NexusTheme.Muted, authority.Message);
        if (authority.IsActive)
        {
            if (ImGui.Button("Return Nexus navigation to staging", new Vector2(-1, 0)))
                navigationActivation.ReturnAuthorityToStaging();
        }
        else
        {
            if (!authority.CanApprove)
                ImGui.BeginDisabled();
            if (ImGui.Button("Approve Nexus navigation ownership", new Vector2(-1, 0)) &&
                authority.CanApprove)
            {
                navigationActivation.ApproveAuthority();
            }
            if (!authority.CanApprove)
                ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(authority.CanApprove
                    ? "Activates Nexus navigation authority for this session. No route starts automatically."
                    : authority.Message);
        }
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

    private void DrawNavigationRecovery()
    {
        NavigationRecoveryStatus recovery = navigationRecovery.Current;
        if (!recovery.IsRequired)
            return;

        float height = MathF.Ceiling(
            (ImGui.GetTextLineHeightWithSpacing() * 9f) +
            (ImGui.GetStyle().WindowPadding.Y * 2f) + 16f);
        BeginPanel("STOPPED INTENT CHECKPOINT", height);
        NexusTheme.StatusDot(recovery.CanAcknowledge ? NexusTheme.Amber : NexusTheme.Red,
            recovery.CanAcknowledge ? "Ready for explicit acknowledgement" : "Waiting for safe acknowledgement");
        TextWrapped(NexusTheme.Muted, recovery.Message);
        TextWrapped(NexusTheme.Muted,
            "Acknowledgement only clears the stopped checkpoint. It cannot resume or replay movement and does not approve Nexus authority.");
        if (!recovery.CanAcknowledge)
            ImGui.BeginDisabled();
        if (ImGui.Button("Acknowledge stopped intent (no replay)", new Vector2(-1, 0)) &&
            recovery.CanAcknowledge)
        {
            navigationRecovery.Acknowledge(Environment.TickCount64);
        }
        if (!recovery.CanAcknowledge)
            ImGui.EndDisabled();
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
        var manualMove = characterConfig.PauseOnManualMovement;
        if (ImGui.Checkbox("Pause movement automation when I move manually", ref manualMove))
        {
            characterConfig.PauseOnManualMovement = manualMove;
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
        ImGui.TextWrapped("The new shell currently observes only. It does not start duties, move the character, change rotations, access retainers, alter the HUD, or send Discord messages.");
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
            NexusTheme.StatusDot(NexusTheme.Amber, "Migration staged");
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

    private static float ButtonWidth(string label) =>
        MathF.Ceiling(ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f) + 2f);

    private static void TextWrapped(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
}
