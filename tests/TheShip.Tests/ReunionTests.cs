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
}
