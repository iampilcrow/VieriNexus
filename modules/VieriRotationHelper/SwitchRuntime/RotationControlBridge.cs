using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using WrathCombo.API;
using WrathCombo.API.Enum;

namespace WrathSwitch;

/// <summary>
/// Makes the main VieriWrathSwitch toggle authoritative. Wrath's global state can be green while
/// the current job has no Auto-Rotation combo enabled, so every explicit ON also leases and readies
/// the current job. The lease is retained and repaired if another automation controller supersedes it.
/// </summary>
internal sealed class RotationControlBridge : IDisposable
{
    private const string CallbackPrefix = "WrathSwitch$Rotation";
    private const long AutomatedOffHoldMilliseconds = 750;
    private static readonly TimeSpan ReadinessGracePeriod = TimeSpan.FromSeconds(8);

    private readonly IPluginLog log;
    private readonly string leaseInternalName;
    private readonly ICallGateProvider<int, string, object> callback;
    private readonly ICallGateSubscriber<bool> ipcReady;
    private readonly ICallGateSubscriber<string, string, string?, Guid?> registerLease;
    private readonly ICallGateSubscriber<bool> getAutoRotationState;
    private readonly ICallGateSubscriber<Guid, bool, SetResult> setAutoRotationState;
    private readonly ICallGateSubscriber<bool> isCurrentJobReady;
    private readonly ICallGateSubscriber<Guid, SetResult> setCurrentJobReady;
    private readonly ICallGateSubscriber<Guid, object> releaseLease;
    private readonly ICallGateSubscriber<bool> codexIsRunning;
    private readonly ICallGateSubscriber<bool> autoDutyIsStopped;
    private readonly ICallGateSubscriber<bool> autoDutyIsPaused;

    private Guid? lease;
    private bool? desiredState;
    private long nextEnforcement;
    private DateTime nextJobReadinessRefresh;
    private DateTime readinessGraceUntil;
    private bool releasing;
    private long transientOffReleaseAt;

    public RotationControlBridge(IDalamudPluginInterface pluginInterface, IPluginLog log,
        string leaseInternalName = "WrathSwitch")
    {
        this.log = log;
        this.leaseInternalName = leaseInternalName;
        callback = pluginInterface.GetIpcProvider<int, string, object>(
            $"{CallbackPrefix}.WrathComboCallback");
        callback.RegisterAction(OnLeaseCancelled);
        ipcReady = pluginInterface.GetIpcSubscriber<bool>("WrathCombo.IPCReady");
        registerLease = pluginInterface.GetIpcSubscriber<string, string, string?, Guid?>(
            "WrathCombo.RegisterForLeaseWithCallback");
        getAutoRotationState = pluginInterface.GetIpcSubscriber<bool>("WrathCombo.GetAutoRotationState");
        setAutoRotationState = pluginInterface.GetIpcSubscriber<Guid, bool, SetResult>(
            "WrathCombo.SetAutoRotationState");
        isCurrentJobReady = pluginInterface.GetIpcSubscriber<bool>(
            "WrathCombo.IsCurrentJobAutoRotationReady");
        setCurrentJobReady = pluginInterface.GetIpcSubscriber<Guid, SetResult>(
            "WrathCombo.SetCurrentJobAutoRotationReady");
        releaseLease = pluginInterface.GetIpcSubscriber<Guid, object>("WrathCombo.ReleaseControl");
        codexIsRunning = pluginInterface.GetIpcSubscriber<bool>("VieriCodex.IsRunning");
        autoDutyIsStopped = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        autoDutyIsPaused = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsPaused");
    }

    public bool Set(bool enabled, bool allowAutomationYield = true)
    {
        desiredState = enabled;
        transientOffReleaseAt = 0;
        nextEnforcement = 0;
        bool applied = Enforce(force: true);
        if (applied && !enabled && allowAutomationYield && IsAutomationActive())
        {
            // Give OFF long enough to be immediate and visible (and to allow a rapid second
            // keypress to turn it back on), then yield to the automation lease already in flight.
            transientOffReleaseAt = Environment.TickCount64 + AutomatedOffHoldMilliseconds;
            log.Information("VieriWrathSwitch applied a temporary OFF while automation is active.");
        }

        return applied;
    }

    public void Update() => Update(Environment.TickCount64);

    internal void Update(long now)
    {
        if (desiredState == null)
            return;

        if (transientOffReleaseAt != 0 && now >= transientOffReleaseAt)
        {
            desiredState = null;
            transientOffReleaseAt = 0;
            RecycleLease();
            log.Information("VieriWrathSwitch yielded temporary OFF control back to active automation.");
            return;
        }

        if (now < nextEnforcement)
            return;

        nextEnforcement = now + 250;
        Enforce(force: false);
    }

    // Only a deliberate new automation session may clear a previous OFF. Combat updates,
    // quest transitions and gear-maintenance resumes must never call this method.
    public bool BeginAutomation()
    {
        if (desiredState != false)
            return false;

        desiredState = null;
        transientOffReleaseAt = 0;
        RecycleLease();
        log.Information("VieriWrathSwitch released the earlier OFF override for a new automation session.");
        return true;
    }

    private bool IsAutomationActive()
    {
        try
        {
            if (codexIsRunning.HasFunction && codexIsRunning.InvokeFunc())
                return true;
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "Could not determine whether VieriCodex automation is active.");
        }

        try
        {
            if (!autoDutyIsStopped.HasFunction || autoDutyIsStopped.InvokeFunc())
                return false;

            return !autoDutyIsPaused.HasFunction || !autoDutyIsPaused.InvokeFunc();
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "Could not determine whether VieriAutoDuty automation is active.");
            return false;
        }
    }

    private bool Enforce(bool force)
    {
        try
        {
            if (!ipcReady.InvokeFunc())
                return false;

            if (lease == null && !RegisterAndApply())
                return false;

            bool enabled = desiredState!.Value;
            bool effectiveState = getAutoRotationState.InvokeFunc();
            if (effectiveState != enabled)
            {
                // Wrath resolves competing leases by their most recent update. A duplicate Set call
                // does not refresh that timestamp, so replace our stale lease to reclaim authority.
                RecycleLease();
                return RegisterAndApply();
            }

            if (!enabled)
                return true;

            if (DateTime.UtcNow >= nextJobReadinessRefresh)
            {
                if (!ApplyCurrentJob())
                    return false;
            }

            if (!force && DateTime.UtcNow >= readinessGraceUntil && !isCurrentJobReady.InvokeFunc())
            {
                // A green global switch with an unready job is the exact silent failure this bridge
                // prevents. Recreate the lease so Wrath performs its full job setup again.
                RecycleLease();
                return RegisterAndApply();
            }

            return true;
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "VieriWrathSwitch could not enforce authoritative rotation control.");
            return false;
        }
    }

    private bool RegisterAndApply()
    {
        lease = registerLease.InvokeFunc(
            leaseInternalName, "VieriWrathSwitch - Main Rotation Control", CallbackPrefix);
        if (lease is not { } activeLease)
            return false;

        SetResult rotationResult = setAutoRotationState.InvokeFunc(activeLease, desiredState!.Value);
        if (!Succeeded(rotationResult))
        {
            log.Warning("VieriWrathSwitch could not control Wrath Auto-Rotation ({Result}).", rotationResult);
            RecycleLease();
            return false;
        }

        nextJobReadinessRefresh = DateTime.MinValue;
        readinessGraceUntil = DateTime.UtcNow + ReadinessGracePeriod;
        return !desiredState.Value || ApplyCurrentJob();
    }

    private bool ApplyCurrentJob()
    {
        if (lease is not { } activeLease)
            return false;

        SetResult result = setCurrentJobReady.InvokeFunc(activeLease);
        if (!Succeeded(result))
        {
            log.Warning("VieriWrathSwitch could not ready the current job for Auto-Rotation ({Result}).",
                result);
            RecycleLease();
            return false;
        }

        nextJobReadinessRefresh = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        if (result is not SetResult.Duplicate)
            readinessGraceUntil = DateTime.UtcNow + ReadinessGracePeriod;
        return true;
    }

    private void RecycleLease()
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
            log.Verbose(ex, "VieriWrathSwitch could not recycle its rotation lease.");
        }
        finally
        {
            releasing = false;
            lease = null;
            nextJobReadinessRefresh = DateTime.MinValue;
        }
    }

    private void OnLeaseCancelled(int reason, string additionalInfo)
    {
        lease = null;
        nextJobReadinessRefresh = DateTime.MinValue;
        nextEnforcement = releasing ? Environment.TickCount64 + 250 : 0;
        if (!releasing)
            log.Warning("VieriWrathSwitch rotation control was revoked ({Reason}): {Info}", reason, additionalInfo);
    }

    private static bool Succeeded(SetResult result) =>
        result is SetResult.Okay or SetResult.OkayWorking or SetResult.Duplicate;

    public void Dispose()
    {
        desiredState = null;
        transientOffReleaseAt = 0;
        RecycleLease();
        callback.UnregisterAction();
    }
}

