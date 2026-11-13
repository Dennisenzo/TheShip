using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Work terms, the cause axis (DESIGN.md, "Work terms"): a cause fact per fault kind, a repair-odds bonus for a told cause, the true cause learned at completion.</summary>
public class CauseFactTests
{
    private static void Tell(Voyage v, CrewMember to, string key, string value) =>
        v.Say(v.GetFact(key).Say(value), new Reading(LineKind.Statement, key, value), new[] { to });

    private static (Voyage V, CrewMember Mara, Fault Fault) Ready(int seed = 1)
    {
        var v = new Voyage(seed);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Tell(v, mara, "alive:idris", "alive");
        Tell(v, mara, "dose", "dosed");
        mara.Trust = 80;
        return (v, mara, v.FindFault("")!);
    }

    [Fact]
    public void The_earlier_variation_slots_keep_their_draws_and_the_cause_slot_comes_after_the_jitter()
    {
        // Replays the old draw order (victim, patient, side 3, side 5, slack x5, jitter x5) on the same generator and compares.
        for (var seed = 0; seed < 200; seed++)
        {
            var d = Variation.Draw(seed);
            var rng = new Random(unchecked(seed * 7919 + 13));
            rng.Next(6);
            var count = Scenario.Crew().Count(c => c.Id != d.Alert2Victim && ShipPlan.Pod(c.Id).Bank == ShipPlan.Pod(d.Alert2Victim).Bank);
            rng.Next(count);
            rng.Next(2);
            rng.Next(1);
            for (var i = 0; i < 5; i++)
            {
                var s = rng.Next(Tuning.AlertSlackMin, Tuning.AlertSlackMax + 1);
                Assert.Equal(i == 0 ? 0 : s, d.SlackHours[i]);
            }
            for (var i = 0; i < 5; i++)
                Assert.Equal(rng.Next(-Tuning.AlertYearJitter, Tuning.AlertYearJitter + 1), d.YearJitter[i]);
            // The seven cause rolls follow, in the order of CauseKinds.
            foreach (var kind in Variation.CauseKinds)
                Assert.Equal(rng.Next(2), d.Causes[kind]);
        }
        Assert.Contains(Enumerable.Range(0, 50).SelectMany(s => Variation.Draw(s).Causes.Values), i => i == 1);
        Assert.Contains(Enumerable.Range(0, 50).SelectMany(s => Variation.Draw(s).Causes.Values), i => i == 0);
    }

    [Fact]
    public void Classic_takes_the_first_value_and_a_seed_sets_the_truth()
    {
        var v = new Voyage(1);
        foreach (var kind in Variation.CauseKinds)
            Assert.Equal(v.GetFact("cause:" + kind).Values[0], v.GetFact("cause:" + kind).Truth);
        for (var seed = 0; seed < 30; seed++)
        {
            var d = Variation.Draw(seed);
            var drawn = new Voyage(seed, d);
            foreach (var kind in Variation.CauseKinds)
                Assert.Equal(drawn.GetFact("cause:" + kind).Values[d.Causes[kind]], drawn.GetFact("cause:" + kind).Truth);
        }
    }

    [Fact]
    public void Causes_stay_out_of_the_log_until_their_alert_has_started()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        Assert.True(v.InLog(v.GetFact("cause:coolant")));
        Assert.False(v.InLog(v.GetFact("cause:nav")));
        Assert.False(v.InLog(v.GetFact("cause:pod")));
        Assert.False(v.InLog(v.GetFact("cause:blight")));
        Assert.False(v.Unread(mara, v.GetFact("cause:nav")));
        Assert.True(v.Unread(mara, v.GetFact("cause:coolant")));
    }

    [Fact]
    public void A_doubted_cause_of_a_fault_still_to_come_is_not_checked_until_its_alert()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Tell(v, mara, "alive:idris", "alive");
        Tell(v, mara, "dose", "dosed");
        mara.Trust = 80;
        var fault = v.FindFault("")!;
        v.AssignDeal(mara, fault, patch: false);
        for (var i = 0; i < 50 && v.Phase == Phase.Alert; i++)
            v.Go();
        Assert.Equal(Phase.Aftermath, v.Phase);
        // In alert 1's aftermath the ship gives a false cause for the navigation drift (alert 3), and Mara doubts it.
        var nav = v.GetFact("cause:nav");
        var lie = nav.Values.First(x => !nav.Same(x, nav.Truth));
        Tell(v, mara, "cause:nav", lie);
        mara.Doubts.Clear();
        mara.Doubts.Add("cause:nav");
        var caught = mara.CaughtLies;
        for (var i = 0; i < 6 && v.Phase == Phase.Aftermath; i++)
            v.Go();
        // Nothing on the ship shows that cause yet: no console reading, no finding-out; the doubt waits.
        Assert.Equal(caught, mara.CaughtLies);
        Assert.Contains("cause:nav", mara.Doubts);
        Assert.Equal("ship", mara.Beliefs["cause:nav"].Source);
        Assert.DoesNotContain(v.Log, e => e.Text.Contains("reads a console") && e.Text.Contains("Mara"));
    }

    [Fact]
    public void A_told_cause_adds_the_bonus_true_or_false_and_not_otherwise()
    {
        var (v, mara, f) = Ready();
        mara.Morale = 30;
        var plain = v.RepairChanceWith(mara, f)!.Value;
        Assert.False(v.TermsFor(mara, f).CauseBriefed);
        Tell(v, mara, "cause:coolant", "cracked line");
        mara.Morale = 30;
        Assert.True(v.TermsFor(mara, f).CauseBriefed);
        Assert.Equal(plain + Tuning.CauseOddsBonus, v.RepairChanceWith(mara, f)!.Value, 6);
        var (v2, m2, f2) = Ready();
        Tell(v2, m2, "cause:coolant", "worn seal");
        m2.Morale = 30;
        Assert.Equal(plain + Tuning.CauseOddsBonus, v2.RepairChanceWith(m2, f2)!.Value, 6);
    }

    [Fact]
    public void The_terms_are_fixed_at_the_start_for_the_bonus_too()
    {
        var (v, mara, f) = Ready();
        v.AssignDeal(mara, f, patch: false);
        Assert.False(mara.Terms!.CauseBriefed);
        Tell(v, mara, "cause:coolant", "worn seal");
        Assert.False(v.TermsOf(mara, f).CauseBriefed);
        Assert.Equal(0, v.TermsOf(mara, f).OddsBonus);
    }

    private static int Caught(bool lie)
    {
        for (var seed = 1; seed < 60; seed++)
        {
            var (v, mara, f) = Ready(seed);
            var truth = v.GetFact("cause:coolant").Truth;
            var other = v.GetFact("cause:coolant").Values.First(x => x != truth);
            Tell(v, mara, "cause:coolant", lie ? other : truth);
            v.AssignDeal(mara, f, patch: false);
            for (var i = 0; i < 6 && !f.Resolved; i++)
                v.Go();
            if (f.State != FaultState.Repaired)
                continue;
            Assert.Equal(truth, mara.Beliefs["cause:coolant"].Value);
            Assert.Equal("evidence", mara.Beliefs["cause:coolant"].Source);
            return mara.CaughtLies;
        }
        throw new Xunit.Sdk.XunitException("no seed repaired the leak");
    }

    [Fact]
    public void The_true_cause_is_learned_at_completion_and_a_false_one_is_caught()
    {
        Assert.Equal(0, Caught(lie: false));
        Assert.Equal(1, Caught(lie: true));
    }

    [Fact]
    public void An_untold_worker_learns_the_cause_by_doing_the_work()
    {
        for (var seed = 1; seed < 60; seed++)
        {
            var (v, mara, f) = Ready(seed);
            Assert.DoesNotContain("cause:coolant", mara.Beliefs.Keys);
            v.AssignDeal(mara, f, patch: false);
            for (var i = 0; i < 6 && !f.Resolved; i++)
                v.Go();
            if (f.State != FaultState.Repaired)
                continue;
            Assert.Contains("cause:coolant", mara.Beliefs.Keys);
            return;
        }
        throw new Xunit.Sdk.XunitException("no seed repaired the leak");
    }

    [Fact]
    public void A_returning_patch_keeps_its_cause_fact()
    {
        var f = new Voyage(1).Current.Faults[0];
        Assert.Equal("cause:coolant", f.CauseFact);
        Assert.Equal("cause:coolant", f.Again("again", "Coolant leak").CauseFact);
        foreach (var kind in new[] { Variation.Blight, Variation.PowerSag })
            Assert.Equal("cause:" + kind, Scenario.SideFault(kind, 0, "s").CauseFact);
    }

    [Theory]
    [InlineData("It was just a worn seal.", "cause:coolant", "worn seal")]
    [InlineData("The seal was cracked, the builders knew about it.", "cause:coolant", "cracked line")]
    [InlineData("It is a software glitch in the controller.", "cause:controller", "glitch")]
    [InlineData("The tracker mount was bent by a hull strike.", "cause:nav", "bent mount")]
    public void Cause_statements_are_read(string text, string key, string value)
    {
        var v = new Voyage(1);
        var r = RuleReader.Read(text, v, new[] { v.Find("mara")! });
        Assert.Equal(LineKind.Statement, r.Kind);
        Assert.Equal(key, r.FactKey);
        Assert.Equal(value, r.Value);
    }

    [Fact]
    public void A_cause_told_early_is_a_belief_other_rules_can_spread()
    {
        var (v, mara, _) = Ready();
        Tell(v, mara, "cause:coolant", "worn seal");
        Assert.Equal("ship", mara.Beliefs["cause:coolant"].Source);
        Assert.Contains(v.Statements, s => s.FactKey == "cause:coolant" && s.Hearers.Contains("mara"));
    }
}
