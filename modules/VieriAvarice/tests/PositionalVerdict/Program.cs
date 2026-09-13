using Avarice.Positional;
using Avarice.Structs;
var cases = new (PositionalState Before, bool? Hit, PositionalState Expected)[] {
    (PositionalState.Unknown, null, PositionalState.Unknown),
    (PositionalState.Unknown, false, PositionalState.Failure),
    (PositionalState.Unknown, true, PositionalState.Success),
    (PositionalState.Success, false, PositionalState.Success),
    (PositionalState.Success, null, PositionalState.Success),
    (PositionalState.Failure, true, PositionalState.Success),
    (PositionalState.Failure, null, PositionalState.Failure),
};
foreach (var test in cases)
    if (PositionalVerdict.Merge(test.Before, test.Hit) != test.Expected) throw new Exception(test.ToString());
Console.WriteLine($"{cases.Length} positional verdict checks passed; unknown results are not misses.");
var watcher = new Avarice.Data.RotationHelperWatcher();
foreach (var test in new (uint[]? State, bool Available, uint Side)[] {
    (new uint[] {1,1,34621,2,1,41},true,2),
    (new uint[] {1,1,34622,1,0,41},true,1),
    (new uint[] {1,1,0,0,0,41},true,0),
    (new uint[] {1,0,0,0,0,0},false,0),
    (new uint[] {2,1,34621,2,0,41},false,0),
    (new uint[] {1,1},false,0),
    (null,false,0),
})
{
    Avarice.Data.Svc.PluginInterface.State = test.State;
    if (watcher.TryGet(123, out var side) != test.Available || side != test.Side)
        throw new Exception("IPC availability/side contract failed");
}
Avarice.Data.Svc.PluginInterface.Throw = true;
if (watcher.TryGet(123, out _)) throw new Exception("Missing provider must allow standalone fallback");
Console.WriteLine("8 IPC consumer checks passed, including authoritative none and provider unload.");

var gate = new Avarice.GameplayOverlayGate();
if (gate.Evaluate(false, false, false, true, 0)) throw new Exception("Overlay visible before login");
if (gate.Evaluate(true, true, true, true, 1_000)) throw new Exception("Overlay visible between areas");
if (gate.Evaluate(true, true, true, false, 2_000)) throw new Exception("Overlay skipped settle period");
if (gate.Evaluate(true, true, true, false, 2_749)) throw new Exception("Overlay appeared before stable settle period");
if (!gate.Evaluate(true, true, true, false, 2_750)) throw new Exception("Overlay did not appear after stable settle period");
if (gate.Evaluate(true, false, true, false, 3_000)) throw new Exception("Overlay visible without an available player");
if (gate.Evaluate(true, true, true, false, 4_000)) throw new Exception("Overlay did not reset after player became unavailable");
if (!gate.Evaluate(true, true, true, false, 4_750)) throw new Exception("Overlay did not recover after a second stable settle period");
Console.WriteLine("8 gameplay overlay gate checks passed.");

namespace Dalamud.Plugin.Ipc
{
    internal interface ICallGateSubscriber<T, TResult> { TResult InvokeFunc(T arg); }
}
namespace Avarice.Data
{
    internal static class Svc { internal static readonly FakeIpc PluginInterface = new(); }
    internal sealed class FakeIpc : Dalamud.Plugin.Ipc.ICallGateSubscriber<ulong,uint[]>
    {
        internal uint[]? State;
        internal bool Throw;
        internal Dalamud.Plugin.Ipc.ICallGateSubscriber<ulong,uint[]> GetIpcSubscriber<T,TResult>(string name) => this;
        public uint[] InvokeFunc(ulong arg) => Throw ? throw new Exception("Provider unloaded") : State!;
    }
}
