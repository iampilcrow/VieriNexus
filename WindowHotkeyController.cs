using Dalamud.Game.ClientState.Keys;

namespace VieriRotationHelper;

// Matches the recordable window shortcut used by VieriDeck and VieriCodex.
// This controller never changes the rotation engine or switch state.
internal sealed class WindowHotkeyController(Plugin plugin)
{
    private readonly HashSet<VirtualKey> initiallyDown = [];
    private bool wasDown;
    internal bool IsCapturing { get; private set; }
    private Configuration Config => plugin.Configuration;

    internal string Name
    {
        get
        {
            if (Config.WindowHotkey == VirtualKey.NO_KEY) return "Not assigned";
            List<string> parts = [];
            if (Config.WindowHotkeyControl) parts.Add("Ctrl");
            if (Config.WindowHotkeyShift) parts.Add("Shift");
            if (Config.WindowHotkeyAlt) parts.Add("Alt");
            parts.Add(Config.WindowHotkey.GetFancyName());
            return string.Join(" + ", parts);
        }
    }

    internal void Update()
    {
        if (!Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer == null)
        {
            CancelCapture();
            wasDown = false;
            return;
        }
        if (IsCapturing)
        {
            if (!plugin.SettingsOpen) CancelCapture();
            else Capture();
            return;
        }
        if (!Config.WindowHotkeyEnabled || Config.WindowHotkey == VirtualKey.NO_KEY)
        {
            wasDown = false;
            return;
        }
        var control = ModifierDown(VirtualKey.CONTROL, VirtualKey.LCONTROL, VirtualKey.RCONTROL);
        var shift = ModifierDown(VirtualKey.SHIFT, VirtualKey.LSHIFT, VirtualKey.RSHIFT);
        var alt = ModifierDown(VirtualKey.MENU, VirtualKey.LMENU, VirtualKey.RMENU);
        var matches = Config.WindowHotkeyExactModifiers
            ? control == Config.WindowHotkeyControl && shift == Config.WindowHotkeyShift && alt == Config.WindowHotkeyAlt
            : (!Config.WindowHotkeyControl || control) && (!Config.WindowHotkeyShift || shift) && (!Config.WindowHotkeyAlt || alt);
        var down = Down(Config.WindowHotkey) && matches;
        if (down && !wasDown) plugin.ToggleSettings();
        wasDown = down;
    }

    internal void StartCapture()
    {
        initiallyDown.Clear();
        foreach (var key in Plugin.KeyState.GetValidVirtualKeys())
            if (Down(key)) initiallyDown.Add(key);
        IsCapturing = true;
        wasDown = false;
    }

    internal void CancelCapture()
    {
        IsCapturing = false;
        initiallyDown.Clear();
    }

    internal void Clear()
    {
        CancelCapture();
        Config.WindowHotkey = VirtualKey.NO_KEY;
        wasDown = false;
        plugin.Save();
    }

    private void Capture()
    {
        foreach (var key in initiallyDown.ToArray())
            if (!Down(key)) initiallyDown.Remove(key);
        if (Down(VirtualKey.ESCAPE) && !initiallyDown.Contains(VirtualKey.ESCAPE))
        {
            CancelCapture();
            // Do not toggle the window if the old binding is still being held.
            wasDown = true;
            return;
        }
        foreach (var key in Plugin.KeyState.GetValidVirtualKeys())
        {
            if (!Down(key) || initiallyDown.Contains(key) || IsModifier(key) ||
                key is VirtualKey.LBUTTON or VirtualKey.RBUTTON) continue;
            Config.WindowHotkey = key;
            Config.WindowHotkeyControl = ModifierDown(VirtualKey.CONTROL, VirtualKey.LCONTROL, VirtualKey.RCONTROL);
            Config.WindowHotkeyShift = ModifierDown(VirtualKey.SHIFT, VirtualKey.LSHIFT, VirtualKey.RSHIFT);
            Config.WindowHotkeyAlt = ModifierDown(VirtualKey.MENU, VirtualKey.LMENU, VirtualKey.RMENU);
            wasDown = true;
            CancelCapture();
            plugin.Save();
            return;
        }
    }

    private static bool Down(VirtualKey key) => Plugin.KeyState.IsVirtualKeyValid(key) && Plugin.KeyState[key];
    private static bool ModifierDown(VirtualKey generic, VirtualKey left, VirtualKey right) =>
        Down(generic) || Down(left) || Down(right);
    private static bool IsModifier(VirtualKey key) => key is
        VirtualKey.SHIFT or VirtualKey.LSHIFT or VirtualKey.RSHIFT or
        VirtualKey.CONTROL or VirtualKey.LCONTROL or VirtualKey.RCONTROL or
        VirtualKey.MENU or VirtualKey.LMENU or VirtualKey.RMENU or VirtualKey.LWIN or VirtualKey.RWIN;
}
