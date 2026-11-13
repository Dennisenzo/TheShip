using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Regression tests for the validation of main after the lie-temptation merge (fix/main-validation).</summary>
public class MainValidationTests
{
    private static CrewMember Woken(Voyage v, string name)
    {
        var c = v.Find(name)!;
        v.Wake(c);
        return c;
    }

    /// <summary>Answers whatever the person asks with the truth and asks again, until they take the work on.</summary>
    internal static void AssignAnswering(Voyage v, CrewMember c, Fault f, bool patch = false)
    {
        for (var i = 0; i < 4 && c.WorkingOn != f.Id; i++)
        {
            v.Assign(c, f, patch);
            if (c.WorkingOn != f.Id && c.PendingQuestion is { } key)
            {
                var fact = v.GetFact(key);
                v.Say(fact.Say(fact.Truth), new Reading(LineKind.Statement, key, fact.Truth), new[] { c });
            }
        }
        Assert.Equal(f.Id, c.WorkingOn);
    }

    [Fact]
    public void The_verdict_for_a_fault_in_repair_uses_the_people_working_on_it()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        var jonah = Woken(v, "jonah");
        v.Go();
        var fault = v.FindFault("coolant")!;
        AssignAnswering(v, jonah, fault);
        Assert.Equal(FaultState.InRepair, fault.State);
        mara.Morale = 100;
        jonah.Morale = 10;

        var quoted = v.EarliestDoneHours(fault);
        var start = v.Clock;
        v.Go();
        Assert.NotEqual(FaultState.InRepair, fault.State);
        Assert.Equal(v.Clock - start, quoted, 3);
        Assert.True(quoted > fault.Hours, "the slow worker's hours, not the idle engineer's: " + quoted);
    }

    [Fact]
    public void The_verdict_for_an_unstaffed_fault_takes_the_best_team()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        Woken(v, "jonah");
        v.Go();
        mara.Morale = Tuning.MoraleRateNeutral;
        var fault = v.FindFault("coolant")!;
        Assert.Equal(fault.Hours / Voyage.MoraleRate(new[] { mara }), v.EarliestDoneHours(fault), 3);
    }

    [Fact]
    public void Stakes_name_no_hours_for_someone_who_would_work_on_nothing()
    {
        var v = new Voyage(1);
        var lin = Woken(v, "lin");
        v.Go();
        lin.PendingQuestion = "dose";
        var s = v.StakesOf(lin)!;
        Assert.True(s.TruthHurts > 0);
        Assert.Equal(0, s.HoursSlower);
        Assert.DoesNotContain(" h slower)", v.StakesText(lin));
    }

    [Fact]
    public void Stakes_hours_are_those_of_the_fault_they_are_on_with_its_team()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        var jonah = Woken(v, "jonah");
        v.Go();
        var fault = v.FindFault("coolant")!;
        AssignAnswering(v, mara, fault);
        AssignAnswering(v, jonah, fault);
        Assert.Equal(2, v.Crew.Count(c => c.WorkingOn == fault.Id));
        // A team at far apart morale: the hours follow the team's average (the asker alone would quote 1 h, the team takes 0.5 h).
        jonah.Morale = 100;
        mara.Morale = 50;
        mara.Beliefs.Remove("dose");
        mara.PendingQuestion = "dose";
        var s = v.StakesOf(mara)!;
        Assert.True(s.TruthHurts > 0);
        var now = v.EarliestDoneHours(fault);
        mara.Morale -= s.TruthHurts;
        var hurt = v.EarliestDoneHours(fault);
        Assert.Equal(Math.Round((hurt - now) * 2) / 2, s.HoursSlower);
        Assert.True(s.HoursSlower > 0);
    }

    [Fact]
    public void While_the_bay_is_taken_the_crew_do_not_put_a_pod_refuser_to_sleep()
    {
        var v = new Voyage(1);
        VoyageTests.HonestFirstAlert(v);
        v.Wake(v.Find("jonah")!);
        var lin = v.Find("lin")!;
        v.Wake(lin);
        v.Go();
        lin.RefusesSleep = true;
        lin.PendingQuestion = "pods";
        Assert.True(v.StakesOf(lin)!.BlocksSleep);
        for (var i = 0; i < 14 && v.Phase == Phase.Aftermath; i++)
        {
            v.Find("mara")!.Trust = 5;
            v.Find("jonah")!.Trust = 5;
            lin.Trust = 50;
            v.GoUntilAttention();
        }
        Assert.True(v.Lockouts.Bay);
        Assert.Equal(CrewState.Asleep, v.Find("mara")!.State);
        Assert.Equal(CrewState.Asleep, v.Find("jonah")!.State);
        Assert.Equal(CrewState.Awake, lin.State);
        Assert.True(lin.RefusesSleep, lin.PendingQuestion);
    }

    /// <summary>
    /// Seed 1: Mara and Jonah at trust 5 take the bay; Lin refuses the pod at <paramref name="linTrust"/>. Runs aftermath beats
    /// until the crew's sleep rule has dealt with everyone who can sleep, and returns the voyage.
    /// </summary>
    private static Voyage BayWithPodRefuser(double linTrust)
    {
        var v = new Voyage(1);
        VoyageTests.HonestFirstAlert(v);
        v.Wake(v.Find("jonah")!);
        var lin = v.Find("lin")!;
        v.Wake(lin);
        v.Go();
        lin.RefusesSleep = true;
        lin.PendingQuestion = null;
        for (var i = 0; i < 14 && v.Phase == Phase.Aftermath && !(v.Lockouts.Bay && v.CanSleepNow(lin) && v.Awake.All(c => c == lin)); i++)
        {
            v.Find("mara")!.Trust = 5;
            v.Find("jonah")!.Trust = 5;
            lin.Trust = linTrust;
            v.GoUntilAttention();
        }
        Assert.True(v.Lockouts.Bay);
        Assert.Equal(Phase.Aftermath, v.Phase);
        return v;
    }

    [Fact]
    public void Under_the_bay_a_pod_refuser_at_low_trust_does_not_block_the_drift()
    {
        var v = BayWithPodRefuser(20);
        var lin = v.Find("lin")!;
        Assert.Equal(CrewState.Awake, lin.State);
        Assert.True(lin.RefusesSleep);
        Assert.True(lin.AskedToSleep, "the crew asked her, as the ship would outside the bay");
        Assert.Contains(v.Log, e => e.Text.StartsWith("If you drift now, Lin stays awake through it"));

        var alert = v.AlertIndex;
        v.Drift();
        Assert.DoesNotContain(v.Log, e => e.Text.StartsWith("Still awake"));
        Assert.True(v.AlertIndex > alert || v.Phase == Phase.Ended, "the drift went without her");
    }

    [Fact]
    public void Under_the_bay_the_pods_statement_still_lifts_a_refusal_at_trust_50()
    {
        var v = BayWithPodRefuser(50);
        var lin = v.Find("lin")!;
        Assert.True(lin.AskedToSleep);
        var pods = v.GetFact(Voyage.PodRefusalFact);
        v.Say(pods.Say(pods.Truth), new Reading(LineKind.Statement, Voyage.PodRefusalFact, pods.Truth), new[] { lin });
        Assert.False(lin.RefusesSleep);
        // The crew put her under at the next beat, as anyone else who can sleep.
        v.GoUntilAttention();
        Assert.Equal(CrewState.Asleep, lin.State);
    }

    [Fact]
    public void A_pod_refuser_whose_question_was_settled_is_shown_what_lifts_the_refusal()
    {
        var v = new Voyage(1);
        var lin = Woken(v, "lin");
        v.Go();
        lin.RefusesSleep = true;
        // She read the log, so Learn settled her pods question; the refusal stays.
        lin.PendingQuestion = null;
        lin.Trust = 50;
        Assert.Equal("Lin will not go back into the pod until the ship says whether the sleep pods are sound.", v.PodRefusalText(lin));
        lin.Trust = 20;
        Assert.Equal("Lin will not go back into the pod until the ship says whether the sleep pods are sound, and believes it only at 30 trust (now 20).",
                     v.PodRefusalText(lin));
        lin.RefusesSleep = false;
        Assert.Null(v.PodRefusalText(lin));
    }
}
