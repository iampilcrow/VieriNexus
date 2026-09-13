using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;

namespace WrathCombo;

/// <summary>Read-only player-status shadow for the forward suggestion camera.</summary>
internal sealed class PredictionStatusState
{
    internal readonly record struct Snapshot(uint Id, float Remaining, ushort Stacks = 0, uint SourceId = 0);
    private readonly Dictionary<uint, Snapshot> statuses = [];

    internal PredictionStatusState(IEnumerable<Snapshot> initial, uint preferredSource = 0)
    {
        foreach (var status in initial)
            if (status.Remaining != 0)
            {
                if (!statuses.TryGetValue(status.Id, out var existing) ||
                    status.SourceId == preferredSource || existing.SourceId != preferredSource)
                    statuses[status.Id] = status;
            }
    }

    internal bool TryGet(uint id, out Snapshot status) => statuses.TryGetValue(id, out status);
    internal bool Has(uint id) => statuses.ContainsKey(id);

    internal void Progress(float seconds)
    {
        if (seconds <= 0) return;
        foreach (var id in new List<uint>(statuses.Keys))
        {
            var value = statuses[id];
            // Negative game durations are indefinite/transitional and should not
            // be expired by the short prediction horizon.
            if (value.Remaining < 0) continue;
            var remaining = value.Remaining - seconds;
            if (remaining <= 0) statuses.Remove(id);
            else statuses[id] = value with { Remaining = remaining };
        }
    }

    internal void Advance(uint action)
    {
        if (!PredictionActionEffects.TryGet(action, out var effect)) return;
        foreach (var id in effect.Removes) Remove(id);
        foreach (var grant in effect.Grants)
            Add(grant.Id, grant.Duration <= 0 ? 30 : grant.Duration, grant.Stacks);
    }

    internal void AdvanceTarget(uint action)
    {
        if (!PredictionActionEffects.TryGet(action, out var effect)) return;
        foreach (var grant in effect.TargetGrants)
            Add(grant.Id, grant.Duration <= 0 ? 30 : grant.Duration, grant.Stacks);
    }

    internal void Add(uint id, float duration = 30, ushort stacks = 0) =>
        statuses[id] = new(id, duration, stacks);

    internal void Remove(uint id) => statuses.Remove(id);

    internal void Consume(uint id, ushort amount = 1)
    {
        if (!statuses.TryGetValue(id, out var status)) return;
        if (status.Stacks == 0 || status.Stacks <= amount) statuses.Remove(id);
        else statuses[id] = status with { Stacks = (ushort)(status.Stacks - amount) };
    }
}

/// <summary>A synthetic status exposed only while Wrath evaluates a forecast frame.</summary>
internal sealed class PredictedStatus(PredictionStatusState.Snapshot value) : IStatus
{
    public nint Address => 0;
    public uint StatusId => value.Id;
    public RowRef<Status> GameData => default;
    public ushort Param => value.Stacks;
    public float RemainingTime => value.Remaining;
    public uint SourceId => value.SourceId;
    public IGameObject? SourceObject => null;
    public bool Equals(IStatus? other) => other?.StatusId == StatusId;
    public override bool Equals(object? obj) => obj is IStatus other && Equals(other);
    public override int GetHashCode() => (int)StatusId;
}
