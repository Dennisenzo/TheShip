using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>A doubt on something the person already saw for themselves is settled, not kept forever (validator finding, Low).</summary>
public class DoubtSettleTests
{
    private static string WrongDate(Voyage v) => (int.Parse(v.GetFact("date").Truth) + 7).ToString();

    private static void SayValue(Voyage v, CrewMember to, string key, string value) =>
        v.Say("About " + key + ": " + value + ".", new Reading(LineKind.Statement, key, value), new[] { to });

    private static (Voyage v, CrewMember mara) Caught()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Trust = 70;
        SayValue(v, mara, "date", WrongDate(v));
        v.ReadLog(mara, v.GetFact("date"));
        SayValue(v, mara, "date", v.GetFact("date").Truth); // the admission
        Assert.True(mara.Beliefs["date"].Firsthand);
        return (v, mara);
    }

    [Fact]
    public void Telling_the_truth_again_about_what_they_saw_adds_no_doubt()
    {
        var (v, mara) = Caught();
        mara.Trust = 5;
        mara.Doubts.Clear();
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.DoesNotContain("date", mara.Doubts);
        Assert.True(mara.Beliefs["date"].Firsthand);
    }

    [Fact]
    public void Reading_a_log_entry_they_already_hold_the_truth_on_settles_the_doubt()
    {
        var (v, mara) = Caught();
        mara.Doubts.Add("date");
        mara.AuditDoubts.Add("date");
        v.ReadLog(mara, v.GetFact("date"));
        Assert.DoesNotContain("date", mara.Doubts);
        Assert.DoesNotContain("date", mara.AuditDoubts);
    }

    [Fact]
    public void A_doubt_on_a_seen_fact_does_not_stick_through_the_beats()
    {
        var (v, mara) = Caught();
        mara.Doubts.Add("date");
        for (var i = 0; i < 4 && mara.State == CrewState.Awake; i++)
            v.Go();
        Assert.DoesNotContain("date", mara.Doubts);
    }

    /// <summary>An accused engineer reading a rewrite on the fact they caught the ship on still rolls to spot it; unspotted, the entry counts as read.</summary>
    [Fact]
    public void An_accused_engineer_can_still_spot_a_rewrite_and_otherwise_has_read_it()
    {
        var spotted = 0;
        var unspotted = 0;
        for (var seed = 1; seed <= 40; seed++)
        {
            var v = new Voyage(seed);
            var mara = VoyageTests.HonestFirstAlert(v);
            v.ReleaseOwed(mara);
            mara.Trust = 70;
            var date = v.GetFact("date");
            SayValue(v, mara, "date", WrongDate(v));
            v.ReadLog(mara, date);
            Assert.NotNull(v.OpenAccusation(mara));
            v.SetLog("date", LogAccess.Altered, WrongDate(v));
            Assert.True(v.Unread(mara, date));
            v.ReadLog(mara, date);
            Assert.True(date.Same(mara.Beliefs["date"].Value, date.Truth));
            if (v.LogsLocked)
                spotted++;
            else
            {
                unspotted++;
                Assert.False(v.Unread(mara, date));
            }
        }
        Assert.True(spotted > 0 && unspotted > 0, $"spotted {spotted}, unspotted {unspotted}");
    }
}
