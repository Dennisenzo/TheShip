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
        v.Go();
        var fault = v.FindFault("coolant")!;
        AssignAnswering(v, mara, fault);
        mara.Morale = 60;
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
}
