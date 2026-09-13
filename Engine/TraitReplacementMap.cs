using System.Collections.Generic;

namespace WrathCombo;

internal static class TraitReplacementMap
{
    internal const sbyte TraitReplacement = 3;
    internal readonly record struct Replacement(uint BaseAction, uint DisplayAction, sbyte Type);

    internal static IReadOnlyDictionary<uint, uint> Build(IEnumerable<Replacement> replacements)
    {
        var direct = new Dictionary<uint, uint>();
        foreach (var replacement in replacements)
        {
            if (replacement.Type == TraitReplacement &&
                replacement.BaseAction != 0 && replacement.DisplayAction != 0)
                direct[replacement.DisplayAction] = replacement.BaseAction;
        }

        // Trait upgrades can themselves be upgraded by a later trait. Collapse
        // the chain so every displayed rank advances the same canonical combo.
        var result = new Dictionary<uint, uint>();
        foreach (var displayAction in direct.Keys)
        {
            var recorded = direct[displayAction];
            var visited = new HashSet<uint> { displayAction };
            while (direct.TryGetValue(recorded, out var parent) && visited.Add(recorded))
                recorded = parent;
            result[displayAction] = recorded;
        }
        return result;
    }
}
