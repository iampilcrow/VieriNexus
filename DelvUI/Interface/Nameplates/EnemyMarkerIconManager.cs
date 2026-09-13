using DelvUI.Config;
using DelvUI.Interface.GeneralElements;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DelvUI.Interface.Nameplates
{
    public sealed unsafe class EnemyMarkerIconManager : IDisposable
    {
        private const string HandoffFileName = ".vieri-marker-icon-handoff";
        private static readonly TimeSpan LeaseRefreshInterval = TimeSpan.FromSeconds(1);

        public static EnemyMarkerIconManager? Instance { get; private set; }

        private readonly Dictionary<nint, NodeState> _nodeStates = new();
        private readonly string _handoffPath;
        private nint _addonAddress;
        private DateTime _lastLeaseRefresh = DateTime.MinValue;

        public int EnemyMarkerCount { get; private set; }
        public int VisibleEnemyMarkerCount { get; private set; }

        private EnemyMarkerIconManager()
        {
            DirectoryInfo? configRoot = Directory.GetParent(ConfigurationManager.Instance.ConfigDirectory);
            _handoffPath = Path.Combine(configRoot?.FullName ?? ConfigurationManager.Instance.ConfigDirectory, HandoffFileName);
            ImportLegacySettings();
            RefreshHandoffLease(force: true);
        }

        public static void Initialize()
        {
            Instance?.Dispose();
            Instance = new EnemyMarkerIconManager();
        }

        public void Update()
        {
            RefreshHandoffLease(force: false);

            try
            {
                UpdateNodes();
            }
            catch (Exception exception)
            {
                Plugin.Logger.Error(exception, "Failed to update integrated enemy MarkerIcon nodes.");
            }
        }

        public void Dispose()
        {
            RestoreCurrentNodes();
            _nodeStates.Clear();
            TryDeleteHandoffLease();
            Instance = null;
        }

        private void ImportLegacySettings()
        {
            EnemyNameplateConfig enemyConfig = ConfigurationManager.Instance.GetConfigObject<EnemyNameplateConfig>();
            EnemyMarkerIconConfig config = enemyConfig.MarkerIconConfig ??= new EnemyMarkerIconConfig();
            if (config.MarkerConfigVersion < 1)
            {
                // 2.7.0.9 used a separate visible adjustment checkbox because the
                // nested page was non-disableable. Move that saved value onto the
                // normal Enabled field before making the page collapsible.
                config.Enabled = config.AdjustMarkerIcons;
                config.MarkerConfigVersion = 1;
                ConfigurationManager.Instance.ForceNeedsSave();
                ConfigurationManager.Instance.SaveConfigurations(true);
            }

            if (config.ImportedFromVieriMarkerIcon)
            {
                return;
            }

            DirectoryInfo? configRoot = Directory.GetParent(ConfigurationManager.Instance.ConfigDirectory);
            string legacyPath = Path.Combine(configRoot?.FullName ?? ConfigurationManager.Instance.ConfigDirectory, "MarkerIconPriority.json");
            if (!File.Exists(legacyPath))
            {
                return;
            }

            try
            {
                JObject legacy = JObject.Parse(File.ReadAllText(legacyPath));
                config.Enabled = legacy.Value<bool?>("Enabled") ?? config.Enabled;
                config.AdjustMarkerIcons = config.Enabled;
                config.OffsetX = Math.Clamp(legacy.Value<float?>("OffsetX") ?? config.OffsetX, -300f, 300f);
                config.OffsetY = Math.Clamp(legacy.Value<float?>("OffsetY") ?? config.OffsetY, -300f, 300f);
                config.Scale = Math.Clamp(legacy.Value<float?>("Scale") ?? config.Scale, 0.25f, 5f);
                config.RenderOnTop = legacy.Value<bool?>("RenderOnTop") ?? config.RenderOnTop;
                config.ForceFullOpacity = legacy.Value<bool?>("ForceFullOpacity") ?? config.ForceFullOpacity;
                config.ImportedFromVieriMarkerIcon = true;
                config.MarkerConfigVersion = 1;

                ConfigurationManager.Instance.ForceNeedsSave();
                ConfigurationManager.Instance.SaveConfigurations(true);
                Plugin.Logger.Info($"Imported VieriMarkerIcon settings from '{legacyPath}'. The original file was left untouched.");
            }
            catch (Exception exception)
            {
                Plugin.Logger.Error(exception, $"Could not import VieriMarkerIcon settings from '{legacyPath}'.");
            }
        }

        private void UpdateNodes()
        {
            if (!Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer == null)
            {
                RestoreCurrentNodes();
                EnemyMarkerCount = 0;
                VisibleEnemyMarkerCount = 0;
                return;
            }

            EnemyMarkerIconConfig config = ConfigurationManager.Instance.GetConfigObject<EnemyNameplateConfig>().MarkerIconConfig;
            AddonNamePlate* addon = Plugin.GameGui.GetAddonByName<AddonNamePlate>("NamePlate");
            if (addon == null || addon->NamePlateObjectArray == null)
            {
                _addonAddress = 0;
                _nodeStates.Clear();
                EnemyMarkerCount = 0;
                VisibleEnemyMarkerCount = 0;
                return;
            }

            if (_addonAddress != (nint)addon)
            {
                _nodeStates.Clear();
                _addonAddress = (nint)addon;
            }

            EnemyMarkerCount = 0;
            VisibleEnemyMarkerCount = 0;
            HashSet<nint> encountered = new();

            for (int index = 0; index < AddonNamePlate.NumNamePlateObjects; index++)
            {
                AddonNamePlate.NamePlateObject* namePlate = &addon->NamePlateObjectArray[index];
                AtkImageNode* marker = namePlate->MarkerIcon;
                if (marker == null)
                {
                    continue;
                }

                AtkResNode* node = &marker->AtkResNode;
                if (namePlate->NamePlateKind != UIObjectKind.BattleNpcEnemy)
                {
                    RestoreAndForget(node);
                    continue;
                }

                EnemyMarkerCount++;
                if (!node->IsVisible())
                {
                    RestoreAndForget(node);
                    continue;
                }

                VisibleEnemyMarkerCount++;
                encountered.Add((nint)node);

                if (!ConfigurationManager.Instance.ShowHUD)
                {
                    if (!_nodeStates.TryGetValue((nint)node, out NodeState hiddenState))
                    {
                        hiddenState = NodeState.Capture(node);
                    }
                    else
                    {
                        hiddenState.CaptureExternalChanges(node);
                    }

                    hiddenState.Hide(node);
                    _nodeStates[(nint)node] = hiddenState;
                    continue;
                }

                if (!config.Enabled)
                {
                    RestoreAndForget(node);
                    continue;
                }

                if (!_nodeStates.TryGetValue((nint)node, out NodeState state))
                {
                    state = NodeState.Capture(node);
                }
                else
                {
                    state.CaptureExternalChanges(node);
                }

                state.Apply(node, config);
                _nodeStates[(nint)node] = state;
            }

            foreach (nint staleAddress in _nodeStates.Keys.Where(address => !encountered.Contains(address)).ToArray())
            {
                _nodeStates.Remove(staleAddress);
            }
        }

        private void RestoreCurrentNodes()
        {
            AddonNamePlate* addon = Plugin.GameGui.GetAddonByName<AddonNamePlate>("NamePlate");
            if (addon == null || addon->NamePlateObjectArray == null || (nint)addon != _addonAddress)
            {
                _nodeStates.Clear();
                _addonAddress = 0;
                return;
            }

            for (int index = 0; index < AddonNamePlate.NumNamePlateObjects; index++)
            {
                AtkImageNode* marker = addon->NamePlateObjectArray[index].MarkerIcon;
                if (marker != null)
                {
                    RestoreAndForget(&marker->AtkResNode);
                }
            }
        }

        private void RestoreAndForget(AtkResNode* node)
        {
            if (_nodeStates.Remove((nint)node, out NodeState state))
            {
                state.Restore(node);
            }
        }

        private void RefreshHandoffLease(bool force)
        {
            if (!force && DateTime.UtcNow - _lastLeaseRefresh < LeaseRefreshInterval)
            {
                return;
            }

            try
            {
                File.WriteAllText(_handoffPath, DateTime.UtcNow.Ticks.ToString());
                _lastLeaseRefresh = DateTime.UtcNow;
            }
            catch (Exception exception)
            {
                Plugin.Logger.Warning(exception, "Could not refresh the VieriMarkerIcon compatibility handoff.");
            }
        }

        private void TryDeleteHandoffLease()
        {
            try
            {
                if (File.Exists(_handoffPath))
                {
                    File.Delete(_handoffPath);
                }
            }
            catch (Exception exception)
            {
                Plugin.Logger.Warning(exception, "Could not remove the VieriMarkerIcon compatibility handoff.");
            }
        }

        private struct NodeState
        {
            public float OriginalX;
            public float OriginalY;
            public float OriginalScaleX;
            public float OriginalScaleY;
            public byte OriginalAlpha;
            public bool OriginalRenderedOnTop;
            public float AppliedX;
            public float AppliedY;
            public float AppliedScaleX;
            public float AppliedScaleY;
            public byte AppliedAlpha;
            public bool AppliedRenderedOnTop;

            public static NodeState Capture(AtkResNode* node)
            {
                NodeState state = new()
                {
                    OriginalX = node->X,
                    OriginalY = node->Y,
                    OriginalScaleX = node->ScaleX,
                    OriginalScaleY = node->ScaleY,
                    OriginalAlpha = node->Color.A,
                    OriginalRenderedOnTop = node->IsRenderedOnTop,
                };
                state.CopyOriginalToApplied();
                return state;
            }

            public void CaptureExternalChanges(AtkResNode* node)
            {
                if (!NearlyEqual(node->X, AppliedX)) OriginalX = node->X;
                if (!NearlyEqual(node->Y, AppliedY)) OriginalY = node->Y;
                if (!NearlyEqual(node->ScaleX, AppliedScaleX)) OriginalScaleX = node->ScaleX;
                if (!NearlyEqual(node->ScaleY, AppliedScaleY)) OriginalScaleY = node->ScaleY;
                if (node->Color.A != AppliedAlpha) OriginalAlpha = node->Color.A;
                if (node->IsRenderedOnTop != AppliedRenderedOnTop) OriginalRenderedOnTop = node->IsRenderedOnTop;
            }

            public void Apply(AtkResNode* node, EnemyMarkerIconConfig config)
            {
                AppliedX = OriginalX + config.OffsetX;
                AppliedY = OriginalY + config.OffsetY;
                AppliedScaleX = OriginalScaleX * config.Scale;
                AppliedScaleY = OriginalScaleY * config.Scale;
                AppliedAlpha = config.ForceFullOpacity ? byte.MaxValue : OriginalAlpha;
                AppliedRenderedOnTop = config.RenderOnTop || OriginalRenderedOnTop;
                node->SetPositionFloat(AppliedX, AppliedY);
                node->SetScale(AppliedScaleX, AppliedScaleY);
                node->SetAlpha(AppliedAlpha);
                node->IsRenderedOnTop = AppliedRenderedOnTop;
                node->IsDirty = true;
            }

            public void Hide(AtkResNode* node)
            {
                AppliedX = node->X;
                AppliedY = node->Y;
                AppliedScaleX = node->ScaleX;
                AppliedScaleY = node->ScaleY;
                AppliedAlpha = 0;
                AppliedRenderedOnTop = node->IsRenderedOnTop;
                node->SetAlpha(0);
                node->IsDirty = true;
            }

            public void Restore(AtkResNode* node)
            {
                node->SetPositionFloat(OriginalX, OriginalY);
                node->SetScale(OriginalScaleX, OriginalScaleY);
                node->SetAlpha(OriginalAlpha);
                node->IsRenderedOnTop = OriginalRenderedOnTop;
                node->IsDirty = true;
            }

            private void CopyOriginalToApplied()
            {
                AppliedX = OriginalX;
                AppliedY = OriginalY;
                AppliedScaleX = OriginalScaleX;
                AppliedScaleY = OriginalScaleY;
                AppliedAlpha = OriginalAlpha;
                AppliedRenderedOnTop = OriginalRenderedOnTop;
            }

            private static bool NearlyEqual(float left, float right) => MathF.Abs(left - right) < 0.0001f;
        }
    }
}
