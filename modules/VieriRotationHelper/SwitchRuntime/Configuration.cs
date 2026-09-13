using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin;

namespace WrathSwitch;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool ShowWindow { get; set; } = true;
    public bool LockWindow { get; set; }
    public bool ShowHotkeyText { get; set; } = true;
    public bool HideWhenGameUiHidden { get; set; } = true;
    public bool BlockAutomatedMovement { get; set; }
    public bool CombatOnlyRotation { get; set; }
    public bool AnimateManualControlAlert { get; set; } = true;
    public float Scale { get; set; } = 1f;
    public float Opacity { get; set; } = .96f;
    public Vector4 EnabledColor { get; set; } = new(.08f, .48f, .20f, 1f);
    public Vector4 DisabledColor { get; set; } = new(.60f, .10f, .12f, 1f);
    public Vector4 UnavailableColor { get; set; } = new(.23f, .24f, .26f, 1f);
    public Vector2 WindowPosition { get; set; } = new(300, 300);
    public bool WindowPositionInitialized { get; set; }

    public bool HotkeyEnabled { get; set; } = true;
    public VirtualKey Hotkey { get; set; } = VirtualKey.NO_KEY;
    public bool HotkeyControl { get; set; }
    public bool HotkeyShift { get; set; }
    public bool HotkeyAlt { get; set; }
    public bool ExactModifiers { get; set; } = true;

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;
    [NonSerialized] private Action? embeddedSave;

    public void Initialize(IDalamudPluginInterface pi)
    {
        pluginInterface = pi;
        Scale = Math.Clamp(Scale, .65f, 2f);
        Opacity = Math.Clamp(Opacity, .25f, 1f);
    }

    public void InitializeEmbedded(Action save)
    {
        pluginInterface = null;
        embeddedSave = save;
        Scale = Math.Clamp(Scale, .65f, 2f);
        Opacity = Math.Clamp(Opacity, .25f, 1f);
    }

    public void Save()
    {
        if (embeddedSave != null)
            embeddedSave();
        else
            pluginInterface?.SavePluginConfig(this);
    }
}

