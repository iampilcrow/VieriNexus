using System;
using System.Collections.Generic;

namespace WrathCombo;

/// <summary>
/// Deterministic Red Mage state used only by the read-only suggestion timeline.
/// Random Verfire/Verstone procs are not invented; existing and guaranteed procs
/// are tracked until consumed.
/// </summary>
internal sealed class RdmPredictionState
{
    internal readonly record struct StatusSnapshot(uint Id, float Remaining, ushort Stacks = 0);

    internal const uint Swiftcast = 167;
    internal const uint VerfireReady = 1234;
    internal const uint VerstoneReady = 1235;
    internal const uint Acceleration = 1238;
    internal const uint Embolden = 1239;
    internal const uint Dualcast = 1249;
    internal const uint Manafication = 1971;
    internal const uint MagickedSwordplay = 3875;
    internal const uint ThornedFlourish = 3876;
    internal const uint GrandImpactReady = 3877;
    internal const uint PrefulgenceReady = 3878;

    private static readonly HashSet<uint> KnownStatuses =
    [
        Swiftcast, VerfireReady, VerstoneReady, Acceleration, Embolden, Dualcast,
        Manafication, MagickedSwordplay, ThornedFlourish, GrandImpactReady,
        PrefulgenceReady,
    ];

    private readonly Dictionary<uint, StatusSnapshot> statuses = [];

    internal int BlackMana { get; private set; }
    internal int WhiteMana { get; private set; }
    internal int ManaStacks { get; private set; }

    internal RdmPredictionState(int blackMana, int whiteMana, int manaStacks,
        IEnumerable<StatusSnapshot>? initialStatuses = null)
    {
        BlackMana = Math.Clamp(blackMana, 0, 100);
        WhiteMana = Math.Clamp(whiteMana, 0, 100);
        ManaStacks = Math.Clamp(manaStacks, 0, 3);
        if (initialStatuses == null)
            return;

        foreach (var status in initialStatuses)
        {
            if (KnownStatuses.Contains(status.Id) && status.Remaining > 0)
                statuses[status.Id] = status;
        }
    }

    internal bool TryHasStatus(uint statusId, out bool present)
    {
        if (!KnownStatuses.Contains(statusId))
        {
            present = false;
            return false;
        }

        present = statuses.ContainsKey(statusId);
        return true;
    }

    internal bool TryGetStatusRemaining(uint statusId, out float remaining)
    {
        if (!KnownStatuses.Contains(statusId))
        {
            remaining = 0;
            return false;
        }

        remaining = statuses.TryGetValue(statusId, out var status) ? status.Remaining : 0;
        return true;
    }

    internal bool TryGetStatus(uint statusId, out StatusSnapshot status)
    {
        if (!KnownStatuses.Contains(statusId))
        {
            status = default;
            return false;
        }
        return statuses.TryGetValue(statusId, out status);
    }

    internal void Progress(float seconds)
    {
        if (seconds <= 0)
            return;

        foreach (var id in new List<uint>(statuses.Keys))
        {
            var status = statuses[id];
            var remaining = status.Remaining - seconds;
            if (remaining <= 0)
                statuses.Remove(id);
            else
                statuses[id] = status with { Remaining = remaining };
        }
    }

    internal void Advance(uint action, bool hasBaseCastTime)
    {
        var hadDualcast = Has(Dualcast);
        var hadSwiftcast = Has(Swiftcast);
        var hadAcceleration = Has(Acceleration);
        var wasInstant = hadDualcast || hadSwiftcast ||
                         hadAcceleration && IsAccelerationSpell(action);

        switch (action)
        {
            case 7518: // Acceleration
                Add(Acceleration, 20);
                Add(GrandImpactReady, 30);
                return;
            case 7520: // Embolden
                Add(Embolden, 20);
                Add(ThornedFlourish, 30);
                return;
            case 7521: // Manafication
                Add(Manafication, 15);
                Add(MagickedSwordplay, 15, 3);
                Add(PrefulgenceReady, 30);
                return;
        }

        ApplyGaugeEffects(action);

        if (action == 7510)
            Remove(VerfireReady);
        else if (action == 7511)
            Remove(VerstoneReady);
        else if (action == 37006)
            Remove(GrandImpactReady);
        else if (action == 37005)
            Remove(ThornedFlourish);
        else if (action == 37007)
            Remove(PrefulgenceReady);

        if (hadAcceleration && IsAccelerationSpell(action))
        {
            Remove(Acceleration);
            if (action is 7505 or 25855)
                Add(VerfireReady, 30);
            else if (action is 7507 or 25856)
                Add(VerstoneReady, 30);
        }

        if (hasBaseCastTime)
        {
            if (hadDualcast)
                Remove(Dualcast);
            if (hadSwiftcast)
                Remove(Swiftcast);

            // A spell that was genuinely hard-cast grants Dualcast. An instant
            // spell consumes the existing instant-cast state without replacing it.
            if (!wasInstant)
                Add(Dualcast, 15);
        }

        if (IsSwordplayAction(action))
            ConsumeStack(MagickedSwordplay);
    }

    private void ApplyGaugeEffects(uint action)
    {
        switch (action)
        {
            case 7503: // Jolt
            case 7524: // Jolt II
            case 37004: // Jolt III
                AddMana(2, 2);
                break;
            case 7509: // Scatter
            case 16526: // Impact
                AddMana(3, 3);
                break;
            case 7510: // Verfire
                AddMana(5, 0);
                break;
            case 7511: // Verstone
                AddMana(0, 5);
                break;
            case 7505: // Verthunder
            case 25855: // Verthunder III
                AddMana(6, 0);
                break;
            case 7507: // Veraero
            case 25856: // Veraero III
                AddMana(0, 6);
                break;
            case 16524: // Verthunder II
                AddMana(7, 0);
                break;
            case 16525: // Veraero II
                AddMana(0, 7);
                break;
            case 7527: // Enchanted Riposte
            case 45960: // Enchanted Riposte under Magicked Swordplay
                SpendManaAndAddStack(20);
                break;
            case 7528: // Enchanted Zwerchhau
            case 7529: // Enchanted Redoublement
            case 45961: // Enchanted Zwerchhau under Magicked Swordplay
            case 45962: // Enchanted Redoublement under Magicked Swordplay
                SpendManaAndAddStack(15);
                break;
            case 7530: // Enchanted Moulinet
                SpendManaAndAddStack(20);
                break;
            case 37002: // Enchanted Moulinet Deux
            case 37003: // Enchanted Moulinet Trois
                SpendManaAndAddStack(15);
                break;
            case 16528: // Enchanted Reprise
                AddMana(-5, -5);
                break;
            case 7525: // Verflare
                AddMana(11, 0);
                ManaStacks = 0;
                break;
            case 7526: // Verholy
                AddMana(0, 11);
                ManaStacks = 0;
                break;
            case 16530: // Scorch
            case 25858: // Resolution
                AddMana(4, 4);
                break;
        }
    }

    private void SpendManaAndAddStack(int amount)
    {
        if (!Has(MagickedSwordplay))
            AddMana(-amount, -amount);
        ManaStacks = Math.Clamp(ManaStacks + 1, 0, 3);
    }

    private void AddMana(int black, int white)
    {
        BlackMana = Math.Clamp(BlackMana + black, 0, 100);
        WhiteMana = Math.Clamp(WhiteMana + white, 0, 100);
    }

    private bool Has(uint id) => statuses.ContainsKey(id);

    private void Add(uint id, float duration, ushort stacks = 0) =>
        statuses[id] = new StatusSnapshot(id, duration, stacks);

    private void Remove(uint id) => statuses.Remove(id);

    private void ConsumeStack(uint id)
    {
        if (!statuses.TryGetValue(id, out var status))
            return;
        if (status.Stacks <= 1)
            statuses.Remove(id);
        else
            statuses[id] = status with { Stacks = (ushort)(status.Stacks - 1) };
    }

    private static bool IsAccelerationSpell(uint action) =>
        action is 7505 or 7507 or 25855 or 25856 or 16526;

    private static bool IsSwordplayAction(uint action) =>
        action is 7527 or 7528 or 7529 or 7530 or 37002 or 37003 or 45960 or 45961 or 45962;
}
