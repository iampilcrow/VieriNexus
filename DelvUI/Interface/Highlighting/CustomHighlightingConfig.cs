using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DelvUI.Config;
using DelvUI.Config.Attributes;
using DelvUI.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DelvUI.Interface.Highlighting
{
    public enum CustomHighlightObjectType
    {
        Any,
        Player,
        Enemy,
        Npc,
        Pet,
        Minion
    }

    public enum CustomHighlightActivation
    {
        Off,
        Always,
        CurrentTargetOnly
    }

    public sealed class CustomHighlightRule
    {
        public string Id = Guid.NewGuid().ToString("N");
        public bool Enabled = true;
        public string Name = "";
        public CustomHighlightObjectType ObjectType = CustomHighlightObjectType.Any;
        public CustomHighlightActivation OutlineMode = CustomHighlightActivation.Always;
        public VieriNativeOutlineColor OutlineColor = VieriNativeOutlineColor.Yellow;
        public CustomHighlightActivation LineMode = CustomHighlightActivation.Off;
        // Kept so colors saved by an earlier 2.7.0.6 development build migrate safely.
        public PluginConfigColor LineColor = PluginConfigColor.FromHex(0xFFFF4DFF);
        public bool NativeLineColorInitialized = false;
        public VieriNativeOutlineColor NativeLineColor = VieriNativeOutlineColor.Magenta;
        public float LineThickness = 2f;

        public bool EnsureNativeLineColor()
        {
            if (NativeLineColorInitialized)
            {
                return false;
            }

            NativeLineColor = HighlightingConfig.ToNativeColor(LineColor.Vector);
            NativeLineColorInitialized = true;
            return true;
        }
    }

    [Section("Highlighting")]
    [SubSection("Custom", 0)]
    public sealed class CustomHighlightingConfig : PluginConfigObject
    {
        private static readonly string[] ObjectTypeLabels = { "Any", "Player", "Enemy", "NPC", "Pet", "Minion" };
        private static readonly string[] ActivationLabels = { "Off", "Always", "Current Target Only" };
        private static readonly string[] OutlineColorLabels = { "Red", "Green", "Blue", "Yellow", "Orange", "Magenta" };

        public List<CustomHighlightRule> Rules = new();

        [JsonIgnore] private string _newName = "";
        [JsonIgnore] private int _newObjectType;

        public CustomHighlightingConfig()
        {
            Enabled = true;
        }

        public new static CustomHighlightingConfig DefaultConfig() => new();

        [ManualDraw]
        public bool Draw(ref bool changed)
        {
            if (!Enabled)
            {
                return false;
            }

            ImGui.TextWrapped("Named rules override Player, Party, Friend, Enemy, NPC, Pet, and Minion highlighting. Matching is exact and case-insensitive; a type-specific rule takes priority over an Any rule with the same name.");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(280f);
            ImGui.InputText("Exact name##CustomHighlightName", ref _newName, 100);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(130f);
            ImGui.Combo("Type##CustomHighlightType", ref _newObjectType, ObjectTypeLabels);

            if (ImGui.Button("Use Current Target"))
            {
                IGameObject? target = Plugin.TargetManager.Target;
                if (target != null)
                {
                    _newName = target.Name.ToString();
                    _newObjectType = (int)GuessObjectType(target);
                }
            }

            ImGui.SameLine();

            bool canAdd = !string.IsNullOrWhiteSpace(_newName);
            if (!canAdd)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Add Rule"))
            {
                Rules.Add(new CustomHighlightRule
                {
                    Name = _newName.Trim(),
                    ObjectType = (CustomHighlightObjectType)_newObjectType
                });
                _newName = "";
                changed = true;
            }

            if (!canAdd)
            {
                ImGui.EndDisabled();
            }

            ImGui.Spacing();

            int removeIndex = -1;
            for (int i = 0; i < Rules.Count; i++)
            {
                CustomHighlightRule rule = Rules[i];
                if (string.IsNullOrEmpty(rule.Id))
                {
                    rule.Id = Guid.NewGuid().ToString("N");
                    changed = true;
                }

                changed |= rule.EnsureNativeLineColor();

                ImGui.PushID(rule.Id);
                ImGui.Separator();

                bool enabled = rule.Enabled;
                if (ImGui.Checkbox("##Enabled", ref enabled))
                {
                    rule.Enabled = enabled;
                    changed = true;
                }

                ImGui.SameLine();
                if (ImGui.Button("Delete"))
                {
                    removeIndex = i;
                }

                ImGui.SameLine();
                string heading = string.IsNullOrWhiteSpace(rule.Name)
                    ? "Unnamed Rule"
                    : $"{rule.Name}  ({ObjectTypeLabels[(int)rule.ObjectType]})";

                if (ImGui.CollapsingHeader(heading + "##Rule"))
                {
                    string name = rule.Name;
                    ImGui.SetNextItemWidth(300f);
                    if (ImGui.InputText("Exact Name", ref name, 100))
                    {
                        rule.Name = name;
                        changed = true;
                    }

                    int objectType = (int)rule.ObjectType;
                    ImGui.SetNextItemWidth(180f);
                    if (ImGui.Combo("Object Type", ref objectType, ObjectTypeLabels))
                    {
                        rule.ObjectType = (CustomHighlightObjectType)objectType;
                        changed = true;
                    }

                    int outlineMode = (int)rule.OutlineMode;
                    ImGui.SetNextItemWidth(180f);
                    if (ImGui.Combo("Outline", ref outlineMode, ActivationLabels))
                    {
                        rule.OutlineMode = (CustomHighlightActivation)outlineMode;
                        changed = true;
                    }

                    if (rule.OutlineMode != CustomHighlightActivation.Off)
                    {
                        int outlineColor = (int)rule.OutlineColor;
                        ImGui.SetNextItemWidth(180f);
                        if (ImGui.Combo("Outline Color", ref outlineColor, OutlineColorLabels))
                        {
                            rule.OutlineColor = (VieriNativeOutlineColor)outlineColor;
                            changed = true;
                        }
                    }

                    int lineMode = (int)rule.LineMode;
                    ImGui.SetNextItemWidth(180f);
                    if (ImGui.Combo("Guide Line", ref lineMode, ActivationLabels))
                    {
                        rule.LineMode = (CustomHighlightActivation)lineMode;
                        changed = true;
                    }

                    if (rule.LineMode != CustomHighlightActivation.Off)
                    {
                        int lineColor = (int)rule.NativeLineColor;
                        ImGui.SetNextItemWidth(180f);
                        if (ImGui.Combo("Line Color", ref lineColor, OutlineColorLabels))
                        {
                            rule.NativeLineColor = (VieriNativeOutlineColor)lineColor;
                            changed = true;
                        }

                        float thickness = rule.LineThickness;
                        if (ImGui.DragFloat("Line Thickness", ref thickness, 0.1f, 0.5f, 8f, "%.1f"))
                        {
                            rule.LineThickness = thickness;
                            changed = true;
                        }
                    }

                }

                ImGui.PopID();
            }

            if (removeIndex >= 0)
            {
                Rules.RemoveAt(removeIndex);
                changed = true;
            }

            return false;
        }

        public bool EnsureNativeLineColors()
        {
            bool changed = false;
            foreach (CustomHighlightRule rule in Rules)
            {
                changed |= rule.EnsureNativeLineColor();
            }

            return changed;
        }

        private static CustomHighlightObjectType GuessObjectType(IGameObject gameObject)
        {
            if (gameObject.ObjectKind == ObjectKind.Pc)
            {
                return CustomHighlightObjectType.Player;
            }

            if (gameObject.ObjectKind == ObjectKind.Companion)
            {
                return CustomHighlightObjectType.Minion;
            }

            if (gameObject.ObjectKind == ObjectKind.EventNpc)
            {
                return CustomHighlightObjectType.Npc;
            }

            if (gameObject is IBattleNpc battleNpc)
            {
                BattleNpcSubKind subKind = (BattleNpcSubKind)battleNpc.SubKind;
                if (subKind is BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy)
                {
                    return CustomHighlightObjectType.Pet;
                }

                return Utils.IsHostile(gameObject)
                    ? CustomHighlightObjectType.Enemy
                    : CustomHighlightObjectType.Npc;
            }

            return CustomHighlightObjectType.Any;
        }
    }
}
