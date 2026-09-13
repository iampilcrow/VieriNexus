using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal sealed class WorldSnapshotObserver(
    IClientState clientState,
    IPlayerState playerState,
    IObjectTable objectTable,
    ICondition condition,
    WorldStateStore store,
    Func<IReadOnlyDictionary<ProviderId, ProviderHealthSnapshot>> observeProviders)
{
    private long revision;
    private long nextUpdate;

    internal void Update(long now)
    {
        if (now < nextUpdate)
            return;
        nextUpdate = now + 250;

        var player = objectTable.LocalPlayer;
        var capturedAt = DateTimeOffset.UtcNow;
        var character = player is null
            ? Observed<CharacterSnapshot>.Unknown(capturedAt)
            : Observed<CharacterSnapshot>.Known(new CharacterSnapshot(
                new CharacterKey(playerState.ContentId, player.HomeWorld.RowId),
                player.Name.ToString(),
                player.ClassJob.RowId,
                player.Level,
                condition[ConditionFlag.InCombat],
                player.ClassJob.Value.Name.ExtractText(),
                player.ClassJob.Value.Abbreviation.ExtractText()), capturedAt);

        store.Publish(new WorldSnapshot(
            Interlocked.Increment(ref revision),
            capturedAt,
            new SessionSnapshot(
                clientState.IsLoggedIn,
                condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51],
                condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51],
                player is { IsTargetable: true },
                clientState.TerritoryType),
            character,
            observeProviders()));
    }
}
