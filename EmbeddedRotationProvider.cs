using FFXIVClientStructs.FFXIV.Client.Game;

namespace VieriRotationHelper;

internal sealed class EmbeddedRotationProvider : IDisposable
{
    private readonly WrathCombo.ReadOnlyRuntime? runtime;
    private readonly WrathLiveProvider wrath;
    private readonly Configuration configuration;
    internal uint EntryAction { get; private set; }
    internal string Status { get; private set; } = "Waiting for a character.";
    internal bool IsActive => runtime != null;
    private long nextErrorLog;

    internal EmbeddedRotationProvider(Plugin owner, WrathLiveProvider wrath)
    {
        this.wrath = wrath;
        configuration = owner.Configuration;
        if (wrath.IsLoaded)
        {
            Status = "The separate Wrath Combo plugin is loaded. Disable it and reload VieriRotationHelper to activate the integrated engine without duplicate hooks.";
            return;
        }
        try
        {
            var imported = wrath.GetOptions(configuration);
            runtime = new WrathCombo.ReadOnlyRuntime(Plugin.PluginInterface, owner,
                wrath.GetNativeAdjusted, imported, json =>
                {
                    configuration.WrathOptionsSnapshot = json;
                    owner.Save();
                });
        }
        catch (Exception ex)
        {
            Status = "Decision engine initialization failed: " + ex.Message;
            Plugin.Log.Error(ex, Status);
        }
    }

    internal RotationSuggestion GetLead(RotationAnchor anchor, RotationMode mode)
    {
        try
        {
            if (runtime == null)
                return new(0, mode, SuggestionSource.EmbeddedVieri, false, Status);
            var decision = runtime.Evaluate(anchor.JobId, mode == RotationMode.Aoe, null);
            EntryAction = decision.EntryAction;
            Status = $"{anchor.Job}: {decision.Preset} — {decision.Detail}";
            return new(decision.ActionId, mode, SuggestionSource.EmbeddedVieri, true,
                $"{decision.Preset}: {decision.Detail}", decision.UsesEntryButton);
        }
        catch (Exception ex)
        {
            if (Environment.TickCount64 >= nextErrorLog)
            {
                nextErrorLog = Environment.TickCount64 + 10000;
                Plugin.Log.Error(ex, $"Independent Wrath evaluator failed for {anchor.Job}/{mode}.");
            }
            EntryAction = 0;
            Status = "Suggestions paused: " + ex.Message;
            return new(0, mode, SuggestionSource.EmbeddedVieri, false,
                "Suggestions paused: " + ex.Message);
        }
    }

    public void Dispose() => runtime?.Dispose();

    internal bool GuidanceUsesAoe(int enemies, bool fallback) => runtime?.GuidanceUsesAoe(enemies, fallback) ?? fallback;

    internal void OpenSettings()
    {
        if (runtime != null)
            runtime.OpenEngineSettings();
    }

    internal IReadOnlyList<RotationSuggestion> Forecast(
        RotationAnchor anchor,
        RotationMode mode,
        uint lead,
        int count)
    {
        if (count <= 1 || runtime == null)
            return [];
        return runtime.Forecast(anchor.JobId, mode == RotationMode.Aoe, null, lead, count)
            .Select(decision => new RotationSuggestion(decision.ActionId, mode,
                SuggestionSource.EmbeddedVieri, true,
                $"Predicted from {decision.Preset} after advancing the shadow combat timeline.",
                decision.UsesEntryButton))
            .ToArray();
    }
}
