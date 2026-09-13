using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace VieriRotationHelper;

internal sealed class RotationCoordinator(
    IObjectTable objectTable,
    ITargetManager targetManager,
    WrathLiveProvider wrath,
    EmbeddedRotationProvider embedded,
    TargetAnalysis targets,
    Configuration configuration)
{
    private readonly Dictionary<(RotationMode Mode, byte Job, ulong Target), (uint Frame, RotationFrame Value)> frameCache = [];

    internal RotationFrame Evaluate(RotationMode mode)
    {
        var player = objectTable.LocalPlayer;
        if (player == null)
            return new(null, null, [], wrath.IsLoaded, false, mode, 0, 0, "Waiting for a character.");

        var classJob = (byte)player.ClassJob.RowId;
        if (!JobCatalog.TryGet(classJob, out var anchor))
            return new(null, null, [], wrath.IsLoaded, false, mode, 0, 0,
                $"Class/job {classJob} is not a supported combat job.");

        var targetSnapshot = targets.Snapshot(anchor.AoeAction);
        var effectiveMode = mode == RotationMode.Dynamic
            ? (targetSnapshot.EnemyCount >= Math.Clamp(configuration.DynamicAoeTargetCount, 2, 8)
                ? RotationMode.Aoe
                : RotationMode.SingleTarget)
            : mode;

        return EvaluateEffective(anchor, effectiveMode, targetSnapshot);
    }

    internal RotationFrame EvaluateGuidance()
    {
        var player = objectTable.LocalPlayer;
        if (player == null || !JobCatalog.TryGet((byte)player.ClassJob.RowId, out var anchor))
            return new(null, null, [], wrath.IsLoaded, false, RotationMode.SingleTarget, 0, 0,
                "Waiting for a supported combat job.");
        var targetsNow = targets.Snapshot(anchor.AoeAction);
        var fallbackAoe = targetsNow.EnemyCount >= Math.Clamp(configuration.DynamicAoeTargetCount, 2, 8);
        var effective = embedded.GuidanceUsesAoe(targetsNow.EnemyCount, fallbackAoe)
            ? RotationMode.Aoe : RotationMode.SingleTarget;
        return EvaluateEffective(anchor, effective, targetsNow);
    }

    private unsafe RotationFrame EvaluateEffective(RotationAnchor anchor, RotationMode effectiveMode, TargetSnapshot targetSnapshot)
    {
        var currentFrame = Framework.Instance()->FrameCounter;
        var cacheKey = (effectiveMode, anchor.JobId, targetManager.Target?.GameObjectId ?? 0);
        if (frameCache.TryGetValue(cacheKey, out var cached) && cached.Frame == currentFrame)
            return cached.Value;

        var embeddedLead = embedded.GetLead(anchor, effectiveMode);
        var sourceAction = embedded.EntryAction;

        RotationSuggestion lead;
        var parity = false;
        if (wrath.IsLoaded && sourceAction != 0 && embeddedLead.ActionId != 0 &&
            wrath.GetAdjusted(sourceAction) != wrath.GetNativeAdjusted(sourceAction))
        {
            var exact = wrath.GetAdjusted(sourceAction);
            lead = new RotationSuggestion(exact, effectiveMode, SuggestionSource.LiveWrath, true,
                "Exact action currently exposed by Wrath Combo for this rotation entry point.", true);
            parity = exact == embeddedLead.ActionId;
        }
        else
        {
            lead = embeddedLead;
        }

        // Keep a shared eight-action sequence for both the visible bar and
        // positional consumers. The bar independently limits how many it draws.
        var forecast = lead.ActionId == 0 ? [] : embedded.Forecast(anchor, effectiveMode, lead.ActionId, 8);
        var status = lead.Source == SuggestionSource.LiveWrath
            ? parity ? "LIVE WRATH · PARITY" : "LIVE WRATH · FORECAST DIFFERS"
            : "INDEPENDENT WRATH RULES";
        var actionEnemyCount = targets.Snapshot(lead.ActionId).EnemyCount;
        var value = new RotationFrame(anchor, lead, forecast, wrath.IsLoaded, parity, effectiveMode,
            targetSnapshot.EnemyCount, actionEnemyCount, status);
        frameCache[cacheKey] = (currentFrame, value);
        return value;
    }
}
