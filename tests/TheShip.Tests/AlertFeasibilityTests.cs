using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Every alert can be saved by a fully parallel plan; one worker cannot always save all of it (DESIGN.md, "Alerts": the clock rule).</summary>
public class AlertFeasibilityTests
{
    private static IEnumerable<Variation> Sample() =>
        Enumerable.Range(1, 60).Select(Variation.Draw).Append(Variation.Classic);

    [Fact]
    public void The_fully_parallel_plan_saves_every_fault_of_every_alert()
    {
        foreach (var variation in Sample())
            foreach (var alert in Scenario.Alerts(variation).Skip(1))
            {
                // Everyone needed is thawed together, then each fault works at full rate: the clock covers thaw + work.
                Assert.True(alert.Faults.Sum(f => f.People) <= Tuning.MaxAwake, $"alert {alert.Number} needs too many people at once ({variation})");
                foreach (var f in alert.Faults)
                    Assert.True(Tuning.ThawHours + f.Hours <= f.ClockHours + 0.001, $"alert {alert.Number} {f.Id} cannot be done in {f.ClockHours} h ({variation})");
            }
    }

    /// <summary>One worker, most urgent clock first, who thaws first: whether every fault is done in time. A two-person fault is out of reach.</summary>
    private static bool OneWorkerSavesAll(Alert alert)
    {
        var t = Tuning.ThawHours;
        foreach (var f in alert.Faults.OrderBy(f => f.ClockHours))
        {
            if (f.People > 1)
                return false;
            t += f.Hours;
            if (t > f.ClockHours + 0.001)
                return false;
        }
        return true;
    }

    [Fact]
    public void One_worker_cannot_save_all_of_a_multi_fault_alert()
    {
        var failed = new Dictionary<int, int>();
        var total = 0;
        foreach (var variation in Sample())
        {
            total++;
            foreach (var alert in Scenario.Alerts(variation).Where(a => a.Faults.Count > 1))
                if (!OneWorkerSavesAll(alert))
                    failed[alert.Number] = failed.GetValueOrDefault(alert.Number) + 1;
        }
        // Alert 3 always holds a two-person navigation fix; alerts 2 and 5 are too much for one worker unless the slack is large.
        Assert.Equal(total - 1, failed.GetValueOrDefault(3));   // the Classic variation has no side fault, so alert 3 is a single fault there
        Assert.True(failed.GetValueOrDefault(5) > 0, "alert 5 should sometimes be too much for one worker");
        Assert.True(failed.GetValueOrDefault(2) > 0, "alert 2 should sometimes be too much for one worker");
    }
}
