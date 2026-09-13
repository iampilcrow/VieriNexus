using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DelvUI.Config;
using DelvUI.Helpers;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Numerics;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ObjectHighlightColor = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectHighlightColor;
using AtkUnitBase = FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase;

namespace DelvUI.Interface.Highlighting
{
    internal sealed class HighlightingManager : IDisposable
    {
        private enum HighlightCategory
        {
            Player,
            Enemy,
            PartyMember,
            AllianceMember,
            Friend,
            OtherPlayer,
            Pet,
            Npc,
            Minion,
            Object
        }

        public static HighlightingManager Instance { get; private set; } = null!;

        private readonly Dictionary<nint, ObjectHighlightColor> _appliedColors = new();
        private readonly Dictionary<nint, LineTarget> _lineTargets = new();
        private readonly HighlightingPartyRoster _partyRoster = new();
        private readonly Dictionary<int, HighlightCombatRole> _partyIconRoles = new();
        private DateTime _lastUpdate = DateTime.MinValue;

        private readonly record struct LineTarget(Vector3 Position, uint Color, float Thickness);

        public bool HasLines => _lineTargets.Count > 0;

        private HighlightingManager()
        {
            foreach (ClassJob job in Plugin.DataManager.GetExcelSheet<ClassJob>())
            {
                HighlightCombatRole role = HighlightingPartyRoster.RoleFromClassJob(job.Role);
                if (role == HighlightCombatRole.None) continue;
                for (uint style = 0; style < 3; style++)
                {
                    uint iconId = JobsHelper.IconIDForJob(job.RowId, style);
                    if (iconId != 0) _partyIconRoles[(int)iconId] = role;
                }
            }
            _partyIconRoles[62581] = HighlightCombatRole.Tank;
            _partyIconRoles[62582] = HighlightCombatRole.Healer;
            _partyIconRoles[62583] = HighlightCombatRole.Damage;
        }

        public static void Initialize()
        {
            Instance = new HighlightingManager();
            Instance.MigrateCustomColors();
        }

        public void Update()
        {
            if (!Plugin.ClientState.IsLoggedIn ||
                Plugin.ObjectTable.LocalPlayer == null ||
                !ConfigurationManager.Instance.ShowHUD ||
                IsCharacterPreviewVisible())
            {
                ClearAppliedHighlights();
                _lineTargets.Clear();
                _partyRoster.Clear();
                _lastUpdate = DateTime.MinValue;
                return;
            }

            bool updateNativeHighlights = DateTime.UtcNow - _lastUpdate >= TimeSpan.FromMilliseconds(250);
            RefreshPartyRoster();

            Dictionary<nint, IGameObject> currentObjects = new();
            Dictionary<nint, (IGameObject GameObject, ObjectHighlightColor Color)> desiredHighlights = new();
            Dictionary<nint, LineTarget> desiredLines = new();
            nint targetAddress = Plugin.TargetManager.Target?.Address ?? nint.Zero;
            CustomHighlightingConfig customConfig = ConfigurationManager.Instance.GetConfigObject<CustomHighlightingConfig>();
            if (customConfig.EnsureNativeLineColors())
            {
                ConfigurationManager.Instance.ForceNeedsSave();
            }

            foreach (IGameObject gameObject in Plugin.ObjectTable)
            {
                if (gameObject.Address == nint.Zero)
                {
                    continue;
                }

                if (updateNativeHighlights)
                {
                    currentObjects[gameObject.Address] = gameObject;
                }

                HighlightCategory category = GetCategory(gameObject);
                bool isCurrentTarget = gameObject.Address == targetAddress;
                CustomHighlightRule? customRule = customConfig.Enabled
                    ? GetCustomRule(customConfig, gameObject, category)
                    : null;

                if (customRule != null)
                {
                    if (ShouldApply(customRule.OutlineMode, isCurrentTarget))
                    {
                        desiredHighlights[gameObject.Address] = (gameObject, ToNativeColor(customRule.OutlineColor));
                    }

                    if (ShouldApply(customRule.LineMode, isCurrentTarget))
                    {
                        desiredLines[gameObject.Address] = new LineTarget(gameObject.Position, ToLineColor(customRule.NativeLineColor), Math.Clamp(customRule.LineThickness, 0.5f, 8f));
                    }

                    // A matching custom rule always wins, even when one of its modes is Off.
                    continue;
                }

                // The old generic Objects category was not reliably highlightable and has
                // been replaced by named Custom rules.
                if (category == HighlightCategory.Object)
                {
                    continue;
                }

                HighlightingConfig config = GetConfig(category);
                if (!config.Enabled)
                {
                    continue;
                }

                ObjectHighlightColor roleColor = ObjectHighlightColor.None;
                bool roleHighlight = config is RoleHighlightingConfig { HighlightAllByRole: true } &&
                                     TryGetRoleHighlightColor(gameObject, out roleColor);
                if (updateNativeHighlights &&
                    (roleHighlight || config.HighlightAll || (config.HighlightCurrentTarget && isCurrentTarget)))
                {
                    desiredHighlights[gameObject.Address] = (gameObject,
                        roleHighlight ? roleColor : ToNativeColor(config.NativeOutlineColor));
                }

                if (config.DrawLinesToAll || (config.DrawLineToCurrentTarget && isCurrentTarget))
                {
                    desiredLines[gameObject.Address] = new LineTarget(gameObject.Position, ToLineColor(config.NativeLineColor), Math.Clamp(config.LineThickness, 0.5f, 8f));
                }
            }

            _lineTargets.Clear();
            foreach ((nint address, LineTarget lineTarget) in desiredLines)
            {
                _lineTargets[address] = lineTarget;
            }

            if (!updateNativeHighlights)
            {
                return;
            }

            _lastUpdate = DateTime.UtcNow;

            foreach ((nint address, ObjectHighlightColor previousColor) in _appliedColors)
            {
                if (!currentObjects.TryGetValue(address, out IGameObject? gameObject))
                {
                    continue;
                }

                if (!desiredHighlights.TryGetValue(address, out var desired) || desired.Color != previousColor)
                {
                    SetHighlight(gameObject, ObjectHighlightColor.None);
                }
            }

            foreach ((IGameObject gameObject, ObjectHighlightColor color) in desiredHighlights.Values)
            {
                // Reapply periodically because game state changes can clear the native effect.
                SetHighlight(gameObject, color);
            }

            _appliedColors.Clear();
            foreach ((nint address, var desired) in desiredHighlights)
            {
                _appliedColors[address] = desired.Color;
            }
        }

        public void DrawLines()
        {
            IPlayerCharacter? localPlayer = Plugin.ObjectTable.LocalPlayer;
            if (localPlayer == null || !Plugin.GameGui.WorldToScreen(localPlayer.Position, out Vector2 start))
            {
                return;
            }

            foreach ((nint address, LineTarget target) in _lineTargets)
            {
                if (!Plugin.GameGui.WorldToScreen(target.Position, out Vector2 end))
                {
                    continue;
                }

                float padding = MathF.Max(1f, target.Thickness);
                Vector2 minimum = Vector2.Min(start, end) - new Vector2(padding);
                Vector2 maximum = Vector2.Max(start, end) + new Vector2(padding);
                Vector2 size = Vector2.Max(maximum - minimum, Vector2.One);

                DrawHelper.DrawInWindow($"VieriHighlightLine_{address:X}", minimum, size, false, drawList =>
                {
                    drawList.AddLine(start, end, target.Color, target.Thickness);
                });
            }
        }

        public void Dispose()
        {
            ClearAppliedHighlights();
            _lineTargets.Clear();
            Instance = null!;
        }

        private void ClearAppliedHighlights()
        {
            foreach (IGameObject gameObject in Plugin.ObjectTable)
            {
                if (gameObject.Address != nint.Zero && _appliedColors.ContainsKey(gameObject.Address))
                {
                    SetHighlight(gameObject, ObjectHighlightColor.None);
                }
            }

            _appliedColors.Clear();
        }

        private static HighlightingConfig GetConfig(HighlightCategory category)
        {
            ConfigurationManager manager = ConfigurationManager.Instance;
            HighlightingConfig config = category switch
            {
                HighlightCategory.Player => manager.GetConfigObject<PlayerHighlightingConfig>(),
                HighlightCategory.Enemy => manager.GetConfigObject<EnemyHighlightingConfig>(),
                HighlightCategory.PartyMember => manager.GetConfigObject<PartyMembersHighlightingConfig>(),
                HighlightCategory.AllianceMember => manager.GetConfigObject<AllianceMembersHighlightingConfig>(),
                HighlightCategory.Friend => manager.GetConfigObject<FriendsHighlightingConfig>(),
                HighlightCategory.OtherPlayer => manager.GetConfigObject<OtherPlayersHighlightingConfig>(),
                HighlightCategory.Pet => manager.GetConfigObject<PetsHighlightingConfig>(),
                HighlightCategory.Npc => manager.GetConfigObject<NpcsHighlightingConfig>(),
                HighlightCategory.Minion => manager.GetConfigObject<MinionsHighlightingConfig>(),
                _ => throw new InvalidOperationException("Generic objects do not have a category highlighting configuration.")
            };

            if (config.EnsureCustomColors())
            {
                manager.ForceNeedsSave();
            }

            return config;
        }

        private static CustomHighlightRule? GetCustomRule(CustomHighlightingConfig config, IGameObject gameObject, HighlightCategory category)
        {
            string name = gameObject.Name.ToString().Trim();
            if (name.Length == 0)
            {
                return null;
            }

            CustomHighlightObjectType objectType = category switch
            {
                // Party category settings include NPC companions, but named NPC rules
                // must keep matching those actors rather than becoming Player rules.
                HighlightCategory.PartyMember or HighlightCategory.AllianceMember =>
                    gameObject.ObjectKind == ObjectKind.Pc ? CustomHighlightObjectType.Player : CustomHighlightObjectType.Npc,
                HighlightCategory.Player or HighlightCategory.Friend or HighlightCategory.OtherPlayer => CustomHighlightObjectType.Player,
                HighlightCategory.Enemy => CustomHighlightObjectType.Enemy,
                HighlightCategory.Npc => CustomHighlightObjectType.Npc,
                HighlightCategory.Pet => CustomHighlightObjectType.Pet,
                HighlightCategory.Minion => CustomHighlightObjectType.Minion,
                _ => CustomHighlightObjectType.Any
            };

            CustomHighlightRule? anyMatch = null;
            foreach (CustomHighlightRule rule in config.Rules)
            {
                if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.Name) ||
                    !string.Equals(rule.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rule.ObjectType == objectType)
                {
                    return rule;
                }

                if (rule.ObjectType == CustomHighlightObjectType.Any && anyMatch == null)
                {
                    anyMatch = rule;
                }
            }

            return anyMatch;
        }

        private static bool ShouldApply(CustomHighlightActivation activation, bool isCurrentTarget)
        {
            return activation == CustomHighlightActivation.Always ||
                   (activation == CustomHighlightActivation.CurrentTargetOnly && isCurrentTarget);
        }

        private unsafe void RefreshPartyRoster()
        {
            _partyRoster.Clear();
            // Read the same authoritative rows as Party Frames, independently of
            // frame visibility, preview mode, or whether the native HUD is hidden.
            if (AtkStage.Instance() == null) return;
            PartyListNumberArray* partyList = PartyListNumberArray.Instance();
            if (partyList == null) return;
            int partyCount = Math.Clamp(partyList->PartyListCount, 0, partyList->PartyMembers.Length);
            int trustCount = Math.Clamp(partyList->TrustCount, 0, partyList->TrustMembers.Length);
            for (int i = 0; i < partyCount; i++)
                _partyRoster.Add(partyList->PartyMembers[i].EntityId, partyList->PartyMembers[i].ClassIconId);
            for (int i = 0; i < trustCount; i++)
                _partyRoster.Add(partyList->TrustMembers[i].EntityId, partyList->TrustMembers[i].ClassIconId);
            // Deliberately do not add the separate Pets/Chocobo rows.
        }

        private HighlightCategory GetCategory(IGameObject gameObject)
        {
            if (gameObject.GameObjectId == Plugin.ObjectTable.LocalPlayer?.GameObjectId)
            {
                return HighlightCategory.Player;
            }

            if (gameObject is ICharacter groupedCharacter)
            {
                if (_partyRoster.IsPartyMember(gameObject.EntityId,
                        (groupedCharacter.StatusFlags & StatusFlags.PartyMember) != 0))
                {
                    return HighlightCategory.PartyMember;
                }

                if ((groupedCharacter.StatusFlags & StatusFlags.AllianceMember) != 0)
                {
                    return HighlightCategory.AllianceMember;
                }
            }

            switch (gameObject.ObjectKind)
            {
                case ObjectKind.Pc:
                    if (gameObject is ICharacter character)
                    {
                        if ((character.StatusFlags & StatusFlags.Friend) != 0)
                        {
                            return HighlightCategory.Friend;
                        }
                    }

                    return HighlightCategory.OtherPlayer;

                case ObjectKind.BattleNpc:
                    if (gameObject is IBattleNpc battleNpc)
                    {
                        BattleNpcSubKind subKind = (BattleNpcSubKind)battleNpc.SubKind;
                        if (subKind is BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy)
                        {
                            return HighlightCategory.Pet;
                        }

                        if (subKind is BattleNpcSubKind.Combatant or BattleNpcSubKind.BNpcPart)
                        {
                            return Utils.IsHostile(gameObject) ? HighlightCategory.Enemy : HighlightCategory.Npc;
                        }

                        if (battleNpc.SubKind == 10)
                        {
                            return HighlightCategory.Npc;
                        }
                    }

                    return HighlightCategory.Object;

                case ObjectKind.EventNpc:
                    return HighlightCategory.Npc;
                case ObjectKind.Companion:
                    return HighlightCategory.Minion;
                default:
                    return HighlightCategory.Object;
            }
        }

        private void MigrateCustomColors()
        {
            ConfigurationManager manager = ConfigurationManager.Instance;
            bool changed = false;
            changed |= manager.GetConfigObject<PlayerHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<EnemyHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<PartyMembersHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<AllianceMembersHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<FriendsHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<OtherPlayersHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<PetsHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<NpcsHighlightingConfig>().EnsureCustomColors();
            changed |= manager.GetConfigObject<MinionsHighlightingConfig>().EnsureCustomColors();

            if (changed)
            {
                manager.ForceNeedsSave();
            }
        }

        private static ObjectHighlightColor ToNativeColor(VieriNativeOutlineColor color)
        {
            return (ObjectHighlightColor)((byte)color + 1);
        }

        private bool TryGetRoleHighlightColor(IGameObject gameObject, out ObjectHighlightColor color)
        {
            color = ObjectHighlightColor.None;
            if (gameObject is not ICharacter character)
            {
                return false;
            }

            color = _partyRoster.ResolveRole(gameObject.EntityId, character.ClassJob.ValueNullable?.Role ?? 0, _partyIconRoles) switch
            {
                HighlightCombatRole.Tank => ToNativeColor(VieriNativeOutlineColor.Blue),
                HighlightCombatRole.Healer => ToNativeColor(VieriNativeOutlineColor.Green),
                HighlightCombatRole.Damage => ToNativeColor(VieriNativeOutlineColor.Red),
                _ => ObjectHighlightColor.None
            };

            return color != ObjectHighlightColor.None;
        }

        private static uint ToLineColor(VieriNativeOutlineColor color)
        {
            // ImGui draw-list colors use packed ABGR.
            return color switch
            {
                VieriNativeOutlineColor.Red => 0xFF0000FF,
                VieriNativeOutlineColor.Green => 0xFF00FF00,
                VieriNativeOutlineColor.Blue => 0xFFFF0000,
                VieriNativeOutlineColor.Yellow => 0xFF00FFFF,
                VieriNativeOutlineColor.Orange => 0xFF0080FF,
                _ => 0xFFFF00FF
            };
        }

        private static unsafe bool IsCharacterPreviewVisible()
        {
            AtkUnitBase* character = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("Character", 1).Address;
            if (character != null && character->IsVisible)
            {
                return true;
            }

            AtkUnitBase* inspect = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("CharacterInspect", 1).Address;
            return inspect != null && inspect->IsVisible;
        }

        private static unsafe void SetHighlight(IGameObject gameObject, ObjectHighlightColor color)
        {
            NativeGameObject* nativeObject = (NativeGameObject*)gameObject.Address;
            nativeObject->Highlight(color);
        }
    }
}
