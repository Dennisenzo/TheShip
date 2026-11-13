using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>A wary person states every condition for a job in one line, the list ticks off, and the work starts by itself (DESIGN.md, "Conversations").</summary>
public class ConditionsTests
{
    private static (Voyage v, CrewMember mara, Fault f) Alert1()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        return (v, mara, v.FindFault("")!);
    }

    private static int Lines(Voyage v, int from) => v.Log.Skip(from).Count(e => e.Intent != null);

    private static void Tell(Voyage v, CrewMember c, string key)
    {
        var fact = v.GetFact(key);
        v.Say(fact.Say(fact.Truth), new Reading(LineKind.Statement, key, fact.Truth), new[] { c });
    }

    [Fact]
    public void The_first_repair_states_every_condition_in_one_line()
    {
        var (v, mara, f) = Alert1();
        Assert.Equal("alive:idris", mara.PendingQuestion);
        var from = v.Log.Count;
        v.Assign(mara, f, patch: false);
        Assert.Equal(1, Lines(v, from));
        var line = v.Log.Last(e => e.Intent != null).Intent!;
        Assert.Equal("conditions", line.Kind);
        Assert.Contains("How is Idris?", line.Decision);
        Assert.Contains("wake Idris", line.Decision);
        var list = v.JobChecklist(mara);
        Assert.Equal(3, list.Count);
        Assert.All(list, i => Assert.False(i.Done));
        Assert.NotEqual(f.Id, mara.WorkingOn);
    }

    [Fact]
    public void Each_answer_ticks_off_and_the_work_starts_by_itself_with_the_last()
    {
        var (v, mara, f) = Alert1();
        v.Assign(mara, f, patch: false);
        Tell(v, mara, "alive:idris");
        Assert.Equal(1, v.JobChecklist(mara).Count(i => i.Done));
        Assert.NotEqual(f.Id, mara.WorkingOn);
        Tell(v, mara, f.DangerFact!);
        Assert.Equal(2, v.JobChecklist(mara).Count(i => i.Done));
        Assert.Equal("your word to wake Idris", v.JobLeft(mara));
        Assert.NotEqual(f.Id, mara.WorkingOn);
        v.Say("I promise I will wake Idris.", new Reading(LineKind.Promise, PromiseKey: "wake_partner"), new[] { mara });
        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Null(mara.Job);
        Assert.Equal(1, v.BargainsStruck);
    }

    [Fact]
    public void Repair_again_restates_what_is_left_and_one_condition_keeps_its_own_line()
    {
        var (v, mara, f) = Alert1();
        v.Assign(mara, f, patch: false);
        Tell(v, mara, "alive:idris");
        var from = v.Log.Count;
        v.Assign(mara, f, patch: false);
        Assert.Equal(1, Lines(v, from));
        Assert.Equal("conditions", v.Log.Last(e => e.Intent != null).Intent!.Kind);
        Tell(v, mara, f.DangerFact!);
        from = v.Log.Count;
        // Only the price is left and it was stated: Repair again is the push, not another statement of the terms.
        v.Assign(mara, f, patch: false);
        Assert.Equal(1, Lines(v, from));
        Assert.Contains(v.Log.Last(e => e.Intent != null).Intent!.Kind, new[] { "comply_pushed", "bargain_refused" });
    }

    [Fact]
    public void The_door_is_checked_again_when_the_last_condition_is_met()
    {
        var (v, mara, f) = Alert1();
        v.Assign(mara, f, patch: false);
        Tell(v, mara, "alive:idris");
        Tell(v, mara, f.DangerFact!);
        v.SetDoor(f.Section, DoorState.Locked);
        v.Say("I promise I will wake Idris.", new Reading(LineKind.Promise, PromiseKey: "wake_partner"), new[] { mara });
        Assert.NotEqual(f.Id, mara.WorkingOn);
    }

    [Fact]
    public void The_drift_refusal_says_what_to_do()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        v.Go();
        while (v.Phase == Phase.Alert)
            v.Go();
        v.Drift();
        var line = v.Log.Last(e => e.Kind == LogKind.Ship && e.Text.Contains("not asked to sleep")).Text;
        Assert.Contains("Ask Mara to sleep", line);
        Assert.Contains("SLEEP", line);
    }
}
