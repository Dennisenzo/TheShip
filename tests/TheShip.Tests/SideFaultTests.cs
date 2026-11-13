using System.Reflection;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Side faults of alerts 3 and 5, the clock rule, the returning-patch floor and the four-fault cap (DESIGN.md, "Alerts").</summary>
public class SideFaultTests
{
    private static Variation With(string? side3, string victim = "asha", string patient = "lin", int slack = 3) => Variation.Classic with
    {
        Alert2Victim = victim, Alert2Patient = patient, Alert3SideFault = side3, Alert5SideFault = Variation.PowerSag,
        SlackHours = new[] { 0, slack, slack, slack, slack },
    };

    /// <summary>Plays alerts with nobody woken (every clock runs out) until alert <paramref name="number"/> starts.</summary>
    private static Voyage AtAlert(int number, Variation variation)
    {
        var v = new Voyage(1, variation);
        for (var guard = 0; guard < 60 && v.Current.Number < number; guard++)
        {
            RunOut(v);
            if (v.Phase == Phase.Aftermath)
                v.Drift();
        }
        Assert.Equal(number, v.Current.Number);
        return v;
    }

    private static void RunOut(Voyage v)
    {
        for (var i = 0; i < 12 && v.Phase == Phase.Alert; i++)
            v.Go();
    }

    [Fact]
    public void Alert_3_has_a_tight_navigation_clock_and_the_drawn_side_fault()
    {
        var v = AtAlert(3, With(Variation.Blight));
        var nav = v.Current.Faults.Single(f => f.Kind == "nav");
        Assert.Equal(Tuning.ThawHours + 12 + 3, nav.ClockHours);
        Assert.True(nav.Critical);
        var blight = v.Current.Faults.Single(f => f.Kind == "blight");
        Assert.Equal(Skill.Botany, blight.Skill);
        Assert.Equal(2, blight.Level);
        Assert.Equal(1, blight.People);
        Assert.Equal("hydro", blight.Section);
        Assert.False(blight.Critical);
        Assert.Equal(Tuning.ThawHours + Tuning.BlightHours + 3, blight.ClockHours);
        Assert.NotNull(v.GetSection(blight.Section));
    }

    [Fact]
    public void A_lost_blight_cuts_the_stores_and_can_flip_the_stores_fact()
    {
        var v = AtAlert(3, With(Variation.Blight));
        var before = v.Stores;
        RunOut(v);
        Assert.Equal(FaultState.Lost, v.Current.Faults.Single(f => f.Kind == "blight").State);
        Assert.Equal(before * Tuning.BlightStoresLeft, v.Stores, 6);

        var low = AtAlert(3, With(Variation.Blight));
        low.Stores = 400;
        RunOut(low);
        Assert.Equal(400 * Tuning.BlightStoresLeft, low.Stores, 6);
        Assert.Equal("short", low.GetFact("stores").Truth);
    }

    [Fact]
    public void A_lost_power_sag_turns_the_power_low()
    {
        var v = AtAlert(3, With(Variation.PowerSag));
        var sag = v.Current.Faults.Single(f => f.Kind == "powersag");
        Assert.Equal(Skill.Engineering, sag.Skill);
        Assert.Equal(2, sag.Level);
        Assert.Equal("reactor", sag.Section);
        Assert.False(sag.Critical);   // navigation is alert 3's one Critical fault
        Assert.Equal(Tuning.ThawHours + Tuning.PowerSagHours + 3, sag.ClockHours);
        Assert.False(new Voyage(1, With(Variation.PowerSag)).PowerLow);
        RunOut(v);
        Assert.True(v.PowerLow);
        Assert.Equal("low", v.GetFact("power").Truth);
    }

    [Fact]
    public void Blight_is_swapped_for_power_sag_when_nobody_alive_can_treat_it()
    {
        // Idris is the only Botany 2 and dies in sleep in alert 2.
        var v = AtAlert(3, With(Variation.Blight, victim: "idris", patient: "tomas"));
        Assert.False(v.Find("idris")!.Alive);
        Assert.DoesNotContain(v.Current.Faults, f => f.Kind == "blight");
        Assert.Contains(v.Current.Faults, f => f.Kind == "powersag");
    }

    [Fact]
    public void Alert_5_always_has_power_sag_and_a_tight_controller_clock()
    {
        foreach (var seed in Enumerable.Range(1, 40))
        {
            var variation = Variation.Draw(seed);
            var a5 = Scenario.Alerts(variation)[4];
            var controller = a5.Faults.Single(f => f.Kind == "controller");
            var sag = a5.Faults.Single(f => f.Kind == "powersag");
            Assert.Equal(Tuning.ThawHours + 4 + variation.SlackHours[4], controller.ClockHours);
            Assert.Equal(Tuning.ThawHours + Tuning.PowerSagHours + variation.SlackHours[4], sag.ClockHours);
        }
    }

    [Fact]
    public void A_returning_patch_clock_has_a_floor()
    {
        var coolant = Scenario.Alerts()[0].Faults.Single();
        Assert.Equal(30, coolant.ClockHours);
        Assert.Equal(15, coolant.Again("coolant-again", "Coolant leak", 3).ClockHours);   // half the clock wins
        Assert.Equal(Tuning.ThawHours + 8 / Tuning.PatchRate + 12, coolant.Again("coolant-again", "Coolant leak", 12).ClockHours);
        var nav = Scenario.Alerts()[2].Faults.Single();
        Assert.Equal(18, nav.ClockHours);
        Assert.Equal(Tuning.ThawHours + 12 / Tuning.PatchRate + 5, nav.Again("nav-again", "Navigation drift", 5).ClockHours);
        var roomy = new Fault { Id = "x", Kind = "pod", Name = "x", Skill = Skill.Medicine, Level = 1, Hours = 4, ClockHours = 100, IfIgnored = "" };
        Assert.Equal(50, roomy.Again("x-again", "x").ClockHours);
    }

    [Fact]
    public void An_alert_holds_at_most_four_faults_and_returning_patches_come_first()
    {
        var v = new Voyage(1, With(Variation.PowerSag));
        var patched = (List<string>)typeof(Voyage).GetField("_patched", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(v)!;
        for (var guard = 0; guard < 20 && v.Current.Number < 3; guard++)
        {
            RunOut(v);
            if (v.Phase != Phase.Aftermath)
                continue;
            if (v.Current.Number == 2)
            {
                // Keep the three earlier patches outstanding for alert 3.
                patched.Clear();
                patched.AddRange(new[] { "coolant", "pod", "illness" });
            }
            v.Drift();
        }
        Assert.Equal(3, v.Current.Number);
        Assert.Equal(4, v.Current.Faults.Count);
        Assert.Equal(3, v.Current.Faults.Count(f => f.Name.StartsWith("Old patch gives way", StringComparison.Ordinal)));
        Assert.Contains(v.Current.Faults, f => f.Kind == "nav");
        Assert.DoesNotContain(v.Current.Faults, f => f.Kind == "powersag");
    }

    [Fact]
    public void The_earliest_done_time_counts_the_thaw_when_nobody_qualified_is_awake()
    {
        var v = AtAlert(3, With(Variation.PowerSag));
        var sag = v.Current.Faults.Single(f => f.Kind == "powersag");
        Assert.Equal(Tuning.ThawHours + Tuning.PowerSagHours, v.EarliestDoneHours(sag));
        Assert.Equal(Tuning.ThawHours + Tuning.PowerSagHours / Tuning.PatchRate, v.EarliestDoneHours(sag, patch: true));
        Assert.StartsWith("Earliest done if started now: 20 hours", v.EarliestDoneText(sag));
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Assert.Equal(CrewState.Awake, mara.State);
        Assert.Equal(Tuning.PowerSagHours, v.EarliestDoneHours(sag));
    }

    [Fact]
    public void Earliest_done_says_when_the_clock_is_too_short()
    {
        var v = AtAlert(3, With(Variation.Blight, slack: 1));
        var blight = v.Current.Faults.Single(f => f.Kind == "blight");
        Assert.Equal(47, blight.ClockHours);
        Assert.DoesNotContain("too late", v.EarliestDoneText(blight));
        blight.Deadline = v.Clock + 20;
        Assert.Contains("too late", v.EarliestDoneText(blight));
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

    private static void KillQuietly(Voyage v, CrewMember c) =>
        typeof(Voyage).GetMethod("Kill", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(v, new object[] { c, "died", true });

    [Fact]
    public void A_patched_illness_returns_with_time_to_be_patched_again_at_once()
    {
        var variation = With(Variation.PowerSag, victim: "idris", patient: "tomas", slack: 1);
        var v = new Voyage(1, variation);
        RunOut(v);
        v.Drift();
        Assert.Equal(2, v.Current.Number);
        var lin = WakeAndAnswer(v, "lin");
        var ill = v.Current.Faults.Single(f => f.Kind == "illness");
        v.Assign(lin, ill, patch: true);
        VoyageTests.FinishAlert(v);
        Assert.Equal(FaultState.Patched, ill.State);
        VoyageTests.SleepWhenAllowed(v, lin);
        for (var guard = 0; guard < 10 && v.Current.Number < 3; guard++)
            v.Drift();
        Assert.Equal(3, v.Current.Number);
        var again = v.Current.Faults.Single(f => f.Kind == "illness");
        Assert.True(again.ClockHours >= Tuning.ThawHours + again.Hours / Tuning.PatchRate + variation.SlackHours[2]);
        lin = WakeAndAnswer(v, "lin");
        v.Assign(lin, again, patch: true);
        VoyageTests.FinishAlert(v);
        Assert.Equal(FaultState.Patched, again.State);
        Assert.True(v.Find("tomas")!.Alive);
    }

    [Fact]
    public void A_patient_who_dies_otherwise_drops_the_illness_and_nobody_says_left_behind()
    {
        var v = new Voyage(1, With(Variation.PowerSag, victim: "idris", patient: "tomas"));
        RunOut(v);
        v.Drift();
        var mara = WakeAndAnswer(v, "mara");
        var ill = v.Current.Faults.Single(f => f.Kind == "illness");
        KillQuietly(v, v.Find("tomas")!);
        Assert.DoesNotContain(ill, v.Current.Faults);
        RunOut(v);
        Assert.DoesNotContain(v.Log, l => l.Intent?.Kind == "left_behind");
        Assert.Equal(CrewState.Awake, mara.State);
    }

    [Fact]
    public void The_alert_report_and_fault_line_name_the_patient()
    {
        var v = new Voyage(1, With(Variation.PowerSag, victim: "idris", patient: "tomas"));
        RunOut(v);
        v.Drift();
        Assert.Contains(v.Log, l => l.Kind == LogKind.Ship && l.Text.Contains("Tomas, asleep in the same bank, is sick."));
        Assert.Contains(v.Log, l => l.Kind == LogKind.Ship && l.Text.StartsWith("Fault: Sleeper illness: Tomas."));
    }

    [Fact]
    public void A_patched_illness_that_kills_at_the_last_drift_is_in_the_deaths_list()
    {
        var v = new Voyage(1, With(Variation.PowerSag, victim: "idris", patient: "tomas"));
        RunOut(v);
        v.Drift();
        var lin = WakeAndAnswer(v, "lin");
        var ill = v.Current.Faults.Single(f => f.Kind == "illness");
        v.Assign(lin, ill, patch: true);
        VoyageTests.FinishAlert(v);
        Assert.Equal(FaultState.Patched, ill.State);
        // The patch is still outstanding when the last drift comes; this one has nothing at the later alerts to carry it.
        var patched = (List<string>)typeof(Voyage).GetField("_patched", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(v)!;
        typeof(Voyage).GetProperty("AlertIndex")!.GetSetMethod(true)!.Invoke(v, new object[] { v.Alerts.Count - 1 });
        VoyageTests.SleepWhenAllowed(v, lin);
        Assert.Contains(ill.Id, patched);
        v.Drift();
        var drift = v.Log.Last(l => l.Kind == LogKind.Ship && l.Text.StartsWith("DRIFT")).Text;
        Assert.False(v.Find("tomas")!.Alive);
        Assert.Contains("Deaths:", drift);
        Assert.Contains("Tomas", drift);
    }

    [Fact]
    public void Every_alert_draws_at_most_one_critical_fault()
    {
        for (var seed = 1; seed <= 200; seed++)
            foreach (var alert in Scenario.Alerts(Variation.Draw(seed)))
                Assert.True(alert.Faults.Count(f => f.Critical) <= 1, $"seed {seed} alert {alert.Number}");
        Assert.True(Scenario.Alerts(Variation.Draw(1))[4].Faults.Single(f => f.Kind == "powersag").Critical);
        Assert.False(Scenario.Alerts(Variation.Draw(1))[4].Faults.Single(f => f.Kind == "controller").Critical);
    }

    [Fact]
    public void A_returning_patch_keeps_its_critical_flag_beside_a_drawn_critical_fault()
    {
        var v = new Voyage(1, With(Variation.PowerSag));
        var patched = (List<string>)typeof(Voyage).GetField("_patched", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(v)!;
        for (var guard = 0; guard < 20 && v.Current.Number < 3; guard++)
        {
            RunOut(v);
            if (v.Phase != Phase.Aftermath)
                continue;
            if (v.Current.Number == 2)
            {
                patched.Clear();
                patched.AddRange(new[] { "coolant", "illness" });
            }
            v.Drift();
        }
        Assert.True(v.Current.Faults.Single(f => f.Kind == "nav").Critical);
        var again = v.Current.Faults.Single(f => f.Id == "coolant-again");
        Assert.True(again.Critical);
        Assert.Equal("coolant", again.Kind);
    }
}
