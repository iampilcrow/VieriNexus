using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VieriNexus.Application;
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
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal Configuration Configuration { get; }

    private readonly WindowSystem windows = new("VieriNexus");
    private readonly NexusWindow mainWindow;
    private readonly DependencyService dependencyService;
    private readonly GameplayReadyGate gameplayReadyGate = new();
    private readonly WorldSnapshotObserver worldObserver;
    private readonly ManualMovementSafetyService manualMovementSafety;
    private readonly NavigationExecutionSafetyCoordinator navigationExecutionSafety;
    private readonly NavigationAuthorityCoordinator navigationAuthority;
    private readonly NavigationDiagnosticsService navigationDiagnostics;
    private readonly NavigationRecoveryService navigationRecovery;
    private readonly NexusIpcProvider ipc;
    private bool sessionInitialized;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        dependencyService = new DependencyService(PluginInterface);
        var legacyInventory = new LegacyConfigurationInventory(PluginInterface);
        LegacyImportState navigationImport = Configuration.ForLegacyImport("navplotter");
        var navigationMigration = new NavigationMigrationService(
            legacyInventory,
            PluginInterface.GetPluginConfigDirectory(),
            navigationImport.Imported ? navigationImport.ReceiptId : null);
        var moduleRegistry = BuiltInModuleCatalog.Create();
        var worldStore = new WorldStateStore();
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
        var manualMovementInput = new GameManualMovementInputSource();
        var manualMovement = new ManualMovementSafetyCoordinator(navigationStop);
        manualMovementSafety = new ManualMovementSafetyService(
            Configuration,
            worldStore,
            manualMovementInput,
            manualMovement);
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
                    navigationMigration.StagedSnapshot is not null,
                    source.IsLoaded,
                    dependencyService.RequiredReady,
                    navigationStop.IsProviderAvailable,
                    manualMovementSafety.IsReadyForActivation,
                    navigationExecutionSafety.IsReadyForActivation);
            });
        var navigationActivation = new NavigationActivationService(
            dependencyService,
            navigationMigration,
            resourceLeases,
            navigationStop,
            manualMovementSafety,
            navigationExecutionSafety,
            navigationAuthority);
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
        worldObserver = new WorldSnapshotObserver(
            ClientState,
            PlayerState,
            ObjectTable,
            Condition,
            worldStore,
            navigationDiagnostics.ProviderHealth);

        var logoPath = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "Assets", "VieriNexusLogo.png");
        ISharedImmediateTexture logo = TextureProvider.GetFromFile(logoPath);
        mainWindow = new NexusWindow(this, dependencyService, legacyInventory, navigationMigration, navigationActivation, navigationDiagnostics, navigationRecovery, moduleRegistry, worldStore, logo);
        windows.AddWindow(mainWindow);

        ipc = new NexusIpcProvider(PluginInterface, dependencyService, navigationMigration, navigationActivation, worldStore);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VieriNexus. Subcommands: home, show, hide, dependencies, migration.",
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
        navigationRecovery.Update(now);
        navigationDiagnostics.Update(DateTimeOffset.UtcNow);

        if (!ClientState.IsLoggedIn)
        {
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
            if (!Configuration.FirstRunComplete || Configuration.OpenOnLogin)
            {
                Configuration.SelectedPage = Configuration.FirstRunComplete ? "Home" : "Dependencies";
                mainWindow.IsOpen = true;
            }
        }

        windows.Draw();
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
        switch (arguments.Trim().ToLowerInvariant())
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
}
