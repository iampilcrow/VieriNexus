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
    private readonly SplashWindow splashWindow;
    private readonly GameplayReadyGate gameplayReadyGate = new();
    private readonly WorldSnapshotObserver worldObserver;
    private readonly NexusIpcProvider ipc;
    private bool sessionInitialized;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        var dependencyService = new DependencyService(PluginInterface);
        var legacyInventory = new LegacyConfigurationInventory(PluginInterface);
        var moduleRegistry = BuiltInModuleCatalog.Create();
        var worldStore = new WorldStateStore();
        worldObserver = new WorldSnapshotObserver(ClientState, PlayerState, ObjectTable, Condition, worldStore);

        var logoPath = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "Assets", "VieriNexusLogo.png");
        ISharedImmediateTexture logo = TextureProvider.GetFromFile(logoPath);
        mainWindow = new NexusWindow(this, dependencyService, legacyInventory, moduleRegistry, worldStore, logo);
        splashWindow = new SplashWindow(logo) { IsOpen = false };
        windows.AddWindow(mainWindow);
        windows.AddWindow(splashWindow);

        ipc = new NexusIpcProvider(PluginInterface, dependencyService, worldStore);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VieriNexus. Subcommands: show, hide, dependencies, migration, splash.",
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

        if (!ClientState.IsLoggedIn)
        {
            sessionInitialized = false;
            splashWindow.IsOpen = false;
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
            if (Configuration.ShowSplashOnLogin)
                splashWindow.Show();
            if (!Configuration.FirstRunComplete || Configuration.OpenOnLogin)
                mainWindow.IsOpen = true;
        }

        windows.Draw();
    }

    private void OpenMain() => mainWindow.IsOpen = true;

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
                mainWindow.IsOpen = true;
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
            case "splash":
                splashWindow.Show();
                break;
            default:
                mainWindow.Toggle();
                break;
        }
    }
}
