namespace VieriRotationHelper;

internal readonly record struct ActionBinding(uint Action, uint EffectiveAction, string Key);

internal static class HotbarKeySelection
{
    internal static string? Resolve(IReadOnlyList<ActionBinding> bindings, uint action, uint anchor,
        bool preferAnchor, bool forceAnchor = false)
    {
        if (forceAnchor)
            foreach (var slot in bindings)
                if (slot.Action == anchor)
                    return slot.Key;
        if (preferAnchor)
            foreach (var slot in bindings)
                if (slot.Action == anchor && slot.EffectiveAction == action)
                    return slot.Key;
        foreach (var slot in bindings)
            if (slot.Action == action && slot.EffectiveAction == action)
                return slot.Key;
        foreach (var slot in bindings)
            if (slot.EffectiveAction == action)
                return slot.Key;
        // A related action isn't necessarily a button that fires this action.
        return null;
    }
}
