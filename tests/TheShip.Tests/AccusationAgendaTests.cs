using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>The accusation agenda: admit, deny, dodge, ignore (DESIGN.md, "How a lie is caught").</summary>
public class AccusationAgendaTests
{
    private static string WrongDate(Voyage v) => (int.Parse(v.GetFact("date").Truth) + 7).ToString();

    private static (Voyage v, CrewMember mara, Statement lie) Lied()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Trust = 70;
        var wrong = WrongDate(v);
        v.Say("It is year " + wrong + ".", new Reading(LineKind.Statement, "date", wrong), new[] { mara });
        return (v, mara, v.Statements.Last());
    }

    private static (Voyage v, CrewMember mara, Statement lie) Accused()
    {
        var (v, mara, lie) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        Assert.NotNull(v.OpenAccusation(mara));
        return (v, mara, lie);
    }

    private static int Lines(Voyage v, string kind) => v.Log.Count(e => e.CrewId == "mara" && e.Intent?.Kind == kind);

    private static void SayValue(Voyage v, CrewMember to, string key, string value) =>
        v.Say("About " + key + ": " + value + ".", new Reading(LineKind.Statement, key, value), new[] { to });

    private static string Wrong(Voyage v, string key)
    {
        var f = v.GetFact(key);
        return f.Values.First(x => !f.Same(x, f.Truth));
    }

    [Fact]
    public void A_late_finding_out_opens_an_accusation_with_the_usual_cost()
    {
        var (v, mara, lie) = Lied();
        var trust = mara.Trust;
        v.ReadLog(mara, v.GetFact("date"));
        var a = v.OpenAccusation(mara)!;
        Assert.Equal("date", a.FactKey);
        Assert.Equal(lie.Id, a.StatementId);
        Assert.Equal(Tuning.AccusationBeats, a.BeatsLeft);
        Assert.True(trust - mara.Trust >= Tuning.LieTrust - 0.001);
        var text = v.AccusationText(mara)!;
        Assert.Contains("Mara caught you", text);
        Assert.Contains("read the log entry", text);
        Assert.Contains((Tuning.AccusationBeats * 2) + " weeks", text);
    }

    [Fact]
    public void A_lie_caught_at_once_opens_no_accusation()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Wake(v.Find("asha")!);
        v.Go();
        VoyageTests.Say(v, mara, "Asha is dead.");
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal(0, v.AccusationsOpened);
    }

    [Fact]
    public void Saying_the_truth_admits_it_and_gives_back_part_of_the_cost()
    {
        var (v, mara, lie) = Accused();
        var trust = mara.Trust;
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal(trust + Tuning.AccusationAdmitTrust, mara.Trust, 3);
        Assert.Equal("admitted", lie.Accused["mara"]);
        Assert.Equal(1, Lines(v, "accusation_admitted"));
        Assert.Equal(1, v.AccusationsAdmitted);
        Assert.Contains("date", mara.AdmittedFacts);
    }

    [Fact]
    public void The_refund_is_paid_once_per_person_and_fact()
    {
        var (v, mara, _) = Accused();
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        // Lied to again on the same fact and found out late again: an accusation opens, but admitting it pays nothing more.
        v.Statements.Add(new Statement
        {
            Id = 900, Year = v.Year, Text = "again", Kind = StatementKind.False, FactKey = "date", Value = WrongDate(v),
            TruthThen = v.GetFact("date").Truth, Hearers = new List<string> { "mara" },
        });
        mara.Beliefs["date"] = new Belief(WrongDate(v), "ship", v.Year);
        v.ReadLog(mara, v.GetFact("date"));
        Assert.NotNull(v.OpenAccusation(mara));
        var trust = mara.Trust;
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Equal(trust, mara.Trust, 3);
        Assert.Null(v.OpenAccusation(mara));
    }

    [Fact]
    public void Denying_it_is_caught_at_once_whatever_the_year()
    {
        var (v, mara, lie) = Accused();
        mara.Beliefs["date"] = new Belief(v.GetFact("date").Truth, "evidence", v.Year - 5);
        var trust = mara.Trust;
        SayValue(v, mara, "date", WrongDate(v));
        Assert.True(v.Statements.Last().FoundOutBy.ContainsKey("mara"));
        Assert.True(trust - mara.Trust >= Tuning.LieTrust - 0.001);
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal("denied", lie.Accused["mara"]);
        Assert.Equal(1, v.AccusationsDenied);
    }

    [Fact]
    public void Without_an_accusation_a_sight_from_years_ago_does_not_catch_a_new_lie_at_once()
    {
        var (v, mara, _) = Accused();
        mara.Accusations.Clear();
        mara.Beliefs["date"] = new Belief(v.GetFact("date").Truth, "evidence", v.Year - 5);
        SayValue(v, mara, "date", WrongDate(v));
        Assert.False(v.Statements.Last().FoundOutBy.ContainsKey("mara"));
    }

    [Fact]
    public void A_dodge_costs_once_and_the_accusation_stays_open()
    {
        var (v, mara, _) = Accused();
        var trust = mara.Trust;
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        Assert.Equal(trust - Tuning.Dodge, mara.Trust, 3);
        Assert.NotNull(v.OpenAccusation(mara));
    }

    [Fact]
    public void Ignoring_it_closes_it_after_the_beats_and_they_doubt_everything_the_ship_said()
    {
        var (v, mara, lie) = Accused();
        var ships = mara.Beliefs.Where(kv => kv.Value.Source == "ship").Select(kv => kv.Key).ToList();
        v.Go();
        Assert.Equal(Tuning.AccusationBeats - 1, v.OpenAccusation(mara)!.BeatsLeft);
        v.Go();
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal(1, Lines(v, "accusation_closed"));
        Assert.Equal("ignored", lie.Accused["mara"]);
        Assert.Equal(1, v.AccusationsIgnored);
        Assert.All(ships, k => Assert.True(mara.Doubts.Contains(k) || mara.Beliefs[k].Source != "ship"));
    }

    [Fact]
    public void The_audit_confirms_the_true_and_catches_the_false()
    {
        var (v, mara, _) = Lied();
        SayValue(v, mara, "stores", Wrong(v, "stores"));
        v.ReadLog(mara, v.GetFact("date"));
        Assert.Equal(1, mara.CaughtLies);
        var confirmedBefore = Lines(v, "confirmed");
        for (var i = 0; i < 12 && mara.State == CrewState.Awake; i++)
            v.Go();
        Assert.Equal(2, mara.CaughtLies);
        Assert.True(Lines(v, "confirmed") > confirmedBefore);
    }

    [Fact]
    public void A_second_late_finding_out_waits_its_turn_oldest_first()
    {
        var (v, mara, first) = Lied();
        SayValue(v, mara, "stores", Wrong(v, "stores"));
        v.ReadLog(mara, v.GetFact("date"));
        v.ReadLog(mara, v.GetFact("stores"));
        Assert.Equal(2, mara.Accusations.Count);
        Assert.Equal("date", v.OpenAccusation(mara)!.FactKey);
        Assert.Equal("stores", mara.Accusations[1].FactKey);
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Equal("stores", v.OpenAccusation(mara)!.FactKey);
        Assert.Equal(Tuning.AccusationBeats, v.OpenAccusation(mara)!.BeatsLeft);
        Assert.Equal("admitted", first.Accused["mara"]);
    }

    [Fact]
    public void Going_to_sleep_with_it_open_closes_it_unanswered()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        for (var i = 0; i < 20 && mara.HoursAwake < Tuning.MinAwakeHours; i++)
            v.Go();
        var wrong = WrongDate(v);
        v.Say("It is year " + wrong + ".", new Reading(LineKind.Statement, "date", wrong), new[] { mara });
        var lie = v.Statements.Last();
        v.ReadLog(mara, v.GetFact("date"));
        mara.Trust = 70;
        mara.PendingQuestion = null;
        v.AskSleep(mara);
        Assert.Equal(CrewState.Asleep, mara.State);
        Assert.Empty(mara.Accusations);
        Assert.Equal("ignored", lie.Accused["mara"]);
        Assert.Equal(1, Lines(v, "accusation_closed"));
    }

    [Fact]
    public void A_death_closes_it_with_no_line()
    {
        var (v, mara, _) = Accused();
        v.Kill(mara, "died of a test");
        Assert.Empty(mara.Accusations);
        Assert.Equal(0, Lines(v, "accusation_closed"));
    }

    [Fact]
    public void The_ending_record_says_how_it_was_answered()
    {
        var (v, mara, _) = Accused();
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        VoyageTests.SleepWhenAllowed(v, mara);
        for (var i = 0; i < 300 && v.Phase != Phase.Ended; i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        Assert.Contains(v.Ending!.Record, r => r.Contains("(read the log entry), admitted"));
    }
}
