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

    /// <summary>Rows of the "Waiting on you" panel a text takes: word wrap at 45 columns (326 px at 7.19 px a character, as <c>Painter.Wrap</c>).</summary>
    internal static int WaitingRows(string text)
    {
        const int columns = 45;
        var rows = 0;
        var line = 0;
        foreach (var word in text.Split(' '))
        {
            if (line > 0 && line + 1 + word.Length > columns)
            {
                rows++;
                line = 0;
            }
            line += (line > 0 ? 1 : 0) + word.Length;
        }
        return rows + (line > 0 ? 1 : 0);
    }

    [Fact]
    public void The_short_terms_fit_two_rows_of_waiting_for_every_person_fault_and_belief()
    {
        // The panel holds 158 px of items (19 px a row, 4 px between items): a six-row stakes item or a four-row bargain leaves two rows.
        var seen = 0;
        // The text names the person, not the fault, so a few runs' fault sets (side faults vary) are enough.
        for (var seed = 0; seed < 20; seed++)
        {
            var v = new Voyage(seed, Variation.Draw(seed));
            foreach (var f in v.Alerts.SelectMany(a => a.Faults).Concat(v.Alerts.SelectMany(a => a.Faults).Select(f => f.Again(f.Id + "-again", f.Name, 0))))
                foreach (var c in v.Crew)
                    foreach (var dose in new string?[] { null, "safe", "dosed" })
                        foreach (var cause in new[] { false, true })
                        {
                            c.Beliefs.Clear();
                            if (dose != null) Tell(v, c, "dose", dose);
                            if (cause && f.CauseFact is { } cf) Tell(v, c, cf, v.GetFact(cf).Values[0]);
                            var text = v.TermsShort(c, f);
                            if (text.Length == 0) continue;
                            seen++;
                            Assert.True(WaitingRows(text) <= 2, $"seed {seed}: {text}");
                            NoTruthWording(text);
                        }
        }
        Assert.True(seen > 0);
    }

    [Fact]
    public void The_terms_item_is_short_with_the_full_text_as_its_tip()
    {
        var (v, mara, f) = Ready();
        var item = v.TermsItem(mara)!.Value;
        Assert.Equal(v.TermsShort(mara, f), item.Short);
        Assert.Equal(v.TermsText(mara, f), item.Full);
        Assert.StartsWith("Mara, unbriefed: full dose, hurt costs trust; cause untold", item.Short);
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
