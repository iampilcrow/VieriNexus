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
        ("OVERVIEW", "Progress Atlas", "Atlas"),
        ("OVERVIEW", "Queue", "Queue"),
        ("OVERVIEW", "Plugins", "Plugins"),
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
    private readonly CodexMigrationService codexMigration;
    private readonly CommandCenterMigrationService commandCenterMigration;
    private readonly CommandCenterCatalogService commandCenterCatalog;
    private readonly QuestionableCompatibilityService questionableCompatibility;
    private readonly NavigationLibraryService navigationLibrary;
    private readonly NavigationActivationService navigationActivation;
    private readonly NavigationDiagnosticsService navigationDiagnostics;
    private readonly NavigationRouteRuntimeService navigationRuntime;
    private readonly ProgressionProviderService progressionProviders;
    private readonly ProgressionRuntimeService progressionRuntime;
    private readonly ProgressionQueueRuntimeService progressionQueue;
    private readonly SoloDutyRotationRuntimeService soloDutyRotation;
    private readonly ProgressAtlasService progressAtlas;
    private readonly ProgressAtlasActionService progressAtlasActions;
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
    private string progressAtlasMessage = string.Empty;
    private GearUpgradePreview? gearUpgradePreview;
    private readonly HashSet<int> selectedGearUpgradeSlots = [];
    private string gearShoppingMessage = string.Empty;
    private string maintenanceMessage = string.Empty;
    private NexusItemTransactionPreview? protectedSalePreview;
    private string migrationQuickStartMessage = string.Empty;
    private string commandCenterSearch = string.Empty;
    private string commandCenterCustomCommand = string.Empty;
    private string commandCenterCustomDescription = string.Empty;
    private string commandCenterMessage = string.Empty;
    private string questionableCompatibilityMessage = string.Empty;
    private bool confirmQuestionableCompatibilityRollback;
    private Guid? pendingDeleteQueueStepId;
    private bool confirmClearProgressionQueue;

    internal NexusWindow(
        Plugin plugin,
        DependencyService dependencies,
        LegacyConfigurationInventory legacyInventory,
        NavigationMigrationService navigationMigration,
        AutoDutyMigrationService autoDutyMigration,
        CodexMigrationService codexMigration,
        CommandCenterMigrationService commandCenterMigration,
        CommandCenterCatalogService commandCenterCatalog,
        QuestionableCompatibilityService questionableCompatibility,
        NavigationLibraryService navigationLibrary,
        NavigationActivationService navigationActivation,
        NavigationDiagnosticsService navigationDiagnostics,
        NavigationRouteRuntimeService navigationRuntime,
        ProgressionProviderService progressionProviders,
        ProgressionRuntimeService progressionRuntime,
        ProgressionQueueRuntimeService progressionQueue,
        SoloDutyRotationRuntimeService soloDutyRotation,
        ProgressAtlasService progressAtlas,
        ProgressAtlasActionService progressAtlasActions,
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
        this.codexMigration = codexMigration;
        this.commandCenterMigration = commandCenterMigration;
        this.commandCenterCatalog = commandCenterCatalog;
        this.questionableCompatibility = questionableCompatibility;
        this.navigationLibrary = navigationLibrary;
        this.navigationActivation = navigationActivation;
        this.navigationDiagnostics = navigationDiagnostics;
        this.navigationRuntime = navigationRuntime;
        this.progressionProviders = progressionProviders;
        this.progressionRuntime = progressionRuntime;
        this.progressionQueue = progressionQueue;
        this.soloDutyRotation = soloDutyRotation;
        this.progressAtlas = progressAtlas;
        this.progressAtlasActions = progressAtlasActions;
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
        if (plugin.Configuration.SelectedPage == "Command Center")
            plugin.Configuration.SelectedPage = "Plugins";
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
            case "Queue": DrawProgressionQueue(); break;
            case "Progress Atlas": DrawProgressAtlas(); break;
            case "Gear & Inventory": DrawGearAndInventory(); break;
            case "Routes & Navigation": DrawRoutesAndNavigation(); break;
            case "Plugins": DrawPlugins(); break;
            case "Command Center": DrawPlugins(); break;
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
            StatusCard("CHARACTER", character?.Name ?? "Waiting", character is null ? "No character snapshot" : $"Level {character.Level} • {ClassJobDisplay.Label(character)}", character is null ? NexusTheme.Muted : NexusTheme.Cyan);
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
        DrawQuestionableCompatibility();

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
            if (source.Id == "codex")
            {
                DrawCodexMigration();
                continue;
            }
            if (source.Id == "deck")
            {
                DrawCommandCenterMigration();
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
        CodexMigrationStatus codexStatus = codexMigration.Status();
        CommandCenterMigrationStatus commandStatus = commandCenterMigration.Status();
        bool navigationReady = navigationStatus.Preview?.CanImport == true;
        bool operationsReady = operationsStatus.Preview?.CanImport == true;
        bool navigationPrepared = navigationStatus.LastReceipt is not null && navigationLibrary.HasWorkingLibrary;
        bool operationsPrepared = operationsStatus.LastReceipt is not null && autoDutyMigration.HasWorkingProfiles;
        LegacyImportState codexImportState = plugin.Configuration.ForLegacyImport("codex");
        bool codexReady = codexStatus.Preview?.CanImport == true;
        bool codexPrepared = codexStatus.LastReceipt is not null && codexImportState.Activated;
        bool commandReady = commandStatus.Preview?.CanImport == true;
        bool commandPrepared = commandStatus.LastReceipt is not null && commandStatus.WorkingSnapshot is not null;
        bool needsNavigation = navigationReady && !navigationPrepared;
        bool needsOperations = operationsReady && !operationsPrepared;
        bool needsCodex = codexReady && !codexPrepared;
        bool needsCommands = commandReady && !commandPrepared;
        bool blocked = (navigationStatus.SourceFound && !navigationReady && !navigationPrepared) ||
                       (operationsStatus.SourceFound && !operationsReady && !operationsPrepared) ||
                       (codexStatus.SourceFound && !codexReady && !codexPrepared) ||
                       (commandStatus.SourceFound && !commandReady && !commandPrepared);
        bool foundAnything = navigationStatus.SourceFound || operationsStatus.SourceFound || codexStatus.SourceFound ||
                              commandStatus.SourceFound || navigationPrepared || operationsPrepared || codexPrepared || commandPrepared;

        BeginAutoPanel("SET UP THIS COMPUTER");
        if (blocked && !needsNavigation && !needsOperations && !needsCodex && !needsCommands)
        {
            NexusTheme.StatusDot(NexusTheme.Red,
                "A detected settings source needs attention in its detailed Migration card");
        }
        else if (!needsNavigation && !needsOperations && !needsCodex && !needsCommands)
        {
            NexusTheme.StatusDot(foundAnything ? NexusTheme.Green : NexusTheme.Amber,
                foundAnything
                    ? "Every currently supported Vieri settings source on this computer is prepared"
                    : "No currently supported Vieri settings source was detected on this computer");
        }
        else
        {
            NexusTheme.StatusDot(NexusTheme.Cyan,
                $"Detected {(needsNavigation ? 1 : 0) + (needsOperations ? 1 : 0) + (needsCodex ? 1 : 0) + (needsCommands ? 1 : 0)} supported settings source(s) ready to prepare");
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
                if (needsCodex)
                {
                    MigrationWriteResult imported = codexStatus.LastReceipt is null
                        ? codexMigration.Import()
                        : new MigrationWriteResult(true, codexStatus.Message, codexStatus.LastReceipt);
                    results.Add(imported.Message);
                    CodexMigrationSnapshot? snapshot = codexMigration.Status().StagedSnapshot;
                    if (imported.Success && imported.Receipt is { } receipt && snapshot is not null)
                    {
                        codexImportState.Reviewed = codexImportState.Imported = true;
                        codexImportState.SourceVersion = "codex-preferences-v1";
                        codexImportState.ImportedAt = receipt.CreatedAtUtc;
                        codexImportState.ReceiptId = receipt.Id;
                        codexImportState.ImportedItemCount = 11 + snapshot.SavedQueueSteps;
                        if (ApplyCodexPreferences(snapshot, codexImportState, out string applyMessage))
                            results.Add(applyMessage);
                        else
                            results.Add(applyMessage);
                    }
                }
                if (needsCommands)
                {
                    MigrationWriteResult imported = commandCenterMigration.Import();
                    results.Add(imported.Message);
                    if (imported.Success && imported.Receipt is { } receipt)
                    {
                        LegacyImportState state = plugin.Configuration.ForLegacyImport("deck");
                        state.Reviewed = state.Imported = true;
                        state.SourceVersion = "command-center-v1";
                        state.ImportedAt = receipt.CreatedAtUtc;
                        state.ReceiptId = receipt.Id;
                        state.ImportedItemCount = commandStatus.Preview!.Snapshot!.Favorites.Count +
                                                  commandStatus.Preview.Snapshot.CustomCommands.Values.Sum(commands => commands.Count);
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

    private void DrawQuestionableCompatibility()
    {
        QuestionableCompatibilityStatus status = questionableCompatibility.Status;
        Vector4 color = status.Health switch
        {
            QuestionableCompatibilityHealth.Active or QuestionableCompatibilityHealth.Native => NexusTheme.Green,
            QuestionableCompatibilityHealth.Conflict or QuestionableCompatibilityHealth.Failed => NexusTheme.Red,
            QuestionableCompatibilityHealth.Checking => NexusTheme.Cyan,
            _ => NexusTheme.Amber,
        };

        BeginAutoPanel("QUESTIONABLE COMPATIBILITY");
        NexusTheme.StatusDot(color, status.Health switch
        {
            QuestionableCompatibilityHealth.Active => "Protected stock Questionable routes ready",
            QuestionableCompatibilityHealth.Native => "Stock Questionable already contains every correction",
            QuestionableCompatibilityHealth.Conflict => "A protected route changed unexpectedly",
            QuestionableCompatibilityHealth.Failed => "Compatibility preparation failed safely",
            QuestionableCompatibilityHealth.Disabled => "Compatibility management disabled",
            _ => "Preparing protected stock Questionable routes",
        });
        TextWrapped(NexusTheme.Muted, status.Message);
        TextWrapped(NexusTheme.Muted,
            "Nexus manages only five exact quest-route corrections. Every other route and all quest execution remain stock Questionable-owned and update normally.");

        if (!plugin.Configuration.ManageQuestionableRouteCorrections)
        {
            if (ImGui.Button("Enable automatic route protection"))
            {
                plugin.Configuration.ManageQuestionableRouteCorrections = true;
                plugin.Save();
                questionableCompatibility.RequestRecheck();
                questionableCompatibilityMessage = "Automatic protection enabled; Nexus will verify the current stock bundle while Questionable is idle.";
            }
        }
        else if (status.Health == QuestionableCompatibilityHealth.Active)
        {
            if (!confirmQuestionableCompatibilityRollback)
            {
                if (ImGui.Button("Restore stock routes only..."))
                    confirmQuestionableCompatibilityRollback = true;
            }
            else
            {
                ImGui.TextColored(NexusTheme.Amber,
                    "Restore the exact pre-Nexus bundle and stop managing these five corrections?");
                if (ImGui.Button("Confirm restore stock routes"))
                {
                    QuestionableCompatibilityInstallResult result = questionableCompatibility.RestoreOfficialBundle();
                    questionableCompatibilityMessage = result.Message;
                    if (result.State != QuestionableCompatibilityInstallState.Failed &&
                        result.State != QuestionableCompatibilityInstallState.Conflict)
                    {
                        plugin.Configuration.ManageQuestionableRouteCorrections = false;
                        plugin.Save();
                    }
                    confirmQuestionableCompatibilityRollback = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                    confirmQuestionableCompatibilityRollback = false;
            }
        }
        else if (status.Health is QuestionableCompatibilityHealth.Conflict or QuestionableCompatibilityHealth.Failed)
        {
            if (ImGui.Button("Check current Questionable bundle again"))
                questionableCompatibility.RequestRecheck();
        }

        if (!string.IsNullOrWhiteSpace(questionableCompatibilityMessage))
            TextWrapped(NexusTheme.Cyan, questionableCompatibilityMessage);
        EndAutoPanel();
    }

    private void DrawCodexMigration()
    {
        const string importLabel = "Back up and import Progression preferences";
        const string applyLabel = "Apply to current character";
        const string rollbackLabel = "Rollback Progression import";
        CodexMigrationStatus status = codexMigration.Status();
        LegacyImportState state = plugin.Configuration.ForLegacyImport("codex");
        string operationMessage = status.Message;

        BeginAutoPanel("PROGRESSION & ATLAS");
        NexusTheme.StatusDot(status.SourceFound ? NexusTheme.Green : NexusTheme.Muted,
            status.SourceFound ? "VieriCodex configuration located" : "Not found on this computer");
        ImGui.TextColored(NexusTheme.Gold, "Destination: Nexus Progression and Progress Atlas");
        if (status.Preview?.Snapshot is { } preview)
        {
            int enabled = new[]
            {
                preview.MainScenarioQuests, preview.CombatClassJobQuests || preview.RoleQuests,
                preview.AetherCurrentQuests, preview.FieldAetherCurrents, preview.AetheryteAttunements,
                preview.MapExploration, preview.HuntingLogs, preview.SideQuests, preview.Achievements,
            }.Count(value => value);
            ImGui.TextUnformatted($"{enabled} enabled activity group(s) • {preview.SavedQueueSteps} saved queue step(s)");
            TextWrapped(NexusTheme.Muted,
                "MSQ, Class/Job/Role, logs, currents, travel nodes, exploration, side quests, achievements, duties, the level stop, and the full Nexus-owned job queue are mapped together. Old provider instruction pointers are never resumed.");
            foreach (MigrationIssue issue in status.Preview.Issues.Take(2))
            {
                Vector4 color = issue.Severity == MigrationIssueSeverity.Error ? NexusTheme.Red :
                    issue.Severity == MigrationIssueSeverity.Warning ? NexusTheme.Amber : NexusTheme.Muted;
                TextWrapped(color, $"• {issue.Message}");
            }
        }
        else
        {
            TextWrapped(NexusTheme.Muted, operationMessage);
        }

        bool canImport = status.Preview?.CanImport == true;
        ImGui.BeginDisabled(!canImport);
        if (ImGui.Button(importLabel, new Vector2(ButtonWidth(importLabel), 0)) && canImport)
        {
            MigrationWriteResult result = codexMigration.Import();
            operationMessage = result.Message;
            if (result.Success && result.Receipt is { } receipt && codexMigration.Status().StagedSnapshot is { } snapshot)
            {
                state.Reviewed = state.Imported = true;
                state.SourceVersion = "codex-preferences-v1";
                state.ImportedAt = receipt.CreatedAtUtc;
                state.ReceiptId = receipt.Id;
                state.ImportedItemCount = 11 + snapshot.SavedQueueSteps;
                ApplyCodexPreferences(snapshot, state, out operationMessage);
            }
        }
        ImGui.EndDisabled();

        if (status.StagedSnapshot is { } staged && (!state.Activated ||
            world.Current.Character.Value is { } current && state.AppliedCharacterKey != current.Key.ToString()))
        {
            SameLineIfFits(applyLabel);
            if (ImGui.Button(applyLabel, new Vector2(ButtonWidth(applyLabel), 0)))
                ApplyCodexPreferences(staged, state, out operationMessage);
        }

        if (status.LastReceipt is { } savedReceipt)
        {
            SameLineIfFits(rollbackLabel);
            if (ImGui.Button(rollbackLabel, new Vector2(ButtonWidth(rollbackLabel), 0)))
            {
                MigrationWriteResult result = codexMigration.Rollback(savedReceipt.Id);
                operationMessage = result.Message;
                if (result.Success)
                {
                    RestoreCodexPreferences(state);
                    state.Imported = state.ReadyForActivation = state.Activated = false;
                    state.ImportedAt = null;
                    state.ReceiptId = null;
                    state.ImportedItemCount = 0;
                    state.AppliedCharacterKey = string.Empty;
                    state.PreviousProgression = null;
                    state.PreviousProgressionQueue = null;
                    state.PreviousAtlas = null;
                    plugin.Save();
                }
            }
        }

        TextWrapped(state.Activated ? NexusTheme.Green : NexusTheme.Muted,
            state.Activated
                ? $"Preferences are active for {state.AppliedCharacterKey}; the VieriCodex source remains unchanged."
                : operationMessage);
        EndAutoPanel();
    }

    private bool ApplyCodexPreferences(
        CodexMigrationSnapshot snapshot,
        LegacyImportState state,
        out string message)
    {
        CharacterSnapshot? character = world.Current.Character.Value;
        if (character is null || !character.Key.IsKnown)
        {
            message = "The import is staged. Log in, then apply it to the current character.";
            state.ReadyForActivation = true;
            plugin.Save();
            return false;
        }

        string characterKey = character.Key.ToString();
        CharacterConfiguration configuration = plugin.Configuration.ForCharacter(characterKey);
        if (!state.Activated || !string.Equals(state.AppliedCharacterKey, characterKey, StringComparison.Ordinal))
        {
            state.PreviousProgression = Clone(configuration.Progression);
            state.PreviousProgressionQueue = Clone(configuration.ProgressionQueue);
            state.PreviousAtlas = Clone(configuration.Atlas);
        }

        configuration.Progression.AllowMainScenario = snapshot.MainScenarioQuests;
        configuration.Progression.AllowJobQuests = snapshot.CombatClassJobQuests || snapshot.RoleQuests;
        configuration.Progression.AllowHuntingLog = snapshot.HuntingLogs;
        configuration.Progression.AllowSideQuests = snapshot.SideQuests;
        configuration.Progression.AllowDuties = snapshot.RequiredMsqDungeons;
        if (snapshot.LevelStopEnabled)
            configuration.Progression.TargetLevel = snapshot.TargetLevel;
        configuration.ProgressionQueue = ProgressionQueuePolicy.Import(snapshot);
        CodexQueueStepSnapshot? matchingQueueStep = snapshot.QueueSteps.FirstOrDefault(step =>
            step.Enabled && step.ClassJobId == character.ClassJobId);
        if (matchingQueueStep is not null)
        {
            configuration.Progression.TargetLevel = matchingQueueStep.TargetLevel;
            switch (matchingQueueStep.Method)
            {
                case 0:
                    configuration.Progression.AllowHuntingLog = snapshot.QueueSettings.AutomaticUsesHuntingLog;
                    configuration.Progression.AllowSideQuests = snapshot.QueueSettings.AutomaticUsesSideQuests;
                    configuration.Progression.AllowDuties = snapshot.QueueSettings.AutomaticUsesDungeonGrind;
                    break;
                case 1:
                    configuration.Progression.AllowHuntingLog = true;
                    configuration.Progression.AllowSideQuests = configuration.Progression.AllowDuties = false;
                    break;
                case 2:
                    configuration.Progression.AllowSideQuests = true;
                    configuration.Progression.AllowHuntingLog = configuration.Progression.AllowDuties = false;
                    break;
                case 3:
                    configuration.Progression.AllowDuties = true;
                    configuration.Progression.AllowHuntingLog = configuration.Progression.AllowSideQuests = false;
                    break;
            }
        }
        configuration.Atlas.AllowAetherCurrentQuests = snapshot.AetherCurrentQuests;
        configuration.Atlas.AllowFieldAetherCurrents = snapshot.FieldAetherCurrents;
        configuration.Atlas.AllowAetheryteAttunements = snapshot.AetheryteAttunements;
        configuration.Atlas.AllowMapExploration = snapshot.MapExploration;
        configuration.Atlas.AllowAchievements = snapshot.Achievements;
        state.Activated = state.ReadyForActivation = true;
        state.AppliedCharacterKey = characterKey;
        plugin.Save();
        message = snapshot.QueueSteps.Count == 0
            ? $"Applied the verified VieriCodex preferences to {character.Name}."
            : $"Applied the verified VieriCodex preferences and all {snapshot.QueueSteps.Count} Nexus-owned queue step(s) to {character.Name}.";
        return true;
    }

    private void RestoreCodexPreferences(LegacyImportState state)
    {
        if (state.AppliedCharacterKey.Length == 0 || state.PreviousProgression is null || state.PreviousAtlas is null)
            return;
        CharacterConfiguration configuration = plugin.Configuration.ForCharacter(state.AppliedCharacterKey);
        configuration.Progression = Clone(state.PreviousProgression);
        if (state.PreviousProgressionQueue is not null)
            configuration.ProgressionQueue = Clone(state.PreviousProgressionQueue);
        configuration.Atlas = Clone(state.PreviousAtlas);
    }

    private static ProgressionDraftConfiguration Clone(ProgressionDraftConfiguration value) => new()
    {
        TargetLevel = value.TargetLevel,
        AllowMainScenario = value.AllowMainScenario,
        AllowJobQuests = value.AllowJobQuests,
        AllowHuntingLog = value.AllowHuntingLog,
        AllowSideQuests = value.AllowSideQuests,
        AllowDuties = value.AllowDuties,
        MinimumGilReserve = value.MinimumGilReserve,
    };

    private static ProgressionQueueConfiguration Clone(ProgressionQueueConfiguration value) => new()
    {
        Steps = value.Steps.Select(step => new ProgressionQueueStepConfiguration
        {
            Id = step.Id,
            Enabled = step.Enabled,
            ClassJobId = step.ClassJobId,
            TargetLevel = step.TargetLevel,
            Method = step.Method,
            FallbackPolicy = step.FallbackPolicy,
            Status = step.Status,
            StatusDetail = step.StatusDetail,
        }).ToList(),
        Settings = new ProgressionQueueSettingsConfiguration
        {
            SkipTargetsAlreadyReached = value.Settings.SkipTargetsAlreadyReached,
            AutomaticallySwitchJobs = value.Settings.AutomaticallySwitchJobs,
            AutomaticallyAdvance = value.Settings.AutomaticallyAdvance,
            ResumeAfterRestart = value.Settings.ResumeAfterRestart,
            AutomaticUsesHuntingLog = value.Settings.AutomaticUsesHuntingLog,
            AutomaticUsesSideQuests = value.Settings.AutomaticUsesSideQuests,
            AutomaticUsesDuties = value.Settings.AutomaticUsesDuties,
            OnStepFailure = value.Settings.OnStepFailure,
        },
        CurrentIndex = value.CurrentIndex,
        IsRunning = value.IsRunning,
        IsPaused = value.IsPaused,
        State = value.State,
        ActiveGoalId = value.ActiveGoalId,
        EffectiveMethod = value.EffectiveMethod,
        StatusDetail = value.StatusDetail,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static AtlasAutomationConfiguration Clone(AtlasAutomationConfiguration value) => new()
    {
        AllowAetherCurrentQuests = value.AllowAetherCurrentQuests,
        AllowFieldAetherCurrents = value.AllowFieldAetherCurrents,
        AllowAetheryteAttunements = value.AllowAetheryteAttunements,
        AllowMapExploration = value.AllowMapExploration,
        AllowAchievements = value.AllowAchievements,
    };

    private void DrawCommandCenterMigration()
    {
        const string importLabel = "Create backup and import Plugins";
        const string rollbackLabel = "Rollback Plugins import";
        CommandCenterMigrationStatus status = commandCenterMigration.Status();
        LegacyImportState state = plugin.Configuration.ForLegacyImport("deck");
        bool staged = status.LastReceipt is not null;
        string operationMessage = status.Message;

        BeginAutoPanel("PLUGIN LAUNCHER");
        NexusTheme.StatusDot(status.SourceFound ? NexusTheme.Green : NexusTheme.Muted,
            status.SourceFound ? "VieriDeck configuration located" : "Not found on this computer");
        ImGui.TextColored(NexusTheme.Gold, "Destination: Nexus Plugins");
        if (status.Preview?.Snapshot is { } snapshot)
        {
            int customCommands = snapshot.CustomCommands.Values.Sum(commands => commands.Count);
            ImGui.TextUnformatted($"{snapshot.Favorites.Count} favorite(s) • {snapshot.HiddenPlugins.Count} hidden • {customCommands} custom command(s)");
            TextWrapped(NexusTheme.Muted,
                "Plugin filters, favorites, hidden entries, preferred and custom commands, the selected plugin, layout reference, and hotkey are mapped together.");
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

        bool canImport = status.Preview?.CanImport == true;
        if (!canImport)
            ImGui.BeginDisabled();
        if (ImGui.Button(importLabel, new Vector2(ButtonWidth(importLabel), 0)) && canImport)
        {
            MigrationWriteResult result = commandCenterMigration.Import();
            operationMessage = result.Message;
            if (result.Success && result.Receipt is { } receipt)
            {
                staged = true;
                state.Reviewed = state.Imported = true;
                state.SourceVersion = "command-center-v1";
                state.ImportedAt = receipt.CreatedAtUtc;
                state.ReceiptId = receipt.Id;
                state.ImportedItemCount = status.Preview!.Snapshot!.Favorites.Count +
                                          status.Preview.Snapshot.CustomCommands.Values.Sum(commands => commands.Count);
                plugin.Save();
            }
        }
        if (!canImport)
            ImGui.EndDisabled();
        if (staged && status.LastReceipt is { } savedReceipt)
        {
            SameLineIfFits(rollbackLabel);
            if (ImGui.Button(rollbackLabel, new Vector2(ButtonWidth(rollbackLabel), 0)))
            {
                MigrationWriteResult result = commandCenterMigration.Rollback(savedReceipt.Id);
                operationMessage = result.Message;
                if (result.Success)
                {
                    state.Imported = false;
                    state.ImportedAt = null;
                    state.ReceiptId = null;
                    state.ImportedItemCount = 0;
                    plugin.Save();
                }
            }
        }
        TextWrapped(staged ? NexusTheme.Green : NexusTheme.Muted, operationMessage);
        TextWrapped(NexusTheme.Muted, staged
            ? "Verified staging plus a separate editable Nexus working copy • VieriDeck remains unchanged"
            : "Import creates a timestamped source backup before Nexus writes anything.");
        EndAutoPanel();
    }

    private void DrawPlugins()
    {
        PageHeading("Plugins", "Open installed plugins, keep favorites close, and view each plugin's commands in place.");
        CommandCenterSnapshot? settings = commandCenterMigration.WorkingSnapshot;
        if (settings is null)
        {
            BeginAutoPanel("SETUP REQUIRED");
            NexusTheme.StatusDot(NexusTheme.Amber, "The Nexus plugin page is not initialized yet");
            ImGui.TextWrapped("Import VieriDeck on the Migration page to preserve this computer's favorites, commands, filters, and hotkey.");
            if (ImGui.Button("Open Migration", new Vector2(ButtonWidth("Open Migration"), 0)))
            {
                plugin.Configuration.SelectedPage = "Migration";
                plugin.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("Start fresh", new Vector2(ButtonWidth("Start fresh"), 0)))
                commandCenterMigration.StartFresh(out commandCenterMessage);
            EndAutoPanel();
            return;
        }

        commandCenterCatalog.Refresh();

        BeginAutoPanel("QUICK ACCESS");
        if (ImGui.Button("Dalamud Plugins", new Vector2(ButtonWidth("Dalamud Plugins"), 0)))
            commandCenterMessage = commandCenterCatalog.Run("/xlplugins")
                ? "Opened Dalamud Plugins."
                : "Dalamud Plugins could not be opened.";
        SameLineIfFits("Dalamud Settings");
        if (ImGui.Button("Dalamud Settings", new Vector2(ButtonWidth("Dalamud Settings"), 0)))
            commandCenterMessage = commandCenterCatalog.Run("/xlsettings")
                ? "Opened Dalamud Settings."
                : "Dalamud Settings could not be opened.";
        SameLineIfFits("Refresh plugin list");
        if (ImGui.Button("Refresh plugin list", new Vector2(ButtonWidth("Refresh plugin list"), 0)))
        {
            commandCenterCatalog.Refresh(true);
            commandCenterMessage = "Refreshed the installed plugin list and commands.";
        }
        string overlayLabel = plugin.Configuration.ShowOperationsOverlay
            ? "Hide Nexus Overlay"
            : "Show Nexus Overlay";
        SameLineIfFits(overlayLabel);
        if (ImGui.Button(overlayLabel, new Vector2(ButtonWidth(overlayLabel), 0)))
        {
            plugin.Configuration.ShowOperationsOverlay = !plugin.Configuration.ShowOperationsOverlay;
            plugin.Save();
            commandCenterMessage = plugin.Configuration.ShowOperationsOverlay
                ? "Opened the Nexus operations overlay."
                : "Closed the Nexus operations overlay.";
        }
        SameLineIfFits("Nexus Settings");
        if (ImGui.Button("Nexus Settings", new Vector2(ButtonWidth("Nexus Settings"), 0)))
        {
            plugin.Configuration.SelectedPage = "Settings";
            plugin.Save();
        }
        EndAutoPanel();

        BeginAutoPanel("FIND PLUGINS");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###plugin-search", "Search plugins or commands", ref commandCenterSearch, 180);
        bool showUnloaded = settings.ShowUnloadedPlugins;
        if (ImGui.Checkbox("Show disabled plugins", ref showUnloaded))
            UpdateCommandCenter(value => value with { ShowUnloadedPlugins = showUnloaded });
        SameLineIfFits("Hide plugins without actions");
        bool hideEmpty = settings.HidePluginsWithoutActions;
        if (ImGui.Checkbox("Hide plugins without actions", ref hideEmpty))
            UpdateCommandCenter(value => value with { HidePluginsWithoutActions = hideEmpty });
        if (settings.HiddenPlugins.Count > 0)
        {
            string restoreLabel = $"Restore {settings.HiddenPlugins.Count} hidden";
            SameLineIfFits(restoreLabel);
            if (ImGui.Button(restoreLabel))
                UpdateCommandCenter(value => value with { HiddenPlugins = [] });
        }
        EndAutoPanel();

        HashSet<string> favorites = settings.Favorites.ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> hidden = settings.HiddenPlugins.ToHashSet(StringComparer.OrdinalIgnoreCase);
        CommandCenterEntry[] visible = commandCenterCatalog.Entries
            .Where(entry => entry.Id != "__dalamud")
            .Where(entry => !hidden.Contains(entry.Id))
            .Where(entry => showUnloaded || entry.IsLoaded)
            .Where(entry => !hideEmpty || CanOpen(entry, settings) || CommandsFor(entry, settings).Count > 0)
            .Where(entry => string.IsNullOrWhiteSpace(commandCenterSearch) ||
                            entry.Name.Contains(commandCenterSearch, StringComparison.OrdinalIgnoreCase) ||
                            entry.Description.Contains(commandCenterSearch, StringComparison.OrdinalIgnoreCase) ||
                            CommandsFor(entry, settings).Any(command => command.Command.Contains(commandCenterSearch, StringComparison.OrdinalIgnoreCase) ||
                                                                       command.Help.Contains(commandCenterSearch, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        PluginPageGroups groups = PluginPagePolicy.Group(visible.Select(entry => entry.Id).ToArray(), settings.Favorites);
        Dictionary<string, CommandCenterEntry> byId = visible.ToDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase);
        CommandCenterEntry[] favoriteEntries = groups.Favorites.Select(id => byId[id]).ToArray();
        BeginAutoPanel("★ FAVORITES");
        ImGui.TextDisabled($"{favoriteEntries.Length} plugin{(favoriteEntries.Length == 1 ? string.Empty : "s")}");
        bool favoritesOnly = settings.OnlyShowFavorites;
        if (ImGui.Checkbox("Only show favorites", ref favoritesOnly))
            UpdateCommandCenter(value => value with { OnlyShowFavorites = favoritesOnly });
        if (favoriteEntries.Length == 0)
            ImGui.TextDisabled("Select ☆ on any plugin below to pin it here.");
        foreach (CommandCenterEntry entry in favoriteEntries)
            DrawPluginCard(entry, settings, favorites);
        EndAutoPanel();

        if (!favoritesOnly)
        {
            CommandCenterEntry[] remaining = groups.AllOtherPlugins.Select(id => byId[id]).ToArray();
            BeginAutoPanel("ALL PLUGINS");
            NexusTheme.StatusDot(NexusTheme.Green, $"{remaining.Length} plugin{(remaining.Length == 1 ? string.Empty : "s")}");
            if (remaining.Length == 0)
                ImGui.TextDisabled("No other plugins match the current filters.");
            foreach (CommandCenterEntry entry in remaining)
                DrawPluginCard(entry, settings, favorites);
            EndAutoPanel();
        }

        if (!string.IsNullOrWhiteSpace(commandCenterMessage))
            TextWrapped(NexusTheme.Green, commandCenterMessage);

        BeginAutoPanel("PLUGIN PAGE BEHAVIOR");
        bool closeAfter = settings.CloseAfterOpeningPlugin;
        if (ImGui.Checkbox("Close Nexus after opening a plugin", ref closeAfter))
            UpdateCommandCenter(value => value with { CloseAfterOpeningPlugin = closeAfter });
        bool hotkeyEnabled = settings.HotkeyEnabled;
        if (ImGui.Checkbox("Enable Plugins page hotkey", ref hotkeyEnabled))
            UpdateCommandCenter(value => value with { HotkeyEnabled = hotkeyEnabled });
        ImGui.TextUnformatted($"Hotkey: {plugin.CommandCenterHotkeyName}");
        if (plugin.IsCapturingCommandCenterHotkey)
        {
            TextWrapped(NexusTheme.Amber, "Press the new key combination. Press Escape to cancel.");
            if (ImGui.Button("Cancel key capture"))
                plugin.CancelCommandCenterHotkeyCapture();
        }
        else if (ImGui.Button("Set hotkey"))
            plugin.StartCommandCenterHotkeyCapture();
        ImGui.SameLine();
        if (ImGui.Button("Clear hotkey"))
            plugin.ClearCommandCenterHotkey();
        bool exact = settings.ExactModifiers;
        if (ImGui.Checkbox("Require exact modifier keys", ref exact))
            UpdateCommandCenter(value => value with { ExactModifiers = exact });
        if (settings.Hotkey != 0 && !settings.HotkeyControl && !settings.HotkeyShift && !settings.HotkeyAlt)
            TextWrapped(NexusTheme.Amber, "This unmodified key can also trigger while you are typing in chat.");
        TextWrapped(NexusTheme.Muted, "Open this page directly with /nexus plugins. The older /nexus commands alias still works.");
        EndAutoPanel();
    }

    private void DrawPluginCard(CommandCenterEntry entry, CommandCenterSnapshot settings, HashSet<string> favorites)
    {
        ImGui.PushID("plugin-" + entry.Id);
        ImGui.Separator();
        bool favorite = favorites.Contains(entry.Id);
        ImGui.PushStyleColor(ImGuiCol.Text, favorite
            ? new Vector4(1f, .78f, .18f, 1f)
            : new Vector4(.72f, .72f, .72f, 1f));
        if (ImGui.SmallButton(favorite ? "★" : "☆"))
            ToggleCommandCenterFavorite(entry.Id, favorite);
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(favorite ? "Remove from Favorites" : "Add to Favorites");
        ImGui.SameLine();
        ImGui.TextColored(entry.IsLoaded ? NexusTheme.Green : NexusTheme.Muted, "●");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(entry.IsLoaded ? $"Loaded • {entry.Version}" : $"Disabled • {entry.Version}");
        ImGui.SameLine();
        ImGui.TextUnformatted(entry.Name);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup("###plugin-actions");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Right-click for more plugin actions.");
        DrawPluginActionsPopup(entry, settings);

        bool canOpen = CanOpen(entry, settings);
        if (!canOpen)
            ImGui.BeginDisabled();
        if (ImGui.SmallButton(canOpen ? "Show/Hide Settings" : "No plugin window") && canOpen)
        {
            commandCenterMessage = OpenCommandCenterEntry(entry, settings);
            if (settings.CloseAfterOpeningPlugin &&
                (commandCenterMessage.StartsWith("Opened", StringComparison.Ordinal) ||
                 commandCenterMessage.StartsWith("Ran", StringComparison.Ordinal)))
                IsOpen = false;
        }
        if (!canOpen)
            ImGui.EndDisabled();

        IReadOnlyList<CommandCenterCommand> commands = CommandsFor(entry, settings);
        bool showCommands = settings.CommandPanelOpen &&
                            settings.SelectedPluginId.Equals(entry.Id, StringComparison.OrdinalIgnoreCase);
        SameLineIfFits(showCommands ? "Hide Commands" : $"Show Commands ({commands.Count})");
        if (ImGui.SmallButton(showCommands ? "Hide Commands" : $"Show Commands ({commands.Count})"))
        {
            PluginCommandPanelState state = PluginPagePolicy.ToggleCommands(
                settings.CommandPanelOpen,
                settings.SelectedPluginId,
                entry.Id);
            showCommands = state.IsOpen;
            UpdateCommandCenter(value => value with
            {
                SelectedPluginId = state.SelectedPluginId,
                CommandPanelOpen = state.IsOpen,
            });
        }
        if (!string.IsNullOrWhiteSpace(entry.Description))
            TextWrapped(NexusTheme.Muted, entry.Description);

        if (showCommands)
            DrawPluginCommands(entry, settings, commands);
        ImGui.PopID();
    }

    private void DrawPluginCommands(
        CommandCenterEntry entry,
        CommandCenterSnapshot settings,
        IReadOnlyList<CommandCenterCommand> commands)
    {
        ImGui.Indent(24f);
        ImGui.TextColored(NexusTheme.Gold, $"{entry.Name.ToUpperInvariant()} COMMANDS");
        ImGui.Separator();
        bool canSettings = entry.Plugin is { IsLoaded: true, HasConfigUi: true };
        if (!canSettings)
            ImGui.BeginDisabled();
        if (ImGui.SmallButton("Open Plugin Settings") && canSettings)
        {
            commandCenterCatalog.Open(entry, settings: true, out _);
            if (settings.CloseAfterOpeningPlugin)
                IsOpen = false;
        }
        if (!canSettings)
            ImGui.EndDisabled();
        ImGui.TextDisabled("Left-click runs a command. Right-click copies it.");
        if (commands.Count == 0)
            ImGui.TextDisabled("No slash commands were published, documented, or saved for this plugin.");
        string? preferred = settings.PreferredCommands.GetValueOrDefault(entry.Id);
        foreach (CommandCenterCommand command in commands)
        {
            ImGui.PushID("run-" + command.Command);
            bool isPreferred = string.Equals(preferred, command.Command, StringComparison.OrdinalIgnoreCase);
            if (ImGui.Button(command.Command, new Vector2(Math.Min(360, Math.Max(130, ButtonWidth(command.Command))), 0)))
                commandCenterMessage = commandCenterCatalog.Run(command.Command)
                    ? $"Ran {command.Command}."
                    : $"Command unavailable: {command.Command}";
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.SetClipboardText(command.Command);
                commandCenterMessage = $"Copied {command.Command}.";
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Left-click: run command\nRight-click: copy command");
            ImGui.SameLine();
            if (ImGui.SmallButton(isPreferred ? "Quick action" : "Make quick"))
                SetPreferredCommand(entry.Id, command.Command);
            ImGui.TextWrapped(CommandDescription(command));
            ImGui.PopID();
        }
        if (ImGui.CollapsingHeader("Custom commands"))
        {
            TextWrapped(NexusTheme.Muted,
                "Add a command when a plugin does not publish it through Dalamud. Include required arguments in the command itself.");
            ImGui.SetNextItemWidth(Math.Min(430, ImGui.GetContentRegionAvail().X));
            ImGui.InputTextWithHint("###new-command", "/command or /command subcommand", ref commandCenterCustomCommand, 1_024);
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("###new-command-description", "Optional description", ref commandCenterCustomDescription, 2_048);
            if (ImGui.Button("Add custom command") && AddCustomCommand(entry.Id))
            {
                commandCenterCustomCommand = string.Empty;
                commandCenterCustomDescription = string.Empty;
            }
            if (settings.CustomCommands.TryGetValue(entry.Id, out IReadOnlyList<CommandCenterCustomCommand>? custom))
            {
                foreach (CommandCenterCustomCommand item in custom)
                {
                    ImGui.TextUnformatted(item.Command);
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Remove###remove-{item.Command}"))
                        RemoveCustomCommand(entry.Id, item.Command);
                }
            }
        }
        ImGui.Unindent(24f);
    }

    private void DrawPluginActionsPopup(CommandCenterEntry entry, CommandCenterSnapshot settings)
    {
        if (!ImGui.BeginPopup("###plugin-actions"))
            return;
        bool canSettings = entry.Plugin is { IsLoaded: true, HasConfigUi: true };
        if (ImGui.MenuItem("Open settings", string.Empty, false, canSettings))
        {
            commandCenterCatalog.Open(entry, settings: true, out _);
            if (settings.CloseAfterOpeningPlugin)
                IsOpen = false;
        }
        if (ImGui.MenuItem("Hide from list"))
            HideCommandCenterEntry(entry.Id);
        if (ImGui.MenuItem("Copy internal name"))
        {
            ImGui.SetClipboardText(entry.Id);
            commandCenterMessage = $"Copied {entry.Id}.";
        }
        ImGui.EndPopup();
    }

    private static string CommandDescription(CommandCenterCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Help))
            return "No description was supplied by this plugin.";
        return command.Help.Contains('\n')
            ? "Base command. Its documented subcommands are listed separately."
            : command.Help;
    }

    private IReadOnlyList<CommandCenterCommand> CommandsFor(CommandCenterEntry entry, CommandCenterSnapshot settings)
    {
        IEnumerable<CommandCenterCommand> commands = entry.Commands;
        if (settings.CustomCommands.TryGetValue(entry.Id, out IReadOnlyList<CommandCenterCustomCommand>? custom))
            commands = commands.Concat(custom.Select(item => new CommandCenterCommand(item.Command, item.Description, IsCustom: true)));
        return commands.GroupBy(command => command.Command, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(command => command.IsCustom).First())
            .OrderByDescending(command => string.Equals(settings.PreferredCommands.GetValueOrDefault(entry.Id), command.Command, StringComparison.OrdinalIgnoreCase))
            .ThenBy(command => command.Command, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool HasPluginWindow(CommandCenterEntry entry) => entry.Id == "__dalamud" ||
        entry.Plugin is { IsLoaded: true } plugin && (plugin.HasMainUi || plugin.HasConfigUi);

    private bool CanOpen(CommandCenterEntry entry, CommandCenterSnapshot settings) =>
        HasPluginWindow(entry) || QuickCommand(entry, settings) is not null;

    private string OpenCommandCenterEntry(CommandCenterEntry entry, CommandCenterSnapshot settings)
    {
        if (HasPluginWindow(entry))
            return commandCenterCatalog.Open(entry, settings: false, out bool closed)
                ? closed ? $"Closed {entry.Name}." : $"Opened {entry.Name}."
                : $"{entry.Name} did not expose a window.";
        string? command = QuickCommand(entry, settings);
        return command is not null && commandCenterCatalog.Run(command)
            ? $"Ran {command} for {entry.Name}."
            : $"{entry.Name} has no available quick action.";
    }

    private string? QuickCommand(CommandCenterEntry entry, CommandCenterSnapshot settings)
    {
        IReadOnlyList<CommandCenterCommand> commands = CommandsFor(entry, settings);
        if (settings.PreferredCommands.TryGetValue(entry.Id, out string? preferred) &&
            commands.Any(command => command.Command.Equals(preferred, StringComparison.OrdinalIgnoreCase)))
            return preferred;
        CommandCenterCommand[] native = entry.Commands.Where(command => !command.IsDerived).ToArray();
        string normalizedId = NormalizeCommandCenterName(entry.Id);
        string normalizedName = NormalizeCommandCenterName(entry.Name);
        CommandCenterCommand? exact = native.FirstOrDefault(command =>
            NormalizeCommandCenterName(command.Command.TrimStart('/')) == normalizedId ||
            NormalizeCommandCenterName(command.Command.TrimStart('/')) == normalizedName);
        if (exact is not null)
            return exact.Command;
        return native.Where(command => command.Help.Contains('\n') ||
                                       command.Help.Contains("open", StringComparison.OrdinalIgnoreCase) ||
                                       command.Help.Contains("toggle", StringComparison.OrdinalIgnoreCase))
                   .OrderBy(command => command.Command.Length)
                   .FirstOrDefault()?.Command
               ?? native.OrderBy(command => command.Command.Length).FirstOrDefault()?.Command;
    }

    private static string NormalizeCommandCenterName(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private void UpdateCommandCenter(Func<CommandCenterSnapshot, CommandCenterSnapshot> change) =>
        commandCenterMigration.Update(change, out commandCenterMessage);

    private void ToggleCommandCenterFavorite(string id, bool currentlyFavorite) => UpdateCommandCenter(snapshot =>
    {
        HashSet<string> values = snapshot.Favorites.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (currentlyFavorite)
            values.Remove(id);
        else
            values.Add(id);
        return snapshot with { Favorites = values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray() };
    });

    private void HideCommandCenterEntry(string id) => UpdateCommandCenter(snapshot =>
    {
        HashSet<string> values = snapshot.HiddenPlugins.ToHashSet(StringComparer.OrdinalIgnoreCase);
        values.Add(id);
        return snapshot with { HiddenPlugins = values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray() };
    });

    private void SetPreferredCommand(string id, string command) => UpdateCommandCenter(snapshot =>
    {
        Dictionary<string, string> values = snapshot.PreferredCommands
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        values[id] = command;
        return snapshot with { PreferredCommands = values };
    });

    private bool AddCustomCommand(string id)
    {
        string command = commandCenterCustomCommand.Trim();
        if (command.Length == 0)
            return false;
        if (command[0] != '/')
            command = "/" + command;
        string normalized = command;
        return commandCenterMigration.Update(snapshot =>
        {
            Dictionary<string, IReadOnlyList<CommandCenterCustomCommand>> groups = snapshot.CustomCommands
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            List<CommandCenterCustomCommand> commands = groups.GetValueOrDefault(id)?.ToList() ?? [];
            if (!commands.Any(item => item.Command.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                commands.Add(new(normalized, commandCenterCustomDescription.Trim()));
            groups[id] = commands;
            return snapshot with { CustomCommands = groups };
        }, out commandCenterMessage);
    }

    private void RemoveCustomCommand(string id, string command) => UpdateCommandCenter(snapshot =>
    {
        Dictionary<string, IReadOnlyList<CommandCenterCustomCommand>> groups = snapshot.CustomCommands
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (!groups.TryGetValue(id, out IReadOnlyList<CommandCenterCustomCommand>? existing))
            return snapshot;
        CommandCenterCustomCommand[] remaining = existing
            .Where(item => !item.Command.Equals(command, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (remaining.Length == 0)
            groups.Remove(id);
        else
            groups[id] = remaining;
        return snapshot with { CustomCommands = groups };
    });

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
            "Nexus owns maintenance order, item protection, travel, interaction, resource locking, Stop, timeouts, and completion. It takes you to the correct Grand Company officer or inn before using AutoRetainer only for turn-in mechanics and Glamour Log only for eligible storage mechanics. AutoDuty is not called.");

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

            if (ImGui.Button("Desynthesize eligible items"))
                maintenanceRuntime.Start(NexusMaintenanceOperation.Desynthesize, out maintenanceMessage);
            SameLineIfFits("Grand Company turn-ins");
            if (ImGui.Button("Grand Company turn-ins"))
                maintenanceRuntime.Start(NexusMaintenanceOperation.GrandCompanyTurnIn, out maintenanceMessage);

            if (ImGui.Button("Entrust eligible storage items"))
                maintenanceRuntime.StartStorage(out maintenanceMessage);

            if (ImGui.Button(protectedSalePreview is null ? "Review protected selling" : "Refresh protected selling"))
            {
                protectedSalePreview = maintenanceRuntime.PreviewProtectedSelling();
                maintenanceMessage = protectedSalePreview.Summary;
            }
        }
        if (!string.IsNullOrWhiteSpace(maintenanceMessage))
            TextWrapped(NexusTheme.Cyan, maintenanceMessage);
        if (!status.IsActive && protectedSalePreview is { } sale)
        {
            ImGui.Spacing();
            ImGui.TextColored(NexusTheme.Gold, "PROTECTED SELLING REVIEW");
            TextWrapped(sale.Items.Count == 0 ? NexusTheme.Muted : NexusTheme.Amber, sale.Summary);
            foreach (NexusInventoryItemSnapshot item in sale.Items.Take(20))
                ImGui.BulletText($"{item.Name} ×{item.Quantity} — {item.VendorPrice:N0} gil each");
            if (sale.Items.Count > 20)
                TextWrapped(NexusTheme.Muted, $"…and {sale.Items.Count - 20} more exact slot(s).");
            bool canApprove = sale.Items.Count > 0;
            if (!canApprove)
                ImGui.BeginDisabled();
            if (ImGui.Button("Approve exact list at open NPC shop", new Vector2(-1, 0)))
            {
                maintenanceRuntime.StartApprovedSelling(sale.Signature, out maintenanceMessage);
                protectedSalePreview = null;
            }
            if (!canApprove)
                ImGui.EndDisabled();
            TextWrapped(NexusTheme.Muted,
                "EXP-bonus equipment, collectables, gearset items, tradeable zero-spiritbond gear, and anything that changes after review are always rejected.");
        }

        if (profile?.Maintenance.InDutyMaintenance == true)
            TextWrapped(NexusTheme.Amber,
                $"In-duty withdrawal policy is preserved at {profile.Maintenance.InDutyDurabilityPercent}% durability plus configured inventory pressure. It remains inactive until stock AutoDuty exposes a safe leave/resume contract; Nexus will not pretend Stop alone withdrew from a duty.");
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
        ProgressionQueueConfiguration queue = characterConfiguration.ProgressionQueue;
        BeginAutoPanel("JOB QUEUE");
        NexusTheme.StatusDot(queue.IsRunning ? NexusTheme.Green : queue.IsPaused ? NexusTheme.Amber : NexusTheme.Muted,
            queue.IsRunning ? $"Running step {queue.CurrentIndex + 1} of {queue.Steps.Count}" :
            queue.IsPaused ? "Paused" : $"{queue.Steps.Count} configured step(s)");
        TextWrapped(NexusTheme.Muted, queue.StatusDetail);
        if (ImGui.Button("Open Job Queue", new Vector2(-1, 0)))
        {
            plugin.Configuration.SelectedPage = "Queue";
            plugin.Save();
        }
        EndAutoPanel();
        if (draftConfiguration.TargetLevel == 0)
        {
            draftConfiguration.TargetLevel = Math.Min(
                ReachJobLevelPlanner.MaximumSupportedLevel,
                Math.Max(1, character.Level + 2));
            plugin.Save();
        }

        BeginAutoPanel("REACH JOB LEVEL");
        NexusTheme.StatusDot(NexusTheme.Cyan,
            $"{character.Name} • {ClassJobDisplay.Label(character)} • Level {character.Level}");
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
        if (ImGui.BeginTable("###LevelingMethods", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            bool mainScenario = draftConfiguration.AllowMainScenario;
            if (ImGui.Checkbox("Main Scenario quests", ref mainScenario))
            {
                draftConfiguration.AllowMainScenario = mainScenario;
                changed = true;
            }
            ImGui.TableNextColumn();
            bool jobQuests = draftConfiguration.AllowJobQuests;
            if (ImGui.Checkbox("Class/Job/Role Quests", ref jobQuests))
            {
                draftConfiguration.AllowJobQuests = jobQuests;
                changed = true;
            }
            ImGui.TableNextColumn();
            bool duties = draftConfiguration.AllowDuties;
            if (ImGui.Checkbox("Duties", ref duties))
            {
                draftConfiguration.AllowDuties = duties;
                changed = true;
            }
            ImGui.TableNextColumn();
            bool huntingLog = draftConfiguration.AllowHuntingLog;
            if (ImGui.Checkbox("Hunting Log", ref huntingLog))
            {
                draftConfiguration.AllowHuntingLog = huntingLog;
                changed = true;
            }
            ImGui.TableNextColumn();
            bool sideQuests = draftConfiguration.AllowSideQuests;
            if (ImGui.Checkbox("General side quests", ref sideQuests))
            {
                draftConfiguration.AllowSideQuests = sideQuests;
                changed = true;
            }
            ImGui.EndTable();
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
                progressionRuntime.CurrentMetrics.Gil,
                draftConfiguration.AllowMainScenario),
            providers.Questing,
            providers.Duties,
            progressionRuntime.IsHuntingLogReady,
            progressionRuntime.HuntingLogReadinessDetail);
        DrawProgressionRuntime(plan, character, draftConfiguration);
        if (ImGui.CollapsingHeader("Provider details###ProgressionProviders"))
            DrawProgressionProviders(providers);
        if (ImGui.CollapsingHeader("Plan details###ProgressionPlan"))
            DrawProgressionPlan(plan);
    }

    private void DrawProgressionQueue()
    {
        PageHeading("Job Queue", "Build an ordered multi-job leveling queue. Nexus owns the targets and verification; stock providers perform bounded work.");

        CharacterSnapshot? character = world.Current.Character.Value;
        if (character is null || !character.Key.IsKnown)
        {
            BeginAutoPanel("JOB QUEUE");
            NexusTheme.StatusDot(NexusTheme.Muted, "Waiting for the current character");
            TextWrapped(NexusTheme.Muted, "Log in to load this character's Nexus-owned queue.");
            EndAutoPanel();
            return;
        }

        CharacterConfiguration owner = plugin.Configuration.ForCharacter(character.Key.ToString());
        ProgressionQueueConfiguration queue = owner.ProgressionQueue;
        ProgressionQueuePolicy.Normalize(queue);
        ProgressionQueueStepConfiguration? current = queue.CurrentIndex >= 0 && queue.CurrentIndex < queue.Steps.Count
            ? queue.Steps[queue.CurrentIndex]
            : null;

        BeginAutoPanel("QUEUE STATUS");
        Vector4 queueColor = queue.IsRunning ? NexusTheme.Green : queue.IsPaused ? NexusTheme.Amber :
            queue.State == ProgressionQueueRuntimeState.Completed ? NexusTheme.Green : NexusTheme.Muted;
        string status = queue.IsRunning ? "Running" : queue.IsPaused ? "Paused" :
            queue.State == ProgressionQueueRuntimeState.Completed ? "Complete" : "Stopped";
        NexusTheme.StatusDot(queueColor, current is null
            ? $"{status} • {queue.Steps.Count} configured step(s)"
            : $"{status} • Step {queue.CurrentIndex + 1}/{queue.Steps.Count} • {progressionQueue.JobLabel(current.ClassJobId)}");
        TextWrapped(queueColor, queue.StatusDetail);
        if (current is not null)
            ImGui.TextUnformatted($"Level {progressionQueue.Level(current.ClassJobId)} → {current.TargetLevel} • {QueueMethodName(queue.EffectiveMethod ?? current.Method)}");

        if (!queue.IsRunning && !queue.IsPaused)
        {
            bool canStart = queue.Steps.Any(step => step.Enabled) && progressionQueue.IsFastJobSwitcherLoaded;
            if (!canStart)
                ImGui.BeginDisabled();
            if (ImGui.Button("Start Queue"))
                progressionMessage = progressionQueue.Start(character).Message;
            if (!canStart)
                ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Reset Progress"))
                progressionQueue.Reset(character);
        }
        else if (queue.IsPaused)
        {
            if (ImGui.Button("Resume Queue"))
                progressionMessage = progressionQueue.Resume(character).Message;
            ImGui.SameLine();
            if (ImGui.Button("Stop Queue"))
                progressionMessage = progressionQueue.Stop(character).Message;
        }
        else if (ImGui.Button("Stop Queue"))
            progressionMessage = progressionQueue.Stop(character).Message;
        if (!string.IsNullOrWhiteSpace(progressionMessage))
            TextWrapped(NexusTheme.Cyan, progressionMessage);
        EndAutoPanel();

        BeginAutoPanel("FAST JOB SWITCHER");
        NexusTheme.StatusDot(progressionQueue.IsFastJobSwitcherLoaded ? NexusTheme.Green : NexusTheme.Red,
            progressionQueue.IsFastJobSwitcherLoaded
                ? "Ready • Nexus verifies every requested job before advancing"
                : progressionQueue.IsFastJobSwitcherInstalled
                    ? "Installed but disabled"
                    : "Required dependency is missing");
        TextWrapped(NexusTheme.Muted,
            "Nexus uses the same lower-case class/job slash commands proven in VieriCodex, then waits for the game to confirm the equipped job.");
        if (!progressionQueue.IsFastJobSwitcherLoaded)
        {
            DependencyStatus dependency = dependencies.Snapshot().First(item => item.Definition.Id == "fast-job-switcher");
            if (ImGui.Button(dependency.Health == DependencyHealth.Missing ? "Install Fast Job Switcher" : "Open Installed Plugins"))
                dependencies.OpenInstaller(dependency);
        }
        EndAutoPanel();

        if (ImGui.CollapsingHeader("Queue settings###ProgressionQueueSettings"))
        {
            BeginAutoPanel("AUTOMATION");
            ProgressionQueueSettingsConfiguration settings = queue.Settings;
            bool changed = QueueCheckbox("Skip targets already reached", settings.SkipTargetsAlreadyReached,
                value => settings.SkipTargetsAlreadyReached = value);
            changed |= QueueCheckbox("Automatically switch jobs", settings.AutomaticallySwitchJobs,
                value => settings.AutomaticallySwitchJobs = value);
            changed |= QueueCheckbox("Automatically advance", settings.AutomaticallyAdvance,
                value => settings.AutomaticallyAdvance = value);
            changed |= QueueCheckbox("Resume safely after reload", settings.ResumeAfterRestart,
                value => settings.ResumeAfterRestart = value);
            ImGui.Spacing();
            ImGui.TextColored(NexusTheme.Gold, "AUTOMATIC / SMART MAY USE");
            changed |= QueueCheckbox("Hunting Log", settings.AutomaticUsesHuntingLog,
                value => settings.AutomaticUsesHuntingLog = value);
            changed |= QueueCheckbox("Side Quests", settings.AutomaticUsesSideQuests,
                value => settings.AutomaticUsesSideQuests = value);
            changed |= QueueCheckbox("Duties", settings.AutomaticUsesDuties,
                value => settings.AutomaticUsesDuties = value);
            ImGui.SetNextItemWidth(180f);
            string failureName = QueueFallbackName(settings.OnStepFailure);
            if (ImGui.BeginCombo("On step failure", failureName))
            {
                foreach (ProgressionQueueFallbackPolicy policy in new[]
                         {
                             ProgressionQueueFallbackPolicy.PauseQueue,
                             ProgressionQueueFallbackPolicy.StopQueue,
                             ProgressionQueueFallbackPolicy.SkipStep,
                         })
                {
                    if (ImGui.Selectable(QueueFallbackName(policy), settings.OnStepFailure == policy))
                    {
                        settings.OnStepFailure = policy;
                        changed = true;
                    }
                }
                ImGui.EndCombo();
            }
            if (changed)
                plugin.Save();
            EndAutoPanel();
        }

        NexusTheme.SectionTitle("Queue steps");
        TextWrapped(NexusTheme.Muted,
            "Steps run top to bottom. Each target is verified from the game's permanent job level before Nexus advances.");
        for (int index = 0; index < queue.Steps.Count; index++)
            DrawProgressionQueueStep(character, queue, index);

        if (ImGui.Button("Add Current Job"))
        {
            queue.Steps.Add(new ProgressionQueueStepConfiguration
            {
                ClassJobId = character.ClassJobId,
                TargetLevel = Math.Min(ReachJobLevelPlanner.MaximumSupportedLevel, Math.Max(character.Level + 1, 2)),
            });
            plugin.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Add Job"))
        {
            NexusClassJob defaultJob = progressionQueue.CombatJobs.FirstOrDefault(job => job.Level > 0)
                ?? new NexusClassJob(character.ClassJobId, character.ClassJobName, character.ClassJobAbbreviation, character.Level);
            queue.Steps.Add(new ProgressionQueueStepConfiguration
            {
                ClassJobId = defaultJob.Id,
                TargetLevel = Math.Min(ReachJobLevelPlanner.MaximumSupportedLevel, Math.Max(defaultJob.Level + 1, 2)),
            });
            plugin.Save();
        }
        ImGui.SameLine();
        if (!confirmClearProgressionQueue)
        {
            if (ImGui.Button("Clear Queue..."))
                confirmClearProgressionQueue = true;
        }
        else
        {
            ImGui.TextColored(NexusTheme.Red, "Clear every queue step?");
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                progressionQueue.Stop(character);
                queue.Steps.Clear();
                queue.CurrentIndex = -1;
                queue.State = ProgressionQueueRuntimeState.Stopped;
                queue.StatusDetail = "Queue cleared.";
                confirmClearProgressionQueue = false;
                plugin.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel###CancelClearProgressionQueue"))
                confirmClearProgressionQueue = false;
        }
    }

    private void DrawProgressionQueueStep(
        CharacterSnapshot character,
        ProgressionQueueConfiguration queue,
        int index)
    {
        ProgressionQueueStepConfiguration step = queue.Steps[index];
        bool active = queue.IsRunning && queue.CurrentIndex == index;
        bool ownsCurrent = queue.CurrentIndex == index && (queue.IsRunning || queue.IsPaused);
        BeginAutoPanel($"STEP {index + 1} • {progressionQueue.JobLabel(step.ClassJobId).ToUpperInvariant()}");
        NexusTheme.StatusDot(active ? NexusTheme.Green :
                step.Status is ProgressionQueueStepStatus.Completed or ProgressionQueueStepStatus.AlreadySatisfied
                    ? NexusTheme.Green
                    : step.Status == ProgressionQueueStepStatus.Blocked ? NexusTheme.Red : NexusTheme.Muted,
            $"{step.Status} • Level {progressionQueue.Level(step.ClassJobId)} → {step.TargetLevel}");
        bool changed = false;
        bool enabled = step.Enabled;
        if (ownsCurrent)
            ImGui.BeginDisabled();
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            step.Enabled = enabled;
            step.Status = enabled ? ProgressionQueueStepStatus.Waiting : ProgressionQueueStepStatus.Disabled;
            changed = true;
        }

        ImGui.SetNextItemWidth(Math.Max(220f, ImGui.GetContentRegionAvail().X * .42f));
        if (ImGui.BeginCombo("Class / Job", progressionQueue.JobLabel(step.ClassJobId)))
        {
            foreach (NexusClassJob job in progressionQueue.CombatJobs)
            {
                if (job.Level == 0)
                    continue;
                if (ImGui.Selectable($"{job.Name} ({job.Abbreviation}) • Level {job.Level}", step.ClassJobId == job.Id))
                {
                    step.ClassJobId = job.Id;
                    step.TargetLevel = Math.Max(step.TargetLevel, Math.Min(100, job.Level + 1));
                    step.Status = ProgressionQueueStepStatus.Waiting;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }

        int target = step.TargetLevel;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderInt("Target level", ref target, 1, ReachJobLevelPlanner.MaximumSupportedLevel))
        {
            step.TargetLevel = target;
            changed = true;
        }

        ImGui.SetNextItemWidth(220f);
        if (ImGui.BeginCombo("Leveling method", QueueMethodName(step.Method)))
        {
            foreach (ProgressionQueueMethod method in Enum.GetValues<ProgressionQueueMethod>())
            {
                if (ImGui.Selectable(QueueMethodName(method), step.Method == method))
                {
                    step.Method = method;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(220f);
        if (ImGui.BeginCombo("If this method cannot run", QueueFallbackName(step.FallbackPolicy)))
        {
            foreach (ProgressionQueueFallbackPolicy policy in Enum.GetValues<ProgressionQueueFallbackPolicy>())
            {
                if (policy == ProgressionQueueFallbackPolicy.Duties && step.TargetLevel < 15)
                    continue;
                if (ImGui.Selectable(QueueFallbackName(policy), step.FallbackPolicy == policy))
                {
                    step.FallbackPolicy = policy;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        if (ownsCurrent)
            ImGui.EndDisabled();

        if (!string.IsNullOrWhiteSpace(step.StatusDetail))
            TextWrapped(NexusTheme.Muted, step.StatusDetail);

        if (ImGui.Button("Move Up") && index > 0)
        {
            queue.Steps.RemoveAt(index);
            queue.Steps.Insert(index - 1, step);
            if (queue.CurrentIndex == index) queue.CurrentIndex--;
            else if (queue.CurrentIndex == index - 1) queue.CurrentIndex++;
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("Move Down") && index < queue.Steps.Count - 1)
        {
            queue.Steps.RemoveAt(index);
            queue.Steps.Insert(index + 1, step);
            if (queue.CurrentIndex == index) queue.CurrentIndex++;
            else if (queue.CurrentIndex == index + 1) queue.CurrentIndex--;
            changed = true;
        }
        ImGui.SameLine();
        if (!queue.IsRunning && !queue.IsPaused && ImGui.Button("Run From Here"))
            progressionMessage = progressionQueue.Start(character, index).Message;
        ImGui.SameLine();
        if (pendingDeleteQueueStepId != step.Id)
        {
            if (!ownsCurrent && ImGui.Button("Remove..."))
                pendingDeleteQueueStepId = step.Id;
        }
        else
        {
            ImGui.TextColored(NexusTheme.Red, "Remove this step?");
            ImGui.SameLine();
            if (ImGui.Button("Remove###ConfirmRemoveQueueStep"))
            {
                queue.Steps.RemoveAt(index);
                if (queue.CurrentIndex > index) queue.CurrentIndex--;
                pendingDeleteQueueStepId = null;
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel###CancelRemoveQueueStep"))
                pendingDeleteQueueStepId = null;
        }
        if (changed)
            plugin.Save();
        EndAutoPanel();
    }

    private static bool QueueCheckbox(string label, bool current, Action<bool> set)
    {
        bool value = current;
        if (!ImGui.Checkbox(label, ref value))
            return false;
        set(value);
        return true;
    }

    private static string QueueMethodName(ProgressionQueueMethod method) => method switch
    {
        ProgressionQueueMethod.Automatic => "Automatic / Smart",
        ProgressionQueueMethod.HuntingLog => "Hunting Log",
        ProgressionQueueMethod.SideQuests => "Side Quests",
        ProgressionQueueMethod.Duties => "Duties",
        _ => method.ToString(),
    };

    private static string QueueFallbackName(ProgressionQueueFallbackPolicy policy) => policy switch
    {
        ProgressionQueueFallbackPolicy.PauseQueue => "Pause Queue",
        ProgressionQueueFallbackPolicy.StopQueue => "Stop Queue",
        ProgressionQueueFallbackPolicy.SkipStep => "Skip Step",
        ProgressionQueueFallbackPolicy.Automatic => "Fall back to Automatic",
        ProgressionQueueFallbackPolicy.SideQuests => "Fall back to Side Quests",
        ProgressionQueueFallbackPolicy.Duties => "Fall back to Duties",
        _ => policy.ToString(),
    };

    private void DrawProgressAtlas()
    {
        PageHeading("Progress Atlas", "Your character's completion, read directly from current game data by Nexus.");

        CharacterSnapshot? character = world.Current.Character.Value;
        ProgressAtlasSnapshot snapshot = progressAtlas.Current;
        BeginAutoPanel("CHARACTER PROGRESS");
        if (character is null || !snapshot.IsCharacterAvailable)
        {
            NexusTheme.StatusDot(NexusTheme.Muted, "Waiting for the current character");
            TextWrapped(NexusTheme.Muted, "Log in to load this character's Progress Atlas.");
            EndAutoPanel();
            return;
        }

        NexusTheme.StatusDot(NexusTheme.Cyan,
            $"{character.Name} • {ClassJobDisplay.Label(character)} • Level {character.Level}");
        TextWrapped(NexusTheme.Muted,
            "This data belongs to Nexus and remains available after VieriCodex is disabled.");
        EndAutoPanel();

        foreach (ProgressAtlasCategorySnapshot category in snapshot.Categories)
        {
            BeginAutoPanel(category.Name.ToUpperInvariant());
            if (!category.IsLoaded)
            {
                NexusTheme.StatusDot(NexusTheme.Amber, category.Detail);
                TextWrapped(NexusTheme.Muted, $"Known total: {category.Total:N0}");
                EndAutoPanel();
                continue;
            }

            string completion = category.Total == 0
                ? "No current game-data entries"
                : $"{category.Completed:N0} of {category.Total:N0} complete • {category.Remaining:N0} remaining";
            NexusTheme.StatusDot(category.Remaining == 0 ? NexusTheme.Green : NexusTheme.Cyan, completion);
            ImGui.ProgressBar(category.Completion, new Vector2(-1, 22),
                category.Total == 0 ? "—" : $"{category.Completion:P0}");
            TextWrapped(NexusTheme.Muted, category.Detail);
            DrawProgressAtlasCategoryAction(category);
            if (category.Id == ProgressAtlasCategoryId.HuntingLogs)
                DrawHuntingLogAtlasTargets();
            EndAutoPanel();
        }

        BeginAutoPanel("ATLAS EXECUTION");
        ProgressAtlasActionStatus action = progressAtlasActions.Status;
        NexusTheme.StatusDot(action.IsActive ? NexusTheme.Cyan : NexusTheme.Muted,
            action.IsActive ? action.Title : "No Atlas travel action is active");
        TextWrapped(action.IsActive ? NexusTheme.Cyan : NexusTheme.Muted,
            string.IsNullOrWhiteSpace(progressAtlasMessage) ? action.Message : progressAtlasMessage);
        if (action.IsActive && ImGui.Button("Stop Atlas action###StopAtlasAction"))
            progressAtlasActions.Stop(out progressAtlasMessage);
        TextWrapped(NexusTheme.Muted,
            "Nexus chooses and verifies one exact objective at a time. Stock Lifestream and vnavmesh provide only teleport and pathing; actions stop without replay after reload or provider loss.");
        EndAutoPanel();
    }

    private void DrawProgressAtlasCategoryAction(ProgressAtlasCategorySnapshot category)
    {
        ProgressAtlasActionStatus status = progressAtlasActions.Status;
        int remaining;
        string label;
        Func<(bool Success, string Message)> start;
        switch (category.Id)
        {
            case ProgressAtlasCategoryId.Aetherytes:
                remaining = progressAtlasActions.RemainingAetherytes;
                label = $"Attune next reachable location ({remaining})###AtlasAetheryte";
                start = () => (progressAtlasActions.StartNextAetheryte(out string result), result);
                break;
            case ProgressAtlasCategoryId.AetherCurrents:
                remaining = progressAtlasActions.RemainingFieldCurrents;
                label = $"Collect next reachable field current ({remaining})###AtlasFieldCurrent";
                start = () => (progressAtlasActions.StartNextFieldCurrent(out string result), result);
                break;
            case ProgressAtlasCategoryId.Exploration:
                remaining = progressAtlasActions.RemainingExplorationRegions;
                label = $"Explore next reachable region ({remaining})###AtlasExploration";
                start = () => (progressAtlasActions.StartNextExploration(out string result), result);
                break;
            default:
                return;
        }

        ImGui.BeginDisabled(remaining == 0 || status.IsActive);
        if (ImGui.Button(label))
        {
            (_, progressAtlasMessage) = start();
        }
        ImGui.EndDisabled();

        if (category.Id == ProgressAtlasCategoryId.AetherCurrents)
        {
            int readyQuests = progressAtlasActions.ReadyAetherCurrentQuests;
            ImGui.BeginDisabled(readyQuests == 0 || status.IsActive);
            if (ImGui.Button($"Run next ready current quest ({readyQuests})###AtlasCurrentQuest"))
                progressAtlasActions.StartNextAetherCurrentQuest(out progressAtlasMessage);
            ImGui.EndDisabled();
            if (readyQuests == 0 && progressAtlasActions.RemainingAetherCurrentQuests > 0)
                TextWrapped(NexusTheme.Muted,
                    $"{progressAtlasActions.RemainingAetherCurrentQuests} quest current(s) remain; the next one appears here when its level and prerequisites unlock.");
        }
    }

    private void DrawHuntingLogAtlasTargets()
    {
        IReadOnlyList<HuntingLogTargetProgress> targets = progressAtlas.HuntingTargets;
        HuntingLogTargetProgress[] current = targets
            .Where(target => target.IsCurrentRank && target.Killed < target.Required)
            .OrderBy(target => target.LogName, StringComparer.CurrentCulture)
            .ThenBy(target => target.Rank)
            .ThenBy(target => target.TargetName, StringComparer.CurrentCulture)
            .ToArray();
        if (!ImGui.CollapsingHeader($"Current incomplete targets ({current.Length})###AtlasHuntingTargets"))
            return;
        if (current.Length == 0)
        {
            TextWrapped(NexusTheme.Muted, "No incomplete targets are available in the current unlocked ranks.");
            return;
        }

        if (!ImGui.BeginTable("###AtlasHuntingTargetTable", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
            return;
        ImGui.TableSetupColumn("Log", ImGuiTableColumnFlags.WidthStretch, .34f);
        ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch, .46f);
        ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthStretch, .20f);
        ImGui.TableHeadersRow();
        foreach (HuntingLogTargetProgress target in current)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextWrapped($"{target.LogName} • Rank {target.Rank + 1}");
            ImGui.TableNextColumn();
            ImGui.TextWrapped(target.TargetName);
            if (!target.HasOpenWorldLocation)
                ImGui.TextColored(NexusTheme.Muted, "Duty target");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{target.Killed}/{target.Required}");
        }
        ImGui.EndTable();
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
            int eligibleDuties = draft.AllowDuties
                ? progressionRuntime.EligibleDuties(character.Level).Count
                : 0;
            IReadOnlyList<ProgressionQuestCandidate> eligibleQuestList = progressionRuntime.EligibleQuests(
                character.ClassJobId,
                character.Level,
                draft.AllowMainScenario,
                draft.AllowJobQuests,
                draft.AllowSideQuests);
            int eligibleMainScenarioQuests = eligibleQuestList.Count(
                quest => quest.Kind == ProgressionQuestKind.MainScenario);
            int eligibleClassJobRoleQuests = eligibleQuestList.Count(
                quest => quest.Kind == ProgressionQuestKind.ClassJobRole);
            int eligibleSideQuests = eligibleQuestList.Count(
                quest => quest.Kind == ProgressionQuestKind.GeneralSideQuest);
            int eligibleQuests = eligibleQuestList.Count;
            int eligibleHuntingTargets = draft.AllowHuntingLog
                ? progressionRuntime.EligibleHuntingTargets(character.ClassJobId, character.Level).Count
                : 0;
            BeginAutoPanel("START");
            bool hasExecutableStart = plan.IsExecutionConnected && progressionRuntime.IsGearReadinessReady &&
                (eligibleQuests > 0 || eligibleHuntingTargets > 0 || eligibleDuties > 0);
            NexusTheme.StatusDot(hasExecutableStart ? NexusTheme.Green : NexusTheme.Amber,
                hasExecutableStart
                    ? $"Ready • {eligibleMainScenarioQuests} MSQ • {eligibleClassJobRoleQuests} Class/Job/Role • {eligibleHuntingTargets} hunt target(s) • {eligibleSideQuests} side quest(s) • {eligibleDuties} duties"
                    : !progressionRuntime.IsGearReadinessReady
                        ? "Nexus gear shopping is unavailable"
                        : plan.IsExecutionConnected
                            ? "No eligible activity is ready for the current job and selected methods"
                            : "No executable progression activity is ready");
            TextWrapped(NexusTheme.Muted,
                "Nexus—not the provider—owns the level target, exact quest or duty selection, task history, Stop-after, verification, and replanning.");
            bool canStart = plan.IsValid && !plan.IsSatisfied && hasExecutableStart &&
                plugin.Configuration.ForCharacter(character.Key.ToString()).AllowAutomation &&
                !plugin.Configuration.ForCharacter(character.Key.ToString()).ProgressionQueue.IsRunning &&
                !plugin.Configuration.ForCharacter(character.Key.ToString()).ProgressionQueue.IsPaused;
            if (!canStart)
                ImGui.BeginDisabled();
            if (ImGui.Button("Start level goal", new Vector2(-1, 0)))
            {
                ProgressionActionResult result = progressionRuntime.StartConfigured(
                    character,
                    draft,
                    plugin.Configuration.ForCharacter(character.Key.ToString()).AllowAutomation);
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
        int completedQuests = state.Tasks.Count(task =>
            task.Status == NexusTaskStatus.Succeeded && task.Kind.Value == "vieri.quest.run-one/v1");
        int completedHunts = state.Tasks.Count(task =>
            task.Status == NexusTaskStatus.Succeeded && task.Kind.Value == "vieri.hunting-log.complete-target/v1");
        ImGui.TextUnformatted(
            $"Plan revision: {state.Goal.PlanRevision} • Quests: {completedQuests} • Hunt targets: {completedHunts} • Duties: {completedDuties}");
        if (activeTask is not null)
        {
            ImGui.TextColored(NexusTheme.Gold, activeTask.Title);
            TextWrapped(NexusTheme.Muted, activeTask.StatusDetail ?? activeTask.Reason);
            ImGui.TextUnformatted($"Task state: {activeTask.Status} • Provider: {activeTask.Provider?.Value ?? "Nexus"}");
        }
        SoloDutyRotationRuntimeStatus rotation = soloDutyRotation.Status;
        if (rotation.IsRelevant)
            TextWrapped(rotation.IsActive ? NexusTheme.Green : NexusTheme.Amber, rotation.Message);

        if (state.Goal.Status == GoalStatus.Active)
        {
            bool disableLastRun = state.StopAfterCurrentDuty || activeTask is null;
            if (disableLastRun)
                ImGui.BeginDisabled();
            if (ImGui.Button(state.StopAfterCurrentDuty ? "Stop-after armed" : "Stop after this activity"))
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
            "Stock Questionable is the only quest runtime. VieriCodex remains only as a one-time settings source. AutoDuty is still the bounded duty provider during its final migration.");
        if (!ImGui.BeginTable("###ProgressionProviders", 2, ImGuiTableFlags.SizingStretchSame))
            return;

        ImGui.TableNextColumn();
        DrawProgressionProvider("QUESTING", providers.Questing);
        ImGui.TableNextColumn();
        DrawProgressionProvider("DUTIES", providers.Duties);
        ImGui.EndTable();
        QuestionableCompatibilityStatus compatibility = questionableCompatibility.Status;
        Vector4 compatibilityColor = compatibility.IsReady ? NexusTheme.Green :
            compatibility.Health is QuestionableCompatibilityHealth.Conflict or QuestionableCompatibilityHealth.Failed
                ? NexusTheme.Red
                : NexusTheme.Muted;
        TextWrapped(compatibilityColor, $"Questionable route protection: {compatibility.Message}");
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
                ? $"Runtime provider active: {selection.Selected.DisplayName}"
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
                    ? "Gear readiness, exact Class/Job/Role quests, Hunting Log targets, general side quests, and duties run as separate verified activities. Nexus replans after each one."
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
