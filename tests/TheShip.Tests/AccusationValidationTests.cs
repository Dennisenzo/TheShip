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
}
