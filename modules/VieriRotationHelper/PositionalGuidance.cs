using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;

namespace VieriRotationHelper;

// Versioned, primitive-only IPC: [schema, available, actionId, side, stepsAhead, jobId].
// side: 0 none, 1 rear, 2 flank. Available + none is authoritative, not a fallback request.
internal sealed class PositionalGuidance : IDisposable
{
    private readonly ICallGateProvider<ulong, uint[]> ipc;
    private readonly EmbeddedRotationProvider engine;
    private readonly RotationCoordinator coordinator;
    private long updated;
    private ulong targetId;
    private uint jobId;
    private uint[] snapshot = [1, 0, 0, 0, 0, 0];

    internal PositionalGuidance(EmbeddedRotationProvider engine, RotationCoordinator coordinator)
    {
        this.engine = engine;
        this.coordinator = coordinator;
        ipc = Plugin.PluginInterface.GetIpcProvider<ulong, uint[]>("VieriRotationHelper.PositionalGuidance.V1");
        ipc.RegisterFunc(Get);
    }

    private uint[] Get(ulong requestedTarget)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        var target = Plugin.TargetManager.Target as IBattleChara;
        if (!engine.IsActive) return [1, 0, 0, 0, 0, 0];
        if (player == null || player.IsDead || target == null || target.IsDead ||
            !target.IsTargetable || requestedTarget != target.GameObjectId ||
            !JobCatalog.TryGet((byte)player.ClassJob.RowId, out var anchor))
            return [1, 1, 0, 0, 0, 0];

        var now = Environment.TickCount64;
        if (requestedTarget == targetId && jobId == player.ClassJob.RowId && now - updated < 100)
            return snapshot.ToArray();
        targetId = requestedTarget;
        jobId = player.ClassJob.RowId;
        updated = now;
        snapshot = [1, 1, 0, 0, 0, jobId];
        try
        {
            var frame = coordinator.EvaluateGuidance();
            var actions = frame.Lead == null ? [] : new[] { frame.Lead.ActionId }
                .Concat(frame.Forecast.Select(x => x.ActionId)).ToArray();
            var next = PositionalLookahead.Select(actions, action =>
                Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(action, out var row) && row.ActionCategory.RowId == 4);
            snapshot = [1, 1, next.Action, next.Side, next.Steps, jobId];
        }
        catch (Exception ex)
        {
            // Keep authoritative none on errors instead of presenting unrelated guesses.
            if (now - lastError > 10000)
            {
                lastError = now;
                Plugin.Log.Warning(ex, "Positional guidance temporarily unavailable.");
            }
        }
        return snapshot.ToArray();
    }

    private long lastError;
    public void Dispose() => ipc.UnregisterFunc();
}
