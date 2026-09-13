using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Bindings.ImGui;
using DelvUI.Config;
using DelvUI.Helpers;
using DelvUI.Interface.GeneralElements;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DelvUI.Interface.Nameplates
{
    internal class NameplatesHud : HudElement
    {
        private NameplatesGeneralConfig Config => (NameplatesGeneralConfig)_config;

        private NameplateWithPlayerBar _playerHud;
        private NameplateWithEnemyBar _enemyHud;
        private EnemyNameplateConfig _enemyConfig;
        private NameplateWithPlayerBar _partyMemberHud;
        private NameplateWithPlayerBar _allianceMemberHud;
        private NameplateWithPlayerBar _friendsHud;
        private NameplateWithPlayerBar _otherPlayersHud;
        private NameplateWithBar _petHud;
        private NameplateWithBar _npcHud;
        private Nameplate _minionNPCHud;
        private Nameplate _objectHud;

        private bool _wasHovering;

        public NameplatesHud(NameplatesGeneralConfig config) : base(config)
        {
            ConfigurationManager manager = ConfigurationManager.Instance;
            _playerHud = new NameplateWithPlayerBar(manager.GetConfigObject<PlayerNameplateConfig>());
            _enemyConfig = manager.GetConfigObject<EnemyNameplateConfig>();
            _enemyHud = new NameplateWithEnemyBar(_enemyConfig);
            _partyMemberHud = new NameplateWithPlayerBar(manager.GetConfigObject<PartyMembersNameplateConfig>());
            _allianceMemberHud = new NameplateWithPlayerBar(manager.GetConfigObject<AllianceMembersNameplateConfig>());
            _friendsHud = new NameplateWithPlayerBar(manager.GetConfigObject<FriendPlayerNameplateConfig>());
            _otherPlayersHud = new NameplateWithPlayerBar(manager.GetConfigObject<OtherPlayerNameplateConfig>());
            _petHud = new NameplateWithBar(manager.GetConfigObject<PetNameplateConfig>());
            _npcHud = new NameplateWithBar(manager.GetConfigObject<NPCNameplateConfig>());
            _minionNPCHud = new Nameplate(manager.GetConfigObject<MinionNPCNameplateConfig>());
            _objectHud = new Nameplate(manager.GetConfigObject<ObjectsNameplateConfig>());
        }

        public void StopPreview()
        {
            _enemyHud.StopPreview();
        }

        public void StopMouseover()
        {
            if (_wasHovering)
            {
                InputsHelper.Instance.ClearTarget();
                _wasHovering = false;
            }
        }

        protected override void CreateDrawActions(Vector2 origin)
        {
            if (!_config.Enabled || NameplatesManager.Instance == null)
            {
                StopMouseover();
                return;
            }

            IGameObject? mouseoveredActor = null;
            bool ignoreMouseover = false;
            List<NameplateData> nameplateData = new(NameplatesManager.Instance.Data);
            ApplyEnemyLayout(nameplateData);

            foreach (NameplateData data in nameplateData)
            {
                Nameplate? nameplate = GetNameplate(data);
                if (nameplate == null || !nameplate.Enabled) { continue; }

                // raycasting
                if (IsPointObstructed(data)) { continue; }

                if (nameplate is NameplateWithBar nameplateWithBar)
                {
                    // draw bar
                    AddDrawActions(nameplateWithBar.GetBarDrawActions(data));

                    // find mouseovered nameplate
                    var (isHovering, ignore) = nameplateWithBar.GetMouseoverState(data);
                    if (isHovering)
                    {
                        mouseoveredActor = data.GameObject;
                        ignoreMouseover = ignore;
                    }
                }

                // draw elements
                AddDrawActions(nameplate.GetElementsDrawActions(data));
            }

            // mouseover
            if (mouseoveredActor != null)
            {
                _wasHovering = true;
                InputsHelper.Instance.SetTarget(mouseoveredActor, ignoreMouseover);

                if (InputsHelper.Instance.LeftButtonClicked)
                {
                    Plugin.TargetManager.Target = mouseoveredActor;
                    InputsHelper.Instance.ClearClicks();
                }
                else if (InputsHelper.Instance.RightButtonClicked)
                {
                    InputsHelper.Instance.ClearClicks();
                }
            }
            else if (_wasHovering)
            {
                InputsHelper.Instance.ClearTarget();
                _wasHovering = false;
            }
        }

        private void ApplyEnemyLayout(List<NameplateData> data)
        {
            if (_enemyConfig.LayoutMode != EnemyNameplateLayoutMode.Stack || data.Count < 2)
            {
                return;
            }

            List<int> enemyIndices = new();
            for (int i = 0; i < data.Count; i++)
            {
                if (GetNameplate(data[i]) == _enemyHud)
                {
                    enemyIndices.Add(i);
                }
            }

            if (enemyIndices.Count < 2)
            {
                return;
            }

            int[] parents = new int[enemyIndices.Count];
            for (int i = 0; i < parents.Length; i++)
            {
                parents[i] = i;
            }

            float groupingWidth = Math.Max(1, _enemyConfig.StackGroupingWidth);
            float spacing = Math.Max(1, _enemyConfig.StackSpacing);

            for (int i = 0; i < enemyIndices.Count; i++)
            {
                Vector2 first = data[enemyIndices[i]].ScreenPosition;
                for (int j = i + 1; j < enemyIndices.Count; j++)
                {
                    Vector2 second = data[enemyIndices[j]].ScreenPosition;
                    if (Math.Abs(first.X - second.X) < groupingWidth &&
                        Math.Abs(first.Y - second.Y) < spacing)
                    {
                        Union(parents, i, j);
                    }
                }
            }

            Dictionary<int, List<int>> groups = new();
            for (int i = 0; i < enemyIndices.Count; i++)
            {
                int root = FindRoot(parents, i);
                if (!groups.TryGetValue(root, out List<int>? group))
                {
                    group = new List<int>();
                    groups[root] = group;
                }

                group.Add(enemyIndices[i]);
            }

            foreach (List<int> group in groups.Values)
            {
                if (group.Count > 1)
                {
                    StackGroup(data, group, spacing);
                }
            }
        }

        private static void StackGroup(List<NameplateData> data, List<int> group, float requestedSpacing)
        {
            group.Sort((leftIndex, rightIndex) =>
            {
                NameplateData left = data[leftIndex];
                NameplateData right = data[rightIndex];
                int yComparison = left.ScreenPosition.Y.CompareTo(right.ScreenPosition.Y);
                if (yComparison != 0)
                {
                    return yComparison;
                }

                int xComparison = left.ScreenPosition.X.CompareTo(right.ScreenPosition.X);
                return xComparison != 0
                    ? xComparison
                    : (left.GameObject?.EntityId ?? 0).CompareTo(right.GameObject?.EntityId ?? 0);
            });

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            float top = viewport.Pos.Y + requestedSpacing;
            float bottom = viewport.Pos.Y + viewport.Size.Y - requestedSpacing;
            float availableHeight = Math.Max(0, bottom - top);
            float spacing = group.Count > 1
                ? Math.Min(requestedSpacing, availableHeight / (group.Count - 1))
                : requestedSpacing;

            float averageY = 0;
            foreach (int index in group)
            {
                averageY += data[index].ScreenPosition.Y;
            }

            averageY /= group.Count;
            float stackHeight = spacing * (group.Count - 1);
            float startY = Math.Clamp(averageY - stackHeight / 2f, top, Math.Max(top, bottom - stackHeight));

            for (int i = 0; i < group.Count; i++)
            {
                int dataIndex = group[i];
                NameplateData nameplate = data[dataIndex];
                nameplate.ScreenPosition.Y = startY + spacing * i;
                data[dataIndex] = nameplate;
            }
        }

        private static int FindRoot(int[] parents, int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }

            return index;
        }

        private static void Union(int[] parents, int first, int second)
        {
            int firstRoot = FindRoot(parents, first);
            int secondRoot = FindRoot(parents, second);
            if (firstRoot != secondRoot)
            {
                parents[secondRoot] = firstRoot;
            }
        }

        private unsafe Nameplate? GetNameplate(NameplateData data)
        {
            switch (data.Kind)
            {
                case ObjectKind.Pc:
                    if (data.GameObject?.EntityId == Plugin.ObjectTable.LocalPlayer?.EntityId)
                    {
                        return _playerHud;
                    }

                    if (data.GameObject is ICharacter character)
                    {
                        if ((character.StatusFlags & StatusFlags.PartyMember) != 0) // PartyMember
                        {
                            return _partyMemberHud;
                        }
                        else if ((character.StatusFlags & StatusFlags.AllianceMember) != 0) // AllianceMember
                        {
                            return _allianceMemberHud;
                        }
                        else if ((character.StatusFlags & StatusFlags.Friend) != 0) // Friend
                        {
                            return _friendsHud;
                        }
                    }

                    return _otherPlayersHud;

                case ObjectKind.BattleNpc:
                    if (data.GameObject is IBattleNpc battleNpc)
                    {
                        if ((BattleNpcSubKind)battleNpc.SubKind == BattleNpcSubKind.Pet ||
                            (BattleNpcSubKind)battleNpc.SubKind == BattleNpcSubKind.Buddy)
                        {
                            return _petHud;
                        }
                        else if ((BattleNpcSubKind)battleNpc.SubKind == BattleNpcSubKind.Combatant ||
                                 (BattleNpcSubKind)battleNpc.SubKind == BattleNpcSubKind.BNpcPart)
                        {
                            return Utils.IsHostile(data.GameObject) ? _enemyHud : _npcHud;
                        }
                        else if (battleNpc.SubKind == 10) // island released minions
                        {
                            return _npcHud;
                        }
                    }
                    break;

                case ObjectKind.EventNpc: return _npcHud;
                case ObjectKind.Companion: return _minionNPCHud;
                default: return _objectHud;
            }

            return null;
        }

        private unsafe bool IsPointObstructed(NameplateData data)
        {
            if (data.GameObject == null) { return true; }
            if (Config.OcclusionMode == NameplatesOcclusionMode.None || data.IgnoreOcclusion) { return false; }

            Camera camera = Control.Instance()->CameraManager.Camera->CameraBase.SceneCamera;
            Vector3 cameraPos = camera.Object.Position;

            BGCollisionModule* collisionModule = Framework.Instance()->BGCollisionModule;
            if (collisionModule == null)
            {
                return false;
            }

            int flag = Config.RaycastFlag();
            int* flags = stackalloc int[] { flag, 0, flag, 0 };
            bool obstructed = false;

            // simple mode
            if (Config.OcclusionMode == NameplatesOcclusionMode.Simple)
            {
                Vector3 direction = Vector3.Normalize(data.WorldPosition - cameraPos);
                RaycastHit hit;
                obstructed = collisionModule->RaycastMaterialFilter(&hit, &cameraPos, &direction, data.Distance, 1, flags);
            }
            // full mode
            else
            {
                int obstructionCount = 0;
                Vector2[] points = new Vector2[]
                {
                    data.ScreenPosition + new Vector2(-30, 0), // left
                    data.ScreenPosition + new Vector2(30, 0), // right
                };

                foreach (Vector2 point in points)
                {
                    Ray ray = camera.ScreenPointToRay(point);
                    RaycastHit hit;

                    Vector3 origin = new Vector3(ray.Origin.X, ray.Origin.Y, ray.Origin.Z);
                    Vector3 direction = new Vector3(ray.Direction.X, ray.Direction.Y, ray.Direction.Z);

                    if (collisionModule->RaycastMaterialFilter(&hit, &origin, &direction, data.Distance, 1, flags))
                    {
                        obstructionCount++;
                    }
                }

                obstructed = obstructionCount == points.Length;
            }

            return obstructed;
        }
    }
}
