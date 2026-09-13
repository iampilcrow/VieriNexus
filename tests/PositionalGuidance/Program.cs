using VieriRotationHelper;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
foreach (var (id, side) in new (uint, uint)[] {
    (56,2),(36947,2),(66,1),(3554,2),(3556,1),(88,1),(25772,1),
    (2258,1),(2255,1),(3563,2),(7481,1),(7482,2),(24382,2),(24383,1),
    (36970,2),(36971,1),(34610,2),(34611,2),(34612,1),(34613,1),(34621,2),(34622,1) })
{
    var lead = PositionalLookahead.Select([id], _ => false);
    Check(lead == (id, side, 0u), $"Correct live side for {id}");
    var weave = PositionalLookahead.Select([999,998,id], x => x >= 998);
    Check(weave == (id, side, 2u), $"Looks past abilities for {id}");
    var next = PositionalLookahead.Select([1,id], _ => false);
    Check(next == (id, side, 1u), $"Warns one GCD ahead for {id}");
}
Check(PositionalLookahead.Select([34621,34622], _ => false).Action == 34621, "Nearest positional wins, not later opposite side");
Check(PositionalLookahead.Select([1,2,34622], _ => false) == default, "Do not chase distant speculative positional");
Check(PositionalLookahead.Select([34614,34615], _ => false) == default, "AoE sequence has no invented positional");
Check(PositionalLookahead.Select([], _ => false) == default, "Empty or waiting sequence clears guidance");
Console.WriteLine($"{checks} positional guidance checks passed across all positional jobs.");

namespace VieriRotationHelper { internal enum PositionalKind { None, Flank, Rear } }
