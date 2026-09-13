#region

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WrathCombo.Combos.PvE;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Data.BattleData;
using WrathCombo.Extensions;
using WrathCombo.Services;
using static WrathCombo.CustomComboNS.Functions.Jobs;

#endregion

namespace WrathCombo.Core;

/// <summary> This class facilitates action+icon replacement. </summary>
internal sealed class ActionReplacer : IDisposable
{
    public delegate uint GetActionDelegate(IntPtr actionManager, uint actionID);

    public readonly List<CustomCombo> CustomCombos;
    public readonly Hook<GetActionDelegate> getActionHook;
    public bool ActionReplacingEnabled => getActionHook.IsEnabled;
    private readonly Hook<IsActionReplaceableDelegate> isActionReplaceableHook;
    private bool hotbarRefreshPending;

    public readonly Dictionary<uint, uint> LastActionInvokeFor = [];

    /// <summary>
    ///     Critical for the hook, do not remove or modify.
    /// </summary>
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private IntPtr _actionManager = IntPtr.Zero;

    /// <summary> Initializes a new instance of the <see cref="ActionReplacer" /> class. </summary>
    public ActionReplacer()
    {
        CustomCombos = Assembly.GetAssembly(typeof(CustomCombo))!.GetTypes()
            .Where(t => !t.IsAbstract && t.BaseType == typeof(CustomCombo))
            .Select(Activator.CreateInstance)
            .Cast<CustomCombo>()
            .OrderByDescending(x => x.Preset)
            .ToList();

        // ReSharper disable once RedundantCast
        // Must keep the nint cast
        getActionHook = Svc.Hook.HookFromAddress<GetActionDelegate>((nint)ActionManager.Addresses.GetAdjustedActionId.Value, GetAdjustedActionDetour);
        isActionReplaceableHook = Svc.Hook.HookFromAddress<IsActionReplaceableDelegate>(Service.Address.IsActionIdReplaceable, IsActionReplaceableDetour);

        SetActionReplacing(Service.Configuration.ActionChanging);
        Svc.Framework.Update += RefreshHotbarsAfterLoad;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= RefreshHotbarsAfterLoad;
        getActionHook.Disable();
        isActionReplaceableHook.Disable();
        // Reclassify existing slots with native behavior when the suite unloads.
        RefreshActionSlots();
        getActionHook.Dispose();
        isActionReplaceableHook.Dispose();
    }

    internal void SetActionReplacing(bool enabled)
    {
        if (enabled)
        {
            getActionHook.Enable();
            isActionReplaceableHook.Enable();
        }
        else
        {
            getActionHook.Disable();
            isActionReplaceableHook.Disable();
        }
        hotbarRefreshPending = true;
    }

    private void RefreshHotbarsAfterLoad(IFramework framework)
    {
        if (hotbarRefreshPending && !ReadOnlyRuntime.Active && RefreshActionSlots())
            hotbarRefreshPending = false;
    }

    private static unsafe bool RefreshActionSlots()
    {
        if (!Svc.ClientState.IsLoggedIn || !GenericHelpers.IsScreenReady())
            return false;
        var ui = UIModule.Instance();
        var hotbars = ui == null ? null : ui->GetRaptureHotbarModule();
        if (hotbars == null)
            return false;

        // The game caches whether a slot supports action replacement when it is
        // assigned. Hooks installed after login do not reclassify existing slots.
        // Reapply the identical command through the native setter; this refreshes
        // presentation without changing bindings or saving HOTBAR.DAT.
        foreach (ref var hotbar in hotbars->Hotbars)
            foreach (ref var slot in hotbar.Slots)
                if (slot.CommandType == RaptureHotbarModule.HotbarSlotType.Action && slot.CommandId != 0)
                    slot.Set(ui, slot.CommandType, slot.CommandId);
        Svc.Log.Information("Refreshed existing hotbar action display state after action replacement changed.");
        return true;
    }

    private ulong IsActionReplaceableDetour(uint actionID)
    {
        if (actionID >= All.SingleTargetDPS && Service.Configuration.CustomActionSettings.AlwaysShowIcon)
            return 0;

        return 1;
    }

    /// <summary> Calls the original hook. </summary>
    /// <param name="actionID"> Action ID. </param>
    /// <returns> The result from the hook. </returns>
    internal uint OriginalHook(uint actionID) =>
        getActionHook.Original(_actionManager, actionID);

    public void EnableActionReplacingIfRequired()
    {
        if (Service.Configuration.ActionChanging)
            Service.ActionReplacer.getActionHook.Enable();
    }

    public void DisableActionReplacingIfRequired()
    {
        Service.ActionReplacer.getActionHook.Disable();
    }

#pragma warning disable CS1573
    /// <summary>
    ///     Throttles access to <see cref="GetAdjustedAction(uint)" />.
    /// </summary>
    /// <param name="actionID">The action a combo replaces.</param>
    /// <returns>The action a combo returns.</returns>
    /// <remarks>
    ///     The <see langword="IntPtr" /> parameter is necessary for the hook
    ///     delegate, but is not used in the method.<br />
    ///     Do not remove or modify the <see langword="IntPtr" /> parameter.
    /// </remarks>
    private uint GetAdjustedActionDetour(IntPtr _, uint actionID)
    {
        try
        {
            if (FilteredCombos is null)
                UpdateFilteredCombos();

            // Bail if not wanting to replace actions in this manner
            if (!Player.Available)
                return OriginalHook(actionID);

            // Only refresh every so often
            if (!EzThrottler.Throttle("Actions" + actionID,
                    Service.Configuration.Throttle))
                return LastActionInvokeFor[actionID];

            // Actually get the action
            LastActionInvokeFor[actionID] = GetAdjustedAction(actionID);
            return LastActionInvokeFor[actionID];
        }
        catch (Exception e)
        {
            e.Log();
            return actionID;
        }
    }
#pragma warning restore CS1573

    /// <summary>
    ///     Replaces an action with the result from a combo.
    /// </summary>
    /// <param name="actionID">The action a combo replaces.</param>
    /// <returns>The action a combo returns.</returns>
    private unsafe uint GetAdjustedAction(uint actionID)
    {
        try
        {
            if (ClassLocked() ||
                Player.Object is null ||
                !GenericHelpers.IsScreenReady() ||
                !Svc.ClientState.IsLoggedIn ||
                (DisabledJobsPVE.Any(x => x == Player.Job) && !Svc.ClientState.IsPvP) ||
                (DisabledJobsPVP.Any(x => x == Player.Job) && Svc.ClientState.IsPvP))
                return OriginalHook(actionID);

            foreach (CustomCombo? combo in FilteredCombos)
            {
                if (combo.TryInvoke(actionID, out uint newActionID))
                {
                    if ((Service.Configuration.BlockSpellOnMove &&
                        ActionManager.GetAdjustedCastTime(ActionType.Action, newActionID) > 0 &&
                        CustomComboFunctions.TimeMoving.Ticks > 0) || (Service.Configuration.PenaltyPause > 0 && CustomComboFunctions.PlayerHasActionPenalty(false)))
                    {
                        return All.Cease;
                    }

                    return newActionID;
                }
            }

            return OriginalHook(actionID);
        }

        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Preset error");
            return OriginalHook(actionID);
        }
    }

    internal static bool DisableJobCheck = false;

    /// <summary>
    ///     Checks if the player could be on a job instead of a class.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> if the user could be on a job instead.
    /// </returns>
    public static unsafe bool ClassLocked()
    {
        if (DisableJobCheck) return false;

        if (Player.Object is null) return false;

        if (Player.Level <= 35) return false;

        if (ContentCheck.IsInPOTD)
            return false;

        // DoL and higher except arcanist and rogue
        if (Player.Job is >= Job.MIN and not (Job.ACN or Job.ROG))
            return false;

        if (!UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(66049))
            return false;

        if ((Player.Job is Job.GLA or Job.PGL or Job.MRD or Job.LNC or Job.ARC or Job.CNJ or Job.THM or Job.ACN or Job.ROG) &&
            Svc.Condition[ConditionFlag.BoundByDuty56] && // in an instance duty
            Player.Level > 35) return true;

        return false;
    }

    private delegate ulong IsActionReplaceableDelegate(uint actionID);

    #region Restrict combos to current job

    public static IEnumerable<CustomCombo>? FilteredCombos;

    public void UpdateFilteredCombos()
    {
        var playerJob = Player.Job;
        var upgradedJob = playerJob.GetUpgradedJob();
        if (upgradedJob is Job.BTN or Job.FSH)
            upgradedJob = Job.MIN; // Allow all DoL jobs to be used for DoL combos

        FilteredCombos = CustomCombos.Where(x =>
        {
            var presetData = x.Preset.Attributes();
            if (presetData is null)
                return false;

            if (presetData.IsPvP != CustomComboFunctions.InPvP()) // Are we in PvP?
                return false;

            return
                // Role & Content
                (presetData.JobInfo.Job is Job.ADV && presetData.JobInfo.Role is JobRole role && role.MatchesPlayerJob()) ||
                // Job Specific
                presetData.JobInfo.Job == upgradedJob;
        });

        var filteredCombos = FilteredCombos as CustomCombo[] ?? FilteredCombos.ToArray();

        Svc.Log.Debug(
            $"Now running {filteredCombos.Length} combos\n" +
            string.Join("\n", filteredCombos.Select(x => x.Preset.Attributes().Name)));
    }

    #endregion
}
