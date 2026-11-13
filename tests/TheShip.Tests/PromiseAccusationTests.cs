using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>A broken promise opens an accusation, like a caught lie (DESIGN.md, "How a lie is caught"): admit, keep it late, ignore, dodge.</summary>
public class PromiseAccusationTests
{
    private static (Voyage v, CrewMember mara, CrewMember idris) Broken()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.Trust = 70;
        mara.Morale = 60;
        v.Say("I promise.", new Reading(LineKind.Promise, PromiseKey: "wake_partner"), new[] { mara });
        for (var i = 0; i < 20 && mara.Accusations.Count == 0 && mara.State == CrewState.Awake; i++)
            v.Go();
        return (v, mara, v.Find("idris")!);
    }

    [Fact]
    public void A_promise_broken_at_its_due_date_opens_an_accusation_that_names_it()
    {
        var (v, mara, _) = Broken();
        var a = v.OpenAccusation(mara)!;
        Assert.NotNull(a);
        Assert.Equal("wake_partner", a.PromiseKey);
        Assert.Equal("alive:idris", a.FactKey);
        Assert.Equal(1, v.PromiseAccusationsOpened);
        Assert.Equal("Mara holds you to your word: wake Idris.", v.PromiseAccusationShort(mara, a));
        Assert.StartsWith("Mara holds you to your word: wake Idris.", v.AccusationText(mara));
    }

    [Fact]
    public void Keeping_the_promise_late_closes_it_as_admitted_and_pays_the_admission_once()
    {
        var (v, mara, idris) = Broken();
        var trust = mara.Trust;
        v.Wake(idris);
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal(1, v.AccusationsAdmitted);
        Assert.Equal(1, v.PromiseAccusationsKept);
        Assert.Equal(trust + Tuning.AccusationAdmitTrust, mara.Trust, 3);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "accusation_admitted");
        // The promise stays broken in the ledger.
        Assert.Equal("broken", v.Ledger().Last(e => e.Person == mara && e.Key == "wake_partner").State);
    }

    [Fact]
    public void Telling_them_the_truth_about_the_fact_admits_it_without_a_trust_refund()
    {
        var (v, mara, _) = Broken();
        var trust = mara.Trust;
        var fact = v.GetFact("alive:idris");
        v.Say(fact.Say(fact.Truth), new Reading(LineKind.Statement, fact.Key, fact.Truth), new[] { mara });
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal(1, v.AccusationsAdmitted);
        Assert.True(mara.Trust <= trust + 0.001);
    }

    [Fact]
    public void Ignoring_it_closes_unanswered_and_costs_trust_and_starts_an_audit()
    {
        var (v, mara, _) = Broken();
        var trust = mara.Trust;
        for (var i = 0; i < 6 && mara.Accusations.Count > 0 && mara.State == CrewState.Awake; i++)
            v.Go();
        Assert.Empty(mara.Accusations);
        Assert.Equal(1, v.AccusationsIgnored);
        Assert.True(mara.Trust < trust);
        Assert.Contains(v.Log, e => e.Text.Contains("never answered Mara about the promise you broke (wake Idris)"));
        Assert.Contains(v.Log, e => e.Intent?.Kind == "accusation_closed");
    }

    [Fact]
    public void A_promise_broken_by_going_to_sleep_opens_none()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        v.Say("I promise.", new Reading(LineKind.Promise, PromiseKey: "wake_partner"), new[] { mara });
        mara.HoursAwake = Tuning.MinAwakeHours;
        v.AskSleep(mara);
        Assert.Equal(CrewState.Asleep, mara.State);
        Assert.Equal("broken", v.Ledger().Last(e => e.Person == mara && e.Key == "wake_partner").State);
        Assert.Empty(mara.Accusations);
        Assert.Equal(0, v.PromiseAccusationsOpened);
    }

    [Fact]
    public void A_released_promise_opens_none()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        v.Say("I promise.", new Reading(LineKind.Promise, PromiseKey: "wake_partner"), new[] { mara });
        v.Kill(v.Find("idris")!, "died");
        for (var i = 0; i < 12; i++)
            v.Go();
        Assert.Equal(0, v.PromiseAccusationsOpened);
        Assert.Empty(mara.Accusations);
    }

    [Fact]
    public void A_broken_promise_to_open_the_logs_is_admitted_by_opening_them_late()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        v.ReleaseOwed(mara);
        mara.RefusesSleep = true;
        mara.AskedToSleep = true;
        mara.Trust = 70;
        v.SetLog("all", LogAccess.Restricted);
        v.Say("I promise.", new Reading(LineKind.Promise, PromiseKey: "show_logs"), new[] { mara });
        for (var i = 0; i < 20 && mara.Accusations.Count == 0 && mara.State == CrewState.Awake; i++)
            v.Go();
        var a = v.OpenAccusation(mara)!;
        Assert.Equal("show_logs", a.PromiseKey);
        Assert.Equal("Mara holds you to your word: open the logs.", v.PromiseAccusationShort(mara, a));
        v.SetLog("all", LogAccess.Open);
        Assert.Null(v.OpenAccusation(mara));
        Assert.Equal(1, v.AccusationsAdmitted);
    }
}
