using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using System;
using System.Collections.Generic;
using WrathCombo.Data;
using WrathCombo.Services;

namespace WrathCombo;

/// <summary>
/// A read-only shadow of the short combat timeline used while producing the
/// icons after the live recommendation.  It never writes to the game or to
/// Wrath's live action history.
/// </summary>
internal sealed class PredictionContext : IDisposable
{
    [ThreadStatic] private static PredictionContext? current;
    internal static PredictionContext? Current => current;

    private readonly Dictionary<uint, float> cooldowns = [];
    private readonly Dictionary<uint, uint> charges = [];
    private readonly List<uint> actions = [];
    private readonly List<uint> weaves = [];
    private readonly float gcdTotal;
    private readonly RdmPredictionState? rdm;
    private readonly PredictionStatusState playerStatuses;
    private readonly PredictionStatusState targetStatuses;
    private readonly ulong targetId;
    private readonly JobPredictionState jobState;
    private float elapsed;

    internal uint ComboAction { get; private set; }
    internal uint CanonicalComboAction { get; private set; }
    internal float ComboTimer { get; private set; }
    internal float RemainingGcd { get; private set; }
    internal uint CurrentMp { get; private set; }
    internal IReadOnlyList<uint> Weaves => weaves;
    internal uint LastAction => actions.Count == 0 ? 0 : actions[^1];
    internal float GcdTotal => gcdTotal;

    private unsafe PredictionContext(uint job)
    {
        var manager = ActionManager.Instance();
        ComboAction = manager == null ? 0 : manager->Combo.Action;
        CanonicalComboAction = PredictedComboState.CanonicalAction(ComboAction);
        ComboTimer = manager == null ? 0 : manager->Combo.Timer;
        var gcd = manager == null ? null : manager->GetRecastGroupDetail(57);
        gcdTotal = gcd == null || gcd->Total <= 0 ? 2.5f : gcd->Total;
        RemainingGcd = gcd == null ? 0 : Math.Max(0, gcd->Total - gcd->Elapsed);
        CurrentMp = Player.Object?.CurrentMp ?? 0;

        var allStatuses = new List<PredictionStatusState.Snapshot>();
        foreach (var status in Player.Status)
            allStatuses.Add(new(status.StatusId, status.RemainingTime, status.Param, status.SourceId));
        var localId = (uint)(Player.Object?.GameObjectId ?? 0);
        playerStatuses = new PredictionStatusState(allStatuses, localId);
        var allTargetStatuses = new List<PredictionStatusState.Snapshot>();
        if (ECommons.DalamudServices.Svc.Targets.Target is IBattleChara target)
        {
            targetId = target.GameObjectId;
            foreach (var status in target.StatusList)
                allTargetStatuses.Add(new(status.StatusId, status.RemainingTime, status.Param, status.SourceId));
        }
        targetStatuses = new PredictionStatusState(allTargetStatuses, localId);
        jobState = JobPredictionState.Capture(job);

        if (job == 35)
        {
            var gauge = Service.ComboCache.GetJobGauge<RDMGauge>();
            var statuses = new List<RdmPredictionState.StatusSnapshot>();
            foreach (var status in Player.Status)
                statuses.Add(new(status.StatusId, Math.Max(0, status.RemainingTime), status.Param));
            rdm = new RdmPredictionState(gauge.BlackMana, gauge.WhiteMana, gauge.ManaStacks, statuses);
        }
    }

    internal static PredictionContext Begin(uint job)
    {
        if (current != null)
            throw new InvalidOperationException("A prediction timeline is already active.");
        return current = new PredictionContext(job);
    }

    internal unsafe void Advance(uint encodedAction)
    {
        var action = encodedAction > Combos.PvE.All.Items && encodedAction < Combos.PvE.All.Pomanders
            ? encodedAction - Combos.PvE.All.Items
            : encodedAction;
        if (!ActionWatching.ActionSheet.TryGetValue(action, out var row))
            return;

        var ability = row.ActionCategory.RowId == 4;
        var resourceCost = Data.CustomComboCache.GetResourceCost(action);
        CurrentMp = jobState.AdvanceMana(action, row.ActionCategory.RowId, resourceCost, CurrentMp,
            Player.Object?.MaxMp ?? 10000, playerStatuses);
        var step = ability ? CustomComboNS.Functions.CustomComboFunctions.BaseAnimationLock : Math.Max(RemainingGcd, .1f);
        Progress(step);
        actions.Add(action);
        playerStatuses.Advance(action);
        targetStatuses.AdvanceTarget(action);
        jobState.Advance(action, actions.Count > 1 ? actions[^2] : 0, row.ActionCategory.RowId, playerStatuses);
        rdm?.Advance(action, row.Cast100ms + row.ExtraCastTime100ms > 0);

        var manager = ActionManager.Instance();
        var maximumCharges = manager == null ? (ushort)1 : ActionManager.GetMaxCharges(action, 0);
        var remainingCharges = GetRemainingCharges(action);
        if (remainingCharges > 0)
            charges[action] = remainingCharges - 1;
        var recast = ActionManager.GetAdjustedRecastTime(ActionType.Action, action) / 1000f;
        if (recast > 0)
            cooldowns[action] = maximumCharges > 1 ? recast * maximumCharges : recast;

        if (ability)
        {
            weaves.Add(action);
            RemainingGcd = Math.Max(0, RemainingGcd - step);
        }
        else
        {
            // This mirrors Hilda's forward camera: a GCD advances the combo and
            // opens a fresh weave window before the following recommendation.
            ComboAction = action;
            CanonicalComboAction = PredictedComboState.CanonicalAction(action);
            ComboTimer = 30f;
            RemainingGcd = gcdTotal;
            weaves.Clear();
        }
    }

    internal bool HasCanonicalComboAlternative =>
        CanonicalComboAction != 0 && CanonicalComboAction != ComboAction;

    internal void UseCanonicalComboAction() => ComboAction = CanonicalComboAction;

    internal void RestoreDisplayedComboAction(uint action) => ComboAction = action;

    internal int RdmBlackMana(int live) => rdm?.BlackMana ?? live;
    internal int RdmWhiteMana(int live) => rdm?.WhiteMana ?? live;
    internal int RdmManaStacks(int live) => rdm?.ManaStacks ?? live;
    internal int Gauge(string key, int live) => jobState.Get(key, live);
    internal bool Gauge(string key, bool live) => jobState.Get(key, live);
    internal uint DanceNextStep(uint live) => jobState.DanceNextStep(live);
    internal int BeastChakra(int index, int live) => jobState.BeastChakra(index, live);

    internal bool TryGetPlayerStatus(uint statusId, out bool present)
    {
        if (rdm != null)
            if (rdm.TryHasStatus(statusId, out present)) return true;
        present = playerStatuses.Has(statusId);
        return true;
    }

    internal bool TryGetPlayerStatusRemaining(uint statusId, out float remaining)
    {
        if (rdm != null)
            if (rdm.TryGetStatusRemaining(statusId, out remaining)) return true;
        remaining = playerStatuses.TryGet(statusId, out var status) ? status.Remaining : 0;
        return true;
    }

    internal bool TryGetPlayerStatusSnapshot(uint statusId, out PredictionStatusState.Snapshot status)
    {
        if (rdm != null && rdm.TryHasStatus(statusId, out _))
            return rdm.TryGetStatus(statusId, out var rdmStatus)
                ? ReturnRdm(rdmStatus, out status)
                : ReturnAbsent(out status);
        return playerStatuses.TryGet(statusId, out status);
    }

    private static bool ReturnRdm(RdmPredictionState.StatusSnapshot source,
        out PredictionStatusState.Snapshot status)
    {
        status = new(source.Id, source.Remaining, source.Stacks);
        return true;
    }

    private static bool ReturnAbsent(out PredictionStatusState.Snapshot status)
    {
        status = default;
        return false;
    }

    internal bool TryGetStatusSnapshot(ulong objectId, uint statusId, bool anyOwner, out PredictionStatusState.Snapshot status)
    {
        if (objectId == Player.Object?.GameObjectId)
        {
            if (!TryGetPlayerStatusSnapshot(statusId, out status)) return false;
            return anyOwner || status.SourceId == 0 || status.SourceId == (uint)(Player.Object?.GameObjectId ?? 0);
        }
        if (objectId == targetId)
        {
            if (!targetStatuses.TryGet(statusId, out status)) return false;
            return anyOwner || status.SourceId == 0 || status.SourceId == (uint)(Player.Object?.GameObjectId ?? 0);
        }
        status = default;
        return false;
    }

    internal bool OwnsStatusShadow(ulong objectId) =>
        objectId == Player.Object?.GameObjectId || objectId == targetId;

    internal bool IsGlobalCooldownAction(uint encodedAction)
    {
        var action = encodedAction > Combos.PvE.All.Items && encodedAction < Combos.PvE.All.Pomanders
            ? encodedAction - Combos.PvE.All.Items
            : encodedAction;
        return ActionWatching.ActionSheet.TryGetValue(action, out var row) &&
               row.ActionCategory.RowId != 4;
    }

    internal bool IsComboContinuation(uint encodedAction)
    {
        var action = encodedAction > Combos.PvE.All.Items && encodedAction < Combos.PvE.All.Pomanders
            ? encodedAction - Combos.PvE.All.Items
            : encodedAction;
        if (!ActionWatching.ActionSheet.TryGetValue(action, out var row) ||
            row.ActionCategory.RowId == 4)
            return false;

        var required = row.ActionCombo.RowId;
        return required != 0 &&
               (required == ComboAction ||
                PredictedComboState.CanonicalAction(required) == CanonicalComboAction);
    }

    private void Progress(float seconds)
    {
        elapsed += seconds;
        ComboTimer = Math.Max(0, ComboTimer - seconds);
        playerStatuses.Progress(seconds);
        targetStatuses.Progress(seconds);
        jobState.Progress(seconds);
        rdm?.Progress(seconds);
        foreach (var action in new List<uint>(cooldowns.Keys))
            cooldowns[action] = Math.Max(0, cooldowns[action] - seconds);
    }

    internal bool WasUsed(uint action) => actions.Contains(action);
    internal bool WasLast(uint action) => LastAction == action;
    internal float TimeSince(uint action)
    {
        var index = actions.LastIndexOf(action);
        return index < 0 ? -1 : Math.Max(0, (actions.Count - 1 - index) * .6f);
    }

    internal unsafe float CooldownRemaining(uint action)
    {
        if (cooldowns.TryGetValue(action, out var predicted)) return predicted;
        var manager = ActionManager.Instance();
        if (manager == null) return 0;
        return Math.Max(0, manager->GetRecastTime(ActionType.Action, action) -
                           manager->GetRecastTimeElapsed(ActionType.Action, action) - elapsed);
    }

    internal unsafe uint GetRemainingCharges(uint action)
    {
        if (charges.TryGetValue(action, out var predicted)) return predicted;
        var manager = ActionManager.Instance();
        if (manager == null) return 0;
        var maximum = ActionManager.GetMaxCharges(action, 0);
        return maximum <= 1 ? (CooldownRemaining(action) <= 0 ? 1u : 0u) : manager->GetCurrentCharges(action);
    }

    internal void ReduceCooldown(uint action, float seconds)
    {
        var remaining = CooldownRemaining(action);
        cooldowns[action] = Math.Max(0, remaining - seconds);
    }

    internal float CooldownElapsed(uint action)
    {
        var total = ActionManager.GetAdjustedRecastTime(ActionType.Action, action) / 1000f;
        return Math.Max(0, total - CooldownRemaining(action));
    }

    public void Dispose()
    {
        if (ReferenceEquals(current, this)) current = null;
    }
}
