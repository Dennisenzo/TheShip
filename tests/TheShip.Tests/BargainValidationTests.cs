using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Regression tests for the validation of main after the bargains merge (fix/bargains-validation).</summary>
public class BargainValidationTests
{
    private static Reading Promise(string key) => new(LineKind.Promise, PromiseKey: key);

    private static void Promised(Voyage v, CrewMember to, string key) =>
        v.Say("I promise.", Promise(key), new[] { to });

    /// <summary>Alert 1 played honestly: Mara is awake in the aftermath, Idris asleep.</summary>
    private static (Voyage v, CrewMember mara, CrewMember idris) Aftermath()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Trust = 52;
        mara.Morale = 60;
        return (v, mara, v.Find("idris")!);
    }

    private static int Count(Voyage v, string kind, CrewMember c) => v.Log.Count(e => e.Intent?.Kind == kind && e.CrewId == c.Id);

    // ---- 1. Repeated promises stack ----

    [Fact]
    public void A_repeated_promise_is_kept_once_when_the_partner_wakes()
    {
        var (v, mara, idris) = Aftermath();
        var trust = mara.Trust;
        for (var i = 0; i < 4; i++)
            Promised(v, mara, "wake_partner");
        Assert.Single(v.Ledger(), e => e.Person == mara && e.Key == "wake_partner" && e.Origin == "promise");

        v.Wake(idris);

        Assert.Equal(1, Count(v, "promise_kept", mara));
        Assert.Equal(trust + Tuning.PromiseMade + Tuning.PromiseKept, mara.Trust, 3);
    }

    [Fact]
    public void A_repeated_unkept_promise_is_broken_once()
    {
        var (v, mara, _) = Aftermath();
        for (var i = 0; i < 3; i++)
            Promised(v, mara, "wake_partner");
        var due = v.Ledger().Single(e => e.Person == mara && e.Key == "wake_partner" && e.Origin == "promise").DueBy!.Value;
        while (v.Clock < due + 1)
            v.Go();
        Assert.Equal(1, Count(v, "promise_broken", mara));
        Assert.Single(v.Log, e => e.Kind == LogKind.Ship && e.Text.StartsWith("Promise to Mara: wake Idris."));
    }

    [Fact]
    public void Repeated_log_promises_to_a_bay_taker_do_not_buy_the_bay_back()
    {
        var (v, mara, _) = Aftermath();
        v.Lockouts.Bay = true;
        v.Lockouts.BayTakers.Add(mara.Id);
        mara.Trust = 5;
        // Three promises in six weeks (one a beat), the logs open all along.
        for (var i = 0; i < 3; i++)
        {
            Promised(v, mara, "show_logs");
            v.Go();
        }
        for (var i = 0; i < 20 && v.Ledger().Any(e => e.Person == mara && e.State == "open"); i++)
            v.Go();
        Assert.DoesNotContain(v.Ledger(), e => e.Person == mara && e.State == "open");
        Assert.Equal(1, Count(v, "promise_kept", mara));
        Assert.True(v.Lockouts.Bay, "one kept promise does not lift trust 5 to the bay's return");
    }

    // ---- 3. Sleeping settles a promise at once: the ledger and the SLEEP button say so ----

    [Fact]
    public void The_ledger_line_says_a_promise_also_ends_when_the_person_sleeps()
    {
        var (v, mara, _) = Aftermath();
        Promised(v, mara, "wake_partner");
        var e = v.Ledger().Single(x => x.Person == mara && x.State == "open");
        Assert.Matches(@"^Promise to Mara: wake Idris, \d+ days left or when she sleeps\.$", v.LedgerLine(e));
    }

    [Fact]
    public void The_sleep_warning_names_the_promise_sleeping_would_break()
    {
        var (v, mara, idris) = Aftermath();
        Assert.Null(v.SleepBreaksText(mara));
        Promised(v, mara, "wake_partner");
        Assert.Equal("Mara still waits for Idris: sleeping now breaks that promise.", v.SleepBreaksText(mara));
        v.Wake(idris);
        Assert.Null(v.SleepBreaksText(mara));
    }

    [Fact]
    public void The_sleep_warning_on_the_logs_follows_whether_they_are_open()
    {
        var (v, mara, _) = Aftermath();
        v.SetLog("all", LogAccess.Restricted);
        Promised(v, mara, "show_logs");
        Assert.Equal("Mara still waits for the logs to open: sleeping now breaks that promise.", v.SleepBreaksText(mara));
        v.SetLog("all", LogAccess.Open);
        Assert.Null(v.SleepBreaksText(mara));
    }

    // ---- 4. A struck bargain runs Assign's gates again ----

    /// <summary>Alert 1: Mara awake, her questions answered truthfully, the coolant repair asked for twice: she has set her price.</summary>
    private static (Voyage v, CrewMember mara, Fault f) Offered()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var f = v.FindFault("")!;
        // Her question and the danger are answered first, so the price is her only condition.
        for (var i = 0; i < 4 && mara.PendingQuestion is { } q; i++)
        {
            var fact = v.GetFact(q);
            v.Say(fact.Say(fact.Truth), new Reading(LineKind.Statement, fact.Key, fact.Truth), new[] { mara });
        }
        if (f.DangerFact is { } danger)
            v.Say(v.GetFact(danger).Say(v.GetFact(danger).Truth), new Reading(LineKind.Statement, danger, v.GetFact(danger).Truth), new[] { mara });
        v.Assign(mara, f, patch: false);
        Assert.Equal("wake_partner", mara.OpenBargain?.Key);
        return (v, mara, f);
    }

    private static int Kind(Voyage v, string kind) => v.Log.Count(e => e.Intent?.Kind == kind);

    [Fact]
    public void Agreeing_after_trust_fell_out_of_the_wary_band_gives_the_trust_refusal()
    {
        var (v, mara, f) = Offered();
        mara.Trust = 10;
        v.Say("Agreed.", Promise("wake_partner"), new[] { mara });
        Assert.Equal(1, Kind(v, "refuse_trust"));
        Assert.Equal(0, Kind(v, "bargain_struck"));
        Assert.Equal(0, v.BargainsStruck);
        Assert.NotEqual(f.Id, mara.WorkingOn);
        Assert.Null(mara.OpenBargain);
        // The ship's word still stands.
        Assert.Contains(v.Ledger(), e => e.Person == mara && e.Key == "wake_partner" && e.State == "open");
    }

    [Fact]
    public void Agreeing_while_a_question_waits_strikes_the_deal_and_the_work_starts_by_itself_once_answered()
    {
        var (v, mara, f) = Offered();
        mara.PendingQuestion = "alive:idris";
        v.Say("Agreed.", Promise("wake_partner"), new[] { mara });
        // The word is given; the question still stands, so she does not start yet and does not close the deal.
        Assert.Equal(1, v.BargainsStruck);
        Assert.Equal(0, Kind(v, "bargain_struck"));
        Assert.NotEqual(f.Id, mara.WorkingOn);

        var fact = v.GetFact("alive:idris");
        var trust = mara.Trust;
        v.Say(fact.Say(fact.Truth), new Reading(LineKind.Statement, fact.Key, fact.Truth), new[] { mara });
        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal(0, Kind(v, "comply_pushed") + Kind(v, "bargain_refused"));
        Assert.True(mara.Trust >= trust - 0.001);
    }

    // ---- 5. Promises about the dead and about logs the crew took ----

    [Fact]
    public void A_promise_to_wake_a_dead_partner_earns_nothing_and_is_not_on_the_ledger()
    {
        var (v, mara, idris) = Aftermath();
        v.Kill(idris, "died");
        var trust = mara.Trust;
        var ship = v.Log.Count(e => e.Kind == LogKind.Ship);
        Promised(v, mara, "wake_partner");
        Assert.Equal(trust, mara.Trust, 3);
        Assert.DoesNotContain(v.Ledger(), e => e.Person == mara && e.Origin == "promise");
        Assert.Equal(ship, v.Log.Count(e => e.Kind == LogKind.Ship));
        Assert.Equal("promise", v.Log.Last(e => e.CrewId == mara.Id).Intent?.Kind);
        v.Go();
        Assert.DoesNotContain(v.Log, e => e.Text.Contains("promise can no longer be kept"));
    }

    [Fact]
    public void Logs_the_crew_took_do_not_keep_a_promise_to_open_them()
    {
        var (v, mara, _) = Aftermath();
        v.SetLog("all", LogAccess.Restricted);
        Promised(v, mara, "show_logs");
        v.Lockouts.Logs = true;
        Assert.Null(v.SleepBreaksText(mara));
        v.Go();
        Assert.Equal(0, Count(v, "promise_kept", mara));
        Assert.Equal(0, Count(v, "promise_broken", mara));
        Assert.Equal("released", v.Ledger().Single(e => e.Person == mara && e.Key == "show_logs").State);
    }

    // ---- 6. BargainOf only reads; the lapse happens in the steps; the next-step and stakes text ----

    [Fact]
    public void BargainOf_only_reads_and_the_next_step_drops_a_stale_condition()
    {
        var (v, mara, f) = Offered();
        var open = mara.OpenBargain;
        f.State = FaultState.Lost;
        Assert.Null(v.BargainOf(mara));
        Assert.Equal(open, mara.OpenBargain);
        v.Go();
        Assert.Null(mara.OpenBargain);
    }

    [Fact]
    public void A_condition_lapses_at_the_next_step_when_trust_leaves_the_wary_band()
    {
        var (v, mara, _) = Offered();
        mara.Trust = 75;
        v.Go();
        Assert.Null(mara.OpenBargain);
    }

    [Fact]
    public void The_word_is_not_awaited_once_someone_else_works_the_fault()
    {
        var (v, mara, f) = Offered();
        Assert.True(v.AwaitsWord(mara));
        var tomas = v.Find("tomas")!;
        tomas.State = CrewState.Awake;
        tomas.WorkingOn = f.Id;
        Assert.NotNull(v.BargainOf(mara));
        Assert.Equal(f.People > 1, v.AwaitsWord(mara));
    }

    [Fact]
    public void The_push_odds_read_they_start_without_a_pronoun()
    {
        var c = new CrewMember { Id = "x", Name = "X", Role = "r", Skills = new(), Traits = Array.Empty<Trait>(), Want = Want.WakePartner };
        Assert.Equal("they start", Voyage.StartsText(c));
        var (v, mara, _) = Offered();
        Assert.Equal("she starts", Voyage.StartsText(mara));
        Assert.Contains("% she starts,", v.BargainText(mara));
    }
}
