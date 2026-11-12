using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

public class VariationTests
{
    [Fact]
    public void A_seed_draws_the_same_variation_every_time()
    {
        for (var seed = 0; seed < 200; seed++)
            Assert.Equal(Variation.Draw(seed).ToString(), Variation.Draw(seed).ToString());
    }

    [Fact]
    public void Play_does_not_change_the_variation()
    {
        var v = new Voyage(5, Variation.Draw(5));
        var before = v.Variation.ToString();
        var mara = v.Find("mara")!;
        v.Wake(mara);
        for (var i = 0; i < 5 && v.Phase == Phase.Alert; i++)
            v.Go();
        Assert.Equal(before, v.Variation.ToString());
        Assert.Equal(before, Variation.Draw(5).ToString());
    }

    [Fact]
    public void Draws_respect_the_slots()
    {
        var victims = new HashSet<string>();
        var sides3 = new HashSet<string>();
        for (var seed = 0; seed < 500; seed++)
        {
            var d = Variation.Draw(seed);
            victims.Add(d.Alert2Victim);
            Assert.DoesNotContain(d.Alert2Victim, new[] { "mara", "lin" });
            Assert.NotEqual(d.Alert2Victim, d.Alert2Patient);
            Assert.Equal(ShipPlan.Pod(d.Alert2Victim).Bank, ShipPlan.Pod(d.Alert2Patient).Bank);
            Assert.Contains(d.Alert3SideFault, new[] { Variation.Blight, Variation.PowerSag });
            sides3.Add(d.Alert3SideFault!);
            // Alert 5 always has a power sag, also after a sag in alert 3.
            Assert.Equal(Variation.PowerSag, d.Alert5SideFault);
            Assert.Equal(0, d.SlackHours[0]);
            Assert.All(d.SlackHours.Skip(1), s => Assert.InRange(s, Tuning.AlertSlackMin, Tuning.AlertSlackMax));
            Assert.All(d.YearJitter, j => Assert.InRange(j, -Tuning.AlertYearJitter, Tuning.AlertYearJitter));
        }
        Assert.Equal(4, victims.Count);
        Assert.Equal(2, sides3.Count);
    }

    [Fact]
    public void Classic_is_the_fixed_scenario()
    {
        var years = Scenario.Alerts(Variation.Classic).Select(a => a.Year).ToArray();
        Assert.Equal(new[] { 4, 17, 29, 41, 52 }, years);
        Assert.Equal(years, Scenario.Alerts().Select(a => a.Year).ToArray());
        Assert.Equal("idris", new Voyage(1).Variation.Alert2Victim);
        Assert.Same(Variation.Classic, new Voyage(1).Variation);
    }

    [Fact]
    public void Year_jitter_keeps_the_order_and_arrival()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var years = Scenario.Alerts(Variation.Draw(seed)).Select(a => a.Year).ToArray();
            for (var i = 1; i < years.Length; i++)
                Assert.True(years[i] > years[i - 1], $"seed {seed}");
            Assert.True(years[^1] < Scenario.ArrivalYear);
        }
    }

    [Theory]
    [InlineData("tomas")]
    [InlineData("asha")]
    [InlineData("jonah")]
    [InlineData("idris")]
    public void A_drawn_victim_dies_in_alert_2_and_is_named(string victimId)
    {
        var variation = Variation.Classic with { Alert2Victim = victimId, Alert2Patient = "mara" };
        var v = new Voyage(1, variation);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.SleepWhenAllowed(v, mara);
        v.Drift();
        Assert.Equal(1, v.AlertIndex);
        var victim = v.Find(victimId)!;
        Assert.Equal(CrewState.Dead, victim.State);
        Assert.Contains(v.Log, e => e.Text.Contains(victim.Name + " is dead"));
        var pod = v.Current.Faults.Single(f => f.Kind == "pod");
        Assert.Equal("alive:" + victimId, pod.ExposureFact);
        Assert.Equal(victimId, pod.SubjectId);
        Assert.Equal(ShipPlan.Pod(victimId).Bank, pod.Section);
        Assert.Equal("dead", v.GetFact("alive:" + victimId).Truth);
        foreach (var other in new[] { "idris", "tomas", "asha", "jonah" }.Where(id => id != victimId))
            Assert.Equal(CrewState.Asleep, v.Find(other)!.State);
    }

    [Fact]
    public void The_same_seed_plays_the_same_with_a_drawn_variation()
    {
        string Play(int seed)
        {
            var v = new Voyage(seed, Variation.Draw(seed));
            v.Wake(v.Find("mara")!);
            for (var i = 0; i < 10; i++)
                v.Go();
            return string.Join("|", v.Log.Select(e => e.Text));
        }
        Assert.Equal(Play(9), Play(9));
    }
}
