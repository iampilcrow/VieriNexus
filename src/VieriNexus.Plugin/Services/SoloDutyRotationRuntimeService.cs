using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed record SoloDutyRotationRuntimeStatus(bool IsRelevant, bool IsActive, string Message);

/// <summary>
/// Nexus-owned stock-Questionable solo-duty rotation handoff. Boss Mod retains encounter
/// movement while Wrath handles actions. All IPC values are primitive public-contract values;
/// Nexus neither embeds nor links the rotation provider implementation.
/// </summary>
internal sealed class SoloDutyRotationRuntimeService : IDisposable
{
    private const string CallbackPrefix = "VieriNexus$SoloDutyRotation";
    private const string NormalMovementPreset = "Questionable - Normal Movement";
    private const string QuestBattlePreset = "Questionable - Quest Battles";
    private const int DpsRotationModeOption = 1;
    private const int HealerRotationModeOption = 2;
    private const int ManageKardiaOption = 8;
    private const int AutoRezOption = 9;
    private const int AutoRezDpsJobsOption = 10;
    private const int AutoCleanseOption = 11;
    private const int IncludeNpcsOption = 12;
    private const int OnlyAttackInCombatOption = 13;
    private const int AutoRezOutOfPartyOption = 15;
    private const int DpsAoeTargetsOption = 16;
    private const int HealerAlwaysHardTargetOption = 20;
    private const int InCombatOnlyOption = 0;
    private const int DpsManualMode = 0;
    private const int DpsNearestMode = 6;
    private const int HealerLowestCurrentMode = 2;

    private readonly ProgressionProviderService providers;
    private readonly ProgressionRuntimeService progression;
    private readonly ProgressAtlasActionService atlas;
    private readonly ITargetManager targetManager;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<bool> ipcReady;
    private readonly ICallGateSubscriber<bool> beginRotationAutomation;
    private readonly ICallGateSubscriber<string, string, string?, Guid?> registerLease;
    private readonly ICallGateSubscriber<Guid, bool, object?> setAutoRotationState;
    private readonly ICallGateSubscriber<Guid, object?> setCurrentJobReady;
    private readonly ICallGateSubscriber<Guid, object, object, object?> setConfig;
    private readonly ICallGateSubscriber<Guid, object> releaseLease;
    private readonly ICallGateProvider<int, string, object> callback;
    private readonly ICallGateSubscriber<string?> getActiveBossModPreset;
    private readonly ICallGateSubscriber<string, bool> setActiveBossModPreset;

    private Guid? lease;
    private bool boundaryObserved;
    private bool suppressedForBoundary;
    private bool leaseRevoked;
    private DateTimeOffset nextStartAttempt;
    private DateTimeOffset missingHostileSince;
    private DateTimeOffset fallbackEnabledAt;
    private DateTimeOffset nextFallbackAllowedAt;
    private bool fallbackEnabled;
    private string message = "Solo-duty rotation handoff is idle.";

    internal SoloDutyRotationRuntimeService(
        IDalamudPluginInterface pluginInterface,
        ProgressionProviderService providers,
        ProgressionRuntimeService progression,
        ProgressAtlasActionService atlas,
        ITargetManager targetManager,
        IPluginLog log)
    {
        this.providers = providers;
        this.progression = progression;
        this.atlas = atlas;
        this.targetManager = targetManager;
        this.log = log;
        ipcReady = pluginInterface.GetIpcSubscriber<bool>("WrathCombo.IPCReady");
        beginRotationAutomation = pluginInterface.GetIpcSubscriber<bool>("WrathSwitch.BeginAutomation");
        registerLease = pluginInterface.GetIpcSubscriber<string, string, string?, Guid?>(
            "WrathCombo.RegisterForLeaseWithCallback");
        setAutoRotationState = pluginInterface.GetIpcSubscriber<Guid, bool, object?>(
            "WrathCombo.SetAutoRotationState");
        setCurrentJobReady = pluginInterface.GetIpcSubscriber<Guid, object?>(
            "WrathCombo.SetCurrentJobAutoRotationReady");
        setConfig = pluginInterface.GetIpcSubscriber<Guid, object, object, object?>(
            "WrathCombo.SetAutoRotationConfigState");
        releaseLease = pluginInterface.GetIpcSubscriber<Guid, object>("WrathCombo.ReleaseControl");
        callback = pluginInterface.GetIpcProvider<int, string, object>(
            $"{CallbackPrefix}.WrathComboCallback");
        callback.RegisterAction(OnLeaseCancelled);
        getActiveBossModPreset = pluginInterface.GetIpcSubscriber<string?>("BossMod.Presets.GetActive");
        setActiveBossModPreset = pluginInterface.GetIpcSubscriber<string, bool>("BossMod.Presets.SetActive");
    }

    internal SoloDutyRotationRuntimeStatus Status => new(boundaryObserved, lease.HasValue, message);

    internal void Update(DateTimeOffset now, bool isInDuty)
    {
        bool nexusQuestActive = progression.IsQuestExecutionActive || atlas.IsQuestExecutionActive;
        bool stockSelected = providers.IsStockQuestionableSelected;
        bool candidateBoundary = stockSelected && nexusQuestActive && isInDuty;
        if (!candidateBoundary)
        {
            if (boundaryObserved)
                EndBoundary();
            return;
        }

        boundaryObserved = true;
        if (leaseRevoked)
        {
            leaseRevoked = false;
            suppressedForBoundary = true;
            ReleaseLease();
            RestoreBossModRotation();
            message = "Wrath released Nexus control; Boss Mod rotation is active for the rest of this solo duty.";
            log.Warning("{Message}", message);
            return;
        }

        if (suppressedForBoundary)
            return;

        bool ready = IsRotationProviderReady();
        if (!SoloDutyCombatPolicy.ShouldOwnRotation(stockSelected, nexusQuestActive, isInDuty, ready))
        {
            if (lease.HasValue)
            {
                ReleaseLease();
                RestoreBossModRotation();
            }
            message = "Boss Mod is handling this solo duty; no compatible Wrath IPC is ready.";
            return;
        }

        if (!lease.HasValue)
        {
            if (now < nextStartAttempt)
                return;
            nextStartAttempt = now + TimeSpan.FromSeconds(2);
            if (!TryStart())
            {
                suppressedForBoundary = true;
                RestoreBossModRotation();
                message = "Nexus could not establish the Wrath handoff; Boss Mod rotation remains active for this solo duty.";
            }
            return;
        }

        UpdateFallback(now);
    }

    internal void Shutdown()
    {
        ReleaseLease();
        boundaryObserved = false;
        suppressedForBoundary = false;
        leaseRevoked = false;
        ResetFallback();
        message = "Solo-duty rotation handoff is idle.";
    }

    private bool TryStart()
    {
        try
        {
            if (beginRotationAutomation.HasFunction)
                beginRotationAutomation.InvokeFunc();

            Guid? granted = registerLease.InvokeFunc("VieriNexus", "VieriNexus", CallbackPrefix);
            if (granted is not { } activeLease)
                return false;
            lease = activeLease;

            if (!Succeeded(setAutoRotationState.InvokeFunc(activeLease, true)) ||
                !Succeeded(setCurrentJobReady.InvokeFunc(activeLease)) ||
                !SetConfiguration(activeLease))
            {
                ReleaseLease();
                return false;
            }

            if (!setActiveBossModPreset.InvokeFunc(NormalMovementPreset))
            {
                ReleaseLease();
                return false;
            }

            ResetFallback();
            message = "Nexus is using Wrath for actions while Boss Mod owns solo-duty movement.";
            log.Information("{Message}", message);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Nexus could not start the stock-Questionable solo-duty rotation handoff.");
            ReleaseLease();
            return false;
        }
    }

    private bool SetConfiguration(Guid activeLease)
    {
        (int Option, object Value)[] values =
        [
            (DpsRotationModeOption, DpsManualMode),
            (HealerRotationModeOption, HealerLowestCurrentMode),
            (HealerAlwaysHardTargetOption, false),
            (InCombatOnlyOption, false),
            (IncludeNpcsOption, true),
            (OnlyAttackInCombatOption, false),
            (AutoCleanseOption, true),
            (AutoRezOption, true),
            (AutoRezDpsJobsOption, true),
            (ManageKardiaOption, true),
            (DpsAoeTargetsOption, 3),
            (AutoRezOutOfPartyOption, false),
        ];
        return values.All(value => Succeeded(setConfig.InvokeFunc(activeLease, value.Option, value.Value)));
    }

    private void UpdateFallback(DateTimeOffset now)
    {
        bool hasHostile = targetManager.Target is IBattleNpc
        {
            IsTargetable: true,
            IsDead: false,
        } target && target.StatusFlags.HasFlag(StatusFlags.Hostile);

        if (hasHostile)
        {
            missingHostileSince = default;
            nextFallbackAllowedAt = default;
        }
        else if (missingHostileSince == default)
            missingHostileSince = now;

        TimeSpan missingFor = hasHostile || missingHostileSince == default
            ? TimeSpan.Zero
            : now - missingHostileSince;
        TimeSpan enabledFor = fallbackEnabledAt == default
            ? TimeSpan.Zero
            : now - fallbackEnabledAt;
        bool shouldEnable = fallbackEnabled
            ? !SoloDutyCombatPolicy.ShouldDisableFallback(hasHostile, enabledFor)
            : SoloDutyCombatPolicy.ShouldEnableFallback(
                hasHostile,
                missingFor,
                nextFallbackAllowedAt == default || now >= nextFallbackAllowedAt);
        if (shouldEnable == fallbackEnabled || lease is not { } activeLease)
            return;

        if (!Succeeded(setConfig.InvokeFunc(
                activeLease,
                DpsRotationModeOption,
                shouldEnable ? DpsNearestMode : DpsManualMode)))
        {
            suppressedForBoundary = true;
            ReleaseLease();
            RestoreBossModRotation();
            message = "Wrath targeting control failed; Boss Mod rotation is active for the rest of this solo duty.";
            return;
        }

        fallbackEnabled = shouldEnable;
        if (shouldEnable)
            fallbackEnabledAt = now;
        else
        {
            fallbackEnabledAt = default;
            if (!hasHostile)
            {
                missingHostileSince = now;
                nextFallbackAllowedAt = now + SoloDutyCombatPolicy.TargetFallbackRetryDelay;
            }
        }
    }

    private bool IsRotationProviderReady()
    {
        try
        {
            return ipcReady.HasFunction && ipcReady.InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    private void EndBoundary()
    {
        ReleaseLease();
        boundaryObserved = false;
        suppressedForBoundary = false;
        leaseRevoked = false;
        nextStartAttempt = default;
        ResetFallback();
        message = "Solo-duty rotation handoff is idle.";
    }

    private void RestoreBossModRotation()
    {
        try
        {
            if (getActiveBossModPreset.HasFunction &&
                string.Equals(getActiveBossModPreset.InvokeFunc(), NormalMovementPreset, StringComparison.Ordinal))
                setActiveBossModPreset.InvokeFunc(QuestBattlePreset);
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "Nexus could not restore the Questionable Boss Mod rotation preset.");
        }
    }

    private void ReleaseLease()
    {
        if (lease is not { } activeLease)
            return;
        try
        {
            releaseLease.InvokeAction(activeLease);
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "Nexus could not release its Wrath solo-duty lease during provider teardown.");
        }
        finally
        {
            lease = null;
        }
    }

    private void OnLeaseCancelled(int reason, string additionalInfo)
    {
        lease = null;
        leaseRevoked = true;
        log.Warning("Wrath revoked the Nexus solo-duty lease ({Reason}): {Detail}", reason, additionalInfo);
    }

    private void ResetFallback()
    {
        missingHostileSince = default;
        fallbackEnabledAt = default;
        nextFallbackAllowedAt = default;
        fallbackEnabled = false;
    }

    private static bool Succeeded(object? result) => result?.ToString() is
        "Okay" or "OkayWorking" or "Duplicate";

    public void Dispose()
    {
        Shutdown();
        callback.UnregisterAction();
    }
}
