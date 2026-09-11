using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Text.Json;
using VieriNexus.Application;
using VieriNexus.Domain;
using VieriNexus.Services;
using VieriNexus.UI;

namespace VieriNexus;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/vierinexus";
    private const string ShortCommand = "/nexus";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IAetheryteList AetheryteList { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    internal Configuration Configuration { get; }

    private readonly WindowSystem windows = new("VieriNexus");
    private readonly NexusWindow mainWindow;
    private readonly NexusOperationsOverlay operationsOverlay;
    private readonly DependencyService dependencyService;
    private readonly GameplayReadyGate gameplayReadyGate = new();
    private readonly WorldStateStore worldStore;
    private readonly WorldSnapshotObserver worldObserver;
    private readonly ManualMovementSafetyService manualMovementSafety;
    private readonly NavigationExecutionSafetyCoordinator navigationExecutionSafety;
    private readonly NavigationAuthorityCoordinator navigationAuthority;
    private readonly NavigationDiagnosticsService navigationDiagnostics;
    private readonly NavigationRecoveryService navigationRecovery;
    private readonly NavigationRouteRuntimeService navigationRuntime;
    private readonly NavigationLibraryService navigationLibrary;
    private readonly AutoDutyMigrationService autoDutyMigration;
    private readonly ProgressionProviderService progressionProviders;
    private readonly ProgressionRuntimeService progressionRuntime;
    private readonly ProgressAtlasService progressAtlas;
    private readonly ProgressAtlasActionService progressAtlasActions;
    private readonly NexusHuntingLogService huntingLog;
    private readonly GearShoppingRuntimeService gearShoppingRuntime;
    private readonly NexusMaintenanceRuntimeService maintenanceRuntime;
    private readonly StrikingDummyTravelService strikingDummyTravel;
    private readonly NexusControlService controlService;
    private readonly NexusIpcProvider ipc;
    private bool sessionInitialized;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        dependencyService = new DependencyService(PluginInterface);
        var legacyInventory = new LegacyConfigurationInventory(PluginInterface);
        LegacyImportState autoDutyImport = Configuration.ForLegacyImport("autoduty");
        autoDutyMigration = new AutoDutyMigrationService(
            legacyInventory,
            PluginInterface.GetPluginConfigDirectory(),
            autoDutyImport.Imported ? autoDutyImport.ReceiptId : null);
        LegacyImportState navigationImport = Configuration.ForLegacyImport("navplotter");
        var navigationMigration = new NavigationMigrationService(
            legacyInventory,
            PluginInterface.GetPluginConfigDirectory(),
            navigationImport.Imported ? navigationImport.ReceiptId : null);
        navigationLibrary = new NavigationLibraryService(
            navigationMigration,
            PluginInterface.GetPluginConfigDirectory());
        var moduleRegistry = BuiltInModuleCatalog.Create();
        worldStore = new WorldStateStore();
        var resourceLeases = new ResourceLeaseManager();
        var navigationStopProvider = new VnavmeshNavigationStopProvider(PluginInterface, dependencyService);
        var navigationStop = new NavigationStopCoordinator(navigationStopProvider, TimeSpan.FromSeconds(15));
        var navigationIntentStore = new FileNavigationExecutionIntentStore(Path.Combine(
            PluginInterface.GetPluginConfigDirectory(),
            "NexusData",
            "navigation-execution-intent.v1.json"));
        navigationExecutionSafety = new NavigationExecutionSafetyCoordinator(
            resourceLeases,
            navigationStop,
            navigationStopProvider,
            navigationIntentStore);
        var manualMovement = new ManualMovementSafetyCoordinator(navigationStop);
        manualMovementSafety = new ManualMovementSafetyService();
        navigationRecovery = new NavigationRecoveryService(
            new NavigationRecoveryCoordinator(
                navigationExecutionSafety,
                manualMovement,
                navigationStop),
            manualMovementSafety);
        navigationAuthority = new NavigationAuthorityCoordinator(
            resourceLeases,
            navigationStop,
            navigationExecutionSafety,
            () =>
            {
                PluginPresence source = dependencyService.FindPlugin("VieriNavPlotter");
                return new NavigationAuthorityPrerequisites(
                    navigationLibrary.HasWorkingLibrary,
                    source.IsLoaded,
                    dependencyService.RequiredReady,
                    navigationStop.IsProviderAvailable,
                    manualMovementSafety.IsReadyForActivation,
                    navigationExecutionSafety.IsReadyForActivation);
            });
        var navigationActivation = new NavigationActivationService(
            dependencyService,
            navigationLibrary,
            resourceLeases,
            navigationStop,
            manualMovementSafety,
            navigationExecutionSafety,
            navigationAuthority);
        var navigationPreview = new NavigationRoutePreviewService(ClientState, GameGui);
        var navigationLivePath = new NavigationLivePathService(ObjectTable, GameGui, navigationStopProvider);
        var navigationRecording = new NavigationRouteRecordingService(
            navigationLibrary,
            new NavigationRouteRecordingCoordinator());
        Func<bool> routeStartAllowed = () =>
            worldStore.Current.Character.Value is { Key.IsKnown: true } character &&
            Configuration.ForCharacter(character.Key.ToString()).AllowAutomation;
        var navigationExecution = new NavigationRouteExecutionCoordinator(
            navigationAuthority,
            navigationExecutionSafety,
            navigationStopProvider,
            routeStartAllowed,
            () => ClientState.TerritoryType);
        var suiteTravelProvider = new NexusRouteTravelProvider(
            PluginInterface,
            dependencyService,
            ClientState,
            Condition,
            DataManager,
            AetheryteList,
            navigationStopProvider);
        var suiteTravel = new NavigationSuiteTravelCoordinator(
            suiteTravelProvider,
            () => navigationAuthority.Status.IsActive,
            routeStartAllowed);
        navigationRuntime = new NavigationRouteRuntimeService(
            navigationExecution,
            suiteTravel,
            navigationPreview,
            navigationLivePath,
            navigationRecording,
            navigationLibrary,
            navigationRecovery);
        navigationDiagnostics = new NavigationDiagnosticsService(
            dependencyService,
            resourceLeases,
            navigationStop,
            manualMovementSafety,
            navigationExecutionSafety,
            navigationAuthority,
            new NavigationDiagnosticsMonitor(),
            new NavigationSafetySimulator());
        navigationDiagnostics.Update(DateTimeOffset.UtcNow);
        progressAtlas = new ProgressAtlasService(DataManager, ClientState, PlayerState);
        progressionProviders = new ProgressionProviderService(
            PluginInterface, dependencyService, DataManager, PlayerState, ObjectTable,
            ClientState, Condition, GameGui, navigationLibrary, suiteTravelProvider);
        progressAtlasActions = new ProgressAtlasActionService(
            progressAtlas,
            progressionProviders,
            suiteTravelProvider,
            resourceLeases,
            ClientState,
            Condition,
            ObjectTable,
            TargetManager);
        huntingLog = new NexusHuntingLogService(
            PluginInterface,
            progressAtlas,
            progressionProviders,
            suiteTravelProvider,
            navigationStopProvider,
            dependencyService,
            ClientState,
            PlayerState,
            ObjectTable,
            TargetManager,
            Condition,
            CommandManager);
        progressionRuntime = new ProgressionRuntimeService(
            PluginInterface.GetPluginConfigDirectory(),
            resourceLeases,
            progressionProviders,
            huntingLog,
            DutyState,
            Log);
        gearShoppingRuntime = new GearShoppingRuntimeService(resourceLeases, progressionProviders);
        maintenanceRuntime = new NexusMaintenanceRuntimeService(
            resourceLeases, autoDutyMigration, PlayerState, ObjectTable, Condition, GameGui, DataManager,
            ClientState, suiteTravelProvider, PluginInterface, GameInteropProvider);
        strikingDummyTravel = new StrikingDummyTravelService(
            PluginInterface, ClientState, Condition, DataManager, AetheryteList, navigationRuntime);
        worldObserver = new WorldSnapshotObserver(
            ClientState,
            PlayerState,
            ObjectTable,
            Condition,
            worldStore,
            () => navigationDiagnostics.ProviderHealth()
                .Concat(progressionProviders.ProviderHealth())
                .ToDictionary(pair => pair.Key, pair => pair.Value));

        var logoPath = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "Assets", "VieriNexusLogo.png");
        ISharedImmediateTexture logo = TextureProvider.GetFromFile(logoPath);
        mainWindow = new NexusWindow(this, dependencyService, legacyInventory, navigationMigration, autoDutyMigration,
            navigationLibrary, navigationActivation, navigationDiagnostics,
            navigationRuntime, progressionProviders, progressionRuntime, progressAtlas,
            progressAtlasActions,
            gearShoppingRuntime, maintenanceRuntime,
            moduleRegistry, worldStore, logo);
        windows.AddWindow(mainWindow);
        controlService = new NexusControlService(
            Configuration,
            worldStore,
            DataManager,
            Condition,
            progressionRuntime,
            progressAtlasActions,
            navigationLibrary,
            navigationRuntime,
            gearShoppingRuntime,
            maintenanceRuntime,
            strikingDummyTravel,
            page =>
            {
                Configuration.SelectedPage = page;
                mainWindow.IsOpen = true;
                Save();
            },
            Save);
        operationsOverlay = new NexusOperationsOverlay(
            this,
            navigationRuntime,
            gearShoppingRuntime,
            progressionRuntime,
            maintenanceRuntime,
            strikingDummyTravel,
            controlService,
            page =>
            {
                Configuration.SelectedPage = page;
                mainWindow.IsOpen = true;
                Save();
            });
        windows.AddWindow(operationsOverlay);

        ipc = new NexusIpcProvider(
            PluginInterface,
            dependencyService,
            navigationLibrary,
            navigationActivation,
            progressionRuntime,
            worldStore,
            controlService);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VieriNexus. Controls: status, start, resume, last, stop, maintenance, repair, extract, register, coffers, desynth, gcturnin, storage, sell, play <route>, preview <route>.",
        });
        CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VieriNexus.",
        });
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenMainUi += OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
    }

    public void Dispose()
    {
        progressAtlasActions.Shutdown();
        huntingLog.Shutdown();
        maintenanceRuntime.Shutdown();
        strikingDummyTravel.Stop(out _);
        gearShoppingRuntime.Shutdown();
        progressionRuntime.Shutdown();
        navigationRuntime.Shutdown();
        navigationAuthority.ReturnToStaging(DateTimeOffset.UtcNow);
        navigationExecutionSafety.Shutdown(DateTimeOffset.UtcNow);
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        CommandManager.RemoveHandler(Command);
        CommandManager.RemoveHandler(ShortCommand);
        ipc.Dispose();
        windows.RemoveAllWindows();
        Configuration.Save();
    }

    internal void Save() => Configuration.Save();

    private void Draw()
    {
        var now = Environment.TickCount64;
        worldObserver.Update(now);
        navigationExecutionSafety.Update(DateTimeOffset.UtcNow);
        manualMovementSafety.Update(now);
        navigationAuthority.Update();
        navigationRuntime.Update(now);
        navigationRecovery.Update(now);
        navigationDiagnostics.Update(DateTimeOffset.UtcNow);
        progressionProviders.UpdateGearAdapter();
        progressAtlas.Update(DateTimeOffset.UtcNow);
        progressAtlasActions.Update(DateTimeOffset.UtcNow);
        huntingLog.Update(DateTimeOffset.UtcNow);
        gearShoppingRuntime.Update();
        maintenanceRuntime.Update(DateTimeOffset.UtcNow);
        strikingDummyTravel.Update(DateTimeOffset.UtcNow);
        progressionRuntime.Update(
            worldStore.Current.Character.Value,
            Condition[ConditionFlag.BoundByDuty] ||
            Condition[ConditionFlag.BoundByDuty56] ||
            Condition[ConditionFlag.BoundByDuty95]);

        if (!ClientState.IsLoggedIn)
        {
            if (navigationRuntime.RecordingStatus.IsRecording)
                navigationRuntime.StopRecording("Recording stopped because the character logged out.");
            sessionInitialized = false;
            return;
        }

        var ready = gameplayReadyGate.Evaluate(
            ClientState.IsLoggedIn,
            ObjectTable.LocalPlayer is { IsTargetable: true },
            ClientState.TerritoryType != 0,
            Condition[ConditionFlag.BetweenAreas] || Condition[ConditionFlag.BetweenAreas51],
            now);
        if (!ready)
            return;

        if (!sessionInitialized)
        {
            sessionInitialized = true;
            ApplyPendingOperationsProfile();
            if (!Configuration.FirstRunComplete || Configuration.OpenOnLogin)
            {
                Configuration.SelectedPage = Configuration.FirstRunComplete ? "Home" : "Dependencies";
                mainWindow.IsOpen = true;
            }
        }

        windows.Draw();
        navigationRuntime.DrawPreview();
    }

    private void OpenMain()
    {
        Configuration.SelectedPage = Configuration.FirstRunComplete && dependencyService.RequiredReady
            ? "Home"
            : "Dependencies";
        mainWindow.IsOpen = true;
    }

    private void OpenSettings()
    {
        Configuration.SelectedPage = "Settings";
        mainWindow.IsOpen = true;
    }

    private void OnCommand(string _, string arguments)
    {
        string trimmed = arguments.Trim();
        if (trimmed.StartsWith("play ", StringComparison.OrdinalIgnoreCase))
        {
            PrintControl(controlService.ExecuteLocal("route.play",
                JsonSerializer.Serialize(new { name = trimmed[5..].Trim().Trim('"') })));
            return;
        }
        if (trimmed.StartsWith("preview ", StringComparison.OrdinalIgnoreCase))
        {
            PrintControl(controlService.ExecuteLocal("route.preview",
                JsonSerializer.Serialize(new { name = trimmed[8..].Trim().Trim('"') })));
            return;
        }

        switch (trimmed.ToLowerInvariant())
        {
            case "show":
                OpenMain();
                break;
            case "hide":
                mainWindow.IsOpen = false;
                break;
            case "dependencies":
                Configuration.SelectedPage = "Dependencies";
                mainWindow.IsOpen = true;
                break;
            case "migration":
                Configuration.SelectedPage = "Migration";
                mainWindow.IsOpen = true;
                break;
            case "routes":
                Configuration.SelectedPage = "Routes & Navigation";
                mainWindow.IsOpen = true;
                break;
            case "progression":
                Configuration.SelectedPage = "Progression";
                mainWindow.IsOpen = true;
                break;
            case "atlas":
                Configuration.SelectedPage = "Progress Atlas";
                mainWindow.IsOpen = true;
                break;
            case "stop":
                PrintControl(controlService.ExecuteLocal("stop"));
                break;
            case "status":
            case "start":
            case "resume":
            case "last":
            case "maintenance":
            case "repair":
            case "extract":
            case "register":
            case "coffers":
            case "desynth":
            case "gcturnin":
            case "storage":
            case "sell":
                PrintControl(controlService.ExecuteLocal(trimmed));
                break;
            case "home":
            case "splash":
                Configuration.SelectedPage = "Home";
                mainWindow.IsOpen = true;
                break;
            default:
                if (!mainWindow.IsOpen)
                    Configuration.SelectedPage = Configuration.FirstRunComplete ? "Home" : "Dependencies";
                mainWindow.Toggle();
                break;
        }
    }

    internal void ApplyPendingOperationsProfile()
    {
        AutoDutyMigrationStatus status = autoDutyMigration.Status();
        if (status.LastReceipt is not { } receipt ||
            (Configuration.AppliedOperationsReceiptId == receipt.Id &&
             Configuration.AppliedOperationsCharacterId == PlayerState.ContentId) ||
            autoDutyMigration.ProfileFor(PlayerState.ContentId) is not { } profile)
            return;
        Configuration.ShowOperationsOverlay = profile.Overlay.ShowOverlay;
        Configuration.LockOperationsOverlay = profile.Overlay.LockPosition;
        Configuration.OperationsOverlayTransparent = profile.Overlay.TransparentBackground;
        Configuration.ShowOperationsStatus = profile.Overlay.ShowDutyStatus || profile.Overlay.ShowActionStatus;
        Configuration.AppliedOperationsReceiptId = receipt.Id;
        Configuration.AppliedOperationsCharacterId = PlayerState.ContentId;
        Save();
    }

    private static void PrintControl(VieriNexus.Contracts.NexusCommandResultDto result)
    {
        if (result.Accepted)
            ChatGui.Print($"[VieriNexus] {result.Message}");
        else
            ChatGui.PrintError($"[VieriNexus] {result.Message}");
    }
}
