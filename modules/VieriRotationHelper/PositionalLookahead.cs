namespace VieriRotationHelper;

internal static class PositionalLookahead
{
    internal static (uint Action, uint Side, uint Steps) Select(IReadOnlyList<uint> actions, Func<uint, bool> isAbility)
    {
        var gcds = 0;
        for (var i = 0; i < actions.Count; i++)
        {
            // Do not send the player to a distant speculative positional during a burst.
            if (gcds >= 2) break;
            var side = PositionalCatalog.Get(actions[i]);
            if (side != PositionalKind.None)
                return (actions[i], side == PositionalKind.Rear ? 1u : 2u, (uint)i);
            if (!isAbility(actions[i])) gcds++;
        }
        return default;
    }
}
