using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>A lie about a cause is caught when the work finishes only by a worker skilled enough to see it (DESIGN.md, "Causes"; decided by the user, 2026-11-13).</summary>
public class CauseSeenTests
{
    private static void Tell(Voyage v, CrewMember to, string key, string value) =>
        v.Say(v.GetFact(key).Say(value), new Reading(LineKind.Statement, key, value), new[] { to });

    /// <summary>Finishes the coolant repair told a false cause with the worker's engineering skill set to <paramref name="skill"/>; null when the seed failed the repair.</summary>
    private static (Voyage V, CrewMember Mara)? Finish(int seed, int skill)
    {
        var v = new Voyage(seed);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Tell(v, mara, "alive:idris", "alive");
        Tell(v, mara, "dose", "dosed");
        mara.Trust = 80;
        mara.Skills[Skill.Engineering] = skill;
        var f = v.FindFault("")!;
        var fact = v.GetFact("cause:coolant");
        Tell(v, mara, "cause:coolant", fact.Values.First(x => x != fact.Truth));
        v.AssignDeal(mara, f, patch: false);
        for (var i = 0; i < 6 && !f.Resolved; i++)
            v.Go();
        return f.State == FaultState.Repaired ? (v, mara) : null;
    }

    private static (Voyage V, CrewMember Mara) FirstFinished(int skill)
    {
        for (var seed = 1; seed < 80; seed++)
            if (Finish(seed, skill) is { } done)
                return done;
        throw new Xunit.Sdk.XunitException("no seed repaired the leak");
    }

    [Fact]
    public void A_skilled_worker_sees_the_true_cause_and_catches_the_lie()
    {
        var (v, mara) = FirstFinished(Tuning.CauseSeenSkill);
        Assert.Equal(v.GetFact("cause:coolant").Truth, mara.Beliefs["cause:coolant"].Value);
        Assert.Equal("evidence", mara.Beliefs["cause:coolant"].Source);
        Assert.Equal(1, mara.CaughtLies);
    }

    [Fact]
    public void A_worker_below_the_skill_goes_on_believing_what_they_were_told()
    {
        var (v, mara) = FirstFinished(Tuning.CauseSeenSkill - 1);
        Assert.NotEqual(v.GetFact("cause:coolant").Truth, mara.Beliefs["cause:coolant"].Value);
        Assert.Equal("ship", mara.Beliefs["cause:coolant"].Source);
        Assert.Equal(0, mara.CaughtLies);
    }
}
