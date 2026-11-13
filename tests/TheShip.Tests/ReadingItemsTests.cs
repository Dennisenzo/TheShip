using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// The reading names the open item a line responds to and how (answer, accept, refuse, dodge, work); the game decides the outcome.
/// The model never changes state: an unknown item or an unusable reading is plain talk (DESIGN.md, "Reading").
/// </summary>
public class ReadingItemsTests
{
    /// <summary>Alert 1, Mara's question and the danger answered: her price (wake Idris) is the only condition left.</summary>
    private static (Voyage v, CrewMember mara, Fault f) TermsOpen()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var f = v.FindFault("")!;
        v.Assign(mara, f, patch: false);
        foreach (var key in new[] { "alive:idris", f.DangerFact! })
            v.Say("x", new Reading(LineKind.Statement, key, v.GetFact(key).Truth), new[] { mara });
        Assert.NotNull(v.BargainOf(mara));
        return (v, mara, f);
    }

    private static Reading Item(LineKind kind, string id, ItemResponse response, string? promise = null) => new(kind, PromiseKey: promise, Item: id, Response: response);

    [Fact]
    public void The_grammar_lists_the_open_items_and_every_string_is_bounded()
    {
        var g = Grammars.ReadingFor(new[] { "date" }, new[] { "mara.terms", "mara.asked" });
        Assert.Contains("mara.terms", g);
        Assert.Contains("\\\"none\\\"", g);
        Assert.Contains("\\\"refuse\\\"", g);
        Assert.Contains("\\\"work\\\"", g);
        Assert.DoesNotContain("char*", g);
        Assert.DoesNotContain("char+", g);
        Assert.DoesNotContain("mara.request", Grammars.ReadingFor(new[] { "date" }, new[] { "mara.terms" }));
        Assert.DoesNotContain("mara.terms", Grammars.ReadingFor(new[] { "date" }));
    }

    [Fact]
    public void The_prompt_lists_each_listeners_open_items_and_their_last_lines()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var f = v.FindFault("")!;
        v.Assign(mara, f, patch: false);
        var user = Prompts.ReadUser("its true", new List<CrewMember> { mara }, v);
        Assert.Contains("Open item mara.asked: Mara asked the ship: \"How is Idris?\"", user);
        Assert.Contains("Open item mara.terms:", user);
        Assert.Contains("wake Idris", user);
        // Her own last line is there for a short reply to be read against.
        Assert.Contains("Mara said:", user);
        var system = Prompts.ReadSystem(v);
        Assert.Contains("NOT a promise", system);
        Assert.Contains("\"item\":\"mara.request\",\"response\":\"refuse\"", system);
    }

    [Fact]
    public void A_refusal_of_open_terms_is_the_push_not_a_dodge()
    {
        var (v, mara, f) = TermsOpen();
        var trust = mara.Trust;
        var before = v.Statements.Count;
        v.Say("no, there is no need to", Item(LineKind.Plain, "mara.terms", ItemResponse.Refuse), new[] { mara });
        // The push roll ran: trust paid either way, and she answered it.
        Assert.Equal(trust - Tuning.BargainPushedTrust, mara.Trust, 3);
        Assert.Contains(v.Log.Last(e => e.Intent != null).Intent!.Kind, new[] { "comply_pushed", "bargain_refused" });
        Assert.DoesNotContain(v.Statements.Skip(before), s => s.Kind == StatementKind.Dodge);
    }

    [Fact]
    public void An_order_in_words_with_terms_open_is_the_same_push_and_never_asks_which_fault()
    {
        var (v, mara, f) = TermsOpen();
        var trust = mara.Trust;
        v.Say("just fix the ship damn it", Item(LineKind.Order, "mara.terms", ItemResponse.Work), new[] { mara });
        Assert.Equal(trust - Tuning.BargainPushedTrust, mara.Trust, 3);
        Assert.DoesNotContain(v.Log, e => e.Intent?.Kind == "order");

        // The same order read with no item still means her one job.
        var (v2, mara2, _) = TermsOpen();
        var trust2 = mara2.Trust;
        v2.Say("fix it", new Reading(LineKind.Order), new[] { mara2 });
        Assert.Equal(trust2 - Tuning.BargainPushedTrust, mara2.Trust, 3);
        Assert.DoesNotContain(v2.Log, e => e.Intent?.Kind == "order");
    }

    [Fact]
    public void An_order_with_no_job_on_the_table_still_asks_which_fault_when_there_are_several()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        // Nothing stated yet and one open fault: the order means that fault.
        var f = v.FindFault("")!;
        v.Say("fix it", new Reading(LineKind.Order), new[] { mara });
        Assert.DoesNotContain(v.Log, e => e.Intent?.Kind == "order");
        Assert.NotNull(mara.Job);

        // Asleep-everywhere voyage in an aftermath: no fault, so she asks.
        var v2 = new Voyage(1);
        var tomas = v2.Find("tomas")!;
        v2.Wake(tomas);
        v2.Go();
        VoyageTests.FinishAlert(v2);
        v2.Say("fix it", new Reading(LineKind.Order), new[] { v2.Awake.First() });
        Assert.Contains(v2.Log, e => e.Intent?.Kind == "order");
    }

    [Fact]
    public void Accepting_open_terms_is_the_promise_and_the_work_starts()
    {
        var (v, mara, f) = TermsOpen();
        v.Say("ok, I will", Item(LineKind.Plain, "mara.terms", ItemResponse.Accept), new[] { mara });
        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal(1, v.BargainsStruck);
        Assert.Contains(v.Ledger(), e => e.Person == mara && e.Key == "wake_partner");
    }

    [Theory]
    [InlineData("other")]
    [InlineData("none")]
    [InlineData(null)]
    public void Accepting_terms_with_a_promise_of_no_key_is_the_word_they_asked_for(string? key)
    {
        var (v, mara, f) = TermsOpen();
        v.Say("You have my word.", Item(LineKind.Promise, "mara.terms", ItemResponse.Accept, key), new[] { mara });
        Assert.Equal(f.Id, mara.WorkingOn);
        Assert.Equal(1, v.BargainsStruck);
        Assert.Contains(v.Ledger(), e => e.Person == mara && e.Key == "wake_partner");
    }

    [Fact]
    public void Accepting_terms_with_another_specific_promise_keeps_that_promise()
    {
        var (v, mara, f) = TermsOpen();
        v.Say("I will never thaw you again.", Item(LineKind.Promise, "mara.terms", ItemResponse.Accept, "no_more_thaws"), new[] { mara });
        Assert.Equal(0, v.BargainsStruck);
        Assert.DoesNotContain(v.Ledger(), e => e.Person == mara && e.Key == "wake_partner");
        Assert.Contains(v.Statements, s => s.PromiseKey == "no_more_thaws");
    }

    [Fact]
    public void A_refusal_of_a_request_declines_it_with_its_cost_and_is_no_promise()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        VoyageTests.FinishAlert(v);
        mara.RequestMade = true;
        var trust = mara.Trust;
        var before = v.Statements.Count;
        // The model's old misreading: "I WILL NOT WAKE HIM" as the promise never to thaw them again; with the item it is a refusal.
        v.Say("I WILL NOT WAKE HIM", Item(LineKind.Plain, "mara.request", ItemResponse.Refuse), new[] { mara });
        Assert.True(mara.RequestDeclined);
        Assert.Equal(trust - Tuning.RequestIgnored, mara.Trust, 3);
        Assert.DoesNotContain(v.Statements.Skip(before), s => s.Kind == StatementKind.Promise);
        Assert.DoesNotContain(v.Ledger(), e => e.Key == "no_more_thaws");
        // Declining is not charged again at sleep, and the request leaves "Waiting on you".
        Assert.DoesNotContain(v.OpenItems(new[] { mara }), i => i.Kind == "request");
        Assert.False(mara.RequestOpen);
    }

    [Fact]
    public void A_lie_said_while_refusing_terms_is_recorded_and_the_terms_are_still_refused()
    {
        var (v, mara, f) = TermsOpen();
        var trust = mara.Trust;
        var before = v.Statements.Count;
        v.Say("No, I won't wake him. Idris is dead.", new Reading(LineKind.Statement, "alive:idris", "dead", Item: "mara.terms", Response: ItemResponse.Refuse), new[] { mara });
        var lie = Assert.Single(v.Statements.Skip(before));
        Assert.Equal(StatementKind.False, lie.Kind);
        Assert.Equal("alive:idris", lie.FactKey);
        Assert.Contains(mara.Id, lie.Hearers);
        Assert.Equal("dead", mara.Beliefs["alive:idris"].Value);
        Assert.Contains(v.Log, e => e.Text.StartsWith("Recorded: you told Mara that", StringComparison.Ordinal));
        // The push came after the statement: its cost is in the trust, the lie's own cost waits until it is found out.
        Assert.True(mara.Trust <= trust - Tuning.BargainPushedTrust + 1e-6);
        var kinds = v.Log.Where(e => e.Intent != null).Select(e => e.Intent!.Kind).ToList();
        Assert.Contains(kinds[^1], new[] { "comply_pushed", "bargain_refused" });
    }

    [Fact]
    public void A_statement_said_while_declining_a_request_is_recorded_and_the_request_declined()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        VoyageTests.FinishAlert(v);
        mara.RequestMade = true;
        var before = v.Statements.Count;
        v.Say("No. Idris is fine where he is.", new Reading(LineKind.Statement, "alive:idris", "alive", Item: "mara.request", Response: ItemResponse.Refuse), new[] { mara });
        Assert.True(mara.RequestDeclined);
        var said = Assert.Single(v.Statements.Skip(before));
        Assert.Equal("alive:idris", said.FactKey);
        Assert.Equal("alive", mara.Beliefs["alive:idris"].Value);
    }

    [Fact]
    public void An_item_that_is_not_on_the_table_changes_nothing()
    {
        var (v, mara, f) = TermsOpen();
        var trust = mara.Trust;
        v.Say("no", Item(LineKind.Plain, "idris.terms", ItemResponse.Refuse), new[] { mara });
        v.Say("no", Item(LineKind.Plain, "mara.request", ItemResponse.Refuse), new[] { mara });
        Assert.Equal(trust, mara.Trust, 3);
        Assert.False(mara.RequestDeclined);
        Assert.NotEqual(f.Id, mara.WorkingOn);
    }

    [Fact]
    public void A_refusal_to_answer_a_question_is_a_dodge_and_costs_as_one()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var trust = mara.Trust;
        v.Say("I won't say", Item(LineKind.Plain, "mara.asked", ItemResponse.Refuse), new[] { mara });
        Assert.Equal(trust - Tuning.Dodge, mara.Trust, 3);
        Assert.Contains(v.Statements, s => s.Kind == StatementKind.Dodge);
    }

    [Fact]
    public async Task The_model_reading_with_an_item_drives_the_outcome_through_ShipMind()
    {
        var (v, mara, f) = TermsOpen();
        var llm = new FakeLlm
        {
            Read = "{\"kind\":\"plain\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\",\"item\":\"mara.terms\",\"response\":\"refuse\"}",
        };
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, llm, main);
        var trust = mara.Trust;
        mind.SayAtOnce("no, there is no need to", new List<CrewMember> { mara });
        for (var i = 0; i < 300 && mind.Holding; i++)
        {
            main.Drain();
            await Task.Delay(10);
        }
        main.Drain();
        Assert.Contains("mara.terms", llm.LastReadGrammar);
        Assert.Contains("Open item mara.terms", llm.LastReadUser);
        Assert.Equal(trust - Tuning.BargainPushedTrust, mara.Trust, 3);
    }

    [Fact]
    public void The_test_reader_gives_the_same_field_from_cues()
    {
        var (v, mara, _) = TermsOpen();
        var hearers = new List<CrewMember> { mara };
        var refuse = RuleReader.Read("no, there is no need to", v, hearers);
        Assert.Equal("mara.terms", refuse.Item);
        Assert.Equal(ItemResponse.Refuse, refuse.Response);
        var work = RuleReader.Read("fix the leak", v, hearers);
        Assert.Equal(ItemResponse.Work, work.Response);
        var accept = RuleReader.Read("Agreed.", v, hearers);
        Assert.Equal(LineKind.Promise, accept.Kind);
        Assert.Equal(ItemResponse.Accept, accept.Response);
        Assert.Null(RuleReader.Read("how are you mara", v, hearers).Item);
    }

    [Fact]
    public void An_unusable_or_item_less_reading_stays_plain_talk()
    {
        var v = new Voyage(1);
        var llm = new FakeLlm();
        using var mind = new ShipMind(v, llm, new MainThreadQueue { Immediate = true });
        Assert.Null(mind.Parse("not json"));
        var parsed = mind.Parse("{\"kind\":\"plain\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\",\"item\":\"none\",\"response\":\"refuse\"}");
        Assert.Equal(Reading.Plain, parsed);
        // The older shape with no item field still reads.
        Assert.Equal(LineKind.Dodge, mind.Parse("{\"kind\":\"dodge\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}")!.Kind);
        var item = mind.Parse("{\"kind\":\"order\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\",\"item\":\"mara.terms\",\"response\":\"work\"}")!;
        Assert.Equal("mara.terms", item.Item);
        Assert.Equal(ItemResponse.Work, item.Response);
    }
}
