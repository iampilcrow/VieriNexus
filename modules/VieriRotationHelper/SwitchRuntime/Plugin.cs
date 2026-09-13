using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using WrathSwitch.Windows;

namespace WrathSwitch;

public sealed class Plugin : IDalamudPlugin
{
    private const string MainCommand = "/wrathswitch";
    private const string ShortCommand = "/ws";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public bool WrathAvailable { get; private set; }
    public bool AutoRotationEnabled { get; private set; }
    public bool IsCapturingHotkey { get; private set; }

    private readonly WindowSystem windowSystem = new("VieriWrathSwitch");
    private readonly RotationWindow rotationWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly WrathBridge wrath;
    private readonly RotationControlBridge rotationControl;
    private readonly ICallGateProvider<bool> beginAutomation;
    private readonly MovementSafetyBridge movementSafety;
    private readonly CombatOnlyBridge combatOnly;
    private readonly HashSet<VirtualKey> captureInitiallyDown = new();
    private bool hotkeyWasDown;
    private long nextStatePoll;
    private long saveAt;
    private readonly bool embedded;
    private readonly Func<bool>? gameplayOverlayReady;
    internal bool IsEmbedded => embedded;

    public Plugin() : this(PluginInterface, CommandManager, ClientState, KeyState, GameGui,
        ChatGui, Log, null, null, false, null)
    {
    }

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager,
        IClientState clientState, IKeyState keyState, IGameGui gameGui, IChatGui chatGui,
        IPluginLog log, Configuration? embeddedConfiguration, Action? embeddedSave,
        bool embedded, Func<bool>? gameplayOverlayReady)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        ClientState = clientState;
        KeyState = keyState;
        GameGui = gameGui;
        ChatGui = chatGui;
        Log = log;
        this.embedded = embedded;
        this.gameplayOverlayReady = gameplayOverlayReady;

        Configuration = embeddedConfiguration ?? PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (embeddedConfiguration != null && embeddedSave != null)
            Configuration.InitializeEmbedded(embeddedSave);
        else
            Configuration.Initialize(PluginInterface);
        var leaseInternalName = embedded ? PluginInterface.InternalName : "WrathSwitch";
        wrath = new WrathBridge(PluginInterface);
        rotationControl = new RotationControlBridge(PluginInterface, Log, leaseInternalName);
        beginAutomation = PluginInterface.GetIpcProvider<bool>("WrathSwitch.BeginAutomation");
        beginAutomation.RegisterFunc(rotationControl.BeginAutomation);
        movementSafety = new MovementSafetyBridge(PluginInterface, CommandManager, Log, leaseInternalName);
        combatOnly = new CombatOnlyBridge(PluginInterface, Log, leaseInternalName);

        rotationWindow = new RotationWindow(this) { IsOpen = Configuration.ShowWindow };
        settingsWindow = new SettingsWindow(this);
        windowSystem.AddWindow(rotationWindow);
        windowSystem.AddWindow(settingsWindow);

        CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VieriWrathSwitch settings. Subcommands: toggle, on, off, safe, combat, show, hide, lock, unlock, bind.",
        });
        CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VieriWrathSwitch settings. Subcommands: toggle, on, off, safe, combat, show, hide, lock, unlock, bind.",
        });

        PluginInterface.UiBuilder.Draw += Draw;
        if (!embedded)
        {
            PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
            PluginInterface.UiBuilder.OpenMainUi += OpenSettings;
        }
        RefreshWrathState(true);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= Draw;
        if (!embedded)
        {
            PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
            PluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        }
        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(ShortCommand);
        windowSystem.RemoveAllWindows();
        beginAutomation.UnregisterFunc();
        combatOnly.Dispose();
        movementSafety.Dispose();
        rotationControl.Dispose();
        Configuration.Save();
    }

    public void OpenSettings() => settingsWindow.IsOpen = true;

    public void ToggleRotation() => SetRotation(!AutoRotationEnabled);

    public void ToggleMovementSafety() => SetMovementSafety(!Configuration.BlockAutomatedMovement);

    public void ToggleCombatOnly() => SetCombatOnly(!Configuration.CombatOnlyRotation);

    public void SetCombatOnly(bool enabled)
    {
        Configuration.CombatOnlyRotation = enabled;
        if (enabled)
            combatOnly.Apply();
        else
            combatOnly.Release();
        SaveNow();
        ChatGui.Print(enabled
            ? "[VieriWrathSwitch] In Combat Only: ON. Wrath rotations will remain idle until combat starts."
            : "[VieriWrathSwitch] In Combat Only: OFF. Wrath rotations may operate outside combat again.");
    }

    public void SetMovementSafety(bool enabled)
    {
        Configuration.BlockAutomatedMovement = enabled;
        if (enabled)
            movementSafety.Apply();
        else
            movementSafety.Release();
        SaveNow();
        ChatGui.Print(enabled
            ? "[VieriWrathSwitch] Manual Movement / Targeting Only: ON. Movement, targeting, and automatic camera steering are paused; Wrath rotations remain available for your target."
            : "[VieriWrathSwitch] Manual Movement / Targeting Only: OFF. Paused automation can resume immediately.");
    }

    public void SetRotation(bool enabled)
    {
        if (!WrathAvailable)
        {
            ChatGui.PrintError("[VieriWrathSwitch] Wrath Combo is not loaded or its IPC is not ready.");
            return;
        }

        if (!rotationControl.Set(enabled, allowAutomationYield: !Configuration.BlockAutomatedMovement))
        {
            ChatGui.PrintError("[VieriWrathSwitch] Wrath Combo could not establish authoritative rotation control for the current job.");
            return;
        }

        nextStatePoll = 0;
        RefreshWrathState(true);
    }

    public void StartHotkeyCapture()
    {
        captureInitiallyDown.Clear();
        foreach (var key in KeyState.GetValidVirtualKeys())
            if (KeyState[key])
                captureInitiallyDown.Add(key);
        IsCapturingHotkey = true;
        hotkeyWasDown = false;
    }

    public void CancelHotkeyCapture()
    {
        IsCapturingHotkey = false;
        captureInitiallyDown.Clear();
    }

    public void ClearHotkey()
    {
        CancelHotkeyCapture();
        Configuration.Hotkey = VirtualKey.NO_KEY;
        SaveNow();
    }

    public string HotkeyName
    {
        get
        {
            if (Configuration.Hotkey == VirtualKey.NO_KEY)
                return "Not assigned";
            var parts = new List<string>(4);
            if (Configuration.HotkeyControl) parts.Add("Ctrl");
            if (Configuration.HotkeyShift) parts.Add("Shift");
            if (Configuration.HotkeyAlt) parts.Add("Alt");
            parts.Add(Configuration.Hotkey.GetFancyName());
            return string.Join(" + ", parts);
        }
    }

    public void MarkDirty()
    {
        saveAt = Environment.TickCount64 + 700;
    }

    public void SaveNow()
    {
        Configuration.Save();
        saveAt = 0;
    }

    public void ResetWindowPosition()
    {
        Configuration.WindowPositionInitialized = false;
        rotationWindow.ApplyDefaultPosition();
        SaveNow();
    }

    private void Draw()
    {
        if (!ClientState.IsLoggedIn)
        {
            hotkeyWasDown = false;
            if (saveAt != 0 && Environment.TickCount64 >= saveAt)
                SaveNow();
            return;
        }

        rotationControl.Update();
        RefreshWrathState(false);
        HandleHotkey();
        movementSafety.Update(Configuration.BlockAutomatedMovement);
        combatOnly.Update(Configuration.CombatOnlyRotation);

        rotationWindow.IsOpen = Configuration.ShowWindow;
        if ((gameplayOverlayReady?.Invoke() ?? true) &&
            (!Configuration.HideWhenGameUiHidden || !GameGui.GameUiHidden))
            windowSystem.Draw();

        if (saveAt != 0 && Environment.TickCount64 >= saveAt)
            SaveNow();
    }

    private void RefreshWrathState(bool force)
    {
        var now = Environment.TickCount64;
        if (!force && now < nextStatePoll)
            return;
        nextStatePoll = now + 200;
        WrathAvailable = wrath.TryGetAutoRotationState(out var enabled);
        if (WrathAvailable)
            AutoRotationEnabled = enabled;
    }

    private void HandleHotkey()
    {
        if (IsCapturingHotkey)
        {
            CaptureHotkey();
            return;
        }

        var cfg = Configuration;
        if (!cfg.HotkeyEnabled || cfg.Hotkey == VirtualKey.NO_KEY || !ClientState.IsLoggedIn)
        {
            hotkeyWasDown = false;
            return;
        }

        var control = ModifierDown(VirtualKey.CONTROL, VirtualKey.LCONTROL, VirtualKey.RCONTROL);
        var shift = ModifierDown(VirtualKey.SHIFT, VirtualKey.LSHIFT, VirtualKey.RSHIFT);
        var alt = ModifierDown(VirtualKey.MENU, VirtualKey.LMENU, VirtualKey.RMENU);
        var modifiersMatch = control == cfg.HotkeyControl && shift == cfg.HotkeyShift && alt == cfg.HotkeyAlt;
        if (!cfg.ExactModifiers)
            modifiersMatch = (!cfg.HotkeyControl || control) && (!cfg.HotkeyShift || shift) && (!cfg.HotkeyAlt || alt);

        var down = KeyState.IsVirtualKeyValid(cfg.Hotkey) && KeyState[cfg.Hotkey] && modifiersMatch;
        if (down && !hotkeyWasDown)
            ToggleRotation();
        hotkeyWasDown = down;
    }

    private void CaptureHotkey()
    {
        foreach (var key in captureInitiallyDown.ToArray())
            if (!KeyState[key])
                captureInitiallyDown.Remove(key);

        if (KeyState[VirtualKey.ESCAPE] && !captureInitiallyDown.Contains(VirtualKey.ESCAPE))
        {
            CancelHotkeyCapture();
            return;
        }

        foreach (var key in KeyState.GetValidVirtualKeys())
        {
            if (!KeyState[key] || captureInitiallyDown.Contains(key) || IsModifier(key) || key is VirtualKey.LBUTTON or VirtualKey.RBUTTON)
                continue;

            Configuration.Hotkey = key;
            Configuration.HotkeyControl = ModifierDown(VirtualKey.CONTROL, VirtualKey.LCONTROL, VirtualKey.RCONTROL);
            Configuration.HotkeyShift = ModifierDown(VirtualKey.SHIFT, VirtualKey.LSHIFT, VirtualKey.RSHIFT);
            Configuration.HotkeyAlt = ModifierDown(VirtualKey.MENU, VirtualKey.LMENU, VirtualKey.RMENU);
            CancelHotkeyCapture();
            SaveNow();
            return;
        }
    }

    private static bool IsModifier(VirtualKey key) => key is
        VirtualKey.SHIFT or VirtualKey.LSHIFT or VirtualKey.RSHIFT or
        VirtualKey.CONTROL or VirtualKey.LCONTROL or VirtualKey.RCONTROL or
        VirtualKey.MENU or VirtualKey.LMENU or VirtualKey.RMENU or
        VirtualKey.LWIN or VirtualKey.RWIN;

    private static bool ModifierDown(VirtualKey generic, VirtualKey left, VirtualKey right) =>
        (KeyState.IsVirtualKeyValid(generic) && KeyState[generic]) ||
        (KeyState.IsVirtualKeyValid(left) && KeyState[left]) ||
        (KeyState.IsVirtualKeyValid(right) && KeyState[right]);

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "toggle": ToggleRotation(); break;
            case "on": SetRotation(true); break;
            case "off": SetRotation(false); break;
            case "show": Configuration.ShowWindow = true; SaveNow(); break;
            case "hide": Configuration.ShowWindow = false; SaveNow(); break;
            case "lock": Configuration.LockWindow = true; SaveNow(); break;
            case "unlock": Configuration.LockWindow = false; Configuration.ShowWindow = true; SaveNow(); break;
            case "bind": OpenSettings(); StartHotkeyCapture(); break;
            case "safe": ToggleMovementSafety(); break;
            case "safe on": SetMovementSafety(true); break;
            case "safe off": SetMovementSafety(false); break;
            case "combat": ToggleCombatOnly(); break;
            case "combat on": SetCombatOnly(true); break;
            case "combat off": SetCombatOnly(false); break;
            default: settingsWindow.Toggle(); break;
        }
    }
}

