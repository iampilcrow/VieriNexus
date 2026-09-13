using System;
using System.Collections.Generic;
using System.Linq;
using WrathCombo.CustomComboNS;

namespace WrathCombo.Core;

// Compatibility surface for upstream readers. This build never creates a
// hotbar replacement hook, even when the imported Wrath configuration enables it.
internal sealed class ActionReplacer : IDisposable
{
    public readonly List<CustomCombo> CustomCombos = typeof(CustomCombo).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && t.BaseType == typeof(CustomCombo))
        .Select(t => (CustomCombo)Activator.CreateInstance(t)!)
        .OrderByDescending(c => c.Preset).ToList();
    public readonly Dictionary<uint, uint> LastActionInvokeFor = [];
    public readonly DisabledHook getActionHook = new();
    public bool ActionReplacingEnabled => false;
    internal static bool DisableJobCheck;
    public static IEnumerable<CustomCombo>? FilteredCombos;
    public static bool ClassLocked() => false;
    public void UpdateFilteredCombos() => FilteredCombos = CustomCombos;
    internal uint OriginalHook(uint actionId) => ReadOnlyRuntime.NativeAdjust(actionId);
    public void EnableActionReplacingIfRequired() { }
    public void DisableActionReplacingIfRequired() { }
    public void Dispose() { }

    internal sealed class DisabledHook
    {
        public bool IsEnabled => false;
        public void Enable() { }
        public void Disable() { }
    }
}
