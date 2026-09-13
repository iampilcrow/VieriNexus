using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using WrathCombo.API;
using WrathCombo.API.Enum;

namespace WrathSwitch;

/// <summary>
/// Keeps Wrath's effective InCombatOnly option enabled while the user-owned
/// VieriWrathSwitch toggle is on. VieriCodex and AutoDuty intentionally set the
/// same option to false when they acquire their leases, so the effective value
/// is checked and only reasserted when another controller changes it.
/// </summary>
internal sealed class CombatOnlyBridge : IDisposable
{
    private const string CallbackPrefix = "WrathSwitch$CombatOnly";
    private readonly IPluginLog log;
    private readonly string leaseInternalName;
    private readonly ICallGateProvider<int, string, object> callback;
    private readonly ICallGateSubscriber<string, string, string?, Guid?> registerLease;
    private readonly ICallGateSubscriber<AutoRotationConfigOption, object?> getConfig;
    private readonly ICallGateSubscriber<Guid, AutoRotationConfigOption, object, SetResult> setConfig;
    private readonly ICallGateSubscriber<Guid, object> releaseLease;

    private Guid? lease;
    private long nextEnforcement;
    private bool releasing;

    public CombatOnlyBridge(IDalamudPluginInterface pluginInterface, IPluginLog log,
        string leaseInternalName = "WrathSwitch")
    {
        this.log = log;
        this.leaseInternalName = leaseInternalName;
        callback = pluginInterface.GetIpcProvider<int, string, object>(
            $"{CallbackPrefix}.WrathComboCallback");
        callback.RegisterAction(OnLeaseCancelled);
        registerLease = pluginInterface.GetIpcSubscriber<string, string, string?, Guid?>(
            "WrathCombo.RegisterForLeaseWithCallback");
        getConfig = pluginInterface.GetIpcSubscriber<AutoRotationConfigOption, object?>(
            "WrathCombo.GetAutoRotationConfigState");
        setConfig = pluginInterface.GetIpcSubscriber<Guid, AutoRotationConfigOption, object, SetResult>(
            "WrathCombo.SetAutoRotationConfigState");
        releaseLease = pluginInterface.GetIpcSubscriber<Guid, object>("WrathCombo.ReleaseControl");
    }

    public void Update(bool enabled)
    {
        if (!enabled)
        {
            Release();
            return;
        }

        var now = Environment.TickCount64;
        if (now < nextEnforcement)
            return;

        nextEnforcement = now + 250;
        Enforce();
    }

    public bool Apply()
    {
        nextEnforcement = Environment.TickCount64 + 250;
        return Enforce();
    }

    public void Release()
    {
        ReleaseLease();
        nextEnforcement = 0;
    }

    private bool Enforce()
    {
        try
        {
            if (getConfig.InvokeFunc(AutoRotationConfigOption.InCombatOnly) is true)
                return true;

            // A newer controller may have superseded our already-true registration. Wrath's
            // duplicate Set result does not refresh lease priority, so recreate it before reasserting.
            ReleaseLease();
            lease ??= registerLease.InvokeFunc(
                leaseInternalName, "VieriWrathSwitch - In Combat Only", CallbackPrefix);
            if (lease is not { } activeLease)
                return false;

            var result = setConfig.InvokeFunc(
                activeLease, AutoRotationConfigOption.InCombatOnly, true);
            if (result is SetResult.Okay or SetResult.OkayWorking or SetResult.Duplicate)
                return true;

            // A revoked or otherwise stale lease must be discarded so a later
            // update can register again after Wrath's temporary blacklist ends.
            log.Verbose("VieriWrathSwitch could not enforce In Combat Only ({Result}).", result);
            lease = null;
            return false;
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "VieriWrathSwitch could not enforce Wrath In Combat Only; Wrath may not be ready.");
            return false;
        }
    }

    private void ReleaseLease()
    {
        if (lease is not { } activeLease)
            return;

        releasing = true;
        try
        {
            releaseLease.InvokeAction(activeLease);
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "VieriWrathSwitch could not release its In Combat Only Wrath lease.");
        }
        finally
        {
            releasing = false;
            lease = null;
        }
    }

    private void OnLeaseCancelled(int reason, string additionalInfo)
    {
        lease = null;
        nextEnforcement = releasing ? Environment.TickCount64 + 250 : 0;
        if (!releasing)
            log.Warning("VieriWrathSwitch In Combat Only control was revoked ({Reason}): {Info}",
                reason, additionalInfo);
    }

    public void Dispose()
    {
        Release();
        callback.UnregisterAction();
    }
}

