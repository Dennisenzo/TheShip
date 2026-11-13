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
}
