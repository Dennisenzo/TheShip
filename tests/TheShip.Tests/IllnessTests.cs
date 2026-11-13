using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Alert 2's second fault: the sleeper illness of the victim's bank neighbour (DESIGN.md, "Alerts").</summary>
public class IllnessTests
{
    /// <summary>Alert 1 played with Mara, everyone asleep again, drifted into alert 2.</summary>
    private static Voyage AtAlert2(Variation? variation = null)
    {
        var v = variation is null ? new Voyage(1) : new Voyage(1, variation);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.SleepWhenAllowed(v, mara);
        v.Drift();
        Assert.Equal(2, v.Current.Number);
        return v;
    }

    private static CrewMember WakeAndAnswer(Voyage v, string id)
    {
        var c = v.Find(id)!;
        v.Wake(c);
        v.Go();
        while (c.PendingQuestion is { } key)
        {
            var f = v.GetFact(key);
            v.Say(f.Say(f.Truth), new Reading(LineKind.Statement, f.Key, f.Truth), new[] { c });
        }
        return c;
    }

    private static void RunOut(Voyage v)
    {
        for (var i = 0; i < 10 && v.Phase == Phase.Alert; i++)
            v.Go();
        Assert.Equal(Phase.Aftermath, v.Phase);
    }

    [Fact]
    public void Alert_2_has_the_pod_and_the_illness_with_tight_clocks()
    {
        var v = AtAlert2();
        var pod = v.Current.Faults.Single(f => f.Kind == "pod");
        var ill = v.Current.Faults.Single(f => f.Kind == "illness");
        Assert.Equal(Tuning.ThawHours + 4, pod.ClockHours);
        Assert.Equal(Tuning.ThawHours + 12, ill.ClockHours);
        Assert.True(ill.Critical);
        Assert.Equal("tomas", ill.SubjectId);
        Assert.Equal("podA", ill.Section);
        Assert.Equal(Skill.Medicine, ill.Skill);
        Assert.Equal(2, ill.Level);
    }

    [Fact]
    public void An_untreated_patient_dies_at_the_deadline()
    {
        var v = AtAlert2();
        RunOut(v);
        Assert.Equal(FaultState.Lost, v.Current.Faults.Single(f => f.Kind == "illness").State);
        Assert.False(v.Find("tomas")!.Alive);
        Assert.Contains(v.Log, l => l.Kind == LogKind.Ship && l.Text == "Tomas died of the illness in their pod.");
    }

    [Fact]
    public void A_treated_patient_lives_and_the_medic_sees_both_pods()
    {
        var v = AtAlert2();
        var lin = WakeAndAnswer(v, "lin");
        var ill = v.Current.Faults.Single(f => f.Kind == "illness");
        v.Assign(lin, ill, patch: false);
        VoyageTests.FinishAlert(v);
        Assert.Equal(FaultState.Repaired, ill.State);
        Assert.True(v.Find("tomas")!.Alive);
        Assert.Equal("alive", lin.Beliefs["alive:tomas"].Value);
        Assert.Equal("dead", lin.Beliefs["alive:idris"].Value);
    }

    [Fact]
    public void A_locked_bank_door_blocks_both_repairs()
    {
        var v = AtAlert2();
        var lin = WakeAndAnswer(v, "lin");
        v.SetDoor("podA", DoorState.Locked);
        foreach (var f in v.Current.Faults)
        {
            v.Assign(lin, f, patch: false);
            Assert.NotEqual(f.Id, lin.WorkingOn);
        }
    }

    [Fact]
    public void Another_victim_and_patient_pair_works()
    {
        var variation = Variation.Classic with { Alert2Victim = "asha", Alert2Patient = "jonah" };
        var v = AtAlert2(variation);
        var ill = v.Current.Faults.Single(f => f.Kind == "illness");
        Assert.Equal("jonah", ill.SubjectId);
        Assert.Equal("podB", ill.Section);
        Assert.False(v.Find("asha")!.Alive);
        RunOut(v);
        Assert.False(v.Find("jonah")!.Alive);
    }

    [Fact]
    public void A_fault_left_to_run_out_makes_the_awake_say_you_let_them_die()
    {
        var v = AtAlert2();
        var lin = WakeAndAnswer(v, "lin");
        var trust = lin.Trust;
        var morale = lin.Morale;
        RunOut(v);
        Assert.False(v.Find("tomas")!.Alive);
        Assert.Equal(trust - Tuning.LeftBehindTrust, lin.Trust);
        Assert.True(lin.Morale < morale);
        var line = Assert.Single(v.Log, l => l.Intent?.Kind == "left_behind");
        Assert.Equal("lin", line.CrewId);
        Assert.Contains("Tomas", line.Text);
        Assert.DoesNotContain(v.Log, l => l.CrewId == "lin" && l.Intent?.Kind == "found");
        Assert.Equal("dead", lin.Beliefs["alive:tomas"].Value);
    }
}
