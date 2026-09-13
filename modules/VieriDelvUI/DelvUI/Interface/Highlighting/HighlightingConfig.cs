using DelvUI.Config;
using DelvUI.Config.Attributes;
using System;
using System.Numerics;

namespace DelvUI.Interface.Highlighting
{
    public enum VieriHighlightColor
    {
        Red = 0,
        Green,
        Blue,
        Yellow,
        Orange,
        Magenta,
        Black
    }

    public enum VieriNativeOutlineColor
    {
        Red = 0,
        Green,
        Blue,
        Yellow,
        Orange,
        Magenta
    }

    public abstract class HighlightingConfig : PluginConfigObject
    {
        [Checkbox("Highlight current target", help = "Highlights the current target when it belongs to this category.")]
        [Order(10)]
        public bool HighlightCurrentTarget = true;

        [Checkbox("Highlight all visible", help = "Highlights every loaded object in this category. This includes the current target.")]
        [Order(20)]
        public bool HighlightAll = false;

        // Kept for a lossless migration from VieriDelvUI 2.7.0.4 profiles.
        // New code uses OutlineColor after EnsureCustomColors has migrated this value.
        public VieriHighlightColor HighlightColor = VieriHighlightColor.Yellow;

        public bool CustomColorsInitialized = false;

        // Kept so profiles saved by 2.7.0.5 can be migrated to an honest native choice.
        public PluginConfigColor OutlineColor = PluginConfigColor.FromHex(0xFFFFFF00);

        public bool NativeOutlineColorInitialized = false;

        [Combo("Outline Color", new[] { "Red", "Green", "Blue", "Yellow", "Orange", "Magenta" }, help = "These are the six model-outline colors FFXIV reliably supports. Custom brightness, opacity, cyan/light-blue hues, and thickness are not available through the native outline function. Black is excluded because FFXIV renders it as a large magenta selection aura on many objects.")]
        [Order(30)]
        public VieriNativeOutlineColor NativeOutlineColor = VieriNativeOutlineColor.Yellow;

        [Checkbox("Draw line to current target", help = "Draws a guide line when your current target belongs to this category.")]
        [Order(40)]
        public bool DrawLineToCurrentTarget = false;

        [Checkbox("Draw lines to all visible", help = "Draws guide lines to every loaded object in this category, including your current target.")]
        [Order(50)]
        public bool DrawLinesToAll = false;

        // Kept so guide-line colors saved by 2.7.0.5 can be migrated safely.
        public PluginConfigColor LineColor = PluginConfigColor.FromHex(0xFFFF4DFF);

        public bool NativeLineColorInitialized = false;

        [Combo("Line Color", new[] { "Red", "Green", "Blue", "Yellow", "Orange", "Magenta" }, help = "Guide lines use the same concise six-color palette as native outlines, while keeping an independent color choice.")]
        [Order(60)]
        public VieriNativeOutlineColor NativeLineColor = VieriNativeOutlineColor.Magenta;

        [DragFloat("Line Thickness", min = 0.5f, max = 8f, velocity = 0.1f)]
        [Order(70)]
        public float LineThickness = 2f;

        protected HighlightingConfig()
        {
            Enabled = false;
        }

        public bool EnsureCustomColors()
        {
            bool changed = false;
            if (!CustomColorsInitialized)
            {
                OutlineColor = new PluginConfigColor(HighlightColor switch
                {
                    VieriHighlightColor.Red => new Vector4(1f, 0f, 0f, 1f),
                    VieriHighlightColor.Green => new Vector4(0f, 1f, 0f, 1f),
                    VieriHighlightColor.Blue => new Vector4(0f, 0f, 1f, 1f),
                    VieriHighlightColor.Yellow => new Vector4(1f, 1f, 0f, 1f),
                    VieriHighlightColor.Orange => new Vector4(1f, 0.5f, 0f, 1f),
                    VieriHighlightColor.Magenta => new Vector4(1f, 0f, 1f, 1f),
                    _ => new Vector4(0f, 0f, 0f, 1f)
                });

                CustomColorsInitialized = true;
                changed = true;
            }

            if (!NativeOutlineColorInitialized)
            {
                NativeOutlineColor = ToNativeColor(OutlineColor.Vector);
                NativeOutlineColorInitialized = true;
                changed = true;
            }

            if (!NativeLineColorInitialized)
            {
                NativeLineColor = ToNativeColor(LineColor.Vector);
                NativeLineColorInitialized = true;
                changed = true;
            }

            return changed;
        }

        internal static VieriNativeOutlineColor ToNativeColor(Vector4 color)
        {
            Vector3 requested = new(color.X, color.Y, color.Z);
            float brightness = MathF.Max(requested.X, MathF.Max(requested.Y, requested.Z));
            if (brightness <= 0.001f)
            {
                return VieriNativeOutlineColor.Yellow;
            }

            requested /= brightness;
            (VieriNativeOutlineColor Native, Vector3 Rgb)[] supportedColors =
            {
                (VieriNativeOutlineColor.Red, new Vector3(1f, 0f, 0f)),
                (VieriNativeOutlineColor.Green, new Vector3(0f, 1f, 0f)),
                (VieriNativeOutlineColor.Blue, new Vector3(0f, 0f, 1f)),
                (VieriNativeOutlineColor.Yellow, new Vector3(1f, 1f, 0f)),
                (VieriNativeOutlineColor.Orange, new Vector3(1f, 0.5f, 0f)),
                (VieriNativeOutlineColor.Magenta, new Vector3(1f, 0f, 1f))
            };

            VieriNativeOutlineColor nearest = supportedColors[0].Native;
            float nearestDistance = float.MaxValue;
            foreach ((VieriNativeOutlineColor native, Vector3 rgb) in supportedColors)
            {
                float distance = Vector3.DistanceSquared(requested, rgb);
                if (distance < nearestDistance)
                {
                    nearest = native;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }

    public abstract class RoleHighlightingConfig : HighlightingConfig
    {
        [Checkbox("Highlight all visible by role", help = "Highlights every loaded combat character in this section by role: tanks blue, healers green, and DPS red. Party Members includes Trust and Duty Support NPC companions. Named Custom highlighting rules still take priority.")]
        [Order(25)]
        public bool HighlightAllByRole = false;
    }

    [Section("Highlighting")]
    [SubSection("Player", 0)]
    public sealed class PlayerHighlightingConfig : RoleHighlightingConfig
    {
        public new static PlayerHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Enemies", 0)]
    public sealed class EnemyHighlightingConfig : HighlightingConfig
    {
        public new static EnemyHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Party Members", 0)]
    public sealed class PartyMembersHighlightingConfig : RoleHighlightingConfig
    {
        public new static PartyMembersHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Alliance Members", 0)]
    public sealed class AllianceMembersHighlightingConfig : RoleHighlightingConfig
    {
        public new static AllianceMembersHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Friends", 0)]
    public sealed class FriendsHighlightingConfig : RoleHighlightingConfig
    {
        public new static FriendsHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Other Players", 0)]
    public sealed class OtherPlayersHighlightingConfig : RoleHighlightingConfig
    {
        public new static OtherPlayersHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Pets", 0)]
    public sealed class PetsHighlightingConfig : HighlightingConfig
    {
        public new static PetsHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("NPCs", 0)]
    public sealed class NpcsHighlightingConfig : HighlightingConfig
    {
        public new static NpcsHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Minions", 0)]
    public sealed class MinionsHighlightingConfig : HighlightingConfig
    {
        public new static MinionsHighlightingConfig DefaultConfig() => new();
    }

    [Section("Highlighting")]
    [SubSection("Objects", 0)]
    public sealed class ObjectsHighlightingConfig : HighlightingConfig
    {
        public new static ObjectsHighlightingConfig DefaultConfig() => new();
    }
}
