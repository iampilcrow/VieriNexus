using Dalamud.Game.ClientState.Keys;
using VieriRotationHelper;

var plugin = new Plugin();
var controller = new WindowHotkeyController(plugin);
var checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new Exception(description);
}
void Keys(params VirtualKey[] keys)
{
    Plugin.KeyState.Pressed.Clear();
    Plugin.KeyState.Pressed.UnionWith(keys);
    controller.Update();
}
Check(controller.Name == "Not assigned", "Default is unassigned");
plugin.SettingsOpen = true;
Keys(VirtualKey.LBUTTON, VirtualKey.A);
controller.StartCapture();
controller.Update();
Check(controller.IsCapturing, "Already held keys are ignored");
Keys();
Keys(VirtualKey.LCONTROL);
Check(controller.IsCapturing, "Modifier alone does not complete recording");
Keys(VirtualKey.LCONTROL, VirtualKey.K);
Check(!controller.IsCapturing && plugin.Configuration.WindowHotkey == VirtualKey.K && plugin.Configuration.WindowHotkeyControl, "Records Ctrl+K");
Check(plugin.Saves == 1, "Recording persists once");
controller.Update();
Check(plugin.Toggles == 0, "Recording key does not immediately close window");
Keys();
Keys(VirtualKey.RCONTROL, VirtualKey.K);
controller.Update();
Check(plugin.Toggles == 1, "Right Ctrl matches and holding does not repeat");
Keys();
Keys(VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.K);
Check(plugin.Toggles == 1, "Exact modifiers reject extra Shift");
plugin.Configuration.WindowHotkeyExactModifiers = false;
Keys();
Keys(VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.K);
Check(plugin.Toggles == 2, "Non-exact allows extra modifier");
Keys();
Keys(VirtualKey.K);
Check(plugin.Toggles == 2, "Non-exact still requires configured modifier");
plugin.Configuration.WindowHotkeyEnabled = false;
Keys(VirtualKey.CONTROL, VirtualKey.K);
Check(plugin.Toggles == 2, "Disabled binding does not toggle");
plugin.Configuration.WindowHotkeyEnabled = true;
plugin.SettingsOpen = true;
Keys();
controller.StartCapture();
Keys(VirtualKey.ESCAPE);
Check(!controller.IsCapturing && plugin.Configuration.WindowHotkey == VirtualKey.K && plugin.Saves == 1, "Escape preserves previous binding without saving");
Keys();
controller.StartCapture();
plugin.SettingsOpen = false;
Keys(VirtualKey.A);
Check(!controller.IsCapturing && plugin.Configuration.WindowHotkey == VirtualKey.K, "Closing window cancels pending capture");
plugin.SettingsOpen = true;
Keys();
controller.StartCapture();
Plugin.ClientState.IsLoggedIn = false;
Keys(VirtualKey.A);
Check(!controller.IsCapturing && plugin.Toggles == 2, "Logout cancels capture and disables toggles");
Plugin.ClientState.IsLoggedIn = true;
controller.Clear();
Keys(VirtualKey.CONTROL, VirtualKey.K);
Check(controller.Name == "Not assigned" && plugin.Toggles == 2 && plugin.Saves == 2, "Clear persists and disables old shortcut");
Console.WriteLine($"{checks} window keybind behavior checks passed.");

namespace Dalamud.Game.ClientState.Keys
{
    // Minimal input-service stand-ins; the production controller is linked above.
    public enum VirtualKey { NO_KEY, LBUTTON, RBUTTON, ESCAPE, SHIFT, LSHIFT, RSHIFT, CONTROL, LCONTROL, RCONTROL, MENU, LMENU, RMENU, LWIN, RWIN, A, K }
    public static class KeyNames { public static string GetFancyName(this VirtualKey key) => key.ToString(); }
}
namespace VieriRotationHelper
{
    internal sealed class Configuration
    {
        public bool WindowHotkeyEnabled = true, WindowHotkeyExactModifiers = true;
        public VirtualKey WindowHotkey;
        public bool WindowHotkeyControl, WindowHotkeyShift, WindowHotkeyAlt;
    }
    internal sealed class Input
    {
        public HashSet<VirtualKey> Pressed = [];
        public bool IsVirtualKeyValid(VirtualKey key) => key != VirtualKey.NO_KEY;
        public bool this[VirtualKey key] => Pressed.Contains(key);
        public IEnumerable<VirtualKey> GetValidVirtualKeys() => Enum.GetValues<VirtualKey>().Where(IsVirtualKeyValid);
    }
    internal sealed class Login { public bool IsLoggedIn = true; }
    internal sealed class Objects { public object? LocalPlayer = new(); }
    internal sealed class Plugin
    {
        public static Input KeyState = new();
        public static Login ClientState = new();
        public static Objects ObjectTable = new();
        public Configuration Configuration = new();
        public bool SettingsOpen;
        public int Toggles, Saves;
        public void ToggleSettings() { SettingsOpen = !SettingsOpen; Toggles++; }
        public void Save() => Saves++;
    }
}
