using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Keys;
using VieriRotationHelper.Windows;

namespace VieriRotationHelper;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/vrh";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;

    internal Configuration Configuration { get; }
    private readonly WindowSystem windows = new("VieriRotationHelper");
    private readonly SettingsWindow settingsWindow;
    private readonly EmbeddedRotationProvider engine;
    private readonly PositionalGuidance positionalGuidance;
    private readonly WrathSwitch.Plugin? switchRuntime;
    private readonly GameplayOverlayGate gameplayOverlayGate = new();
    internal OverlayFonts Fonts { get; }
    internal HotkeyResolver Hotkeys { get; }
    internal WindowHotkeyController WindowHotkey { get; }
    internal bool SettingsOpen => settingsWindow.IsOpen;
    internal string EngineStatus => engine.Status;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);
        WindowHotkey = new WindowHotkeyController(this);
        Fonts = new OverlayFonts();
        Hotkeys = new HotkeyResolver(GameGui);

        var wrath = new WrathLiveProvider(PluginInterface);
        engine = new EmbeddedRotationProvider(this, wrath);
        var coordinator = new RotationCoordinator(ObjectTable, TargetManager, wrath, engine,
            new TargetAnalysis(ObjectTable, TargetManager, DataManager), Configuration);
        positionalGuidance = new PositionalGuidance(engine, coordinator);
        var separateSwitchLoaded = PluginInterface.InstalledPlugins.Any(plugin =>
            plugin.InternalName.Equals("WrathSwitch", StringComparison.OrdinalIgnoreCase) && plugin.IsLoaded);
        if (!separateSwitchLoaded)
            switchRuntime = new WrathSwitch.Plugin(PluginInterface, CommandManager, ClientState,
                KeyState, GameGui, ChatGui, Log, Configuration.Switch, Save, true,
                () => GameplayOverlaysReady);
        var display = new ActionDisplay(DataManager);

        windows.AddWindow(new RotationBarWindow(this, coordinator, display, RotationMode.SingleTarget));
        windows.AddWindow(new RotationBarWindow(this, coordinator, display, RotationMode.Aoe));
        windows.AddWindow(new RotationBarWindow(this, coordinator, display, RotationMode.Dynamic));
        settingsWindow = new SettingsWindow(this, wrath);
        windows.AddWindow(settingsWindow);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VieriRotationHelper settings. Use '/vrh toggle' to show or hide suggestions.",
        });
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi += OpenSettings;

        if (wrath.IsLoaded || separateSwitchLoaded)
            ChatGui.PrintError("[VieriRotationHelper] Disable the separate Wrath Combo and VieriWrathSwitch plugins, then reload plugins once to activate the complete integrated suite. Your old settings files are preserved.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= Draw;
        WindowHotkey.CancelCapture();
        PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        CommandManager.RemoveHandler(Command);
        windows.RemoveAllWindows();
        switchRuntime?.Dispose();
        positionalGuidance.Dispose();
        engine.Dispose();
        Fonts.Dispose();
        Configuration.Save();
    }

    internal bool ShouldShow(RotationMode mode)
    {
        if (!Configuration.Enabled || !GameplayOverlaysReady)
            return false;
        if (mode == RotationMode.SingleTarget && !Configuration.ShowSingleTarget)
            return false;
        if (mode == RotationMode.Aoe && !Configuration.ShowAoe)
            return false;
        if (mode == RotationMode.Dynamic && !Configuration.ShowDynamic)
            return false;
        if (!Configuration.ShowOutOfCombat && !Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
            return false;
        if (!Configuration.ShowWithoutTarget && TargetManager.Target == null)
            return false;
        return true;
    }

    internal bool GameplayOverlaysReady => gameplayOverlayGate.Evaluate(
        ClientState.IsLoggedIn,
        ObjectTable.LocalPlayer is { IsTargetable: true },
        ClientState.TerritoryType != 0,
        Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
        Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51],
        Environment.TickCount64);

    internal void Save() => Configuration.Save();
    private void Draw()
    {
        WindowHotkey.Update();
        windows.Draw();
    }
    internal void ToggleSettings() => settingsWindow.IsOpen = !settingsWindow.IsOpen;
    internal void OpenSettings() => settingsWindow.IsOpen = true;
    internal void OpenEngineSettings() => engine.OpenSettings();
    internal void OpenSwitchSettings()
    {
        if (switchRuntime != null)
            switchRuntime.OpenSettings();
        else
            CommandManager.ProcessCommand("/wrathswitch");
    }
    internal bool EmbeddedEngineActive => engine.IsActive;
    internal bool EmbeddedSwitchActive => switchRuntime != null;

    private void OnCommand(string command, string arguments)
    {
        if (arguments.Trim().Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.Enabled = !Configuration.Enabled;
            Save();
            ChatGui.Print($"[VieriRotationHelper] Suggestions {(Configuration.Enabled ? "enabled" : "disabled")}.");
            return;
        }
        OpenSettings();
    }
}
