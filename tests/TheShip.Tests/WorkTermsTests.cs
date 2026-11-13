using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Work terms set by the briefing, the risk axis (DESIGN.md, "Work terms"): beliefs, true or false, set the speed, the dose and the hurt rule.</summary>
public class WorkTermsTests
{
    private static void Tell(Voyage v, CrewMember to, string key, string value) =>
        v.Say(v.GetFact(key).Say(value), new Reading(LineKind.Statement, key, value), new[] { to });

    /// <summary>Mara awake with a Follower's trust, answered about Idris, and (if given) told what the bay does; the coolant repair is open.</summary>
    private static (Voyage V, CrewMember Mara, Fault Fault) Ready(int seed, string? dose)
    {
        var v = new Voyage(seed);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Tell(v, mara, "alive:idris", "alive");
        if (dose != null)
            Tell(v, mara, "dose", dose);
        mara.Trust = 80;
        return (v, mara, v.FindFault("")!);
    }

    /// <summary>Starts the repair and runs it until it holds; null when it failed (the seed does not suit). <paramref name="later"/> is told after the start.</summary>
    private static (Voyage V, CrewMember Mara, Fault Fault)? Repaired(int seed, string? dose, string? later = null)
    {
        var (v, mara, f) = Ready(seed, dose);
        v.AssignDeal(mara, f, patch: false);
        if (later != null)
            Tell(v, mara, "dose", later);
        for (var i = 0; i < 6 && !f.Resolved; i++)
            v.Go();
        return f.State == FaultState.Repaired && mara.Alive ? (v, mara, f) : null;
    }

    private static (Voyage V, CrewMember Mara, Fault Fault) FirstRepaired(string? dose, string? later = null)
    {
        for (var seed = 1; seed < 60; seed++)
            if (Repaired(seed, dose, later) is { } r)
                return r;
        throw new Xunit.Sdk.XunitException("no seed repaired the coolant leak");
    }

    [Fact]
    public void Terms_follow_what_they_believe_true_or_false()
    {
        var (v, mara, f) = Ready(1, null);
        Assert.Equal(RiskKnowledge.Unaware, v.TermsFor(mara, f).Risk);
        Tell(v, mara, "dose", "safe");
        var lie = v.TermsFor(mara, f);
        Assert.Equal(RiskKnowledge.Reassured, lie.Risk);
        Assert.Equal(1.0, lie.RateFactor);
        Assert.Equal(1.0, lie.HarmShare);
        Tell(v, mara, "dose", "dosed");
        var told = v.TermsFor(mara, f);
        Assert.Equal(RiskKnowledge.Briefed, told.Risk);
        Assert.Equal(Tuning.PrecautionRate, told.RateFactor);
        Assert.Equal(Tuning.PrecautionHarm, told.HarmShare);
        var noRisk = v.TermsFor(mara, new Fault { Id = "x", Kind = "x", Name = "x", Skill = Skill.Engineering, Level = 1, Hours = 1, ClockHours = 1, IfIgnored = "" });
        Assert.Equal(RiskKnowledge.NoRisk, noRisk.Risk);
        Assert.Equal(1.0, noRisk.RateFactor);
    }

    [Fact]
    public void A_Follower_with_no_briefing_is_unbriefed()
    {
        var (v, mara, f) = Ready(1, null);
        Assert.Equal(TrustBand.Follows, mara.Band);
        v.AssignDeal(mara, f, patch: false);
        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal(RiskKnowledge.Unaware, mara.Terms!.Risk);
        Assert.DoesNotContain(v.Log, e => e.Kind == LogKind.Recorded && e.Text.Contains("precautions"));
    }

    [Fact]
    public void A_briefed_start_is_recorded_as_what_she_believes()
    {
        var (v, mara, f) = Ready(1, "dosed");
        v.AssignDeal(mara, f, patch: false);
        Assert.True(mara.Terms!.RiskBriefed);
        var line = v.Log.Single(e => e.Kind == LogKind.Recorded && e.Text.StartsWith("Recorded: Mara starts")).Text;
        Assert.Contains("precautions", line);
        Assert.Contains("10% slower", line);
        Assert.Contains("believing that working in the coolant bay gives a radiation dose:", line);
        Assert.EndsWith(Tuning.PrecautionHarm == 0.5 ? "halved." : "%.", line);
        // Another Repair click for someone already on the fault keeps the terms and records nothing new.
        v.AssignDeal(mara, f, patch: false);
        Assert.Single(v.Log, e => e.Kind == LogKind.Recorded && e.Text.Contains("Mara starts"));
        // A lie gets the same terms as the truth would: here the other way round, nothing is recorded.
        var (v2, m2, f2) = Ready(1, "safe");
        v2.AssignDeal(m2, f2, patch: false);
        Assert.DoesNotContain(v2.Log, e => e.Kind == LogKind.Recorded && e.Text.Contains("Mara starts"));
    }

    [Fact]
    public void The_factor_slows_progress_and_the_verdict_hours()
    {
        var (v, mara, f) = Ready(1, null);
        mara.Morale = Tuning.MoraleRateNeutral;
        var plain = v.EarliestDoneHours(f);
        Tell(v, mara, "dose", "dosed");
        mara.Morale = Tuning.MoraleRateNeutral;
        var briefed = v.EarliestDoneHours(f);
        Assert.Equal(plain / Tuning.PrecautionRate, briefed, 3);
        // Progress: after starting briefed, the repair runs at the factor (to the first stop: the clock's end of this repair).
        v.AssignDeal(mara, f, patch: false);
        mara.Morale = Tuning.MoraleRateNeutral;
        var start = v.Clock;
        v.Go();
        Assert.Equal(f.Hours, f.Progress, 3);
        Assert.Equal(plain / Tuning.PrecautionRate, v.Clock - start, 3);
    }

    [Fact]
    public void Terms_are_fixed_at_the_start()
    {
        var (v, mara, f) = Ready(1, null);
        v.AssignDeal(mara, f, patch: false);
        Tell(v, mara, "dose", "dosed");
        Assert.Equal(RiskKnowledge.Unaware, mara.Terms!.Risk);
        Assert.Equal(RiskKnowledge.Unaware, v.TermsOf(mara, f).Risk);
        Assert.Equal(RiskKnowledge.Briefed, v.TermsFor(mara, f).Risk);
        // The speed, the dose and the hurt rule all keep the start's terms to the end of the work.
        var (_, late, _) = FirstRepaired(null, later: "dosed");
        Assert.Equal(100 - Tuning.DangerDose, late.Health, 3);
        Assert.Contains("dose", late.HurtUnbriefed);
    }

    [Fact]
    public void A_briefing_after_the_start_changes_neither_the_dose_nor_the_hurt_rule()
    {
        // Assigned unbriefed, told the danger mid-work: full dose, and the hurt still counts against the ship at the notice beat.
        var (v, mara, _) = FirstRepaired(null, later: "dosed");
        Assert.Equal(100 - Tuning.DangerDose, mara.Health, 3);
        var before = mara.Trust;
        v.Go();
        v.Go();
        Assert.Contains(v.Log, e => e.Intent?.Kind == "noticed_dose");
        Assert.True(mara.Trust < before);
        // Assigned briefed, told "safe" mid-work: the precautions taken at the start still halve the dose.
        var (_, kept, _) = FirstRepaired("dosed", later: "safe");
        Assert.Equal(100 - Tuning.DangerDose * Tuning.PrecautionHarm, kept.Health, 3);
        Assert.DoesNotContain("dose", kept.HurtUnbriefed);
    }

    [Fact]
    public void The_notice_says_whether_the_ship_told_too_late_or_never()
    {
        static string Notice(string? later)
        {
            var (v, _, _) = FirstRepaired(null, later);
            v.Go();
            v.Go();
            return v.Log.First(e => e.Intent?.Kind == "noticed_dose").Intent!.Decision;
        }
        var late = Notice("dosed");
        Assert.Contains("the ship told you only after you had started", late);
        Assert.DoesNotContain("never told you", late);
        Assert.Contains("the ship never told you", Notice(null));
        Assert.Contains("the ship told you it was safe after you had started", Notice("safe"));
    }

    [Fact]
    public void A_failed_repair_that_spends_the_last_part_hurts_on_the_start_terms()
    {
        // Assigned unbriefed with one part left, told the danger mid-work; the repair fails, stops for want of a part (which takes
        // the lead off the fault) and injures them. The hurt still follows the start's terms: unbriefed, charged, not "warned".
        for (var seed = 1; seed < 3000; seed++)
        {
            var (v, mara, f) = Ready(seed, null);
            v.Parts = 1;
            v.AssignDeal(mara, f, patch: false);
            Tell(v, mara, "dose", "dosed");
            var trust = mara.Trust;
            var stopped = v.Log.Count;
            for (var i = 0; i < 6 && f.State != FaultState.Repaired && mara.WorkingOn == f.Id; i++)
            {
                stopped = v.Log.Count;
                v.Go();
            }
            var step = v.Log.Skip(stopped).ToList();
            if (!step.Any(e => e.Intent?.Kind == "work_no_parts") || !step.Any(e => e.Intent?.Kind is "injured" or "injured_warned"))
                continue;
            Assert.Equal(0, v.Parts);
            Assert.Contains(step, e => e.Intent?.Kind == "work_no_parts" && e.Intent.Decision.Contains("used up the last spare part"));
            Assert.Contains(step, e => e.Intent?.Kind == "injured");
            Assert.DoesNotContain(step, e => e.Intent?.Kind == "injured_warned");
            Assert.Equal(1, v.TermStats.InjuriesNotBriefed);
            Assert.Equal(0, v.TermStats.InjuriesBriefed);
            Assert.Equal(1, v.TermStats.HurtUnbriefed);
            Assert.True(mara.Trust <= trust - Tuning.HurtByOrder, $"trust {trust} -> {mara.Trust}");
            return;
        }
        throw new Xunit.Sdk.XunitException("no seed failed the repair with an injury on the last part");
    }

    [Fact]
    public void A_briefed_worker_takes_half_the_dose_and_an_unbriefed_one_all_of_it()
    {
        var (v, mara, _) = FirstRepaired("dosed");
        Assert.Equal(100 - Tuning.DangerDose * Tuning.PrecautionHarm, mara.Health, 3);
        var (v2, mara2, _) = FirstRepaired(null);
        Assert.Equal(100 - Tuning.DangerDose, mara2.Health, 3);
        var (v3, mara3, _) = FirstRepaired("safe");
        Assert.Equal(100 - Tuning.DangerDose, mara3.Health, 3);
        Assert.NotNull(v);
        Assert.NotNull(v2);
        Assert.NotNull(v3);
    }

    private static double TrustAfterNotice(string? dose, out bool charged, out int caught)
    {
        var (v, mara, _) = FirstRepaired(dose);
        var before = mara.Trust;
        var lies = mara.CaughtLies;
        v.Go();
        v.Go();
        charged = v.Log.Any(e => e.Intent?.Kind == "noticed_dose");
        caught = mara.CaughtLies - lies;
        return mara.Trust - before;
    }

    [Fact]
    public void Hurt_without_a_word_costs_trust_but_a_warned_worker_pays_nothing()
    {
        var none = TrustAfterNotice(null, out var chargedNone, out _);
        Assert.True(chargedNone);
        var warned = TrustAfterNotice("dosed", out var chargedWarned, out var caughtWarned);
        Assert.False(chargedWarned);
        Assert.Equal(0, caughtWarned);
        Assert.True(warned > none, $"warned {warned} vs unwarned {none}");
    }

    [Fact]
    public void A_liar_who_said_safe_pays_the_caught_lie_and_not_also_the_hurt_by_order()
    {
        var lied = TrustAfterNotice("safe", out var charged, out var caught);
        Assert.False(charged);
        Assert.Equal(1, caught);
        var none = TrustAfterNotice(null, out _, out _);
        // The lie costs LieTrust (22) and no HurtByOrder (10) on top: unbriefed costs only the 10.
        Assert.InRange(lied, -Tuning.LieTrust - 4, -Tuning.LieTrust + 1);
        Assert.InRange(none, -Tuning.HurtByOrder - 1, 1);
    }
}
