using TheShip.Core;
using System.Linq;
using Xunit;

namespace TheShip.Tests;

public class PodDeathTests
{
    [Fact]
    public void About_a_third_of_failed_thaws_kill_outright()
    {
        int fatal = 0, hurt = 0;
        for (var seed = 1; seed <= 100; seed++)
            foreach (var l in BalanceBot.PlayVoyage(seed, BotKind.Honest).Log.Where(l => l.Kind == LogKind.Ship))
            {
                if (l.Text.StartsWith("The thaw went badly.") && l.Text.EndsWith("is not breathing.")) fatal++;
                else if (l.Text.StartsWith("The thaw went badly.") && l.Text.EndsWith("is hurt.")) hurt++;
            }
        var share = fatal / (double)(fatal + hurt);
        Assert.InRange(share, Tuning.ThawFatalShare - 0.1, Tuning.ThawFatalShare + 0.1);
    }

    [Fact]
    public void A_fatal_thaw_is_reported_with_its_cause()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var v = BalanceBot.PlayVoyage(seed, BotKind.Honest);
            var line = v.Log.FirstOrDefault(l => l.Kind == LogKind.Ship && l.Text.EndsWith("did not survive the thaw."));
            if (line is null) continue;
            var name = line.Text.Split(' ')[0];
            Assert.False(v.Crew.First(c => c.Name == name).Alive);
            return;
        }
        Assert.Fail("no thaw death in 100 runs");
    }

    [Fact]
    public void Sleep_death_chance_scales_with_wear_and_years_and_is_zero_without_wear()
    {
        Assert.Equal(0, Voyage.SleepDeathChance(0, 30));
        Assert.Equal(0, Voyage.SleepDeathChance(10, 0));
        Assert.True(Voyage.SleepDeathChance(10, 10) > Voyage.SleepDeathChance(5, 10));
        Assert.True(Voyage.SleepDeathChance(10, 10) > Voyage.SleepDeathChance(10, 5));
        Assert.Equal(Tuning.SleepDeathPerWearYear * 150, Voyage.SleepDeathChance(15, 10), 9);
    }

    [Fact]
    public void Worn_pods_kill_sleepers_in_a_drift_and_the_report_names_them()
    {
        var found = false;
        for (var seed = 1; seed <= 100 && !found; seed++)
        {
            var v = BalanceBot.PlayVoyage(seed, BotKind.Honest);
            var line = v.Log.FirstOrDefault(l => l.Kind == LogKind.Ship && l.Text.EndsWith("died in sleep, pod failure."));
            if (line is null) continue;
            found = true;
            var name = line.Text.Split(' ')[0];
            Assert.Contains(v.Log, l => l.Text.StartsWith("DRIFT:") && l.Text.Contains("Deaths:") && l.Text.Contains(name));
        }
        Assert.True(found, "no pod-failure sleep death in 100 runs");
    }

    [Fact]
    public void Without_wear_nobody_dies_of_pod_failure()
    {
        var v = new Voyage(1);
        Assert.Equal(0, v.PodWear);
        for (var i = 0; i < 400 && v.Phase != Phase.Ended && v.PodWear == 0; i++)
        {
            if (v.Phase == Phase.Alert) v.Go(); else v.Drift();
        }
        Assert.DoesNotContain(v.Log, l => l.Text.Contains("died in sleep, pod failure"));
    }
}
