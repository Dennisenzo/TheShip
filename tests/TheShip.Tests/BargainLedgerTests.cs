using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>The promise ledger: due dates, how a promise ends, the warning and the one-time trust gain (DESIGN.md, "Conversations").</summary>
public class BargainLedgerTests
{
    private static Reading Promise(string key) => new(LineKind.Promise, PromiseKey: key);

    private static void Promised(Voyage v, CrewMember to, string key) =>
        v.Say("I promise.", Promise(key), new[] { to });

    /// <summary>Alert 1 played honestly: Mara is awake in the aftermath, Idris asleep.</summary>
    private static (Voyage v, CrewMember mara, CrewMember idris) Aftermath()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara); // the first alert's bargain promise is not under test here
        mara.Trust = 70;
        mara.Morale = 60;
        return (v, mara, v.Find("idris")!);
    }

    private static LedgerEntry Entry(Voyage v, CrewMember c, string key) =>
        v.Ledger().Last(e => e.Person == c && e.Key == key);

    [Fact]
    public void A_promise_to_wake_the_partner_is_kept_at_once_when_the_thaw_starts()
    {
        var (v, mara, idris) = Aftermath();
        Promised(v, mara, "wake_partner");
        var entry = Entry(v, mara, "wake_partner");
        Assert.Equal("open", entry.State);
        Assert.Equal(v.Clock + Tuning.PromiseDueDays * 24, entry.DueBy!.Value, 3);
        Assert.Equal("wake Idris", entry.Text);
        var trust = mara.Trust;
        var morale = mara.Morale;

        v.Wake(idris);

        Assert.Equal("kept", Entry(v, mara, "wake_partner").State);
        Assert.Equal(trust + Tuning.PromiseKept, mara.Trust, 3);
        Assert.Equal(morale + Tuning.PromiseKeptMorale, mara.Morale, 3);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "promise_kept");
    }

    [Fact]
    public void An_unkept_promise_is_broken_at_its_due_date_in_the_aftermath_beats()
    {
        var (v, mara, _) = Aftermath();
        Promised(v, mara, "wake_partner");
        var due = Entry(v, mara, "wake_partner").DueBy!.Value;
        var trust = mara.Trust;
        var morale = mara.Morale;
        // Every beat is two weeks; it stays open until the date passes.
        while (v.Clock < due - 0.001)
        {
            Assert.Equal("open", Entry(v, mara, "wake_partner").State);
            v.Go();
        }
        Assert.Equal("broken", Entry(v, mara, "wake_partner").State);
        Assert.True(mara.Trust <= trust - Tuning.PromiseBroken + 0.001);
        Assert.True(mara.Morale <= morale - Tuning.PromiseBrokenMorale + 0.001);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "promise_broken");
    }

    [Fact]
    public void A_promise_to_open_the_logs_is_kept_at_the_due_date_not_at_once()
    {
        var (v, mara, _) = Aftermath();
        v.SetLog("all", LogAccess.Restricted);
        Promised(v, mara, "show_logs");
        v.SetLog("all", LogAccess.Open);
        Assert.Equal("open", Entry(v, mara, "show_logs").State);
        var due = Entry(v, mara, "show_logs").DueBy!.Value;
        while (v.Clock < due - 0.001)
            v.Go();
        Assert.Equal("kept", Entry(v, mara, "show_logs").State);
    }

    [Fact]
    public void A_promise_that_can_no_longer_be_kept_is_released_without_a_penalty()
    {
        var (v, mara, idris) = Aftermath();
        Promised(v, mara, "wake_partner");
        var trust = mara.Trust;
        v.Kill(idris, "died");
        // Her own request would be about the dead too; the beat settles the ledger first.
        v.Go();
        Assert.Equal("released", Entry(v, mara, "wake_partner").State);
        Assert.Contains(v.Log, e => e.Kind == LogKind.Ship && e.Text.Contains("Mara's promise can no longer be kept"));
        Assert.DoesNotContain(v.Log, e => e.Intent?.Kind == "promise_broken");
        Assert.DoesNotContain(v.Ledger(), e => e.State == "open");
        Assert.True(mara.Trust >= trust - 10, "no promise penalty");
    }

    [Fact]
    public void A_promise_the_taken_bay_blocks_is_released()
    {
        var (v, mara, _) = Aftermath();
        Promised(v, mara, "wake_partner");
        v.Lockouts.Bay = true;
        v.Lockouts.BayTakers.Add("jonah");
        v.Go();
        Assert.Equal("released", Entry(v, mara, "wake_partner").State);
    }

    [Fact]
    public void The_due_warning_is_a_ship_line_and_stops_advance()
    {
        var (v, mara, _) = Aftermath();
        Promised(v, mara, "wake_partner");
        AdvanceResult last = new(0, "");
        for (var i = 0; i < 8 && !last.Reason.Contains("promise"); i++)
            last = v.GoUntilAttention();
        Assert.Contains("a promise to Mara falls due", last.Reason);
        var warning = v.Log.Single(e => e.Kind == LogKind.Ship && e.Text.StartsWith("Promise to Mara: wake Idris."));
        Assert.Contains("days left", warning.Text);
        var due = Entry(v, mara, "wake_partner").DueBy!.Value;
        Assert.True(due - v.Clock <= Tuning.PromiseDueWarnDays * 24 + Tuning.AftermathStepHours + 0.001);
        // Warned with time left to act, not on the day it falls due.
        Assert.True(due - v.Clock > Tuning.PromiseDueWarnDays * 24 - 0.001);
        // Once only.
        v.Go();
        Assert.Single(v.Log, e => e.Kind == LogKind.Ship && e.Text.StartsWith("Promise to Mara: wake Idris."));
    }

    [Fact]
    public void Repeating_a_promise_earns_the_small_trust_once_per_waking()
    {
        var (v, mara, _) = Aftermath();
        mara.Trust = 50;
        Promised(v, mara, "other");
        Assert.Equal(50 + Tuning.PromiseMade, mara.Trust, 3);
        Promised(v, mara, "other");
        Promised(v, mara, "no_more_thaws");
        Assert.Equal(50 + Tuning.PromiseMade, mara.Trust, 3);
    }

    [Fact]
    public void A_hearer_with_no_partner_is_never_broken_on_a_wake_partner_promise()
    {
        var v = new Voyage(1);
        var lin = v.Find("lin")!;
        v.Wake(lin);
        v.Go();
        Assert.Equal(CrewState.Awake, lin.State);
        Promised(v, lin, "wake_partner");
        Assert.Equal("released", Entry(v, lin, "wake_partner").State);
        VoyageTests.FinishAlert(v);
        lin.Trust = 70;
        VoyageTests.SleepWhenAllowed(v, lin);
        Assert.DoesNotContain(v.Log, e => e.Intent?.Kind == "promise_broken");
        Assert.DoesNotContain(v.Ledger(), e => e.State == "open");
    }

    [Fact]
    public void A_refuser_who_never_sleeps_still_has_the_promise_resolved_at_its_due_date()
    {
        var (v, mara, _) = Aftermath();
        mara.RefusesSleep = true;
        mara.AskedToSleep = true;
        v.SetLog("all", LogAccess.Restricted);
        Promised(v, mara, "show_logs");
        var due = Entry(v, mara, "show_logs").DueBy!.Value;
        for (var i = 0; i < 12 && v.Clock < due + 14 * 24 && mara.State == CrewState.Awake; i++)
            v.Go();
        Assert.Equal(CrewState.Awake, mara.State);
        Assert.Equal("broken", Entry(v, mara, "show_logs").State);
    }

    [Fact]
    public void Going_to_sleep_settles_an_open_promise()
    {
        var (v, mara, _) = Aftermath();
        Promised(v, mara, "wake_partner");
        VoyageTests.SleepWhenAllowed(v, mara);
        Assert.Equal("broken", Entry(v, mara, "wake_partner").State);
    }

    [Fact]
    public void A_matching_open_promise_stops_the_same_request()
    {
        var (v, mara, _) = Aftermath();
        Promised(v, mara, "wake_partner");
        // Past 200 awake hours: without the promise she would ask for her partner.
        for (var i = 0; i < 20 && mara.HoursAwake <= 220; i++)
            v.Go();
        Assert.True(mara.HoursAwake > 200);
        Assert.False(mara.RequestMade && Entry(v, mara, "wake_partner").State == "open");
    }
}
