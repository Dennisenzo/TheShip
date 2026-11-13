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
}
