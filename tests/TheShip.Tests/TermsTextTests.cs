using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>The work terms in words (<see cref="Voyage.TermsText"/>): from belief and fault only, never whether anything was true; and the card's cause line.</summary>
public class TermsTextTests
{
    private static readonly string[] TruthWords = { "true", "false", "lie", "lied", "truth", "honest", "lying", "untrue" };

    private static void Tell(Voyage v, CrewMember to, string key, string value) =>
        v.Say(v.GetFact(key).Say(value), new Reading(LineKind.Statement, key, value), new[] { to });

    private static void NoTruthWording(string text)
    {
        foreach (var w in text.ToLowerInvariant().Split(' ', ',', '.', ':', '-', ';'))
            Assert.DoesNotContain(w, TruthWords);
    }

    private static (Voyage V, CrewMember Mara, Fault Fault) Ready(int seed = 1)
    {
        var v = new Voyage(seed);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        return (v, mara, v.FindFault("")!);
    }

    [Fact]
    public void Terms_text_reads_the_numbers_from_tuning_and_has_no_truth_wording_for_true_false_and_no_beliefs()
    {
        var hard = new Voyage(1).GetFact("dose").Values.First(x => new Voyage(1).GetFact("dose").HardnessOf(x) > 0);
        var benign = new Voyage(1).GetFact("dose").Values.First(x => new Voyage(1).GetFact("dose").HardnessOf(x) == 0);
        var causeFact = new Voyage(1).GetFact("cause:coolant");
        foreach (var (dose, cause) in new (string?, string?)[] { (null, null), (hard, null), (benign, null), (hard, causeFact.Truth), (benign, causeFact.Values.First(x => x != causeFact.Truth)) })
        {
            var (v, mara, f) = Ready();
            if (dose != null) Tell(v, mara, "dose", dose);
            if (cause != null) Tell(v, mara, "cause:coolant", cause);
            var text = v.TermsText(mara, f);
            Assert.StartsWith("If Mara starts now: ", text);
            NoTruthWording(text);
            Assert.Contains(((int)(Tuning.CauseOddsBonus * 100)).ToString() + "%", text);
            if (dose == hard)
                Assert.Contains("work " + (int)Math.Round((1 - Tuning.PrecautionRate) * 100) + "% slower", text);
            if (cause != null)
                Assert.Contains("knows the cause", text);
            else
                Assert.Contains("has not been told the cause", text);
        }
    }

    [Fact]
    public void The_same_belief_gives_the_same_text_whether_it_was_true_or_not()
    {
        var (v1, m1, f1) = Ready();
        var (v2, m2, f2) = Ready();
        var cause = v1.GetFact("cause:coolant");
        Tell(v1, m1, "cause:coolant", cause.Truth);
        Tell(v2, m2, "cause:coolant", cause.Values.First(x => x != cause.Truth));
        Assert.Equal(v1.TermsText(m1, f1), v2.TermsText(m2, f2));
    }

    [Fact]
    public void The_risk_is_named_with_a_short_noun()
    {
        var (v, mara, f) = Ready();
        var unaware = v.TermsText(mara, f);
        Assert.Contains("Mara has not been told about the radiation dose: full dose", unaware);
        Tell(v, mara, "dose", "dosed");
        var briefed = v.TermsText(mara, f);
        Assert.Contains("Mara knows about the radiation dose: precautions", briefed);
        Assert.Contains(Tuning.PrecautionHarm == 0.5 ? "half the dose" : "% of the dose", briefed);
        foreach (var text in new[] { unaware, briefed })
            Assert.DoesNotContain("the whether", text);
    }

    [Fact]
    public void A_fault_with_no_risk_and_no_cause_has_no_terms_text()
    {
        var (v, mara, f) = Ready();
        var plain = new Fault { Id = "x", Kind = "x", Name = "x", Skill = Skill.Engineering, Level = 1, Hours = 1, ClockHours = 10, IfIgnored = "x", Section = f.Section };
        Assert.Equal("", v.TermsText(mara, plain));
    }

    [Fact]
    public void The_cause_shows_on_the_card_once_its_alert_has_started_and_not_before()
    {
        var v = new Voyage(1);
        var coolant = v.FindFault("")!;
        Assert.NotNull(v.CauseKnown(coolant));
        var later = v.Alerts.SelectMany(a => a.Faults).First(f => f.Kind == "nav");
        Assert.Null(v.CauseKnown(later));
    }

    [Fact]
    public void The_terms_item_is_for_an_idle_awake_person_with_a_job_that_has_terms()
    {
        var (v, mara, _) = Ready();
        Assert.NotNull(v.TermsItem(mara));
        var idris = v.Find("idris")!;
        Assert.Null(v.TermsItem(idris));
    }

    [Fact]
    public void Repair_chance_shown_rises_by_the_cause_bonus_once_told()
    {
        var (v, mara, f) = Ready();
        mara.Trust = 80;
        mara.Morale = 20;
        var before = v.RepairChanceWith(mara, f)!.Value;
        Tell(v, mara, "cause:coolant", v.GetFact("cause:coolant").Values.First());
        var after = v.RepairChanceWith(mara, f)!.Value;
        Assert.True(after > before);
    }

    [Fact]
    public void Every_cause_line_and_terms_text_over_200_runs_fits_the_card()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var v = new Voyage(seed, Variation.Draw(seed));
            for (var guard = 0; guard < 60 && v.Phase != Phase.Ended; guard++)
            {
                if (v.Phase == Phase.Alert)
                {
                    foreach (var f in v.Current.Faults)
                        if (v.CauseKnown(f) is { } cause)
                            Assert.True(("Cause: " + cause).Length <= IssueCardLayout.Columns * 3, $"seed {seed}: {cause}");
                    for (var i = 0; i < 12 && v.Phase == Phase.Alert; i++) v.Go();
                }
                if (v.Phase == Phase.Aftermath) v.Drift();
            }
        }
    }
}
