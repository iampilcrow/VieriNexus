using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using WrathCombo.Core;
using WrathCombo.Attributes;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Services;
using WrathCombo.Services.IPC;

namespace WrathCombo;

public sealed record Decision(uint ActionId, uint EntryAction, string Preset, string Detail,
    bool UsesEntryButton);

/// <summary>Hosts the pinned Wrath engine and provides a side-effect-free prediction
/// context to the suggestion overlay.</summary>
public sealed class ReadOnlyRuntime : IDisposable
{
    // This assembly is always a read-only evaluator. It never installs Wrath's
    // action hook, commands, IPC providers, windows, movement, or auto-rotation.
    internal static bool Active => true;
    internal static bool Hosted => false;
    internal static Func<uint, uint> NativeAdjust { get; private set; } = id => id;
    private static Preset? evaluatingPreset;
    private readonly Dictionary<(uint Job, bool Aoe), Preset> selected = [];
    private readonly HashSet<Type> initializedConfigs = [];
    private string? lastJson;
    private bool disposed;

    public ReadOnlyRuntime(IDalamudPluginInterface pluginInterface, IDalamudPlugin owner,
        Func<uint, uint> nativeAdjust)
    {
        NativeAdjust = nativeAdjust;
        ECommonsMain.Init(pluginInterface, owner, ECommons.Module.ObjectFunctions);
        try
        {
            Service.Configuration = new Configuration();
            Service.ComboCache = new CustomComboCache();
            WrathCombo.P = new WrathCombo(this);
            Service.ActionReplacer = new ActionReplacer();
            ActionWatching.Enable();
            CustomComboFunctions.TimerSetup();
            PredictedComboState.LoadFromGameData();
        }
        catch
        {
            try { Dispose(); } catch { /* preserve the initialization failure */ }
            throw;
        }
    }

    // Compatibility surface for retained upstream code paths. The read-only
    // constructor above never calls the full plugin constructor or saves data.
    internal static Configuration? TakeHostedConfiguration() => null;
    internal static void SaveConfiguration(Configuration configuration) { }

    internal static bool IsPresetEnabled(Preset preset) =>
        (int)preset < 100 || preset == evaluatingPreset || Service.Configuration.EnabledActions.Contains(preset);

    public Decision Evaluate(uint job, bool aoe, string? configurationJson)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Player.Available || Player.IsDead)
            return new(0, 0, "", "Waiting for a living character.", false);
        if (CustomComboFunctions.InPvP())
            return new(0, 0, "", "The bundled recommendations are for PvE, not PvP.", false);

        var candidates = Service.ActionReplacer.CustomCombos
            .Where(c => IsRotation(c.Preset, job, aoe)).ToArray();
        if (candidates.Length == 0)
            return new(0, 0, "", "No upstream PvE rotation is available for this job/mode.", false);

        // Config constructors carry Wrath's real default thresholds/options.
        foreach (var type in candidates.Select(c => c.GetType().DeclaringType)
                     .Where(t => t != null).Distinct())
        {
            var config = type!.GetNestedType("Config", BindingFlags.Public | BindingFlags.NonPublic);
            if (config != null && initializedConfigs.Add(config))
            {
                RuntimeHelpers.RunClassConstructor(config.TypeHandle);
                lastJson = null;
            }
        }
        ApplyConfiguration(configurationJson);
        if (job == 36)
        {
            BlueMageService.PopulateBLUSpells();
            var tank = CustomComboFunctions.HasStatusEffect(Combos.PvE.BLU.Buffs.TankMimicry);
            candidates = candidates.Where(c => PresetStorage.AllPresets[c.Preset].IsBlueTank == tank).ToArray();
        }
        var configured = candidates.FirstOrDefault(c => Service.Configuration.EnabledActions.Contains(c.Preset));
        if (configured != null)
            selected[(job, aoe)] = configured.Preset;
        var combo = configured ?? candidates.FirstOrDefault(c =>
            selected.TryGetValue((job, aoe), out var prior) && c.Preset == prior)
            ?? candidates.FirstOrDefault(c => PresetStorage.AllPresets[c.Preset].ComboType == ComboType.SimpleDPS)
            ?? candidates.First();
        var metadata = PresetStorage.AllPresets[combo.Preset];
        var entry = metadata.ReplaceSkill?.ActionIDs.FirstOrDefault() ?? 0;
        if (entry == 0)
            return new(0, 0, combo.Preset.ToString(), "Upstream rotation has no verified entry action.", false);

        try
        {
            evaluatingPreset = combo.Preset;
            CustomComboFunctions.OverrideTarget = null;
            var action = combo.Suggest(entry);
            if (action > Combos.PvE.All.Items && action < Combos.PvE.All.Pomanders &&
                Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(action - Combos.PvE.All.Items, out _))
                return new(action, entry, combo.Preset.ToString(), "Wrath recommends this consumable; press its actual item hotkey manually.", false);
            // Wrath uses synthetic sentinel actions to request that nothing be cast.
            if (!ActionWatching.ActionSheet.ContainsKey(action))
                return new(0, entry, combo.Preset.ToString(), "Wrath's rules recommend waiting.", configured != null);
            action = NativeAdjust(action);
            if (job == 36 && CustomComboFunctions.IsBlueMageSpellbookAction(action) && !CustomComboFunctions.IsSpellActive(action))
                return new(0, entry, combo.Preset.ToString(), "The recommended Blue Mage spell is not set in the spellbook.", configured != null);
            if (!CustomComboFunctions.ActionLearned(action))
                return new(0, entry, combo.Preset.ToString(), "Recommended action is not learned at the current level.", configured != null);
            return new(action, entry, combo.Preset.ToString(),
                configured != null ? "Pinned Wrath evaluator with your Wrath options." :
                "Independent Wrath evaluator; hotbar consolidation is not required.",
                configured != null);
        }
        finally
        {
            evaluatingPreset = null;
            CustomComboFunctions.OverrideTarget = null;
        }
    }

    private void ApplyConfiguration(string? configurationJson)
    {
        if (configurationJson == lastJson)
            return;

        var config = new Configuration();
        Service.Configuration = config;
        foreach (var setting in UserData.MasterList.Values)
            setting.ResetToDefault();
        if (!string.IsNullOrWhiteSpace(configurationJson))
            JsonConvert.PopulateObject(configurationJson, config,
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });

        // Suggestions may inspect Wrath's settings, but this evaluator must
        // never replace actions, automate combat, announce, or display UI.
        config.ActionChanging = false;
        config.CustomActionSettings.SingleTargetDPS = false;
        config.CustomActionSettings.AoEDPS = false;
        config.OutputOpenerLogs = false;
        config.EnabledOutputLog = false;
        config.TankbusterTTS = false;
        config.TankbusterToast = false;
        config.AoEDamageTTS = false;
        config.AoEDamageToast = false;
        config.HideMessageOfTheDay = true;
        lastJson = configurationJson;
    }

    public bool GuidanceUsesAoe(int enemies, bool fallback)
    {
        var config = AutoRotation.AutoRotationController.cfg;
        if (config?.Enabled != true) return fallback;
        if (AutoRotation.AutoRotationController.LockedST) return false;
        if (AutoRotation.AutoRotationController.LockedAoE) return true;
        return config.DPSSettings.DPSAoETargets is { } threshold && enemies >= threshold;
    }

    public IReadOnlyList<Decision> Forecast(uint job, bool aoe, string? configurationJson,
        uint leadAction, int totalCount)
    {
        if (leadAction == 0 || totalCount <= 1 ||
            leadAction > Combos.PvE.All.Items && leadAction < Combos.PvE.All.Pomanders) return [];
        var result = new List<Decision>(totalCount - 1);
        using var timeline = PredictionContext.Begin(job);
        var prior = leadAction;
        for (var i = 1; i < totalCount; i++)
        {
            timeline.Advance(prior);
            var next = Evaluate(job, aoe, configurationJson);
            if (timeline.HasCanonicalComboAlternative &&
                timeline.IsGlobalCooldownAction(next.ActionId) &&
                !timeline.IsComboContinuation(next.ActionId))
            {
                var displayedCombo = timeline.ComboAction;
                timeline.UseCanonicalComboAction();
                var canonical = Evaluate(job, aoe, configurationJson);
                if (canonical.ActionId != 0 && timeline.IsComboContinuation(canonical.ActionId))
                    next = canonical;
                else
                    timeline.RestoreDisplayedComboAction(displayedCombo);
            }
            if (next.ActionId == 0) break;
            result.Add(next);
            prior = next.ActionId;
        }
        return result;
    }

    private static bool IsRotation(Preset preset, uint job, bool aoe)
    {
        var p = PresetStorage.AllPresets[preset];
        return !p.IsPvP && (uint?)p.JobInfo?.Job == job &&
               p.AutoAction is { IsHeal: false } action && action.IsAoE == aoe &&
               p.ComboType is ComboType.SimpleDPS or ComboType.AdvancedDPS;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CustomComboFunctions.TimerDispose();
        ActionWatching.Dispose();
        Service.ComboCache.Dispose();
        WrathCombo.P?.ActionRetargeting.Dispose();
        WrathCombo.P?.HTTPClient.Dispose();
        WrathCombo.P = null;
        NativeAdjust = id => id;
        ECommonsMain.Dispose();
    }

    public void OpenEngineSettings() { }
}

public sealed partial class WrathCombo
{
    // Deliberately avoids the upstream plugin constructor and every side effect
    // it owns. These helpers are required only by rotation decision readers.
    internal WrathCombo(ReadOnlyRuntime runtime)
    {
        P = this;
        var leases = new Leasing();
        IPCSearch = new Search(leases);
        UIHelper = new UIHelper(leases);
        ActionRetargeting = new ActionRetargeting();
    }
}
