using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Waking the partner pays: a reunion lifts both, and a pair awake together works faster and grows stores (DESIGN.md, "Bargains").</summary>
public class ReunionTests
{
    private static (Voyage v, CrewMember mara, CrewMember idris) Aftermath()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Morale = 60;
        return (v, mara, v.Find("idris")!);
    }

    private static void ThawThrough(Voyage v, CrewMember c)
    {
        for (var i = 0; i < 40 && c.State == CrewState.Thawing; i++)
            v.Go();
    }

    [Fact]
    public void Two_partners_awake_together_both_get_the_reunion_lift_and_a_line()
    {
        var (v, mara, idris) = Aftermath();
        var wear = v.PodWear;
        v.Wake(idris);
        Assert.Equal(0, v.Reunions);
        ThawThrough(v, idris);

        Assert.Equal(CrewState.Awake, idris.State);
        Assert.Equal(1, v.Reunions);
        Assert.Equal(2, v.Log.Count(e => e.Intent?.Kind == "reunion"));
        Assert.Contains(v.Log, e => e.Intent?.Kind == "reunion" && e.CrewId == mara.Id);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "reunion" && e.CrewId == idris.Id);
        // The thaw added one wear; the pair's care takes Tuning.ReunionPodCare off.
        Assert.Equal(Math.Max(0, wear + 1 - Tuning.ReunionPodCare), v.PodWear);
    }

    [Fact]
    public void Nobody_is_lifted_when_the_partner_stays_asleep()
    {
        var (v, mara, idris) = Aftermath();
        v.Go();
        Assert.Equal(CrewState.Asleep, idris.State);
        Assert.Equal(0, v.Reunions);
        Assert.DoesNotContain(v.Log, e => e.Intent?.Kind == "reunion");
        Assert.Equal(1.0, v.PairFactor(mara));
    }

    [Fact]
    public void A_pair_awake_together_works_faster_than_a_lone_partner()
    {
        var (v, mara, idris) = Aftermath();
        Assert.Equal(1.0, v.PairFactor(mara));
        v.Wake(idris);
        ThawThrough(v, idris);
        Assert.Equal(Tuning.PairRate, v.PairFactor(mara));
        Assert.Equal(Tuning.PairRate, v.PairFactor(idris));
    }

    [Fact]
    public void A_pair_awake_together_grows_stores_each_day()
    {
        var (v, mara, idris) = Aftermath();
        v.Wake(idris);
        ThawThrough(v, idris);
        var alone = v.Awake.Count();
        var stores = v.Stores;
        v.Go();
        // Two eat (2 person-days a day) and the pair grows PairGardenPerDay a day, so the stores fall slower than 2 a day.
        var days = 14.0;
        Assert.True(stores - v.Stores < alone * days - 0.5 * Tuning.PairGardenPerDay * days, $"stores {stores} -> {v.Stores}");
    }

    private static (Voyage v, CrewMember mara, CrewMember idris) PairUp()
    {
        var (v, mara, idris) = Aftermath();
        v.Wake(idris);
        ThawThrough(v, idris);
        Assert.Equal(CrewState.Awake, idris.State);
        Assert.Equal(2, v.Awake.Count());
        return (v, mara, idris);
    }

    private static int RunOutLines(Voyage v) => v.Log.Count(e => e.Kind == LogKind.Ship && e.Text.StartsWith("The stores ran out", StringComparison.Ordinal));

    [Fact]
    public void A_pair_on_running_out_stores_hears_the_run_out_once_and_the_advance_does_not_stop_for_it_again()
    {
        var (v, _, _) = PairUp();
        v.Stores = 3;
        for (var i = 0; i < 5; i++)
            v.Go();
        // The garden accrues through each step, interleaved with eating: the stores stay empty instead of refilling by a lump each step.
        Assert.Equal(1, RunOutLines(v));
        Assert.Equal(0, v.Stores);
        var r = v.GoUntilAttention();
        Assert.NotEqual("the stores ran out", r.Reason);
        Assert.Equal(1, RunOutLines(v));
    }

    [Fact]
    public void A_pair_on_empty_stores_eats_part_rations_from_the_garden_and_starves_slower()
    {
        var (v, mara, idris) = PairUp();
        v.Stores = 0;
        // Five two-week steps: 70 days, past the 60 that kill on nothing, but the garden covers 1.2 of the 2 rations.
        for (var i = 0; i < 5; i++)
            v.Go();
        Assert.True(mara.Alive && idris.Alive);
        var share = (2 - Tuning.PairGardenPerDay) / 2;
        Assert.Equal(70 * 24 * share, mara.HoursHungry, 3);
        // At that share, the 60 hungry days come after 60 / share days.
        for (var i = 0; i < 10 && mara.Alive; i++)
            v.Go();
        Assert.False(mara.Alive);
        Assert.Contains(v.Log, e => e.Text.StartsWith("Mara starved", StringComparison.Ordinal));
    }

    [Fact]
    public void Waking_a_partner_counts_the_garden_in_the_stores_warning()
    {
        var (v, mara, idris) = Aftermath();
        // Two eating 2 a day would run out in 50 days (short of 90); with the pair's garden they need 0.8 a day: 125 days.
        v.Stores = 100;
        v.Wake(idris);
        Assert.DoesNotContain(v.Log, e => e.Text.Contains("short of a three-month waking", StringComparison.Ordinal));
        Assert.False(v.CanSleepNow(mara) && mara.HoursAwake < Tuning.MinAwakeHours);
    }

    [Fact]
    public void Waking_someone_who_is_not_a_partner_on_the_same_stores_still_warns()
    {
        var (v, mara, _) = Aftermath();
        v.Stores = 100;
        v.Wake(v.Find("lin")!);
        Assert.Contains(v.Log, e => e.Text.Contains("short of a three-month waking", StringComparison.Ordinal));
    }

    [Fact]
    public void Thawing_the_partner_again_in_the_same_aftermath_pays_the_thaw_wear_without_a_second_pod_care()
    {
        var (v, _, idris) = PairUp();
        // Empty stores waive the three-month minimum, so Idris can go back at once.
        v.Stores = 0;
        v.AskSleep(idris);
        Assert.Equal(CrewState.Asleep, idris.State);
        v.Stores = 1000;
        var wear = v.PodWear;
        v.Wake(idris);
        ThawThrough(v, idris);
        Assert.Equal(CrewState.Awake, idris.State);
        Assert.Equal(2, v.Reunions);
        // Before: each re-thaw cleared ReunionPodCare (2) against the thaw's 1, so wear fell by one a cycle.
        Assert.Equal(wear + 1, v.PodWear);
    }

    [Fact]
    public void The_card_estimate_counts_a_thawing_partner_who_is_up_before_the_work_ends()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        v.AssignDeal(mara, fault, patch: false);
        VoyageTests.Say(v, mara, "It is dangerous. You will take a radiation dose.");
        v.AssignDeal(mara, fault, patch: false);
        Assert.Equal(fault.Id, mara.WorkingOn);

        var alone = v.EarliestDoneHours(fault);
        var rate = (fault.Hours - fault.Progress) / alone;
        Assert.True(alone > Tuning.ThawHours, $"alone {alone}");
        var morale = mara.Morale;
        v.Wake(v.Find("idris")!);
        // Waking him lifts her (her want); keep her morale so only the pair speed moves the estimate.
        mara.Morale = morale;
        // Six hours at Mara's rate, the rest at the pair's.
        var expected = Tuning.ThawHours + (fault.Hours - fault.Progress - Tuning.ThawHours * rate) / (rate * Tuning.PairRate);
        Assert.Equal(expected, v.EarliestDoneHours(fault), 6);
    }

    [Fact]
    public void The_card_estimate_counts_partners_thawing_together_as_a_pair()
    {
        var one = new Voyage(1);
        one.Wake(one.Find("mara")!);
        var alone = one.EarliestDoneHours(one.FindFault("")!);

        var both = new Voyage(1);
        both.Wake(both.Find("mara")!);
        both.Wake(both.Find("idris")!);
        var pair = both.EarliestDoneHours(both.FindFault("")!);
        Assert.Equal((alone - Tuning.ThawHours) / Tuning.PairRate, pair - Tuning.ThawHours, 6);
    }

    [Fact]
    public void The_ship_says_what_a_reunion_does_and_the_terms_name_the_pair_speed()
    {
        var (v, mara, idris) = Aftermath();
        v.Wake(idris);
        var coolant = v.Current.Faults.FirstOrDefault() ?? new Fault { Id = "x", Kind = "x", Name = "x", Skill = Skill.Engineering, Level = 1, Hours = 1, ClockHours = 10, IfIgnored = "x", Section = "engine", CauseFact = "cause:coolant" };
        Assert.Contains("Idris thawing: 10% faster once he is up.", v.TermsText(mara, coolant));
        ThawThrough(v, idris);
        var line = Assert.Single(v.Log, e => e.Kind == LogKind.Ship && e.Text.Contains("together again", StringComparison.Ordinal));
        Assert.Contains("morale +" + Tuning.ReunionMorale, line.Text);
        Assert.Contains("10% faster", line.Text);
        Assert.Contains("person-days of stores a day", line.Text);
        Assert.Contains("pod wear", line.Text);
        Assert.Contains("Mara has Idris awake too: 10% faster.", v.TermsText(mara, coolant));
    }
}
