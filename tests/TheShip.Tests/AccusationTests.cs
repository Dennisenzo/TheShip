using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>The lie comes due: a found-out lie says how it was found, and a late one opens an accusation (DESIGN.md, "How a lie is caught").</summary>
public class AccusationTests
{
    private static string WrongDate(Voyage v) => (int.Parse(v.GetFact("date").Truth) + 7).ToString();

    /// <summary>Mara awake in the aftermath of alert 1, told a wrong year by the ship; returns the lie.</summary>
    private static (Voyage v, CrewMember mara, Statement lie) Lied()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Trust = 70;
        var wrong = WrongDate(v);
        v.Say("It is year " + wrong + ".", new Reading(LineKind.Statement, "date", wrong), new[] { mara });
        var lie = v.Statements.Last();
        Assert.Equal(StatementKind.False, lie.Kind);
        Assert.False(lie.FoundOutBy.ContainsKey("mara"));
        return (v, mara, lie);
    }

    private static LogEntry CaughtLine(Voyage v) => v.Log.Last(e => e.CrewId == "mara" && e.Intent?.Kind == "caught");

    [Fact]
    public void A_lie_found_in_the_log_says_so()
    {
        var (v, mara, lie) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        Assert.Equal("read the log entry", lie.FoundOutHow["mara"]);
        Assert.Contains("you read the log entry", CaughtLine(v).Intent!.Decision);
    }

    [Fact]
    public void A_lie_found_at_a_console_says_so()
    {
        var (v, mara, lie) = Lied();
        mara.Doubts.Add("date");
        v.Go();
        Assert.Equal("read a console", lie.FoundOutHow["mara"]);
        Assert.Contains("you read a console", CaughtLine(v).Intent!.Decision);
    }

    [Fact]
    public void A_lie_against_what_they_just_saw_is_caught_at_once_and_says_so()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Wake(v.Find("asha")!);
        v.Go();
        VoyageTests.Say(v, mara, "Asha is dead.");
        var lie = v.Statements.Last();
        Assert.Equal("already knew better", lie.FoundOutHow["mara"]);
    }

    [Fact]
    public void A_lie_found_when_the_work_finishes_says_so()
    {
        // The cause of the coolant leak, told falsely before the repair, is found out by whoever finishes it.
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        var cause = fault.CauseFact!;
        var wrong = v.GetFact(cause).Values.First(x => !v.GetFact(cause).Same(x, v.GetFact(cause).Truth));
        v.Say("It was " + wrong + ".", new Reading(LineKind.Statement, cause, wrong), new[] { mara });
        var lie = v.Statements.Last();
        v.AssignDeal(mara, fault, patch: false);
        VoyageTests.Say(v, mara, "It is dangerous. You will take a radiation dose.");
        v.AssignDeal(mara, fault, patch: false);
        VoyageTests.FinishAlert(v);
        Assert.True(lie.FoundOutHow.ContainsKey("mara"), string.Join(" / ", v.Log.Select(e => e.Text)) + " kind=" + lie.Kind + " fault=" + fault.State);
        Assert.Equal("saw it when the work finished", lie.FoundOutHow["mara"]);
    }

    [Fact]
    public void The_ending_record_names_how_it_was_found()
    {
        var (v, mara, lie) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        VoyageTests.SleepWhenAllowed(v, mara);
        for (var i = 0; i < 300 && v.Phase != Phase.Ended; i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        Assert.Contains(v.Ending!.Record, r => r.Contains("Found out by Mara in year") && r.Contains("(read the log entry)"));
    }

    /// <summary>
    /// The planner's hypothesis (a lie told the truth about is still charged in full later) held until step 3; now the truth told
    /// first is a self-correction: it costs <see cref="Tuning.SelfCorrectTrust"/> once and the later finding-out charges nothing.
    /// </summary>
    [Fact]
    public void A_lie_corrected_before_it_is_found_is_not_charged_at_the_later_finding_out()
    {
        var (v, mara, lie) = Lied();
        var truth = v.GetFact("date").Truth;
        v.Say("It is year " + truth + ".", new Reading(LineKind.Statement, "date", truth), new[] { mara });
        Assert.Equal(StatementKind.True, v.Statements.Last().Kind);
        Assert.False(lie.FoundOutBy.ContainsKey("mara"));
        Assert.True(lie.Corrected.ContainsKey("mara"));
        var trust = mara.Trust;
        v.ReadLog(mara, v.GetFact("date"));
        Assert.False(lie.FoundOutBy.ContainsKey("mara"));
        Assert.Equal(trust, mara.Trust, 3);
        Assert.Equal(0, mara.CaughtLies);
    }
}
