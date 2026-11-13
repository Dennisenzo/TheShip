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
}
