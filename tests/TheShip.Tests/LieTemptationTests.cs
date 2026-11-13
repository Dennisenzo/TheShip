using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// The stakes of an answer are known before it is given, a believed good answer is a relief (true or not), and a hard truth
/// is credited by name when its trust arrives (docs/plan-lie-temptation.md).
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
        v.Assign(mara, v.FindFault("")!, patch: false);
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
        Assert.Contains("The truth costs 10 morale and earns 8 trust at the next drift.", text);
        Assert.Contains("A reassuring answer lifts morale by 6.", text);
    }

    [Fact]
    public void A_lie_borrows_the_relief()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        v.Assign(mara, v.FindFault("")!, patch: false);
        var morale = mara.Morale;
        Tell(v, mara, "dose", "safe");
        Assert.Equal(StatementKind.False, v.Statements[^1].Kind);
        Assert.Equal(morale + Tuning.ReliefMorale, mara.Morale);
        Assert.Equal(0, mara.PendingTrust);
        Assert.Empty(mara.PendingTruths);
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
        Assert.Contains("(repair odds -5)", v.StakesText(jonah));
        Assert.Contains("Told the truth, Jonah refuses the repair.", v.StakesText(jonah));

        Assert.Equal(Voyage.RepairChance(jonah, fault, jonah.Morale), v.RepairChanceWith(jonah, fault));
        Assert.Equal(0.95, v.RepairChanceWith(jonah, fault), 6);
        Assert.Equal(Tuning.RepairChanceMax, Voyage.RepairChance(v.Find("mara")!, fault, 70));
    }

    [Fact]
    public void A_hard_truth_is_credited_by_name_after_the_drift_report()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        Assert.Single(mara.PendingTruths);
        VoyageTests.SleepWhenAllowed(v, mara);
        var trust = mara.Trust;
        var from = v.Log.Count;
        v.Drift();
        Assert.Equal(Math.Min(100, trust + Tuning.HardTruthTrust), mara.Trust);
        Assert.Empty(mara.PendingTruths);
        var report = v.Log.FindIndex(from, l => l.Text.StartsWith("DRIFT:"));
        var credit = v.Log.FindIndex(from, l => l.Kind == LogKind.Ship && l.Text.StartsWith("Mara thinks better of you"));
        Assert.True(report >= 0 && credit > report);
        Assert.Contains("radiation dose", v.Log[credit].Text);
        Assert.Contains("trust +8", v.Log[credit].Text);
    }

    [Fact]
    public void A_lie_earns_no_credit_at_the_drift()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        v.Assign(mara, fault, patch: false);
        Tell(v, mara, "dose", "safe");
        v.Assign(mara, fault, patch: false);
        VoyageTests.FinishAlert(v);
        Assert.DoesNotContain(v.Log, l => l.Text.Contains("thinks better of you"));
        Assert.Equal(0, mara.PendingTrust);
    }
}
