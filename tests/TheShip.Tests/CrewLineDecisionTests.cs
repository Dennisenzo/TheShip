using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>A crew line has to carry the decision's content (the condition, the question asked) without inventing new asks.</summary>
public class CrewLineDecisionTests
{
    private static readonly IReadOnlyDictionary<string, Belief> Nothing = new Dictionary<string, Belief>();

    private static ShipMind Mind() => new(new Voyage(1), new FakeLlm(), new MainThreadQueue { Immediate = true });

    [Theory]
    [InlineData("ask_danger", "Is it safe in there?", "Is the access port stable enough to get in there?", false)]
    [InlineData("ask_danger", "Is it safe in there?", "Before I go in: is it safe in there? Straight answer.", true)]
    [InlineData("ask_danger", "Is it safe in there?", "Tell me it's safe before I touch it.", true)]
    [InlineData("bargain_refused", "wake Idris", "Not without your word to wake Idris.", true)]
    [InlineData("bargain_refused", "wake Idris", "I won't touch the coolant. Just give me the actual parameters.", false)]
    [InlineData("refuse_answer", "How is Idris?", "Not until you tell me how Idris is.", true)]
    [InlineData("refuse_answer", "How is Idris?", "Give me something concrete, you hear?", false)]
    [InlineData("wake_question", "How is Idris?", "How's Idris? Tell me.", true)]
    [InlineData("request_partner", "Idris", "What are the precise conditions to bring Idris online?", false)]
    [InlineData("request_partner", "Idris", "Can you wake Idris while I'm up?", true)]
    [InlineData("request_partner", "Idris", "I want to see Idris. Wake him while I'm up.", true)]
    [InlineData("request_logs", "", "Open the logs to me. All of them.", true)]
    [InlineData("request_logs", "", "What exactly are you keeping from the crew in there?", false)]
    public void A_line_must_carry_the_question_or_condition_of_the_decision(string kind, string detail, string line, bool accepted)
    {
        using var mind = Mind();
        var intent = new CrewIntent(kind, "decision", detail);
        Assert.Equal(accepted, mind.Acceptable(line, Nothing, "decision", null, intent));
    }

    [Theory]
    [InlineData("promise", "You swore you wouldn't wake me again. What is the actual reason for this lockout?", false)]
    [InlineData("promise", "You swore you wouldn't wake me again. I'll hold you to it.", true)]
    [InlineData("comply", "On it. Which of the valves in that bay is it that you want me to look at?", false)]
    [InlineData("small", "I'm fine. And you, can you even feel anything?", true)]
    public void A_line_that_is_not_a_question_decision_asks_nothing_new(string kind, string line, bool accepted)
    {
        using var mind = Mind();
        Assert.Equal(accepted, mind.Acceptable(line, Nothing, "decision", null, new CrewIntent(kind, "decision")));
    }

    [Fact]
    public void The_prompt_asks_for_the_content_and_forbids_new_asks()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var user = Prompts.CrewUser(new CrewIntent("ask_danger", "Before you start the repair you ask the ship: Is it safe in there?", "Is it safe in there?"), mara, v);
        Assert.Contains("must carry this", user);
        Assert.Contains("Is it safe in there?", user);
        Assert.Contains("do not ask for details, parameters", Prompts.CrewSystem(mara, v));
        Assert.DoesNotContain("must carry this", Prompts.CrewUser(new CrewIntent("comply", "x", "the coolant leak"), mara, v));
    }

    [Fact]
    public void A_stray_quote_is_removed()
    {
        Assert.Equal("Fine. Right. So he is breathing.", LocalLlm.CleanReply("Fine.\" Right. So he is breathing."));
        Assert.Equal("Fine. \"Right\" then.", LocalLlm.CleanReply("Fine. \"Right\" then."));
    }

    [Fact]
    public void The_promise_never_to_thaw_is_told_to_the_person_it_was_made_to()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        v.Say("I won't wake you again.", new Reading(LineKind.Promise, PromiseKey: "no_more_thaws"), new[] { mara });
        var decision = v.Log.Last(e => e.Intent?.Kind == "promise").Intent!.Decision;
        Assert.Contains("never to wake you from the pod again", decision);
        Assert.DoesNotContain("thaw", decision);
    }

    [Fact]
    public void Conditions_and_the_declined_request_have_templates_that_pass_the_check()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        v.Assign(mara, v.FindFault("")!, patch: false);
        var entry = v.Log.Last(e => e.Intent?.Kind == "conditions");
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.True(mind.Acceptable(entry.Text, mara.Beliefs, entry.Intent!.Decision, null, entry.Intent));
        Assert.Contains("How is Idris?", entry.Text);
    }
}
