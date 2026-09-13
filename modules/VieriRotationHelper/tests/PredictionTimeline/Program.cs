using WrathCombo;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }

var replacements = new[]
{
    new TraitReplacementMap.Replacement(2866, 7411, 3),
    new TraitReplacementMap.Replacement(2868, 7412, 3),
    new TraitReplacementMap.Replacement(2873, 7413, 3),
    new TraitReplacementMap.Replacement(121, 132, 3),
    new TraitReplacementMap.Replacement(132, 16532, 3),
    new TraitReplacementMap.Replacement(34606, 34608, 1),
    new TraitReplacementMap.Replacement(34608, 34610, 2),
};
var map = TraitReplacementMap.Build(replacements);

Check(map[7411] == 2866, "MCH trait-upgraded starter maps to its recorded combo action");
Check(map[7412] == 2868, "MCH trait-upgraded second step maps to its recorded combo action");
Check(map[7413] == 2873, "MCH trait-upgraded finisher maps to its recorded combo action");
Check(map[132] == 121, "first trait upgrade maps to the original combo action");
Check(map[16532] == 121, "chained trait upgrades collapse to the original combo action");
Check(!map.ContainsKey(34608), "temporary replacement types are not normalized");
Check(!map.ContainsKey(34610), "dynamic combo steps remain distinct");
Check(!map.ContainsKey(36963), "unrelated actions remain untouched");

var rdm = new RdmPredictionState(50, 50, 0);
rdm.Advance(37004, hasBaseCastTime: true); // Jolt III
Check(rdm.TryHasStatus(RdmPredictionState.Dualcast, out var dualcast) && dualcast,
    "a predicted Jolt hard-cast grants Dualcast");
Check(rdm.BlackMana == 52 && rdm.WhiteMana == 52, "Jolt advances both mana colors");
rdm.Advance(25856, hasBaseCastTime: true); // instant Veraero III
Check(rdm.TryHasStatus(RdmPredictionState.Dualcast, out dualcast) && !dualcast,
    "a predicted long spell consumes Dualcast");
Check(rdm.BlackMana == 52 && rdm.WhiteMana == 58, "Veraero advances only white mana");
rdm.Advance(37004, hasBaseCastTime: true);
Check(rdm.TryHasStatus(RdmPredictionState.Dualcast, out dualcast) && dualcast,
    "the following hard-cast restores the alternating RDM cadence");

var procRdm = new RdmPredictionState(20, 25, 0,
    [new(RdmPredictionState.VerfireReady, 30)]);
procRdm.Advance(7510, hasBaseCastTime: true); // Verfire
Check(procRdm.TryHasStatus(RdmPredictionState.VerfireReady, out var verfire) && !verfire,
    "Verfire Ready is consumed in the prediction timeline");
Check(procRdm.TryHasStatus(RdmPredictionState.Dualcast, out dualcast) && dualcast,
    "a predicted Verfire hard-cast grants Dualcast");
Check(procRdm.BlackMana == 25 && procRdm.WhiteMana == 25,
    "Verfire advances black mana before the next decision");

var acceleratedRdm = new RdmPredictionState(0, 0, 0);
acceleratedRdm.Advance(7518, hasBaseCastTime: false); // Acceleration
acceleratedRdm.Advance(25855, hasBaseCastTime: true); // Verthunder III
Check(acceleratedRdm.TryHasStatus(RdmPredictionState.Acceleration, out var acceleration) && !acceleration,
    "Acceleration is consumed by its predicted spell");
Check(acceleratedRdm.TryHasStatus(RdmPredictionState.VerfireReady, out verfire) && verfire,
    "Acceleration adds its guaranteed Verfire proc");
Check(acceleratedRdm.TryHasStatus(RdmPredictionState.Dualcast, out dualcast) && !dualcast,
    "an Acceleration instant cast does not invent Dualcast");

var swordplayRdm = new RdmPredictionState(0, 0, 0,
    [new(RdmPredictionState.MagickedSwordplay, 15, 3)]);
swordplayRdm.Advance(45960, hasBaseCastTime: false);
Check(swordplayRdm.BlackMana == 0 && swordplayRdm.WhiteMana == 0,
    "Magicked Swordplay melee steps do not spend unavailable mana");
Check(swordplayRdm.ManaStacks == 1,
    "Magicked Swordplay melee steps advance the projected mana-stack combo");

Console.WriteLine($"{checks} prediction timeline checks passed.");
