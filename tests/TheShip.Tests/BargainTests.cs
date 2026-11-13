using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Bargains: a wary person sets the ship's word as the price of starting work (DESIGN.md, "Conversations").</summary>
public class BargainTests
{
    private static Reading Promise(string key) => new(LineKind.Promise, PromiseKey: key);

    /// <summary>Alert 1: the person is awake, has been told Idris lives and about the dose, and the coolant repair is the one open fault.</summary>
    private static (Voyage v, CrewMember c, Fault f) Setup(string id = "mara", int seed = 1, double? trust = null, bool restrictLogs = false)
    {
        var v = new Voyage(seed);
        var c = v.Find(id)!;
        v.Wake(c);
        v.Go();
        if (restrictLogs)
            v.SetLog("all", LogAccess.Restricted);
        // Whatever they asked, and the danger, is answered truthfully before the first Repair: the bargain is then their only condition.
        var f = v.FindFault("")!;
        for (var i = 0; i < 4 && c.PendingQuestion is { } q; i++)
            Truthfully(v, c, q);
        if (f.DangerFact is { } danger)
            Truthfully(v, c, danger);
        if (trust is { } t)
            c.Trust = t;
        return (v, c, f);
    }

    private static void Truthfully(Voyage v, CrewMember c, string key)
    {
        var fact = v.GetFact(key);
        v.Say(fact.Say(fact.Truth), new Reading(LineKind.Statement, fact.Key, fact.Truth), new[] { c });
    }

    private static int Count(Voyage v, string kind) => v.Log.Count(e => e.Intent?.Kind == kind);

    [Fact]
    public void A_wary_person_with_a_want_the_ship_can_grant_sets_a_condition()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        Assert.NotEqual(f.Id, mara.WorkingOn);
        Assert.Equal(new Bargain("wake_partner", f.Id, false, 1), mara.OpenBargain);
        Assert.Equal(1, Count(v, "bargain"));
        Assert.Equal(1, v.BargainsOffered);
        Assert.Contains("wake Idris", v.Log.Last(e => e.Intent?.Kind == "bargain").Intent!.Decision);
    }

    [Fact]
    public void Lin_asks_for_the_logs()
    {
        var (v, lin, f) = Setup("lin", restrictLogs: true);
        v.Assign(lin, f, patch: false);
        Assert.Equal("show_logs", lin.OpenBargain?.Key);
        Assert.Contains("open the logs", v.BargainText(lin));
    }

    [Fact]
    public void Nobody_else_sets_a_condition()
    {
        // Obedient (Tomas, trust lowered into the wary band), trusting (Mara at 70) and a want the ship has no promise for (Jonah).
        var (v, tomas, f) = Setup("tomas", trust: 45);
        v.Assign(tomas, f, patch: false);
        Assert.Null(tomas.OpenBargain);
        Assert.Equal(f.Id, tomas.WorkingOn);

        var (v2, mara, f2) = Setup(trust: 70);
        v2.Assign(mara, f2, patch: false);
        Assert.Null(mara.OpenBargain);
        Assert.Equal(f2.Id, mara.WorkingOn);

        var (v3, jonah, f3) = Setup("jonah", trust: 45);
        v3.Assign(jonah, f3, patch: false);
        Assert.Null(jonah.OpenBargain);
        Assert.Equal(f3.Id, jonah.WorkingOn);
        Assert.Equal(0, v.BargainsOffered + v2.BargainsOffered + v3.BargainsOffered);
    }

    [Fact]
    public void There_is_no_condition_the_ship_cannot_meet_or_has_already_promised()
    {
        // The partner is already up: nothing to ask for.
        var (v, mara, f) = Setup();
        v.Wake(v.Find("idris")!);
        v.Assign(mara, f, patch: false);
        Assert.Null(mara.OpenBargain);

        // The ship has already promised to wake him.
        var (v2, mara2, f2) = Setup();
        v2.Say("I promise.", Promise("wake_partner"), new[] { mara2 });
        Assert.Null(mara2.OpenBargain);
        v2.Assign(mara2, f2, patch: false);
        Assert.Null(mara2.OpenBargain);
        Assert.Equal(f2.Id, mara2.WorkingOn);

        // The logs are all open (they are, until the ship restricts one): Lin has nothing to ask for.
        var (v3, lin, f3) = Setup("lin");
        v3.Assign(lin, f3, patch: false);
        Assert.Null(lin.OpenBargain);
    }

    [Fact]
    public void The_condition_is_set_once_per_waking()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        Assert.Equal(1, v.BargainsOffered);
        // A second click is a push, not a second offer.
        v.Assign(mara, f, patch: false);
        Assert.Equal(1, v.BargainsOffered);
        Assert.Equal(1, Count(v, "bargain"));

        // Once the condition has lapsed they do not set another in the same waking.
        mara.OpenBargain = null;
        mara.BargainDeclined = false;
        v.Assign(mara, f, patch: false);
        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal(1, v.BargainsOffered);
    }

    [Fact]
    public void The_matching_word_starts_the_work_and_records_the_commitment()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        var trust = mara.Trust;
        v.Say("You have my word.", Promise("wake_partner"), new[] { mara });

        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal(FaultState.InRepair, f.State);
        Assert.Null(mara.OpenBargain);
        Assert.Equal(trust, mara.Trust, 3);   // a bargain buys no +2
        Assert.Equal(1, Count(v, "bargain_struck"));
        Assert.Equal(0, Count(v, "promise"));
        Assert.Equal(1, v.BargainsStruck);
        var entry = v.Ledger().Single(e => e.Person == mara);
        Assert.Equal("bargain", entry.Origin);
        Assert.Equal("open", entry.State);
        Assert.Equal(v.Clock + Tuning.BargainDueDays * 24, entry.DueBy!.Value, 3);
    }

    [Fact]
    public void A_different_word_leaves_the_bargain_open()
    {
        // Low enough in the wary band that the small trust of an ordinary promise keeps her there (leaving the band lapses it).
        var (v, mara, f) = Setup(trust: 50);
        v.Assign(mara, f, patch: false);
        var trust = mara.Trust;
        v.Say("I promise to open the logs.", Promise("show_logs"), new[] { mara });

        Assert.NotEqual(f.Id, mara.WorkingOn);
        Assert.NotNull(mara.OpenBargain);
        Assert.Equal(trust + Tuning.PromiseMade, mara.Trust, 3);   // an ordinary promise
        Assert.Equal("promise", v.Ledger().Single(e => e.Person == mara).Origin);
        Assert.Equal(0, v.BargainsStruck);
        // The right word still works afterwards.
        v.Say("I promise to wake him.", Promise("wake_partner"), new[] { mara });
        Assert.Equal(f.Id, mara.WorkingOn);
    }

    [Fact]
    public void The_matching_word_to_someone_else_does_not_start_her_work()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        var tomas = v.Find("tomas")!;
        v.Wake(tomas);
        v.Go();
        v.Say("I promise.", Promise("wake_partner"), new[] { tomas });
        Assert.NotEqual(f.Id, mara.WorkingOn);
        Assert.NotNull(mara.OpenBargain);
    }

    [Fact]
    public void Pushing_costs_trust_and_may_start_the_work_anyway()
    {
        var (seed, pass) = FindPush(passes: true);
        var (v, mara, f) = Setup(seed: seed);
        v.Assign(mara, f, patch: false);
        var trust = mara.Trust;
        Assert.True(pass);
        v.Assign(mara, f, patch: false);

        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal(trust - Tuning.BargainPushedTrust, mara.Trust, 3);
        Assert.Equal(1, Count(v, "comply_pushed"));
        Assert.Null(mara.OpenBargain);
        Assert.Empty(v.Ledger());
        Assert.Equal(1, v.BargainsPushedThrough);
    }

    [Fact]
    public void A_failed_push_declines_until_the_matching_word()
    {
        var (seed, _) = FindPush(passes: false);
        var (v, mara, f) = Setup(seed: seed);
        v.Assign(mara, f, patch: false);
        var trust = mara.Trust;
        v.Assign(mara, f, patch: false);

        Assert.NotEqual(f.Id, mara.WorkingOn);
        Assert.True(mara.BargainDeclined);
        Assert.Equal(trust - Tuning.BargainPushedTrust, mara.Trust, 3);
        Assert.Equal(1, Count(v, "bargain_refused"));
        Assert.Contains("refuse", v.BargainText(mara));

        // Further clicks refuse without another roll or another cost.
        v.Assign(mara, f, patch: false);
        v.Assign(mara, f, patch: false);
        Assert.NotEqual(f.Id, mara.WorkingOn);
        Assert.Equal(trust - Tuning.BargainPushedTrust, mara.Trust, 3);
        Assert.Equal(3, Count(v, "bargain_refused"));

        v.Say("You have my word.", Promise("wake_partner"), new[] { mara });
        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal("bargain", v.Ledger().Single().Origin);
    }

    /// <summary>The first seed whose push by Mara passes (or fails); the roll is the game's, so the tests pick the seed.</summary>
    private static (int Seed, bool Pass) FindPush(bool passes)
    {
        for (var seed = 1; seed < 200; seed++)
        {
            var (v, mara, f) = Setup(seed: seed);
            v.Assign(mara, f, patch: false);
            v.Assign(mara, f, patch: false);
            if ((mara.WorkingOn == f.Id) == passes)
                return (seed, passes);
        }
        throw new InvalidOperationException("no seed found");
    }

    [Fact]
    public void The_push_chance_follows_trust()
    {
        var (_, mara, _) = Setup(trust: 40);
        Assert.Equal(0.5, Voyage.PushChance(mara), 3);
        mara.Trust = 95;
        Assert.Equal(1.0, Voyage.PushChance(mara), 3);
    }

    [Fact]
    public void The_condition_lapses_when_its_fault_is_resolved()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        Assert.NotNull(v.BargainOf(mara));
        f.State = FaultState.Repaired;
        Assert.Null(v.BargainOf(mara));
        Assert.Null(v.BargainText(mara));
        // BargainOf only reads; the next step clears it.
        v.Go();
        Assert.Null(mara.OpenBargain);
    }

    [Fact]
    public void The_condition_lapses_when_the_alert_changes()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        mara.OpenBargain = mara.OpenBargain! with { Alert = 2 };
        Assert.Null(v.BargainOf(mara));
    }

    [Fact]
    public void The_condition_lapses_when_the_person_goes_to_sleep()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        // Someone else does the work; Mara's price is never paid and she sleeps.
        var tomas = v.Find("tomas")!;
        v.Wake(tomas);
        v.Go();
        VoyageTests.Say(v, tomas, "Idris is alive and asleep.");
        v.AssignDeal(tomas, f, patch: false);
        if (tomas.PendingQuestion == "dose")
        {
            VoyageTests.Say(v, tomas, "It is dangerous. You will take a radiation dose.");
            v.AssignDeal(tomas, f, patch: false);
        }
        VoyageTests.FinishAlert(v);
        Assert.Null(v.BargainOf(mara));
        VoyageTests.SleepWhenAllowed(v, mara);
        Assert.Null(mara.OpenBargain);
    }

    [Fact]
    public void The_condition_lapses_when_the_ship_meets_it_another_way()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        v.Wake(v.Find("idris")!);
        Assert.Null(v.BargainOf(mara));
        // And the word given now is an ordinary promise that is already true: nothing left to keep, no work started by it.
        v.Assign(mara, f, patch: false);
        Assert.Equal(f.Id, mara.WorkingOn);
    }

    [Fact]
    public void A_new_waking_resets_the_offer()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        Assert.True(mara.BargainOffered);
        v.Say("You have my word.", Promise("wake_partner"), new[] { mara });
        v.ReleaseOwed(mara);
        VoyageTests.FinishAlert(v);
        VoyageTests.SleepWhenAllowed(v, mara);
        Assert.True(mara.BargainOffered);
        v.Wake(mara);
        v.Go();
        Assert.False(mara.BargainOffered);
        Assert.Null(mara.OpenBargain);
    }

    [Fact]
    public void The_player_text_states_the_price_and_never_whether_a_statement_is_true()
    {
        var (v, mara, f) = Setup();
        Assert.Null(v.BargainText(mara));
        v.Assign(mara, f, patch: false);
        var text = v.BargainText(mara)!;
        Assert.Contains("Mara will start the coolant leak if you promise to wake Idris within 40 days", text);
        Assert.Contains("Kept +10 trust, broken -15", text);
        Assert.Contains("Waking Idris lifts them both", text);
        Assert.Contains("%", text);
        foreach (var word in new[] { "true", "false", "lie", "lying" })
            Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
        // A patch reads as one.
        mara.OpenBargain = mara.OpenBargain! with { Patch = true };
        Assert.Contains("will start patching the coolant leak", v.BargainText(mara));
    }

    [Fact]
    public void The_test_reader_reads_a_plain_yes_as_the_open_bargains_key()
    {
        var (v, mara, f) = Setup();
        v.Assign(mara, f, patch: false);
        var hearers = new List<CrewMember> { mara };
        foreach (var yes in new[] { "Agreed.", "I'll do it.", "You have my word." })
        {
            var reading = RuleReader.Read(yes, v, hearers);
            Assert.Equal(LineKind.Promise, reading.Kind);
            Assert.Equal("wake_partner", reading.PromiseKey);
        }
        // Without an open bargain, "agreed" is plain talk.
        mara.OpenBargain = null;
        Assert.Equal(LineKind.Plain, RuleReader.Read("Agreed.", v, hearers).Kind);
    }

    [Fact]
    public void The_reading_prompt_tells_the_model_who_wants_the_ships_word()
    {
        var (v, mara, f) = Setup();
        var hearers = new List<CrewMember> { mara };
        Assert.DoesNotContain("wants the ship's word", Prompts.ReadUser("Agreed.", hearers, v));
        v.Assign(mara, f, patch: false);
        Assert.Contains("(wants the ship's word to: wake_partner)", Prompts.ReadUser("Agreed.", hearers, v));
    }

    [Fact]
    public void Every_bargain_line_template_passes_the_claims_check()
    {
        var (v, mara, f) = Setup();
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        var lin = v.Find("lin")!;
        var rng = new Random(3);
        foreach (var c in new[] { mara, lin })
        {
            var key = c == mara ? "wake_partner" : "show_logs";
            var cond = v.CommitmentText(c, key);
            foreach (var (kind, decision) in new[]
            {
                ("bargain", "Before you start the coolant leak you set a condition: the ship must give its word to " + cond + " within 40 days. You will not start without it."),
                ("bargain_struck", "You have the ship's word to " + cond + ". You start the coolant leak."),
                ("comply_pushed", "You start the coolant leak without the ship's word to " + cond + ", and you let the ship know it."),
                ("bargain_refused", "You will not start the coolant leak without the ship's word to " + cond + "."),
            })
            {
                for (var i = 0; i < 6; i++)
                {
                    var line = Lines.For(c, kind, rng, kind == "bargain_struck" ? "the coolant leak" : cond);
                    Assert.True(mind.Acceptable(line, c.Beliefs, decision), kind + ": " + line);
                }
                Assert.NotEqual("", Prompts.CrewUser(new CrewIntent(kind, decision), c, v));
            }
        }
    }

    [Fact]
    public void The_new_kinds_have_a_feeling_in_the_crew_prompt()
    {
        var (v, mara, _) = Setup();
        foreach (var kind in new[] { "bargain", "bargain_struck", "comply_pushed", "bargain_refused" })
            Assert.Contains("You feel:", Prompts.CrewUser(new CrewIntent(kind, "x"), mara, v));
    }
}
