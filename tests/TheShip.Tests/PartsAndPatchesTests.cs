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
    private static void SetParts(Voyage v, int parts) => typeof(Voyage).GetProperty(nameof(Voyage.Parts))!.SetValue(v, parts);

    private static (Voyage v, CrewMember mara, Fault fault) RepairUnderWay(int seed)
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
        Assert.Equal(FaultState.InRepair, fault.State);
        return (v, mara, fault);
    }

    [Fact]
    public void A_failed_repair_on_the_last_part_stops_and_leaves_only_a_patch()
    {
        for (var seed = 1; seed <= 1000; seed++)
        {
            var (v, mara, fault) = RepairUnderWay(seed);
            SetParts(v, 1);
            for (var i = 0; i < 50 && fault.State == FaultState.InRepair; i++)
                v.Go();
            if (fault.State == FaultState.Repaired)
                continue;
            // The repair failed on the one part: no redo for free.
            Assert.Equal(0, v.Parts);
            Assert.Equal(FaultState.Open, fault.State);
            Assert.Null(mara.WorkingOn);
            Assert.Contains(v.Log, e => e.Intent?.Kind == "work_no_parts");
            v.Assign(mara, fault, patch: false);
            Assert.Equal("No spare parts are left. This can only be patched.", v.Log.Last().Text);
            v.Assign(mara, fault, patch: true);
            for (var i = 0; i < 50 && fault.State == FaultState.InRepair; i++)
                v.Go();
            Assert.Equal(FaultState.Patched, fault.State);
            return;
        }
        Assert.Fail("no seed in 1..1000 had a failed repair");
    }

    [Fact]
    public void A_failed_repair_after_another_took_the_last_part_does_not_claim_to_have_used_it()
    {
        for (var seed = 1; seed <= 1000; seed++)
        {
            var (v, _, fault) = RepairUnderWay(seed);
            SetParts(v, 0);
            for (var i = 0; i < 50 && fault.State == FaultState.InRepair; i++)
                v.Go();
            var stop = v.Log.Last(e => e.Intent?.Kind == "work_no_parts");
            if (!stop.Intent!.Decision.Contains("failed"))
                continue;
            Assert.DoesNotContain("used up", stop.Intent.Decision);
            Assert.Contains("no spare parts are left", stop.Intent.Decision);
            return;
        }
        Assert.Fail("no seed in 1..1000 had a failed repair");
    }

    [Fact]
    public void A_repair_whose_part_was_used_elsewhere_does_not_finish()
    {
        var (v, mara, fault) = RepairUnderWay(1);
        SetParts(v, 0);
        for (var i = 0; i < 50 && fault.State == FaultState.InRepair; i++)
            v.Go();
        Assert.NotEqual(FaultState.Repaired, fault.State);
        Assert.Equal(0, v.Parts);
        Assert.Null(mara.WorkingOn);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "work_no_parts");
    }

    [Fact]
    public void The_refusal_warning_counts_the_arrival_slip_of_a_navigation_patch()
    {
        // Stores that reach year 60 and 100 days more: enough for arrival on time, not for the slipped one.
        var v = new Voyage(1);
        var mara = VoyageTests.RefuserAtTheLastAlert(v);
        InjectPatched(v, "nav");
        v.Stores = (Scenario.ArrivalYear - v.Year) * 365 + 100;
        v.AskSleep(mara);
        var slipped = Scenario.ArrivalYear + Tuning.NavPatchArrivalSlipYears;
        var warning = v.Log.Last(e => e.Kind == LogKind.Ship && e.Text.StartsWith("If you drift now")).Text;
        Assert.Contains($"The drift to year {slipped:0} is", warning);
        Assert.Contains("Mara starves before the drift ends", warning);
        Assert.DoesNotContain("last that long", warning);
        v.Drift();
        Assert.Equal(CrewState.Dead, mara.State);
    }

    [Fact]
    public void Sleepers_killed_by_the_controller_patch_die_of_it_not_of_low_power()
    {
        var checkedDeaths = 0;
        for (var seed = 1; seed <= 40; seed++)
        {
            var v = ToTheLastDrift(seed);
            if (v.PowerLow)
                continue;
            InjectPatched(v, "controller");
            var asleep = v.Crew.Where(c => c.State == CrewState.Asleep).ToList();
            v.Drift();
            foreach (var c in asleep.Where(c => c.State == CrewState.Dead))
            {
                Assert.Contains(v.Log, e => e.Kind == LogKind.Ship && e.Text == c.Name + " died in sleep when the pod controller patch gave way.");
                checkedDeaths++;
            }
            Assert.DoesNotContain(v.Log, e => e.Text.Contains("died in sleep on low power"));
        }
        Assert.True(checkedDeaths > 0, "no sleeper died of the controller patch in 40 runs");
    }

    [Fact]
    public void A_patched_coolant_leak_at_the_last_drift_leaves_power_low()
    {
        var v = ToTheLastDrift(1);
        Assert.False(v.PowerLow);
        InjectPatched(v, "coolant");
        v.Drift();
        Assert.True(v.PowerLow);
        Assert.Contains("The patch on the coolant leak gave way.", v.Log.Last(e => e.Text.StartsWith("DRIFT:")).Text);
    }

    [Fact]
    public void A_patched_pod_at_the_last_drift_adds_three_wear()
    {
        var v = ToTheLastDrift(1);
        var wear = v.PodWear;
        InjectPatched(v, "pod");
        v.Drift();
        Assert.Equal(wear + 3, v.PodWear);
        Assert.Contains("The patch on the failed pod gave way.", v.Log.Last(e => e.Text.StartsWith("DRIFT:")).Text);
    }

    [Fact]
    public void Crew_name_a_returned_fault_plainly()
    {
        var f = new Fault { Id = "x", Kind = "coolant", Name = "Coolant leak", Skill = Skill.Engineering, Level = 1, Hours = 4, ClockHours = 40, IfIgnored = "none" };
        Assert.Equal("coolant leak", f.CrewName);
        Assert.Equal("coolant leak again", f.Again("x-again", "coolant leak").CrewName);

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
        VoyageTests.SleepWhenAllowed(v, mara);
        v.Drift();
        var again = v.Current.Faults.Single(x => x.Id == "coolant-again");
        v.Wake(mara);
        v.Go();
        v.Assign(mara, again, patch: true);
        var crew = v.Log.Where(e => e.Kind == LogKind.Crew && e.Intent != null).ToList();
        Assert.Contains(crew, e => e.Intent!.Decision.Contains("coolant leak again"));
        Assert.DoesNotContain(crew, e => e.Intent!.Decision.Contains("gives way", StringComparison.OrdinalIgnoreCase)
                                          || e.Text.Contains("gives way", StringComparison.OrdinalIgnoreCase));
    }
}
