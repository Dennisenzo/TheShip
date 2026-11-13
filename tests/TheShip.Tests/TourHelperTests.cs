using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

public class TourHelperTests
{
    [Fact]
    public void Reading_the_log_after_a_lie_opens_an_accusation_with_text()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        var f = v.GetFact("date");
        var wrong = (int.Parse(f.Truth) + 7).ToString();
        v.Say("It is year " + wrong + ".", new Reading(LineKind.Statement, "date", wrong), new[] { mara });
        v.TourReadLog(mara, f);
        Assert.NotNull(v.AccusationText(mara));
    }

    [Fact]
    public void Restoring_after_the_scripted_lie_puts_back_trust_morale_and_caught_lies()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        var (trust, morale, caught) = (mara.Trust, mara.Morale, mara.CaughtLies);
        var f = v.GetFact("date");
        var wrong = (int.Parse(f.Truth) + 7).ToString();
        v.Say("It is year " + wrong + ".", new Reading(LineKind.Statement, "date", wrong), new[] { mara });
        v.TourReadLog(mara, f);
        v.Say("It is year " + f.Truth + ".", new Reading(LineKind.Statement, "date", f.Truth), new[] { mara });
        Assert.True(mara.Trust < trust - 10);
        v.TourRestore(mara, trust, morale, caught);
        Assert.Equal(trust, mara.Trust, 3);
        Assert.Equal(morale, mara.Morale, 3);
        Assert.Equal(caught, mara.CaughtLies);
    }
}
