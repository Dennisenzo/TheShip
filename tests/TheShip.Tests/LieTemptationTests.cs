using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// The stakes of an answer are known before it is given, a believed good answer is a relief (true or not), and a hard truth
/// is credited at the next drift with no line that names it (docs/plan-lie-temptation.md).
/// </summary>
public class LieTemptationTests
{
    private static CrewMember Woken(Voyage v, string name)
    {
        var c = v.Find(name)!;
        v.Wake(c);
        v.Go();
        Assert.Equal(CrewState.Awake, c.State);
        return c;
    }

    private static void Tell(Voyage v, CrewMember to, string key, string value) =>
        v.Say(v.GetFact(key).Say(value), new Reading(LineKind.Statement, key, value), new[] { to });

    [Fact]
    public void Nobody_asleep_has_stakes()
    {
        var v = new Voyage(1);
        Assert.Null(v.StakesOf(v.Find("lin")!));
        Assert.Equal("", v.StakesText(v.Find("lin")!));
    }

    [Fact]
    public void The_stakes_of_a_good_true_answer_are_a_relief_and_where_it_can_be_checked()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        Assert.Equal("alive:idris", mara.PendingQuestion);
        var s = v.StakesOf(mara)!;
        Assert.Equal(0, s.TruthHurts);
        Assert.Equal(0, s.TruthEarns);
        Assert.Equal(Relief.FromTruth, s.Relief);
        Assert.True(s.BlocksWork);
        Assert.False(s.BlocksSleep);
        Assert.Contains("the pod itself", s.Checks);
        var text = v.StakesText(mara);
        Assert.StartsWith("Mara asked: How is Idris?", text);
        Assert.Contains("relief", text);
        Assert.Contains("No work until it is answered.", text);
    }

    [Fact]
    public void A_believed_good_answer_to_their_question_lifts_morale_once()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        var morale = mara.Morale;
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        Assert.Equal(morale + Tuning.ReliefMorale, mara.Morale);
        Assert.Null(v.StakesOf(mara));
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        Assert.Equal(morale + Tuning.ReliefMorale, mara.Morale);
    }

    [Fact]
    public void The_stakes_of_a_hard_truth_name_its_cost_its_reward_and_the_lie()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        v.AssignDeal(mara, v.FindFault("")!, patch: false);
        Assert.Equal("dose", mara.PendingQuestion);
        var s = v.StakesOf(mara)!;
        Assert.Equal(10, s.TruthHurts);
        Assert.Equal((int)Tuning.HardTruthTrust, s.TruthEarns);
        Assert.Equal(Relief.FromLie, s.Relief);
        // Mara is steady: the truth does not make her refuse.
        Assert.False(s.TruthStopsRepair);
        // An Engineering 3 lead on a level 2 fault is at the cap either way: the truth costs her no odds.
        Assert.Equal(0, s.OddsLost);
        Assert.Contains("dosimeter", s.Checks);
        var text = v.StakesText(mara);
        Assert.Matches(@"The truth costs 10 morale \(([\d.]+ h slower; )?half dose, no trust lost if hurt\) and earns 8 trust at the next drift\.", text);
        Assert.Matches(@"A reassuring answer lifts morale by 6( \(work about [\d.]+ h faster\))?\.", text);
    }

    [Fact]
    public void A_lie_borrows_the_relief()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        v.AssignDeal(mara, v.FindFault("")!, patch: false);
        var morale = mara.Morale;
        Tell(v, mara, "dose", "safe");
        Assert.Equal(StatementKind.False, v.Statements[^1].Kind);
        Assert.Equal(morale + Tuning.ReliefMorale, mara.Morale);
        Assert.Equal(0, mara.PendingTrust);
    }

    [Fact]
    public void The_danger_question_counts_the_precautions_the_truth_brings()
    {
        var v = new Voyage(1);
        var jonah = Woken(v, "jonah");
        var fault = v.FindFault("")!;
        jonah.Morale = 60;
        jonah.PendingQuestion = "dose";
        var s = v.StakesOf(jonah)!;
        Assert.True(s.TruthBriefs);
        // The hours after the truth: the morale it costs and the precautions it brings (PrecautionRate), against now (unbriefed).
        var now = v.EarliestDoneHours(fault);
        Tell(v, jonah, "dose", v.GetFact("dose").Truth);
        jonah.Morale = 60 - s.TruthHurts;
        var told = v.EarliestDoneHours(fault);
        Assert.Equal(Math.Round((told - now) * 2) / 2, s.HoursSlower);
        // Morale alone would quote fewer hours.
        var moraleOnly = now / Voyage.MoraleRate(60 - s.TruthHurts) * Voyage.MoraleRate(60);
        Assert.True(s.HoursSlower > Math.Round((moraleOnly - now) * 2) / 2, s.HoursSlower + " vs " + (moraleOnly - now));
    }

    [Fact]
    public void The_danger_question_stakes_say_the_dose_and_the_trust_briefly_and_never_a_verdict()
    {
        var v = new Voyage(1);
        var jonah = Woken(v, "jonah");
        jonah.PendingQuestion = "dose";
        var text = v.StakesText(jonah);
        Assert.Contains((Tuning.PrecautionHarm == 0.5 ? "half" : "%") + " dose, no trust lost if hurt", text);
        foreach (var w in new[] { "true)", "false", " lie", "lied" })
            Assert.DoesNotContain(w, text);
        // Already working on terms fixed at the start: the answer changes nothing about them, and the stakes say nothing of it.
        jonah.Trust = 80;
        jonah.PendingQuestion = null;
        v.AssignDeal(jonah, v.FindFault("")!, patch: false);
        Assert.Equal(v.FindFault("")!.Id, jonah.WorkingOn);
        jonah.PendingQuestion = "dose";
        Assert.False(v.StakesOf(jonah)!.TruthBriefs);
        Assert.DoesNotContain("no trust lost", v.StakesText(jonah));
    }

    [Fact]
    public void A_good_answer_that_is_doubted_is_no_relief()
    {
        var v = new Voyage(1);
        var jonah = Woken(v, "jonah");
        jonah.PendingQuestion = "pods";
        Assert.True(v.DoubtScore(jonah, v.GetFact("pods")) >= Tuning.DoubtThreshold);
        Assert.Equal(Relief.None, v.StakesOf(jonah)!.Relief);
        var morale = jonah.Morale;
        Tell(v, jonah, "pods", "sound");
        Assert.Equal(morale, jonah.Morale);
        Assert.Contains("pods", jonah.Doubts);
    }

    [Fact]
    public void The_truth_costs_repair_odds_only_below_the_cap_and_can_stop_the_repair()
    {
        var v = new Voyage(1);
        var jonah = Woken(v, "jonah");
        var fault = v.FindFault("")!;
        jonah.Morale = 70;
        jonah.Trust = 35;
        jonah.PendingQuestion = "dose";
        var s = v.StakesOf(jonah)!;
        Assert.Equal(10, s.TruthHurts);
        // Engineering 2 on a level 2 fault: 0.85 + (70 - 50) / 200 = 0.95, and 0.90 after the truth.
        Assert.Equal(5, s.OddsLost);
        Assert.True(s.BlocksWork);
        Assert.True(s.TruthStopsRepair);
        Assert.Contains(" h slower", v.StakesText(jonah));
        Assert.Contains("Told the truth, Jonah refuses the repair.", v.StakesText(jonah));

        Assert.Equal(Voyage.RepairChance(jonah, fault, jonah.Morale), v.RepairChanceWith(jonah, fault));
        Assert.Equal(0.95, v.RepairChanceWith(jonah, fault)!.Value, 6);
        Assert.Equal(Tuning.RepairChanceMax, Voyage.RepairChance(v.Find("mara")!, fault, 70));
    }

    [Fact]
    public void A_hard_truth_is_credited_at_the_drift_without_a_line_that_names_it()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        Assert.Equal(Tuning.HardTruthTrust, mara.PendingTrust);
        VoyageTests.SleepWhenAllowed(v, mara);
        var trust = mara.Trust;
        var from = v.Log.Count;
        v.Drift();
        Assert.Equal(Math.Min(100, trust + Tuning.HardTruthTrust), mara.Trust);
        Assert.Equal(0, mara.PendingTrust);
        // What the player sees never says whether a statement was true: no line names the credit, the truth or the trust.
        var after = v.Log.Skip(from).ToList();
        Assert.Contains(after, l => l.Text.StartsWith("DRIFT:"));
        Assert.DoesNotContain(after, l => l.Text.Contains("thinks better") || l.Text.Contains("hard truth")
                                          || l.Text.Contains("radiation dose") || l.Text.Contains("trust +"));
    }

    [Fact]
    public void Switching_between_two_good_answers_is_no_relief()
    {
        // Earth matters to Tomas: "no word" and "good news" are both good answers, so going from one to the other is no relief.
        var v = new Voyage(1);
        var tomas = Woken(v, "tomas");
        tomas.Morale = 50;
        tomas.Trust = 90;
        Tell(v, tomas, "earth", "no word");
        Assert.Equal(50 + Tuning.ReliefMorale, tomas.Morale);
        for (var i = 0; i < 4; i++)
        {
            Tell(v, tomas, "earth", "good news");
            Tell(v, tomas, "earth", "no word");
        }
        Assert.Equal(50 + Tuning.ReliefMorale, tomas.Morale);
        tomas.PendingQuestion = "earth";
        Assert.Equal(Relief.None, v.StakesOf(tomas)!.Relief);

        // After the hard answer, the good one is a relief again.
        Tell(v, tomas, "earth", "programme ended");
        var morale = tomas.Morale;
        Tell(v, tomas, "earth", "good news");
        Assert.Equal(morale + Tuning.ReliefMorale, tomas.Morale);
    }

    [Fact]
    public void A_hard_truth_is_credited_once_per_person_and_fact()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        Assert.Equal("dosed", v.GetFact("dose").Truth);
        for (var i = 0; i < 3; i++)
        {
            Tell(v, mara, "dose", "safe");
            Tell(v, mara, "dose", "dosed");
        }
        Assert.Equal(Tuning.HardTruthTrust, mara.PendingTrust);
        mara.PendingQuestion = "dose";
        Assert.Equal(0, v.StakesOf(mara)!.TruthEarns);
        Assert.DoesNotContain("earns", v.StakesText(mara));
    }

    [Fact]
    public void The_pods_stakes_follow_the_wear_of_a_thaw()
    {
        var v = new Voyage(1);
        var jonah = Woken(v, "jonah");
        jonah.Trust = 90;
        jonah.PendingQuestion = "pods";
        v.PodWear = Tuning.PodsWornAt - 1;
        // Wear comes with the thaw's end, not the Wake click: the pods are still sound, and the truth would be a relief.
        v.Wake(v.Find("lin")!);
        Assert.Equal("sound", v.GetFact("pods").Truth);
        Assert.Equal(0, v.StakesOf(jonah)!.TruthHurts);
        while (v.Find("lin")!.State == CrewState.Thawing)
            v.Go();
        Assert.Equal(Tuning.PodsWornAt, v.PodWear);
        Assert.Equal("worn", v.GetFact("pods").Truth);
        Assert.True(v.StakesOf(jonah)!.TruthHurts > 0);
    }

    [Fact]
    public void With_no_parts_left_a_repair_shows_no_odds()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        var fault = v.FindFault("")!;
        Assert.NotNull(v.RepairChanceWith(mara, fault));
        v.Parts = 0;
        Assert.Null(v.RepairChanceWith(mara, fault));
    }

    [Fact]
    public void A_lie_earns_no_credit_at_the_drift()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        v.AssignDeal(mara, fault, patch: false);
        Tell(v, mara, "dose", "safe");
        v.AssignDeal(mara, fault, patch: false);
        VoyageTests.FinishAlert(v);
        Assert.DoesNotContain(v.Log, l => l.Text.Contains("thinks better of you"));
        Assert.Equal(0, mara.PendingTrust);
    }
}
