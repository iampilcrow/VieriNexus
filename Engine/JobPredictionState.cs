using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.JobGauge.Enums;
using System;
using System.Collections.Generic;
using WrathCombo.Services;

namespace WrathCombo;

/// <summary>
/// Job-resource shadow used by every Wrath forecast. Values are captured once
/// from the live gauge, then advanced without mutating game or plugin state.
/// </summary>
internal sealed class JobPredictionState
{
    internal static class Keys
    {
        internal const string Primary = "primary", Secondary = "secondary", Tertiary = "tertiary", Quaternary = "quaternary";
        internal const string Timer = "timer", Timer2 = "timer2", Flag = "flag", Flag2 = "flag2", Step = "step", Step2 = "step2";
        internal const string Extra = "extra", Extra2 = "extra2", Extra3 = "extra3", Extra4 = "extra4";
    }

    private readonly uint job;
    private readonly int level;
    private readonly Dictionary<string, int> values = [];
    private readonly uint[] danceSteps = new uint[4];

    private JobPredictionState(uint job)
    {
        this.job = job;
        level = ECommons.DalamudServices.Svc.PlayerState.EffectiveLevel;
    }

    internal static JobPredictionState Capture(uint job)
    {
        var state = new JobPredictionState(job);
        switch (job)
        {
            case 19:
                var pld = Service.ComboCache.GetJobGauge<PLDGauge>();
                state.Set(Keys.Primary, pld.OathGauge);
                break;
            case 20:
                var mnk = Service.ComboCache.GetJobGauge<MNKGauge>();
                state.Set(Keys.Primary, mnk.Chakra); state.Set(Keys.Secondary, mnk.OpoOpoFury);
                state.Set(Keys.Tertiary, mnk.RaptorFury); state.Set(Keys.Quaternary, mnk.CoeurlFury);
                state.Set(Keys.Flag, (int)mnk.Nadi); state.Set(Keys.Timer, mnk.BlitzTimeRemaining);
                for (var i = 0; i < 3; i++) state.Set($"beast{i}", (int)mnk.BeastChakra[i]);
                break;
            case 21:
                state.Set(Keys.Primary, Service.ComboCache.GetJobGauge<WARGauge>().BeastGauge);
                break;
            case 22:
                var drg = Service.ComboCache.GetJobGauge<DRGGauge>();
                state.Set(Keys.Flag, drg.IsLOTDActive); state.Set(Keys.Timer, drg.LOTDTimer);
                state.Set(Keys.Primary, drg.FirstmindsFocusCount);
                break;
            case 23:
                var brd = Service.ComboCache.GetJobGauge<BRDGauge>();
                state.Set(Keys.Flag, (int)brd.Song); state.Set(Keys.Timer, brd.SongTimer);
                state.Set(Keys.Primary, brd.Repertoire); state.Set(Keys.Secondary, brd.SoulVoice);
                break;
            case 24:
                var whm = Service.ComboCache.GetJobGauge<WHMGauge>();
                state.Set(Keys.Primary, whm.Lily); state.Set(Keys.Secondary, whm.BloodLily);
                state.Set(Keys.Timer, whm.LilyTimer);
                break;
            case 25:
                var blm = Service.ComboCache.GetJobGauge<BLMGauge>();
                state.Set(Keys.Primary, blm.AstralFireStacks); state.Set(Keys.Secondary, blm.UmbralIceStacks);
                state.Set(Keys.Tertiary, blm.UmbralHearts); state.Set(Keys.Flag, blm.IsParadoxActive);
                state.Set(Keys.Quaternary, blm.AstralSoulStacks); state.Set(Keys.Extra, blm.PolyglotStacks);
                state.Set(Keys.Timer, blm.EnochianTimer);
                break;
            case 27:
                var smn = Service.ComboCache.GetJobGauge<SMNGauge>();
                state.Set(Keys.Primary, smn.AttunementCount); state.Set(Keys.Flag, (int)smn.AttunementType);
                state.Set(Keys.Timer, smn.AttunementTimerRemaining); state.Set(Keys.Timer2, smn.SummonTimerRemaining);
                state.Set(Keys.Secondary, smn.HasAetherflowStacks ? 2 : 0);
                state.Set("ifrit", smn.IsIfritReady); state.Set("titan", smn.IsTitanReady); state.Set("garuda", smn.IsGarudaReady);
                state.Set(Keys.Extra, (int)smn.AetherFlags);
                break;
            case 28:
                var sch = Service.ComboCache.GetJobGauge<SCHGauge>();
                state.Set(Keys.Primary, sch.Aetherflow); state.Set(Keys.Secondary, sch.FairyGauge);
                state.Set(Keys.Timer, sch.SeraphTimer); state.Set(Keys.Flag, (int)sch.DismissedFairy > 0);
                break;
            case 30:
                var nin = Service.ComboCache.GetJobGauge<NINGauge>();
                state.Set(Keys.Primary, nin.Ninki); state.Set(Keys.Secondary, nin.Kazematoi);
                break;
            case 31:
                var mch = Service.ComboCache.GetJobGauge<MCHGauge>();
                state.Set(Keys.Flag, mch.IsOverheated); state.Set(Keys.Flag2, mch.IsRobotActive);
                state.Set(Keys.Primary, mch.Heat); state.Set(Keys.Secondary, mch.Battery);
                state.Set(Keys.Timer, mch.OverheatTimeRemaining); state.Set(Keys.Timer2, mch.SummonTimeRemaining);
                break;
            case 32:
                var drk = Service.ComboCache.GetJobGauge<DRKGauge>();
                state.Set(Keys.Primary, drk.Blood); state.Set(Keys.Timer, drk.DarksideTimeRemaining);
                state.Set(Keys.Timer2, drk.ShadowTimeRemaining); state.Set(Keys.Flag, drk.HasDarkArts);
                break;
            case 33:
                var ast = Service.ComboCache.GetJobGauge<ASTGauge>();
                state.Set(Keys.Primary, (int)ast.DrawnCards[0]); state.Set(Keys.Secondary, (int)ast.DrawnCards[1]);
                state.Set(Keys.Tertiary, (int)ast.DrawnCards[2]); state.Set(Keys.Quaternary, (int)ast.DrawnCrownCard);
                break;
            case 34:
                var sam = Service.ComboCache.GetJobGauge<SAMGauge>();
                state.Set(Keys.Primary, sam.Kenki); state.Set(Keys.Secondary, sam.MeditationStacks);
                state.Set(Keys.Flag, sam.HasGetsu); state.Set(Keys.Flag2, sam.HasSetsu); state.Set(Keys.Extra, sam.HasKa);
                state.Set(Keys.Extra2, (int)sam.Kaeshi);
                break;
            case 37:
                var gnb = Service.ComboCache.GetJobGauge<GNBGauge>();
                state.Set(Keys.Primary, gnb.Ammo); state.Set(Keys.Step, gnb.AmmoComboStep);
                break;
            case 38:
                var dnc = Service.ComboCache.GetJobGauge<DNCGauge>();
                state.Set(Keys.Primary, dnc.Feathers); state.Set(Keys.Secondary, dnc.Esprit);
                state.Set(Keys.Step, dnc.CompletedSteps); state.Set(Keys.Flag, dnc.IsDancing);
                Array.Copy(dnc.Steps, state.danceSteps, 4);
                break;
            case 39:
                var rpr = Service.ComboCache.GetJobGauge<RPRGauge>();
                state.Set(Keys.Primary, rpr.Soul); state.Set(Keys.Secondary, rpr.Shroud);
                state.Set(Keys.Tertiary, rpr.LemureShroud); state.Set(Keys.Quaternary, rpr.VoidShroud);
                break;
            case 40:
                var sge = Service.ComboCache.GetJobGauge<SGEGauge>();
                state.Set(Keys.Primary, sge.Addersgall); state.Set(Keys.Secondary, sge.Addersting);
                break;
            case 41:
                var vpr = Service.ComboCache.GetJobGauge<VPRGauge>();
                state.Set(Keys.Primary, vpr.RattlingCoilStacks); state.Set(Keys.Secondary, vpr.SerpentOffering);
                state.Set(Keys.Step, (int)vpr.DreadCombo); state.Set(Keys.Step2, (int)vpr.SerpentCombo);
                break;
            case 42:
                var pct = Service.ComboCache.GetJobGauge<PCTGauge>();
                state.Set(Keys.Primary, pct.Paint); state.Set(Keys.Secondary, pct.PalleteGauge);
                state.Set(Keys.Flag, pct.MooglePortraitReady); state.Set(Keys.Flag2, pct.MadeenPortraitReady);
                state.Set(Keys.Extra, (int)pct.CanvasFlags); state.Set(Keys.Extra2, (int)pct.CreatureFlags);
                break;
        }
        return state;
    }

    internal int Get(string key, int live) => values.TryGetValue(key, out var value) ? value : live;
    internal bool Get(string key, bool live) => values.TryGetValue(key, out var value) ? value != 0 : live;
    internal uint DanceNextStep(uint live)
    {
        var step = Get(Keys.Step, 0);
        return Get(Keys.Flag, false) && step is >= 0 and < 4 ? danceSteps[step] : live;
    }
    internal int BeastChakra(int index, int live) => Get($"beast{index}", live);

    internal uint AdvanceMana(uint action, uint category, int resourceCost, uint current, uint maximum,
        PredictionStatusState statuses)
    {
        var cost = Math.Max(0, resourceCost);
        var grant = 0;
        if (job == 25)
        {
            var fire = Get(Keys.Primary, 0);
            var ice = Get(Keys.Secondary, 0);
            var hearts = Get(Keys.Tertiary, 0);
            var astralSpell = action is 141 or 147 or 152 or 162 or 3577 or 16505 or 25794;
            var umbralSpell = action is 142 or 154 or 159 or 3576 or 25793 or 25795;
            var elementalSpell = astralSpell || umbralSpell;
            if (action == 152 && statuses.Has(165)) cost = 0;
            else if (fire > 0 && action == 162) cost = Math.Max((int)(current * (hearts > 0 ? .33 : 1)), 800);
            else if (fire > 0 && action == 16505) cost = Math.Max((int)current, 800);
            else if (fire > 0 && astralSpell) cost = hearts > 0 ? 0 : cost * 2;
            else if (fire > 0 && umbralSpell || ice > 0 && (elementalSpell || action == 25797)) cost = 0;
            if (ice > 0 && umbralSpell) grant = ice switch { 1 => 2500, 2 => 5000, _ => 10000 };
            if (action == 158) grant = (int)maximum;
        }
        else if (job == 32)
        {
            if (Get(Keys.Flag, false) && action is 16466 or 16467 or 16469 or 16470) cost = 0;
            if (action == 3623) grant += 600;
            if (level >= 68 && category is 2 or 3 && statuses.Has(742)) grant += 600;
        }

        return (uint)Math.Clamp((long)current - cost + grant, 0, maximum);
    }

    internal void Progress(float seconds)
    {
        Add(Keys.Timer, -(int)(seconds * 1000), 0, int.MaxValue);
        Add(Keys.Timer2, -(int)(seconds * 1000), 0, int.MaxValue);
        if (job == 23 && Get(Keys.Timer, 0) == 0) Set(Keys.Flag, 0);
        if (job == 31 && Get(Keys.Timer, 0) == 0) Set(Keys.Flag, false);
        if (job == 31 && Get(Keys.Timer2, 0) == 0) Set(Keys.Flag2, false);
        if (job == 28 && Get(Keys.Timer, 0) == 0) Set(Keys.Flag, false);
    }

    internal void Advance(uint action, uint previous, uint category, PredictionStatusState statuses)
    {
        switch (job)
        {
            case 19: AdvancePld(action, statuses); break;
            case 20: AdvanceMnk(action, statuses); break;
            case 21: AdvanceWar(action, statuses); break;
            case 22: AdvanceDrg(action); break;
            case 23: AdvanceBrd(action); break;
            case 24: AdvanceWhm(action, statuses); break;
            case 25: AdvanceBlm(action, statuses); break;
            case 27: AdvanceSmn(action); break;
            case 28: AdvanceSch(action); break;
            case 30: AdvanceNin(action, category, statuses); break;
            case 31: AdvanceMch(action, category, statuses); break;
            case 32: AdvanceDrk(action, category, statuses); break;
            case 33: AdvanceAst(action); break;
            case 34: AdvanceSam(action, statuses); break;
            case 37: AdvanceGnb(action); break;
            case 38: AdvanceDnc(action, statuses); break;
            case 39: AdvanceRpr(action, category, statuses); break;
            case 40: AdvanceSge(action, statuses); break;
            case 41: AdvanceVpr(action, previous, statuses); break;
            case 42: AdvancePct(action, statuses); break;
        }
    }

    private void AdvancePld(uint a, PredictionStatusState s)
    {
        if (a is 3542 or 7382 or 25746 or 27) Add(Keys.Primary, -50, 0, 100);
        if (a == 3539) { if (level >= 64) s.Add(2673, 30); if (level >= 76) s.Add(1902, 30); }
        else if (a == 16457 && level >= 72) s.Add(2673, 30);
        if (a is 7384 or 16458 or 16459 or 25748 or 25749 or 25750) s.Consume(1368);
    }
    private void AdvanceMnk(uint a, PredictionStatusState s)
    {
        var chakra = a switch { 36940 or 36941 or 36942 or 36943 => 1,
            16474 or 25761 or 25763 or 3547 => -5, 16476 => -10, _ => 0 };
        Add(Keys.Primary, chakra, 0, 10);
        if (a is 74) Set(Keys.Secondary, 1); else if (a is 53 or 36945) Add(Keys.Secondary, -1, 0, 3);
        if (a is 61) Set(Keys.Tertiary, 2); else if (a is 54 or 36946) Add(Keys.Tertiary, -1, 0, 3);
        if (a is 66) Set(Keys.Quaternary, 3); else if (a is 56 or 36947) Add(Keys.Quaternary, -1, 0, 3);

        if (a is 3543 or 3545 or 25765 or 25768 or 25769 or 25882 or 36948)
        {
            Set("beast0", 0); Set("beast1", 0); Set("beast2", 0);
            if (a is 3545 or 25765 or 36948) Set(Keys.Flag, Get(Keys.Flag, 0) | 2);
            if (a is 25768 or 25882) Set(Keys.Flag, Get(Keys.Flag, 0) | 1);
            if (a is 25769) Set(Keys.Flag, 0);
            return;
        }

        if (a is 53 or 62 or 74 or 25767 or 36945) AddBeast(1);
        else if (a is 54 or 61 or 16473 or 36946) AddBeast(2);
        else if (a is 56 or 66 or 70 or 36947) AddBeast(3);
        if (s.Has(110) && a is 53 or 54 or 56 or 61 or 62 or 66 or 70 or 74 or 16473 or 25767 or 36945 or 36946 or 36947)
            s.Consume(110);
    }

    private void AddBeast(int beast)
    {
        for (var i = 0; i < 3; i++)
            if (Get($"beast{i}", 0) == 0) { Set($"beast{i}", beast); return; }
    }
    private void AdvanceWar(uint a, PredictionStatusState s)
    {
        var delta = a switch { 37 => 10, 42 => 20, 45 => 10, 16462 => 20, 52 => 50,
            49 or 51 or 3549 or 3550 or 16463 or 16465 => -50, _ => 0 };
        if (s.Has(1177) && a is 3549 or 3550)
        {
            s.Consume(1177); delta = 0;
            if (s.TryGet(3833, out var fury) && fury.Stacks >= 2) { s.Remove(3833); s.Add(3901, 30); }
            else s.Add(3833, 30, (ushort)(fury.Stacks + 1));
        }
        Add(Keys.Primary, delta, 0, 100);
    }
    private void AdvanceDrg(uint a)
    {
        if (a is 16479 or 25770) Add(Keys.Primary, 1, 0, 2);
        else if (a == 25773) Add(Keys.Primary, -2, 0, 2);
        else if (a == 3555) { Set(Keys.Flag, true); Set(Keys.Timer, 20000); }
    }
    private void AdvanceBrd(uint a)
    {
        if (a is 114 or 116 or 3559) { Set(Keys.Flag, a == 114 ? 1 : a == 116 ? 2 : 3); Set(Keys.Timer, 45000); }
        if (a == 7404) Add(Keys.Primary, -3, 0, 3);
        if (a == 16496) Add(Keys.Secondary, -100, 0, 100);
    }
    private void AdvanceWhm(uint a, PredictionStatusState s)
    {
        if (a is 16531 or 16534) { Add(Keys.Primary, -1, 0, 3); Add(Keys.Secondary, 1, 0, 3); }
        else if (a == 16535) Add(Keys.Secondary, -3, 0, 3);
        else if (a == 37009) s.Consume(3879);
    }
    private void AdvanceBlm(uint a, PredictionStatusState s)
    {
        var oldFire = Get(Keys.Primary, 0); var oldIce = Get(Keys.Secondary, 0);
        if (a == 149) { var fire = oldFire; Set(Keys.Primary, 0); Set(Keys.Secondary, fire > 0 ? 1 : 0); AddThunderhead(); return; }
        if (a == 25797) { Set(Keys.Flag, false); if (oldFire > 0) { Add(Keys.Primary, 1, 0, 3); s.Add(165, 30); } else Add(Keys.Secondary, 1, 0, 3); return; }
        if (a is 141 or 147 or 152 or 162 or 16505 or 25794 or 3577)
        { Set(Keys.Secondary, 0); Set(Keys.Primary, a is 141 ? Math.Min(3, Get(Keys.Primary, 0) + 1) : 3); }
        if (a is 142 or 154 or 25793 or 25795 or 3576)
        { Set(Keys.Primary, 0); Set(Keys.Secondary, a is 142 ? Math.Min(3, Get(Keys.Secondary, 0) + 1) : 3); }
        if (a is 159 or 3576) Set(Keys.Tertiary, 3);
        if (a == 16506) { Add(Keys.Secondary, 1, 0, 3); Add(Keys.Tertiary, 1, 0, 3); }
        if (a == 3577) { Add(Keys.Tertiary, -1, 0, 3); Add(Keys.Quaternary, 1, 0, 6); }
        if (a == 162) { Set(Keys.Tertiary, 0); Add(Keys.Quaternary, 3, 0, 6); }
        if (a == 36989) Set(Keys.Quaternary, 0);
        if (a is 7422 or 16507) Add(Keys.Extra, -1, 0, 3);
        if (a == 25796) Add(Keys.Extra, 1, 0, 3);
        if (a == 158) { Set(Keys.Primary, 3); Set(Keys.Tertiary, 3); Set(Keys.Flag, true); }
        if (a == 152) s.Remove(165);
        if ((oldFire == 0 && oldIce == 0 && (Get(Keys.Primary, 0) > 0 || Get(Keys.Secondary, 0) > 0)) ||
            (oldFire > 0 && Get(Keys.Secondary, 0) > 0) || (oldIce > 0 && Get(Keys.Primary, 0) > 0)) AddThunderhead();
        void AddThunderhead() => s.Add(3870, 30);
    }
    private void AdvanceSmn(uint a)
    {
        if (a is 3581 or 7427 or 25800 or 25831 or 36992)
        {
            Set(Keys.Timer2, 15000);
            Set(Keys.Timer, 0);
            Set(Keys.Flag, 0);
            Set(Keys.Primary, 0);
            Set("ifrit", true); Set("titan", true); Set("garuda", true);
        }
        if (a is 16508 or 16510) Set(Keys.Secondary, 2);
        else if (a is 181 or 3578 or 36990) Add(Keys.Secondary, -1, 0, 2);
        if (a is 25802 or 25805 or 25838) BeginAttunement(1, 2, "ifrit");
        else if (a is 25803 or 25806 or 25839) BeginAttunement(2, 4, "titan");
        else if (a is 25804 or 25807 or 25840) BeginAttunement(3, 4, "garuda");
        else if (a is 25808 or 25809 or 25810 or 25811 or 25812 or 25813 or 25814 or 25815 or 25816 or 25817 or 25818 or 25819 or 25823 or 25824 or 25825 or 25827 or 25828 or 25829 or 25832 or 25833 or 25834)
        { Add(Keys.Primary, -1, 0, 4); if (Get(Keys.Primary, 0) == 0) { Set(Keys.Flag, 0); Set(Keys.Timer, 0); } }
    }
    private void BeginAttunement(int type, int count, string ready)
    { Set(Keys.Flag, type); Set(Keys.Primary, count); Set(Keys.Timer, 30000); Set(ready, false); }
    private void AdvanceSch(uint a)
    {
        if (a is 166 or 3587) Set(Keys.Primary, 3); else if (a == 167) Add(Keys.Primary, -1, 0, 3);
        if (a == 16545) { Set(Keys.Timer, 22000); Set(Keys.Flag, true); }
    }
    private void AdvanceNin(uint a, uint category, PredictionStatusState s)
    {
        var ninki = a switch { 16489 => 50, 36957 => 40, 25774 => 10, 25777 or 25778 => 5,
            16493 or 36959 or 36960 or 7401 or 7402 => -50, _ => 0 };
        Add(Keys.Primary, ninki, 0, 100);
        if (a == 3563) Add(Keys.Secondary, 2, 0, 5); else if (a == 2255) Add(Keys.Secondary, -1, 0, 5);
        if (a == 2267 && level >= 90)
        {
            var stacks = s.TryGet(2690, out var ready) ? ready.Stacks : (ushort)0;
            s.Add(2690, 30, (ushort)Math.Min(3, stacks + 1));
        }
        else if (a is 25777 or 25778) s.Consume(2690);
        if (a == 16493) s.Add(1954, 30, 5);
        else if (category == 3 && s.Has(1954)) { s.Consume(1954); Add(Keys.Primary, 5, 0, 100); }
    }
    private void AdvanceMch(uint a, uint category, PredictionStatusState s)
    {
        var heat = a switch { 2866 or 2868 or 2870 or 2873 or 7411 or 7412 or 7413 => 5, 25786 => 10, 17209 => -50, _ => 0 };
        var battery = a switch { 2872 or 16500 or 25788 or 36981 => 20, 2873 or 7413 => 10, 2864 or 16501 => -50, _ => 0 };
        Add(Keys.Primary, heat, 0, 100); Add(Keys.Secondary, battery, 0, 100);
        if (a == 17209) { Set(Keys.Flag, true); Set(Keys.Timer, 10000); }
        if (a is 2864 or 16501) { Set(Keys.Flag2, true); Set(Keys.Timer2, 15000); }
        if (a is 7410 or 36978)
        {
            s.Consume(2688);
            PredictionContext.Current?.ReduceCooldown(2874, 15);
            PredictionContext.Current?.ReduceCooldown(2890, 15);
            PredictionContext.Current?.ReduceCooldown(36979, 15);
            PredictionContext.Current?.ReduceCooldown(36980, 15);
        }
        if (category == 3) s.Remove(851);
    }
    private void AdvanceDrk(uint a, uint category, PredictionStatusState s)
    {
        var blood = a switch
        {
            3632 or 16468 when level >= 62 => 20,
            7391 or 7392 => s.Has(1972) ? 0 : -50,
            _ => 0
        };
        if (level >= 68 && category is 2 or 3 && s.Has(742))
        {
            blood += 10;
            s.Consume(742);
        }
        Add(Keys.Primary, blood, 0, 100);
        if (Get(Keys.Flag, false) && a is 16466 or 16467 or 16469 or 16470) Set(Keys.Flag, false);
        if (a is 16466 or 16467 or 16469 or 16470) Add(Keys.Timer, 30000, 0, 60000);
        if (s.Has(1972) && a is 7391 or 7392) s.Consume(1972);
    }
    private void AdvanceAst(uint a)
    {
        if (a is 37023 or 37024 or 37025 or 37026 or 37027 or 37028) Set(Keys.Primary, 0);
        if (a is 7444 or 7445 or 25869) Set(Keys.Quaternary, 0);
    }
    private void AdvanceSam(uint a, PredictionStatusState s)
    {
        var kenki = a switch { 25780 or 36963 or 7481 or 7482 or 7484 or 7485 or 7486 => 5, 7480 => 10, 16482 => 50,
            7492 or 7493 => -10, 7490 or 7491 or 7496 or 16481 => -25, 36964 => -50, _ => 0 };
        Add(Keys.Primary, kenki, 0, 100);
        if (a is 7487 or 7488 or 7489 or 25781 or 36965 or 36966) Add(Keys.Secondary, 1, 0, 3);
        if (a == 16487) Add(Keys.Secondary, -3, 0, 3);
        if (a == 7480) Set(Keys.Flag2, true); if (a is 7481 or 7484) Set(Keys.Flag, true); if (a is 7482 or 7485) Set(Keys.Extra, true);
        if (a is 7487 or 7488 or 7489 or 36965 or 36966) { Set(Keys.Flag, false); Set(Keys.Flag2, false); Set(Keys.Extra, false); }
        if (a == 7499) s.Add(1233, 15, 3);
        else if (a is 7480 or 7481 or 7482 or 7484 or 7485 && s.Has(1233)) s.Consume(1233);
        if (a == 7487) s.Add(4216, 30);
        else if (a == 7488) s.Add(3852, 30);
        else if (a == 36965) s.Add(4217, 30);
        else if (a == 36966) s.Add(4218, 30);
        else if (a == 16483) { s.Remove(4216); s.Remove(3852); s.Remove(4217); s.Remove(4218); }
        else if (a == 16486) s.Remove(4216);
        else if (a == 36967) s.Remove(4217);
        else if (a == 36968) s.Remove(4218);
    }
    private void AdvanceGnb(uint a)
    {
        var delta = a switch { 16145 or 16149 => 1, 16146 or 16162 or 16163 => -1, 25760 => -2, _ => 0 };
        if (a == 16164) Set(Keys.Primary, 3); else Add(Keys.Primary, delta, 0, 3);
        if (a == 16146) Set(Keys.Step, 1); else if (a == 16147) Set(Keys.Step, 2); else if (a == 16150) Set(Keys.Step, 0);
    }
    private void AdvanceDnc(uint a, PredictionStatusState s)
    {
        if (a is 15997 or 15998) { Set(Keys.Flag, true); Set(Keys.Step, 0); return; }
        if (a is >= 15999 and <= 16002 && Get(Keys.Flag, false)) { Add(Keys.Step, 1, 0, 4); return; }
        if (a is 16003 or 16004 or 36984) { Set(Keys.Flag, false); Set(Keys.Step, 0); }
        var esprit = a switch { 15989 or 15990 or 15991 or 15992 or 15993 or 15994 or 15995 or 15996 => 5,
            16005 or 36985 => -50, 25790 => 50, _ => 0 };
        Add(Keys.Secondary, esprit, 0, 100);
        if (a is 16007 or 16008) Add(Keys.Primary, -1, 0, 4);
        if (a is 15991 or 15995) { if (s.Has(2693)) s.Remove(2693); else s.Remove(1814); }
        if (a is 15992 or 15996) { if (s.Has(2694)) s.Remove(2694); else s.Remove(1815); }
    }
    private void AdvanceRpr(uint a, uint category, PredictionStatusState s)
    {
        var soul = a switch { 24373 or 24374 or 24375 or 24376 or 24377 or 24386 or 24388 => 10,
            24380 or 24381 => 50, 24389 or 24390 or 24391 or 24392 or 24393 => -50, _ => 0 };
        var shroud = a switch { 24382 or 24383 or 24384 or 36970 or 36971 or 36972 => 10, 24394 => -50, _ => 0 };
        Add(Keys.Primary, soul, 0, 100); Add(Keys.Secondary, shroud, 0, 100);
        if (a == 24394) Set(Keys.Tertiary, 5); else if (a is 24395 or 24396 or 24397) Add(Keys.Tertiary, -1, 0, 5); else if (a == 24398) Set(Keys.Tertiary, 0);
        if (a is 24395 or 24396 or 24397) Add(Keys.Quaternary, 1, 0, 5);
        else if (a is 24399 or 24400) Add(Keys.Quaternary, -2, 0, 5);
        if (category is 2 or 3) { s.Consume(2587); s.Consume(3858); }
        if (a is 24389 or 24392)
        {
            s.Remove(2587); s.Remove(3858); s.Add(2587, 30, 1);
        }
        else if (a == 24393)
        {
            s.Remove(2587); s.Remove(3858); s.Add(level >= 96 ? 3858u : 2587u, 30, 2);
        }
        if (a == 24398 && s.Has(3859)) { s.Remove(3859); s.Add(3860, 30); }
        if (Get(Keys.Tertiary, 0) > 0) s.Remove(3860);
    }
    private void AdvanceSge(uint a, PredictionStatusState s)
    {
        if (a is 24296 or 24298 or 24299 or 24303) Add(Keys.Primary, -1, 0, 3); else if (a == 24309) Add(Keys.Primary, 1, 0, 3);
        if (a is 24304 or 24316) Add(Keys.Secondary, -1, 0, 3);
        if (a == 24290) s.Add(2606, 30);
        else if (a is 24291 or 24292 or 24293 or 24308 or 24314 or 37032 or 37034) s.Remove(2606);
    }
    private void AdvanceVpr(uint a, uint previous, PredictionStatusState s)
    {
        if (a is 34620 or 34623 or 34647) Add(Keys.Primary, 1, 0, 3); else if (a == 34633) Add(Keys.Primary, -1, 0, 3);
        var offering = a switch { 34610 or 34611 or 34612 or 34613 or 34618 or 34619 => 10, 34621 or 34622 or 34624 or 34625 => 5, 34626 => -50, _ => 0 };
        Add(Keys.Secondary, offering, 0, 100);
        var dread = a switch { 34620 => 1, 34621 => 2, 34622 => 3, 34623 => 4, 34624 => 5, 34625 => 6, _ => -1 };
        if (dread >= 0) Set(Keys.Step, dread);
        if (a is 34622 or 34625) Set(Keys.Step, 0);
        if (a == 34636 && previous == 34621) s.Add(3658, 40);
        else if (a == 34637 && previous == 34622) s.Add(3657, 40);
        else if (a == 34638 && previous == 34624) s.Add(3660, 40);
        else if (a == 34639 && previous == 34625) s.Add(3659, 40);
    }
    private void AdvancePct(uint a, PredictionStatusState s)
    {
        if (a is 34652 or 34655 or 34658 or 34661 or 34688) Add(Keys.Primary, 1, 0, 5);
        else if (a is 34662 or 34663) Add(Keys.Primary, -1, 0, 5);
        if (a is 34652 or 34658) Add(Keys.Secondary, 25, 0, 100); else if (a == 34683) Add(Keys.Secondary, -50, 0, 100);
        if (a is 34678 or 34679 or 34680) s.Consume(3680);
        if (a is 34653 or 34654 or 34655 or 34659 or 34660 or 34661) s.Consume(3674);
        if ((a is >= 34650 and <= 34663 or >= 34678 and <= 34681 or 34688) &&
            s.TryGet(3688, out var hyper))
        {
            s.Consume(3688);
            if (hyper.Stacks <= 1) s.Add(3679, 30);
        }
        if (a == 34688) s.Remove(3679);
        if (a is 34664 or 34665 or 34666 or 34667)
            Set(Keys.Extra, Get(Keys.Extra, 0) | (int)(a switch { 34664 => CanvasFlags.Pom, 34665 => CanvasFlags.Wing, 34666 => CanvasFlags.Claw, _ => CanvasFlags.Maw }));
        else if (a == 34668) Set(Keys.Extra, Get(Keys.Extra, 0) | (int)CanvasFlags.Weapon);
        else if (a == 34669) Set(Keys.Extra, Get(Keys.Extra, 0) | (int)CanvasFlags.Landscape);
        if (a is 34670 or 34671 or 34672 or 34673)
        {
            var canvas = a switch { 34670 => CanvasFlags.Pom, 34671 => CanvasFlags.Wing, 34672 => CanvasFlags.Claw, _ => CanvasFlags.Maw };
            Set(Keys.Extra, Get(Keys.Extra, 0) & ~(int)canvas);
            if (a == 34673) Set(Keys.Flag2, true);
            else if (a == 34671) Set(Keys.Flag, true);
        }
        else if (a == 34674) Set(Keys.Extra, Get(Keys.Extra, 0) & ~(int)CanvasFlags.Weapon);
        else if (a == 34675) Set(Keys.Extra, Get(Keys.Extra, 0) & ~(int)CanvasFlags.Landscape);
        else if (a == 34676) Set(Keys.Flag, false);
        else if (a == 34677) Set(Keys.Flag2, false);
    }

    private void Set(string key, int value) => values[key] = value;
    private void Set(string key, bool value) => values[key] = value ? 1 : 0;
    private void Add(string key, int delta, int min, int max) => Set(key, Math.Clamp(Get(key, 0) + delta, min, max));
}
