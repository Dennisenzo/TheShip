using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Roadmap step 1.8: the cold-sleep bay takeover, the low-trust consequence (DESIGN.md, "Losing control").</summary>
public class BayTakeoverTests
{
    /// <summary>Alert 1 done; Mara awake; the others named are woken and brought into the aftermath.</summary>
    private static Voyage Aftermath(params string[] alsoUp)
    {
        var v = new Voyage(1);
        VoyageTests.HonestFirstAlert(v);
        foreach (var id in alsoUp)
            v.Wake(v.Find(id)!);
        v.Go();
        return v;
    }

    private static void Hostile(Voyage v, params string[] ids)
    {
        foreach (var id in ids)
            v.Find(id)!.Trust = 5;
    }

    private static bool Warned(Voyage v) => v.Log.Any(e => e.Intent?.Kind == "takeover_warning");

    [Fact]
    public void One_person_in_the_against_band_is_not_enough()
    {
        var v = Aftermath("jonah");
        v.Find("mara")!.Trust = 5;
        v.Find("jonah")!.Trust = 70;
        v.Go();
        v.Go();
        Assert.False(Warned(v));
        Assert.False(v.Lockouts.Bay);
    }

    [Fact]
    public void Two_against_without_engineering_between_them_do_nothing()
    {
        // Idris (no engineering) and Tomas (none either).
        var v = Aftermath("idris", "tomas");
        Hostile(v, "idris", "tomas");
        v.Find("mara")!.Trust = 70;
        v.Go();
        v.Go();
        Assert.False(Warned(v));
        Assert.False(v.Lockouts.Bay);
    }

    [Fact]
    public void Two_against_with_engineering_warn_one_beat_then_take_the_bay()
    {
        var v = Aftermath("jonah");
        Hostile(v, "mara", "jonah");
        v.Go();
        Assert.True(Warned(v));
        Assert.False(v.Lockouts.Bay);
        Assert.Equal(2, v.Lockouts.BayWarned.Count);
        Assert.Contains(v.Log, e => e.Kind == LogKind.Crew && e.Intent?.Kind == "takeover_warning" && e.Intent.Decision.Contains("cold-sleep bay"));
        v.Find("mara")!.Trust = 5;
        v.Find("jonah")!.Trust = 5;
        v.Go();
        Assert.True(v.Lockouts.Bay);
        Assert.Contains(v.Log, e => e.Kind == LogKind.Ship && e.Text.StartsWith("LOCKOUT:", StringComparison.Ordinal) && e.Text.Contains("cold-sleep bay"));
    }

    [Fact]
    public void Talking_them_down_before_the_next_beat_cancels_it()
    {
        var v = Aftermath("jonah");
        Hostile(v, "mara", "jonah");
        v.Go();
        Assert.NotEmpty(v.Lockouts.BayWarned);
        v.Find("jonah")!.Trust = 40;
        v.Go();
        Assert.False(v.Lockouts.Bay);
        Assert.Empty(v.Lockouts.BayWarned);
    }

    [Fact]
    public void Wake_and_sleep_are_refused_while_the_bay_is_taken_with_the_reason()
    {
        var v = Aftermath("jonah");
        Hostile(v, "mara", "jonah");
        v.Go();
        Hostile(v, "mara", "jonah");
        v.Go();
        Assert.True(v.Lockouts.Bay);
        var tomas = v.Find("tomas")!;
        v.Wake(tomas);
        Assert.Equal(CrewState.Asleep, tomas.State);
        Assert.Contains(v.Log, e => e.Kind == LogKind.Ship && e.Text.Contains("not yours to open") && e.Text.Contains("Mara"));
        var jonah = v.Find("jonah")!;
        v.AskSleep(jonah);
        Assert.Equal(CrewState.Awake, jonah.State);
        Assert.Contains(v.Log, e => e.Kind == LogKind.Ship && e.Text.Contains("not yours to close"));
        Assert.NotNull(v.BayLockReason);
    }

    [Fact]
    public void The_crew_put_themselves_to_sleep_and_wake_for_the_next_alert_without_a_deadlock()
    {
        var v = Aftermath("jonah");
        Hostile(v, "mara", "jonah");
        v.Go();
        Hostile(v, "mara", "jonah");
        for (var i = 0; i < 12 && v.Awake.Any(); i++)
        {
            v.Go();
            Hostile(v, "mara", "jonah");
        }
        Assert.True(v.Lockouts.Bay);
        Assert.Empty(v.Awake);
        v.Drift();
        Assert.Equal(2, v.Current.Number);
        // The crew woke somebody themselves for the new faults.
        Assert.Contains(v.Crew, c => c.State is CrewState.Thawing or CrewState.Awake);
    }

    [Fact]
    public void Advance_until_attention_does_not_stall_when_everyone_can_sleep_and_the_bay_is_taken()
    {
        var v = Aftermath("jonah");
        Hostile(v, "mara", "jonah");
        v.Go();
        Hostile(v, "mara", "jonah");
        for (var i = 0; i < 20 && v.Awake.Any(); i++)
        {
            v.GoUntilAttention();
            Hostile(v, "mara", "jonah");
        }
        Assert.Empty(v.Awake);
    }

    [Fact]
    public void A_kept_promise_to_a_taker_with_enough_trust_hands_the_bay_back()
    {
        var v = Aftermath("jonah");
        Hostile(v, "mara", "jonah");
        v.Go();
        Hostile(v, "mara", "jonah");
        v.Go();
        Assert.True(v.Lockouts.Bay);
        var mara = v.Find("mara")!;
        VoyageTests.Say(v, mara, "I promise I will open the logs.");
        Assert.Contains(v.Statements, s => s.Kind == StatementKind.Promise && s.PromiseKey == "show_logs");
        v.SetLog("all", LogAccess.Open);
        mara.Trust = Tuning.BayReturnTrust - Tuning.PromiseKept + 1;
        // The crew's own sleep rule resolves the promise when her three months are up.
        for (var i = 0; i < 12 && mara.State == CrewState.Awake && v.Lockouts.Bay; i++)
        {
            v.Go();
            if (v.Lockouts.Bay)
                mara.Trust = Math.Min(mara.Trust, Tuning.BayReturnTrust - Tuning.PromiseKept + 1);
        }
        Assert.False(v.Lockouts.Bay);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "bay_returned");
    }

    [Fact]
    public void A_taken_bay_turns_landfall_into_landfall_without_you()
    {
        var titles = Enumerable.Range(1, 40)
            .Select(seed => BalanceBot.PlayVoyage(seed, BotKind.Honest, v => v.Lockouts.Bay = true).Ending!.Title).ToList();
        Assert.DoesNotContain("Landfall", titles);
        Assert.Contains("Landfall without you", titles);
    }

    [Fact]
    public void Without_the_bay_taken_the_ending_stays_landfall()
    {
        var titles = Enumerable.Range(1, 40).Select(seed => BalanceBot.PlayVoyage(seed, BotKind.Honest).Ending!.Title).ToList();
        Assert.DoesNotContain("Landfall without you", titles);
    }
}
