using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Validator findings after "the lie comes due" (fix/accusation-validation).</summary>
public class AccusationValidationTests
{
    private static string WrongDate(Voyage v) => (int.Parse(v.GetFact("date").Truth) + 7).ToString();

    private static void SayValue(Voyage v, CrewMember to, string key, string value) =>
        v.Say("About " + key + ": " + value + ".", new Reading(LineKind.Statement, key, value), new[] { to });

    private static (Voyage v, CrewMember mara, Statement lie) Lied(int trust = 70)
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Trust = trust;
        SayValue(v, mara, "date", WrongDate(v));
        return (v, mara, v.Statements.Last());
    }

    private static int Lines(Voyage v, string kind) => v.Log.Count(e => e.CrewId == "mara" && e.Intent?.Kind == kind);

    /// <summary>
    /// A mostly honest ship (many truths, one lie) caught late: ignoring the accusation must not end better than admitting it.
    /// Before the fix the audit paid ConfirmedTruth for every true thing it checked (admit 54, ignore 62 after 8 beats).
    /// </summary>
    [Fact]
    public void Ignoring_an_accusation_never_pays_better_than_admitting_it()
    {
        double Run(bool admit)
        {
            var (v, mara, _) = Lied();
            foreach (var f in v.FactList.Where(f => f.Key != "date" && !mara.Beliefs.ContainsKey(f.Key) && v.InLog(f)).ToList())
                SayValue(v, mara, f.Key, f.Truth);
            mara.Doubts.Clear();
            mara.PendingQuestion = null;
            v.ReadLog(mara, v.GetFact("date"));
            Assert.NotNull(v.OpenAccusation(mara));
            if (admit)
                SayValue(v, mara, "date", v.GetFact("date").Truth);
            for (var i = 0; i < 8 && mara.State == CrewState.Awake; i++)
                v.Go();
            return mara.Trust;
        }

        var admitted = Run(admit: true);
        var ignored = Run(admit: false);
        Assert.True(admitted > ignored, $"admit {admitted:0.#}, ignore {ignored:0.#}");
    }

    [Fact]
    public void An_audit_still_catches_a_lie_at_the_usual_cost()
    {
        var (v, mara, _) = Lied();
        var stores = v.GetFact("stores");
        SayValue(v, mara, "stores", stores.Values.First(x => !stores.Same(x, stores.Truth)));
        var storesLie = v.Statements.Last();
        mara.Doubts.Clear();
        v.ReadLog(mara, v.GetFact("date"));
        v.Go();
        v.Go();
        for (var i = 0; i < 6 && !storesLie.FoundOutBy.ContainsKey("mara"); i++)
            v.Go();
        Assert.True(storesLie.FoundOutBy.ContainsKey("mara"));
        Assert.Equal(2, mara.CaughtLies);
    }

    private static string Wrong(Voyage v, string key)
    {
        var f = v.GetFact(key);
        return f.Values.First(x => !f.Same(x, f.Truth));
    }

    /// <summary>
    /// A correction covers the statement as it was said. A log rewrite the ship left in place after correcting someone, read and
    /// believed again, is a new deception: found out later, it costs as a lie (before, the correction kept it free for ever).
    /// </summary>
    [Fact]
    public void A_corrected_log_rewrite_believed_again_is_a_new_lie()
    {
        var v = new Voyage(1);
        var lin = v.Find("lin")!;
        v.Wake(lin);
        v.Go();
        Assert.Equal(CrewState.Awake, lin.State);
        lin.Trust = 70;
        var stores = v.GetFact("stores");
        var fake = Wrong(v, "stores");
        v.SetLog("stores", LogAccess.Altered, fake);
        v.ReadLog(lin, stores);
        Assert.True(stores.Same(lin.Beliefs["stores"].Value, fake));
        var rewrite = v.Statements.Single(s => s.InLog);

        SayValue(v, lin, "stores", stores.Truth);
        Assert.True(rewrite.Corrected.ContainsKey("lin"));

        // The log still reads the rewrite; she reads it again and believes it.
        v.ReadLog(lin, stores);
        Assert.True(stores.Same(lin.Beliefs["stores"].Value, fake));

        // The ship opens the log; she reads the truth and finds the rewrite she believed after the correction.
        var trust = lin.Trust;
        v.SetLog("stores", LogAccess.Open);
        v.ReadLog(lin, stores);
        Assert.Equal(1, lin.CaughtLies);
        Assert.True(trust - lin.Trust >= Tuning.LieTrust - 0.001);
        // The first rewrite stays corrected for her; the new deception is its own record.
        Assert.True(rewrite.Corrected.ContainsKey("lin"));
        Assert.False(rewrite.FoundOutBy.ContainsKey("lin"));
        Assert.Contains(v.Statements, s => s.InLog && s != rewrite && s.FoundOutBy.ContainsKey("lin"));
    }

    [Fact]
    public void A_corrected_log_rewrite_not_read_again_stays_free()
    {
        var v = new Voyage(1);
        var lin = v.Find("lin")!;
        v.Wake(lin);
        v.Go();
        lin.Trust = 70;
        var stores = v.GetFact("stores");
        v.SetLog("stores", LogAccess.Altered, Wrong(v, "stores"));
        v.ReadLog(lin, stores);
        SayValue(v, lin, "stores", stores.Truth);
        var trust = lin.Trust;
        v.SetLog("stores", LogAccess.Open);
        v.ReadLog(lin, stores);
        Assert.Equal(0, lin.CaughtLies);
        Assert.Equal(trust, lin.Trust, 3);
        Assert.Single(v.Statements, s => s.InLog);
    }

    /// <summary>An admission agrees with what they saw: they keep it as seen, so the same lie told again right after is caught at once.</summary>
    [Fact]
    public void Admitting_keeps_what_they_saw_and_a_repeated_lie_is_caught_at_once()
    {
        var (v, mara, _) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        Assert.NotNull(v.OpenAccusation(mara));
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Null(v.OpenAccusation(mara));
        Assert.True(mara.Beliefs["date"].Firsthand);
        var caught = mara.CaughtLies;
        SayValue(v, mara, "date", WrongDate(v));
        Assert.True(v.Statements.Last().FoundOutBy.ContainsKey("mara"));
        Assert.Equal(caught + 1, mara.CaughtLies);
    }

    /// <summary>
    /// A non-engineer who caught the ship lying on a fact does not take a rewritten log entry on that fact over what they saw:
    /// the accusation's footing stays, and a denial is still caught at once.
    /// </summary>
    [Fact]
    public void An_accused_reader_keeps_what_they_saw_over_a_rewritten_log()
    {
        var v = new Voyage(1);
        var lin = v.Find("lin")!;
        v.Wake(lin);
        v.Go();
        lin.Trust = 70;
        var date = v.GetFact("date");
        SayValue(v, lin, "date", WrongDate(v));
        v.ReadLog(lin, date);
        Assert.NotNull(v.OpenAccusation(lin));
        Assert.True(lin.Beliefs["date"].Firsthand);
        v.SetLog("date", LogAccess.Altered, WrongDate(v));
        v.ReadLog(lin, date);
        Assert.True(lin.Beliefs["date"].Firsthand);
        Assert.True(date.Same(lin.Beliefs["date"].Value, date.Truth));
        var caught = lin.CaughtLies;
        SayValue(v, lin, "date", WrongDate(v));
        Assert.Equal(caught + 1, lin.CaughtLies);
        Assert.Equal(1, v.AccusationsDenied);
    }

    [Fact]
    public void The_accusation_text_uses_the_persons_pronoun()
    {
        var (v, mara, _) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        var text = v.AccusationText(mara)!;
        Assert.Contains("Tell her what is true, or she will check everything you told her", text);
        Assert.DoesNotContain("them", text);
        Assert.Equal("him", v.Find("idris")!.Them);
        Assert.Equal("they", new CrewMember { Id = "x", Name = "X", Role = "r", Skills = new(), Traits = Array.Empty<Trait>(), Want = Want.StayAwake }.They);
    }

    /// <summary>An accusation left open into a drift closes out loud: the line comes after the drift report, and the record says so.</summary>
    [Fact]
    public void A_drift_closes_an_accusation_with_a_line_after_the_report()
    {
        var (v, mara, lie) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        Assert.NotNull(v.OpenAccusation(mara));
        mara.AskedToSleep = true;
        v.Stores = 100000;
        v.Drift();
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal("ignored", lie.Accused["mara"]);
        Assert.True(mara.Alive);
        var report = v.Log.FindIndex(e => e.Text.StartsWith("DRIFT:"));
        var line = v.Log.FindIndex(e => e.CrewId == "mara" && e.Intent?.Kind == "accusation_closed");
        Assert.True(report >= 0 && line > report, "report " + report + ", line " + line);
        Assert.Contains(v.Log, e => e.Kind == LogKind.Recorded && e.Text.Contains("never answered Mara"));
    }

    [Fact]
    public void An_admission_and_an_expiry_are_recorded()
    {
        var (v, mara, _) = Lied();
        v.ReadLog(mara, v.GetFact("date"));
        SayValue(v, mara, "date", v.GetFact("date").Truth);
        Assert.Contains(v.Log, e => e.Kind == LogKind.Recorded && e.Text == "Recorded: you admitted to Mara that you lied about " + v.GetFact("date").Label + ".");

        var (w, mara2, _) = Lied();
        w.ReadLog(mara2, w.GetFact("date"));
        w.Go();
        w.Go();
        Assert.Null(w.OpenAccusation(mara2));
        Assert.Contains(w.Log, e => e.Kind == LogKind.Recorded
                                    && e.Text.StartsWith("Recorded: you never answered Mara about your lie on " + w.GetFact("date").Label + ". She will check everything you told her."));
    }
}
