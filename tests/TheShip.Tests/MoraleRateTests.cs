using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Morale sets how fast people work (Tuning.MoraleRate*): relief and a hard truth change the hours.</summary>
public class MoraleRateTests
{
    private static CrewMember Woken(Voyage v, string name)
    {
        var c = v.Find(name)!;
        v.Wake(c);
        v.Go();
        return c;
    }

    private static CrewMember With(double morale) => new Voyage(1).Find("mara")! is var m && Set(m, morale) ? m : m;

    private static bool Set(CrewMember c, double morale)
    {
        c.Morale = morale;
        return true;
    }

    [Fact]
    public void The_rate_is_one_at_neutral_and_clamped_at_both_ends()
    {
        Assert.Equal(1.0, Voyage.MoraleRate(new[] { With(70) }));
        Assert.Equal(1 + (52 - 70) / Tuning.MoraleRateSpan, Voyage.MoraleRate(new[] { With(52) }), 6);
        Assert.InRange(Voyage.MoraleRate(new[] { With(52) }), 0.87, 0.89);
        Assert.InRange(Voyage.MoraleRate(new[] { With(76) }), 1.03, 1.05);
        Assert.Equal(Tuning.MoraleRateMin, Voyage.MoraleRate(new[] { With(0) }));
        Assert.Equal(Tuning.MoraleRateMax, Voyage.MoraleRate(new[] { With(100) }));
        Assert.Equal(1.0, Voyage.MoraleRate(Array.Empty<CrewMember>()));
        Assert.Equal(1.0, Voyage.MoraleRate(new[] { With(60), With(80) }));
    }

    [Fact]
    public void A_shaken_team_takes_longer_by_the_verdict()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        var fault = v.FindFault("")!;
        var steady = v.EarliestDoneHours(fault);
        mara.Morale = 40;
        Assert.True(v.EarliestDoneHours(fault) > steady);
        mara.Morale = 95;
        Assert.True(v.EarliestDoneHours(fault) < steady);
    }

    private static double ClockAfterAnswer(string value)
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        v.AssignDeal(mara, fault, patch: false);
        Assert.Equal("dose", mara.PendingQuestion);
        v.Say(v.GetFact("dose").Say(value), new Reading(LineKind.Statement, "dose", value), new[] { mara });
        v.AssignDeal(mara, fault, patch: false);
        Assert.Equal(FaultState.InRepair, fault.State);
        var start = v.Clock;
        v.Go();
        return v.Clock - start;
    }

    [Fact]
    public void A_fault_finishes_later_after_the_hard_truth_than_after_a_believed_reassuring_answer()
    {
        var hard = ClockAfterAnswer(HardValue());
        var soft = ClockAfterAnswer("safe");
        Assert.True(hard > soft, hard + " vs " + soft);
    }

    private static string HardValue()
    {
        var v = new Voyage(1);
        var f = v.GetFact("dose");
        return f.Values.First(x => f.HardnessOf(x) > 0);
    }

    [Fact]
    public void The_stakes_text_names_hours_and_no_verdict()
    {
        var v = new Voyage(1);
        var mara = Woken(v, "mara");
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        v.AssignDeal(mara, v.FindFault("")!, patch: false);
        var s = v.StakesOf(mara)!;
        Assert.True(s.HoursSlower > 0 && s.HoursFaster > 0);
        var text = v.StakesText(mara);
        Assert.Contains(" h faster)", text);
        Assert.Contains(" h slower; ", text);
        Assert.Contains("no trust lost if hurt)", text);
        // With the terms item under it (one row while the stakes say the precautions) it fits the panel: 158 px, 19 a row, 4 between.
        var item = v.TermsItem(mara)!.Value.Short;
        Assert.Equal(1, TermsTextTests.WaitingRows(item));
        Assert.DoesNotContain("dose", item);
        Assert.True(TermsTextTests.WaitingRows(text) * 19 + 4 + TermsTextTests.WaitingRows(item) * 19 <= 158,
            TermsTextTests.WaitingRows(text) + " rows: " + text);
        Assert.DoesNotContain("repair odds", text);
        Assert.DoesNotContain("true)", text);
        Assert.DoesNotContain("false", text);
        Assert.True(text.Length < 330, text.Length + ": " + text);
    }
}
