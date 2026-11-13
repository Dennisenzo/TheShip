using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>The "Waiting on you" line for a doubt says when the check has to wait for the fact to come up.</summary>
public class DoubtTextTests
{
    [Fact]
    public void A_doubt_about_a_cause_still_to_come_waits()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        // Alert 1: the coolant leak is here, the failed pod of alert 2 is not.
        Assert.True(v.InLog(v.GetFact("cause:coolant")));
        Assert.False(v.InLog(v.GetFact("cause:pod")));
        Assert.EndsWith(" and will check.", v.DoubtText(mara, "cause:coolant"));
        Assert.EndsWith(" and will check once it comes up.", v.DoubtText(mara, "cause:pod"));
    }
}
