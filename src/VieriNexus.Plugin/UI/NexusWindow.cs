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
    private readonly ModuleRegistry modules;
    private readonly WorldStateStore world;
    private readonly ISharedImmediateTexture logo;

    internal NexusWindow(
        Plugin plugin,
        DependencyService dependencies,
        LegacyConfigurationInventory legacyInventory,
        NavigationMigrationService navigationMigration,
        ModuleRegistry modules,
        WorldStateStore world,
        ISharedImmediateTexture logo)
        : base("Vieri Nexus###VieriNexusMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.dependencies = dependencies;
        this.legacyInventory = legacyInventory;
        this.navigationMigration = navigationMigration;
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

    private void DrawNavigationMigration()
    {
        const string importLabel = "Create backup and import to staging";
        const string rollbackLabel = "Rollback staged import";
        NavigationMigrationStatus status = navigationMigration.Status();
        LegacyImportState state = plugin.Configuration.ForLegacyImport("navplotter");
        string operationMessage = status.Message;
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

        if (state.Imported && state.ReceiptId is { } receiptId)
        {
            if (availableButtonWidth >= importButtonWidth + ImGui.GetStyle().ItemSpacing.X + rollbackButtonWidth)
                ImGui.SameLine();
            if (ImGui.Button(rollbackLabel, new Vector2(rollbackButtonWidth, 0)))
            {
                MigrationWriteResult result = navigationMigration.Rollback(receiptId);
                operationMessage = result.Message;
                if (result.Success)
                {
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

        TextWrapped(state.Imported ? NexusTheme.Green : NexusTheme.Muted, operationMessage);
        if (state.Imported)
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
