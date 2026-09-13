using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using WrathCombo.API;
using WrathCombo.API.Enum;

namespace WrathSwitch;

internal sealed class MovementSafetyBridge : IDisposable
{
    private const string CallbackPrefix = "WrathSwitch$ManualControl";
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly string leaseInternalName;
    private readonly ICallGateSubscriber<IReadOnlyList<string>, bool, IReadOnlyList<string>> bossModConfiguration;
    private readonly ICallGateSubscriber<string> bossModGetActivePreset;
    private readonly ICallGateSubscriber<string, bool> bossModSetActivePreset;
    private readonly ICallGateSubscriber<bool> bossModClearActivePreset;
    private readonly ICallGateSubscriber<bool> bossModGetForceDisabled;
    private readonly ICallGateSubscriber<bool> bossModSetForceDisabled;
    private readonly ICallGateSubscriber<bool> navmeshGetMovementAllowed;
    private readonly ICallGateSubscriber<bool, object> navmeshSetMovementAllowed;
    private readonly ICallGateSubscriber<bool> navmeshGetAlignCamera;
    private readonly ICallGateSubscriber<bool, object> navmeshSetAlignCamera;
    private readonly ICallGateSubscriber<bool, bool> codexSetManualControlPause;
    private readonly ICallGateSubscriber<bool> autoDutyIsStopped;
    private readonly ICallGateSubscriber<string, string, string?, Guid?> wrathRegisterLease;
    private readonly ICallGateSubscriber<AutoRotationConfigOption, object?> wrathGetConfig;
    private readonly ICallGateSubscriber<Guid, AutoRotationConfigOption, object, SetResult> wrathSetConfig;
    private readonly ICallGateSubscriber<Guid, object> wrathReleaseLease;
    private readonly ICallGateProvider<int, string, object> wrathCallback;

    private bool applied;
    private bool? previousBossModAiEnabled;
    private bool? previousBossModQuestBattlesEnabled;
    private bool? previousBossModForceDisabled;
    private string? previousBossModActivePreset;
    private bool? previousNavmeshMovementAllowed;
    private bool? previousNavmeshAlignCamera;
    private bool codexPausedByUs;
    private bool autoDutyPausedByUs;
    private Guid? wrathLease;
    private long nextEnforcement;
    private bool releasingWrathLease;

    public MovementSafetyBridge(IDalamudPluginInterface pluginInterface, ICommandManager commandManager,
        IPluginLog log, string leaseInternalName = "WrathSwitch")
    {
        this.commandManager = commandManager;
        this.log = log;
        this.leaseInternalName = leaseInternalName;
        bossModConfiguration =
            pluginInterface.GetIpcSubscriber<IReadOnlyList<string>, bool, IReadOnlyList<string>>(
                "BossMod.Configuration");
        bossModGetActivePreset = pluginInterface.GetIpcSubscriber<string>("BossMod.Presets.GetActive");
        bossModSetActivePreset =
            pluginInterface.GetIpcSubscriber<string, bool>("BossMod.Presets.SetActive");
        bossModClearActivePreset = pluginInterface.GetIpcSubscriber<bool>("BossMod.Presets.ClearActive");
        bossModGetForceDisabled =
            pluginInterface.GetIpcSubscriber<bool>("BossMod.Presets.GetForceDisabled");
        bossModSetForceDisabled =
            pluginInterface.GetIpcSubscriber<bool>("BossMod.Presets.SetForceDisabled");
        navmeshGetMovementAllowed = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.GetMovementAllowed");
        navmeshSetMovementAllowed =
            pluginInterface.GetIpcSubscriber<bool, object>("vnavmesh.Path.SetMovementAllowed");
        navmeshGetAlignCamera = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.GetAlignCamera");
        navmeshSetAlignCamera = pluginInterface.GetIpcSubscriber<bool, object>("vnavmesh.Path.SetAlignCamera");
        codexSetManualControlPause =
            pluginInterface.GetIpcSubscriber<bool, bool>("VieriCodex.SetManualControlPause");
        autoDutyIsStopped = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        wrathRegisterLease =
            pluginInterface.GetIpcSubscriber<string, string, string?, Guid?>(
                "WrathCombo.RegisterForLeaseWithCallback");
        wrathGetConfig = pluginInterface.GetIpcSubscriber<AutoRotationConfigOption, object?>(
            "WrathCombo.GetAutoRotationConfigState");
        wrathSetConfig =
            pluginInterface.GetIpcSubscriber<Guid, AutoRotationConfigOption, object, SetResult>(
                "WrathCombo.SetAutoRotationConfigState");
        wrathReleaseLease = pluginInterface.GetIpcSubscriber<Guid, object>("WrathCombo.ReleaseControl");
        wrathCallback = pluginInterface.GetIpcProvider<int, string, object>(
            $"{CallbackPrefix}.WrathComboCallback");
        wrathCallback.RegisterAction(OnWrathLeaseCancelled);
    }

    public void Update(bool enabled)
    {
        if (!enabled)
        {
            Release();
            return;
        }

        if (!applied)
            Apply();

        if (Environment.TickCount64 < nextEnforcement)
            return;

        nextEnforcement = Environment.TickCount64 + 250;
        Enforce();
    }

    public void Apply()
    {
        if (applied)
            return;

        previousBossModAiEnabled = TryGetBossModConfigBool("AIConfig", "Enabled");
        previousBossModQuestBattlesEnabled = TryGetBossModConfigBool("ZoneModuleConfig", "EnableQuestBattles");
        previousBossModForceDisabled = TryInvoke(() => bossModGetForceDisabled.InvokeFunc());
        previousBossModActivePreset = TryInvoke(() => bossModGetActivePreset.InvokeFunc());
        previousNavmeshMovementAllowed = TryInvoke(() => navmeshGetMovementAllowed.InvokeFunc());
        previousNavmeshAlignCamera = TryInvoke(() => navmeshGetAlignCamera.InvokeFunc());
        applied = true;
        nextEnforcement = 0;
        Enforce();
    }

    public void Release()
    {
        if (!applied)
            return;

        ReleaseWrathLease();

        if (previousNavmeshMovementAllowed is { } movementAllowed)
            TryAction(() => navmeshSetMovementAllowed.InvokeAction(movementAllowed),
                "restore vnavmesh movement permission");

        if (previousNavmeshAlignCamera is { } alignCamera)
            TryAction(() => navmeshSetAlignCamera.InvokeAction(alignCamera),
                "restore vnavmesh camera alignment");

        RestoreBossModPreset();
        RestoreBossModConfigBool("AIConfig", "Enabled", previousBossModAiEnabled);
        // Restore quest-battle processing last so it cannot emit movement hints until the
        // original preset and general AI state are back in place.
        RestoreBossModConfigBool("ZoneModuleConfig", "EnableQuestBattles", previousBossModQuestBattlesEnabled);

        if (codexPausedByUs)
            TryAction(() => codexSetManualControlPause.InvokeFunc(false), "resume VieriCodex");

        if (autoDutyPausedByUs)
            // Restoring movement must not also clear an explicit F1 OFF during this run.
            TryAction(() => commandManager.ProcessCommand("/ad resume keep-rotation"), "resume AutoDuty");

        previousBossModAiEnabled = null;
        previousBossModQuestBattlesEnabled = null;
        previousBossModForceDisabled = null;
        previousBossModActivePreset = null;
        previousNavmeshMovementAllowed = null;
        previousNavmeshAlignCamera = null;
        codexPausedByUs = false;
        autoDutyPausedByUs = false;
        wrathLease = null;
        applied = false;
        nextEnforcement = 0;
    }

    private void Enforce()
    {
        bool? navmeshMovementAllowed = TryInvoke(() => navmeshGetMovementAllowed.InvokeFunc());
        if (navmeshMovementAllowed is { } movementAllowed)
        {
            previousNavmeshMovementAllowed ??= movementAllowed;
            if (movementAllowed)
                TryAction(() => navmeshSetMovementAllowed.InvokeAction(false), "block vnavmesh movement");
        }

        bool? navmeshAlignCamera = TryInvoke(() => navmeshGetAlignCamera.InvokeFunc());
        if (navmeshAlignCamera is { } alignCamera)
        {
            previousNavmeshAlignCamera ??= alignCamera;
            if (alignCamera)
                TryAction(() => navmeshSetAlignCamera.InvokeAction(false), "block vnavmesh camera alignment");
        }

        bool? bossModAiEnabled = TryGetBossModConfigBool("AIConfig", "Enabled");
        if (bossModAiEnabled is { } aiEnabled)
        {
            previousBossModAiEnabled ??= aiEnabled;
            if (aiEnabled)
                TrySetBossModConfigBool("AIConfig", "Enabled", false, "disable BossMod AI");
        }

        // BossMod quest modules (for example Kindred Spirits) can emit forced movement hints
        // independently of vnavmesh's path follower. Pausing the zone module prevents those
        // hints and its background path recalculations while preserving the module for resume.
        bool? questBattlesEnabled = TryGetBossModConfigBool("ZoneModuleConfig", "EnableQuestBattles");
        if (questBattlesEnabled is { } enabled)
        {
            previousBossModQuestBattlesEnabled ??= enabled;
            if (enabled)
                TrySetBossModConfigBool("ZoneModuleConfig", "EnableQuestBattles", false,
                    "pause BossMod quest-battle movement");
        }

        bool? bossModForceDisabled = TryInvoke(() => bossModGetForceDisabled.InvokeFunc());
        if (bossModForceDisabled is { } forceDisabled)
        {
            previousBossModForceDisabled ??= forceDisabled;
            if (!forceDisabled)
                TryAction(() => bossModSetForceDisabled.InvokeFunc(),
                    "force-disable BossMod autorotation movement and targeting");
        }

        if (!codexPausedByUs &&
            TryInvoke(() => codexSetManualControlPause.InvokeFunc(true)) == true)
            codexPausedByUs = true;

        if (!autoDutyPausedByUs && TryInvoke(() => autoDutyIsStopped.InvokeFunc()) == false &&
            TryAction(() => commandManager.ProcessCommand("/ad pause"), "pause AutoDuty"))
            autoDutyPausedByUs = true;

        EnforceWrathManualTargeting();
    }

    private void EnforceWrathManualTargeting()
    {
        bool dpsManual = IsWrathMode(AutoRotationConfigOption.DPSRotationMode,
            (int)DPSRotationMode.Manual);
        bool healerManual = IsWrathMode(AutoRotationConfigOption.HealerRotationMode,
            (int)HealerRotationMode.Manual);

        if (wrathLease != null && (!dpsManual || !healerManual))
            ReleaseWrathLease();

        if (wrathLease == null)
        {
            wrathLease = TryInvoke(() => wrathRegisterLease.InvokeFunc(
                leaseInternalName, "VieriWrathSwitch - Manual Movement / Targeting Only", CallbackPrefix));
        }

        if (wrathLease is not { } lease)
            return;

        if (!TrySetWrathConfig(lease, AutoRotationConfigOption.DPSRotationMode, DPSRotationMode.Manual) ||
            !TrySetWrathConfig(lease, AutoRotationConfigOption.HealerRotationMode, HealerRotationMode.Manual))
            ReleaseWrathLease();
    }

    private bool TrySetWrathConfig(Guid lease, AutoRotationConfigOption option, object value)
    {
        SetResult? result = TryInvoke(() => wrathSetConfig.InvokeFunc(lease, option, value));
        if (result is not (SetResult.Okay or SetResult.OkayWorking or SetResult.Duplicate))
        {
            log.Verbose("VieriWrathSwitch could not set Wrath {Option} to manual targeting ({Result}).",
                option, result);
            return false;
        }

        return true;
    }

    private bool IsWrathMode(AutoRotationConfigOption option, int expected)
    {
        object? value = TryInvoke(() => wrathGetConfig.InvokeFunc(option));
        try
        {
            return value != null && Convert.ToInt32(value) == expected;
        }
        catch
        {
            return false;
        }
    }

    private void ReleaseWrathLease()
    {
        if (wrathLease is not { } lease)
            return;

        releasingWrathLease = true;
        TryAction(() => wrathReleaseLease.InvokeAction(lease), "restore Wrath targeting settings");
        releasingWrathLease = false;
        wrathLease = null;
    }

    private void OnWrathLeaseCancelled(int reason, string additionalInfo)
    {
        wrathLease = null;
        nextEnforcement = releasingWrathLease ? Environment.TickCount64 + 250 : 0;
        if (!releasingWrathLease)
            log.Warning("VieriWrathSwitch Manual Control Wrath lease was revoked ({Reason}): {Info}",
                reason, additionalInfo);
    }

    public void Dispose()
    {
        Release();
        wrathCallback.UnregisterAction();
    }

    private bool? TryGetBossModConfigBool(string configType, string field)
    {
        IReadOnlyList<string>? values = TryInvoke(() =>
            bossModConfiguration.InvokeFunc([configType, field], false));
        return values is { Count: > 0 } && bool.TryParse(values[0], out bool enabled) ? enabled : null;
    }

    private void TrySetBossModConfigBool(string configType, string field, bool value, string operation) =>
        TryAction(() =>
        {
            IReadOnlyList<string> result = bossModConfiguration.InvokeFunc(
                [configType, field, value.ToString().ToLowerInvariant()], false);
            if (result.Count != 0)
                throw new InvalidOperationException(string.Join("; ", result));
        }, operation);

    private void RestoreBossModConfigBool(string configType, string field, bool? value)
    {
        if (value is { } original)
            TrySetBossModConfigBool(configType, field, original, $"restore BossMod {configType}.{field}");
    }

    private void RestoreBossModPreset()
    {
        if (previousBossModForceDisabled != false)
            return;

        if (!string.IsNullOrWhiteSpace(previousBossModActivePreset) &&
            TryInvoke(() => bossModSetActivePreset.InvokeFunc(previousBossModActivePreset)) == true)
            return;

        TryAction(() => bossModClearActivePreset.InvokeFunc(), "clear VieriWrathSwitch's BossMod pause");
    }

    private T? TryInvoke<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch
        {
            return default;
        }
    }

    private bool TryAction(Action action, string operation)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "VieriWrathSwitch could not {Operation}; the related plugin may not be loaded.",
                operation);
            return false;
        }
    }
}

