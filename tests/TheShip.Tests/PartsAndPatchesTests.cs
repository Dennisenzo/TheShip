using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

public class PartsAndPatchesTests
{
    private static void InjectPatched(Voyage v, string faultId)
    {
        var list = (List<string>)typeof(Voyage).GetField("_patched", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(v)!;
        list.Add(faultId);
    }

    [Fact]
    public void The_voyage_starts_with_two_spare_parts()
    {
        Assert.Equal(2, new Voyage(1).Parts);
        Assert.Equal(2, Tuning.StartParts);
    }

    [Fact]
    public void A_failed_repair_spends_a_part_too()
    {
        // Find a seed whose first repair fails once, then compare the parts with a repair that went through at once.
        for (var seed = 1; seed <= 1000; seed++)
        {
            var v = new Voyage(seed);
            var mara = v.Find("mara")!;
            v.Wake(mara);
            v.Go();
            VoyageTests.Say(v, mara, "Idris is alive and asleep.");
            var fault = v.FindFault("")!;
            v.Assign(mara, fault, patch: false);
            VoyageTests.Say(v, mara, "It is dangerous. You will take a radiation dose.");
            v.Assign(mara, fault, patch: false);
            var partsBefore = v.Parts;
            for (var i = 0; i < 50 && v.Phase == Phase.Alert; i++)
                v.Go();
            var failedLines = v.Log.Count(e => e.Text.StartsWith("It didn't take") || e.Text.StartsWith("That failed."));
            if (failedLines == 0)
                continue;
            Assert.Equal(FaultState.Repaired, fault.State);
            Assert.Equal(Math.Max(0, partsBefore - failedLines - 1), v.Parts);
            return;
        }
        Assert.Fail("no seed in 1..1000 had a failed repair");
    }

    [Fact]
    public void A_patch_returns_in_the_next_alert_with_one_prefix()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        v.Assign(mara, fault, patch: true);
        VoyageTests.Say(v, mara, "It is dangerous. You will take a radiation dose.");
        v.Assign(mara, fault, patch: true);
        VoyageTests.FinishAlert(v);
        Assert.Equal(FaultState.Patched, fault.State);
        VoyageTests.SleepWhenAllowed(v, mara);
        v.Drift();
        Assert.Equal(2, v.Current.Number);
        var again = v.Current.Faults.Single(f => f.Id == fault.Id + "-again");
        Assert.StartsWith("Old patch gives way: ", again.Name);
        Assert.DoesNotContain("gives way: old patch", again.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(fault.ClockHours / 2, again.ClockHours);
    }

    [Fact]
    public void A_returned_fault_patched_again_does_not_double_the_prefix()
    {
        var f = new Fault { Id = "x", Kind = "coolant", Name = "Coolant leak", Skill = Skill.Engineering, Level = 1, Hours = 4, ClockHours = 40, IfIgnored = "none" };
        var once = f.Again("x-again", f.Name.ToLowerInvariant());
        var twice = once.Again("x-again-again", once.Name);
        Assert.Equal("Old patch gives way: coolant leak", once.Name);
        Assert.Equal(once.Name, twice.Name);
    }

    [Fact]
    public void An_outstanding_patch_returns_at_the_signal_alert_and_makes_it_an_alert()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.SleepWhenAllowed(v, mara);
        for (var i = 0; i < 200 && !(v.Current.Number == 3 && v.Phase == Phase.Alert); i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        for (var i = 0; i < 50 && v.Phase == Phase.Alert; i++)
            v.Go();
        Assert.Equal(Phase.Aftermath, v.Phase);
        InjectPatched(v, "nav");
        v.Drift();
        Assert.Equal(4, v.Current.Number);
        Assert.Equal(Phase.Alert, v.Phase);
        var again = Assert.Single(v.Current.Faults);
        Assert.Equal("nav-again", again.Id);
    }

    private static Voyage ToTheLastDrift(int seed)
    {
        var v = new Voyage(seed);
        var mara = VoyageTests.RefuserAtTheLastAlert(v);
        v.Stores = 100000;
        return v;
    }

    [Fact]
    public void A_patched_pod_controller_at_the_last_drift_adds_wear_and_kills_sleepers()
    {
        var sleeperDeaths = 0;
        for (var seed = 1; seed <= 40; seed++)
        {
            var v = ToTheLastDrift(seed);
            var wear = v.PodWear;
            var dead = v.Crew.Count(c => c.State == CrewState.Dead);
            InjectPatched(v, "controller");
            v.Drift();
            Assert.Equal(wear + 4, v.PodWear);
            Assert.Contains("The patch on the pod controller gave way.", v.Log.Last(e => e.Text.StartsWith("DRIFT:")).Text);
            sleeperDeaths += v.Crew.Count(c => c.State == CrewState.Dead) - dead;
        }
        Assert.True(sleeperDeaths > 0, "no sleeper died in 40 runs at 15% each");

        var clean = ToTheLastDrift(1);
        var before = clean.Crew.Count(c => c.State == CrewState.Dead);
        clean.Drift();
        Assert.Equal(before, clean.Crew.Count(c => c.State == CrewState.Dead));
    }

    [Fact]
    public void A_patched_navigation_fault_at_the_last_drift_slips_the_arrival()
    {
        // Navigation is patched at alert 3 (set directly), so its clock never runs out.
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.SleepWhenAllowed(v, mara);
        for (var i = 0; i < 200 && !(v.Current.Number == 3 && v.Phase == Phase.Alert); i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        v.Current.Faults.Single().State = FaultState.Patched;
        v.Go();
        while (v.Current.Number < 5 || v.Phase == Phase.Alert)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
            if (v.Current.Number == 5 && v.Phase == Phase.Aftermath)
                break;
        }
        v.Stores = 100000;
        InjectPatched(v, "nav");
        Assert.False(v.NavLost);
        var sleeper = v.Crew.First(c => c.State == CrewState.Asleep);
        var age = sleeper.Age;
        var year = v.Year;
        v.Drift();
        Assert.False(v.NavLost);
        Assert.NotEqual("Adrift", v.Ending!.Title);
        var slip = Tuning.NavPatchArrivalSlipYears;
        var report = v.Log.Last(e => e.Text.StartsWith("DRIFT:")).Text;
        Assert.Contains($"arrival slips {slip:0} years, to year {Scenario.ArrivalYear + slip:0}.", report);
        Assert.Contains($"{Scenario.ArrivalYear - year + slip:0} years pass", report);
        Assert.True(sleeper.Age >= age + (Scenario.ArrivalYear - year + slip) * Tuning.SleepAgeing);
    }

    [Fact]
    public void A_navigation_fault_never_touched_is_adrift()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.SleepWhenAllowed(v, mara);
        for (var i = 0; i < 300 && v.Phase != Phase.Ended; i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        Assert.Equal("Adrift", v.Ending!.Title);
    }
}
