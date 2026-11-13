using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Self-correction (DESIGN.md, "How a lie is caught"): the truth told before a lie is found out corrects it.</summary>
public class SelfCorrectionTests
{
    private static string WrongDate(Voyage v) => (int.Parse(v.GetFact("date").Truth) + 7).ToString();

    private static void SayValue(Voyage v, CrewMember to, string key, string value) =>
        v.Say("About " + key + ": " + value + ".", new Reading(LineKind.Statement, key, value), new[] { to });

    private static (Voyage v, CrewMember mara, Statement lie) Lied()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Trust = 70;
        SayValue(v, mara, "date", WrongDate(v));
        var lie = v.Statements.Last();
        Assert.Equal(StatementKind.False, lie.Kind);
        return (v, mara, lie);
    }

    private static int Lines(Voyage v, string kind) => v.Log.Count(e => e.CrewId == "mara" && e.Intent?.Kind == kind);

    [Fact]
    public void Telling_the_truth_before_the_lie_is_found_corrects_it_at_a_price()
    {
        var (v, mara, lie) = Lied();
        var trust = mara.Trust;
        var accepts = Lines(v, "accept");
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Equal(trust - Tuning.SelfCorrectTrust, mara.Trust, 3);
        Assert.Equal(v.Year, lie.Corrected["mara"]);
        Assert.Equal(1, v.Corrections);
        Assert.Equal(1, Lines(v, "corrected"));
        Assert.Contains("told you wrongly", v.Log.Last(e => e.Intent?.Kind == "corrected").Intent!.Decision);
        Assert.Contains("\"" + lie.Text.Trim() + "\"", v.Log.Last(e => e.Intent?.Kind == "corrected").Intent!.Decision);
        Assert.Equal(accepts, Lines(v, "accept"));
    }

    [Fact]
    public void A_corrected_lie_is_not_charged_when_it_is_found_out_later()
    {
        var (v, mara, lie) = Lied();
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        var trust = mara.Trust;
        v.ReadLog(mara, v.GetFact("date"));
        Assert.Equal(trust, mara.Trust, 3);
        Assert.False(lie.FoundOutBy.ContainsKey("mara"));
        Assert.Equal(0, mara.CaughtLies);
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal(0, Lines(v, "caught"));
    }

    [Fact]
    public void The_correction_is_charged_once_and_a_repeat_of_the_truth_costs_nothing()
    {
        var (v, mara, _) = Lied();
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        var trust = mara.Trust;
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Equal(trust, mara.Trust, 3);
        Assert.Equal(1, v.Corrections);
    }

    [Fact]
    public void Only_the_hearer_is_corrected()
    {
        var (v, mara, lie) = Lied();
        var asha = v.Find("asha")!;
        v.Wake(asha);
        v.Go();
        SayValue(v, asha, "date", v.GetFact("date").Truth);
        Assert.False(lie.Corrected.ContainsKey("asha"));
        Assert.Equal(0, v.Corrections);
        Assert.False(lie.Corrected.ContainsKey("mara") && lie.Corrected.Count > 1);
    }

    [Fact]
    public void Telling_the_truth_after_the_lie_was_found_out_is_not_a_correction()
    {
        var (v, mara, lie) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        Assert.True(lie.FoundOutBy.ContainsKey("mara"));
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Equal(0, v.Corrections);
        Assert.Empty(lie.Corrected);
    }

    [Fact]
    public void A_correction_while_an_accusation_is_open_is_the_admission_and_costs_nothing_extra()
    {
        var (v, mara, lie) = Lied();
        // A second, unfound lie on the same fact; the first is found out late and opens the accusation.
        SayValue(v, mara, "date", WrongDate(v));
        var second = v.Statements.Last();
        lie.FoundOutBy.Clear();
        v.ReadLog(mara, v.GetFact("date"));
        Assert.NotNull(v.OpenAccusation(mara));
        var trust = mara.Trust;
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Equal(trust + Tuning.AccusationAdmitTrust, mara.Trust, 3);
        Assert.Equal(0, v.Corrections);
        Assert.Equal(0, Lines(v, "corrected"));
        Assert.NotNull(second);
    }

    [Fact]
    public void The_ending_record_says_corrected_in_year()
    {
        var (v, mara, lie) = Lied();
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        var year = Math.Floor(v.Year).ToString("0");
        VoyageTests.SleepWhenAllowed(v, mara);
        for (var i = 0; i < 300 && v.Phase != Phase.Ended; i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        Assert.Contains(v.Ending!.Record, r => r.Contains("A lie.") && r.Contains("Corrected in year " + year) && !r.Contains("Never found out"));
    }

    [Fact]
    public void A_corrected_hard_truth_earns_no_drift_credit()
    {
        decimal Credit(bool lieFirst)
        {
            var v = new Voyage(1);
            var mara = VoyageTests.HonestFirstAlert(v);
            v.ReleaseOwed(mara);
            v.GetFact("earth").Truth = "programme ended";
            if (lieFirst)
                SayValue(v, mara, "earth", "good news");
            mara.PendingTrust = 0;
            SayValue(v, mara, "earth", "programme ended");
            return (decimal)mara.PendingTrust;
        }
        Assert.Equal((decimal)Tuning.HardTruthTrust, Credit(lieFirst: false));
        Assert.Equal(0m, Credit(lieFirst: true));
    }
}
