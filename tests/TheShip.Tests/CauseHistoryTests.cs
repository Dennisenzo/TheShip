using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>A hard cause found out for oneself is history and costs no morale; told before the work it costs (the price of a briefing), and a told cause speeds the work.</summary>
public class CauseHistoryTests
{
    private static void Tell(Voyage v, CrewMember to, string key, string value) =>
        v.Say(v.GetFact(key).Say(value), new Reading(LineKind.Statement, key, value), new[] { to });

    private static string Hard(Voyage v, string key) => v.GetFact(key).Values.First(x => v.GetFact(key).HardnessOf(x) > 0);

    [Fact]
    public void A_hard_cause_learned_from_evidence_costs_no_morale()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        var before = mara.Morale;
        v.Learn(mara, "cause:coolant", Hard(v, "cause:coolant"), "evidence");
        Assert.Equal(before, mara.Morale);
        Assert.Equal(Hard(v, "cause:coolant"), mara.Beliefs["cause:coolant"].Value);
    }

    [Fact]
    public void A_hard_cause_told_before_the_work_costs_morale()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var before = mara.Morale;
        Tell(v, mara, "cause:coolant", Hard(v, "cause:coolant"));
        Assert.True(mara.Morale < before);
    }

    [Fact]
    public void A_told_cause_speeds_the_work_without_touching_the_precaution_rate()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var f = v.Current.Faults[0];
        var plain = v.TermsFor(mara, f);
        Tell(v, mara, "cause:coolant", v.GetFact("cause:coolant").Values[0]);
        var told = v.TermsFor(mara, f);
        Assert.Equal(plain.RateFactor, told.RateFactor);
        Assert.Equal(Tuning.CauseRate, told.CauseRateFactor);
        Assert.True(told.Speed > plain.Speed);
    }
}
