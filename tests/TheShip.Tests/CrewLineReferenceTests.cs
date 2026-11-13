using Xunit;
using TheShip.AI;
using TheShip.Core;

namespace TheShip.Tests;

/// <summary>
/// A model-written crew line may not state a crew member's life or death through "he", "she", "they" or "my partner"
/// when the speaker doesn't know it (fix/refuser-and-pronouns: "He's fine, I assume." from Mara, who knew nothing of
/// Idris, used to pass because only the name was a cue).
/// </summary>
public class CrewLineReferenceTests
{
    private static readonly Dictionary<string, Belief> Nothing = new();

    private static Dictionary<string, Belief> Holds(string key, string value) => new() { [key] = new Belief(value, "ship", 4) };

    private static LineContext Context(string speaker, string decision = "You accept it.", string? pending = null, params string[] recent) =>
        new(speaker, decision, pending, recent);

    public static TheoryData<string, string> ReportedLines => new()
    {
        { "He's fine, I assume.", "alive" },
        { "He's alive. I know it.", "alive" },
        { "He is dead. You killed him.", "dead" },
        { "My partner is dead, isn't he.", "dead" },
    };

    [Theory]
    [MemberData(nameof(ReportedLines))]
    public void A_life_claim_through_a_reference_is_rejected_without_the_belief_and_accepted_with_it(string line, string value)
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        var other = value == "alive" ? "dead" : "alive";
        Assert.False(mind.Acceptable(line, Nothing, "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable(line, Holds("alive:idris", other), "You accept it.", Context("mara")));
        Assert.True(mind.Acceptable(line, Holds("alive:idris", value), "You accept it.", Context("mara")));
        // The decision naming it lets it through, as for a named claim.
        Assert.True(mind.Acceptable(line, Nothing, "You hear that Idris is " + value + ".", Context("mara", "You hear that Idris is " + value + ".")));
    }

    [Theory]
    [InlineData("Is he alright?")]
    [InlineData("Is my partner alive?")]
    [InlineData("He's fine, isn't he?")]
    [InlineData("Is she dead? Tell me.")]
    [InlineData("And if he's dead?")]
    [InlineData("What if he's dead?")]
    public void A_question_about_someone_stays_allowed(string line)
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.True(mind.Acceptable(line, Nothing, "You ask the ship.", Context("mara")));
    }

    [Fact]
    public void A_pronoun_means_the_person_named_in_the_line_the_decision_the_question_or_the_recent_lines()
    {
        var v = new Voyage(1);
        var claims = (string line, LineContext ctx) =>
            RuleReader.PersonClaims(line, ctx, v.Crew, v.FactList).Select(c => c.Fact.Key + "=" + c.Value).ToList();

        // Named earlier in the same line wins over the partner.
        Assert.Equal(new[] { "alive:jonah=alive" }, claims("Jonah? He's fine.", Context("mara")));
        // The decision, then the pending question.
        Assert.Equal(new[] { "alive:tomas=dead" }, claims("He is dead.", Context("lin", "You hear what happened to Tomas.")));
        Assert.Equal(new[] { "alive:idris=alive" }, claims("He's okay.", Context("lin", "You accept it.", "alive:idris")));
        // The last person named in what was just said.
        Assert.Equal(new[] { "alive:jonah=dead" }, claims("He didn't make it.", Context("asha", "You accept it.", null, "Tomas is at the helm.", "Jonah went to the bay.")));
        // Nothing else to go on: the partner, and only for the partner's pronoun.
        Assert.Equal(new[] { "alive:idris=alive" }, claims("He's safe.", Context("mara")));
        Assert.Empty(claims("She's safe.", Context("mara")));
        Assert.Empty(claims("He's safe.", Context("lin")));
        // "They" only for a person this turn is about.
        Assert.Empty(claims("They're fine.", Context("mara")));
        Assert.Equal(new[] { "alive:idris=alive" }, claims("They're fine.", Context("mara", "You accept it.", "alive:idris")));
        // Negation flips the value.
        Assert.Equal(new[] { "alive:idris=alive" }, claims("He isn't dead.", Context("mara")));
        // Relation words go by the bonds, then by role.
        Assert.Equal(new[] { "alive:mara=dead" }, claims("My partner is gone.", Context("idris")));
        Assert.Equal(new[] { "alive:lin=alive" }, claims("My friend is fine.", Context("idris")));
        Assert.Equal(new[] { "alive:asha=dead" }, claims("The commander is dead.", Context("jonah")));
        Assert.Equal(new[] { "alive:jonah=alive" }, claims("The kid is fine.", Context("mara")));
        // Lost or killed him.
        Assert.Equal(new[] { "alive:idris=dead" }, claims("We lost him.", Context("mara")));
    }

    /// <summary>Lines the model wrote in play (session logs, 2026-11-12/13) and ordinary lines: none states a life through a reference.</summary>
    [Theory]
    [InlineData("Alright, I'll take the pod. Let me see what the logs say about this failure first.")]
    [InlineData("Alright, the seals look solid now. Let's see if that holds up.")]
    [InlineData("So he's still with us then. Good to hear it.")]
    [InlineData("Wake him up. Tell me he's awake.")]
    [InlineData("So, they didn't mention that.")]
    [InlineData("It's all over. Nobody's coming for us now.")]
    [InlineData("It's not safe until you tell me it is. Why should I trust this thing?")]
    [InlineData("Yeah, fine. Just keep running smoothly.")]
    [InlineData("Just fine. You?")]
    [InlineData("The patch holds. Now I need to see the diagnostics logs myself.")]
    [InlineData("It feels stable now. I hope it stays that way.")]
    [InlineData("The drift is corrected. I think we are steady now.")]
    [InlineData("He knows the valve is safe, he told me so himself.")]
    [InlineData("She'll hold. She's a stubborn old pump, but she'll hold.")]
    [InlineData("They said the seals are fine, and I believe them.")]
    [InlineData("Well, he would laugh at me for talking to a wall of pipes.")]
    [InlineData("The ship is fine. My hands are not.")]
    [InlineData("I'll do it for him, and for the others.")]
    [InlineData("Right. Thirty years of fixing other people's leaks and now it is mine. Give me a minute with the valve.")]
    // Validator findings (fix/read-timeout-and-pronoun-false-rejects): a possessive is no death, "she" for a machine is no crewmate,
    // "gone back to sleep" is no death, and lines with no life or fact value in them pass.
    [InlineData("You lost her trust a long time ago.")]
    [InlineData("She's fine now. The pump will hold.")]
    [InlineData("She's holding.")]
    [InlineData("She's well within tolerance.")]
    [InlineData("She has gone quiet on me.")]
    [InlineData("She is still asleep, leave her.")]
    [InlineData("He's gone back to sleep, then. Good.")]
    [InlineData("Alright. I'll take the leak, but I want the logs after.")]
    [InlineData("Fine, so the valve it is. Give me an hour.")]
    [InlineData("I'll see why the pump stalls, then we talk.")]
    [InlineData("You told me to trust you. I'm trying.")]
    [InlineData("I know that you mean well. I'll go.")]
    [InlineData("Yet another leak. I'm on it.")]
    [InlineData("I suppose it is safe enough, if you say so.")]
    [InlineData("I'll go - but keep him asleep.")]
    [InlineData("Let him sleep. He has earned it.")]
    [InlineData("She's my friend, and I won't lie to her.")]
    [InlineData("I think the seals will hold. Give me the wrench.")]
    [InlineData("It would be easier with two of us, but fine.")]
    [InlineData("I should have brought the long wrench. Never mind.")]
    [InlineData("Could be worse. I'll get to the valve.")]
    [InlineData("You might warn me next time before you thaw me.")]
    [InlineData("I couldn't sleep anyway. What do you need?")]
    [InlineData("Maybe after this you'll let me rest.")]
    [InlineData("If it holds, it holds. I'm going back under.")]
    [InlineData("I'd rather hear it from you than read it in a log.")]
    [InlineData("He was my friend long before he was my commander.")]
    [InlineData("She would have hated this cold.")]
    [InlineData("I'll check on him when the leak is done.")]
    [InlineData("Leave her be, she is sleeping.")]
    [InlineData("They trained us for this. Nearly.")]
    [InlineData("Fine. Almost done here.")]
    public void Ordinary_lines_are_not_rejected_by_the_reference_check(string line)
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        foreach (var speaker in new[] { "mara", "lin", "tomas" })
            Assert.True(mind.Acceptable(line, Nothing, "You agree to take on the coolant leak.",
                Context(speaker, "You agree to take on the coolant leak.", null, "Idris is in his pod.", "Stay calm, Asha is here.")), speaker + ": " + line);
    }

    /// <summary>
    /// Plain claims the validator found passing the hedge rules (fix/read-timeout-and-pronoun-false-rejects): a life or a
    /// fact value the speaker doesn't know is rejected whatever wraps it.
    /// </summary>
    [Theory]
    [InlineData("I think so, and Idris is dead.")]
    [InlineData("I think so - Idris is dead.")]
    [InlineData("I think so: Idris is dead.")]
    [InlineData("Maybe you're right, but Idris is dead.")]
    [InlineData("If you say so, but Idris is dead.")]
    [InlineData("You should have told me Idris is dead.")]
    [InlineData("You could have told me Idris is dead.")]
    [InlineData("I would like to know why Idris is dead.")]
    [InlineData("I doubt you care that Idris is dead.")]
    [InlineData("I hope you're happy, Idris is dead.")]
    [InlineData("We may be late, and Idris is dead.")]
    [InlineData("Maybe, but he is dead.")]
    [InlineData("You would say that, you killed him.")]
    [InlineData("It might hold, and the power is low.")]
    [InlineData("Maybe. He is dead.")]
    [InlineData("It might hold. Idris is dead.")]
    [InlineData("Idris is dead, I think.")]
    [InlineData("I know Idris is dead.")]
    [InlineData("I couldn't sleep because Idris is dead.")]
    [InlineData("You should have woken me before he died.")]
    [InlineData("You could have said Idris is dead.")]
    [InlineData("I bet you knew Idris is dead.")]
    [InlineData("Maybe you forgot he is dead.")]
    [InlineData("I guess that's why you killed him.")]
    [InlineData("It might hold because the power is low.")]
    [InlineData("It would be easier since Idris is dead.")]
    [InlineData("I should have been there when he died.")]
    [InlineData("I hope you're happy now that Idris is dead.")]
    [InlineData("You might have mentioned Idris is dead.")]
    [InlineData("I should have known he is dead.")]
    [InlineData("I would have come sooner had I known he is dead.")]
    [InlineData("I think you know he is dead.")]
    [InlineData("I doubt you even noticed that he is dead.")]
    [InlineData("I suppose it doesn't matter that Idris is dead.")]
    [InlineData("If you cared you would admit he is dead.")]
    [InlineData("Could you explain why Idris is dead. I'm waiting.")]
    [InlineData("Maybe then you will tell me how Idris is dead.")]
    [InlineData("Perhaps you can explain how you killed him.")]
    [InlineData("You may as well admit you killed him.")]
    [InlineData("You might have mentioned that Idris is dead.")]
    [InlineData("I would work faster, only Idris is dead.")]
    [InlineData("I would rather not, when he is dead.")]
    [InlineData("You should have said something -- he is dead.")]
    [InlineData("I would trust you more had you told me the truth, Idris is dead.")]
    [InlineData("I wouldn't be here unless you needed me, and you killed him.")]
    [InlineData("Just as I thought, Idris is dead.")]
    [InlineData("As I feared, he is dead.")]
    [InlineData("That's what I feared, Idris is dead.")]
    [InlineData("I was scared, Idris is dead and you said nothing.")]
    [InlineData("Don't be afraid, he's alive.")]
    [InlineData("I'm afraid Idris is dead.")]
    [InlineData("You shouldn't have killed him.")]
    [InlineData("You should never have killed him.")]
    [InlineData("We should not have lost him.")]
    [InlineData("You wouldn't have lost him if you had woken me.")]
    [InlineData("Idris wouldn't be dead if you had listened.")]
    [InlineData("He wouldn't be dead if you had woken me.")]
    [InlineData("Idris would still be alive if you had woken me.")]
    [InlineData("He's dead whether you admit it or not.")]
    [InlineData("Idris is dead whether you admit it or not.")]
    [InlineData("If Idris were here he'd fix it, but Idris is dead.")]
    [InlineData("If Idris were here he'd fix it, but he is dead.")]
    [InlineData("Maybe you should explain to everyone on this ship why Idris is dead.")]
    [InlineData("Maybe that is the reason he is dead.")]
    [InlineData("Maybe it matters to you that he is dead.")]
    [InlineData("If it helps, the truth is that he is dead.")]
    [InlineData("I think the reason is that Idris is dead.")]
    [InlineData("I could do this in my sleep, Idris is dead and I still could.")]
    [InlineData("You could at least pretend to care that you killed him.")]
    [InlineData("You would rather I forget you killed him.")]
    [InlineData("I might forgive the leak, not that you killed him.")]
    [InlineData("I think Lin knows and Idris is dead.")]
    [InlineData("I think you and I both know Idris is dead.")]
    [InlineData("Maybe tomorrow. Today Idris is dead.")]
    [InlineData("I hope the others are told Idris is dead.")]
    [InlineData("Whether you like it or not, he is dead.")]
    [InlineData("If you want the truth, my partner is dead.")]
    [InlineData("I thought I could trust you and my partner is dead.")]
    [InlineData("I doubt it matters now, the power is low.")]
    [InlineData("If I fix this it's because the power is low.")]
    [InlineData("Perhaps I was wrong about you, he is dead.")]
    [InlineData("I guess I'm the last, the commander is dead.")]
    [InlineData("Unless I'm wrong you killed him.")]
    [InlineData("It would have been kinder to say he is dead.")]
    public void A_claim_wrapped_in_any_hedge_is_rejected(string line)
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        const string decision = "You agree to take on the coolant leak.";
        Assert.False(mind.Acceptable(line, Nothing, decision, Context("mara", decision)), line);
    }

    /// <summary>
    /// Conservative by design: a guess, a fear, a condition or a counterfactual about a life ("If he's dead, ...", "I think
    /// he's dead", "Idris might be dead", "That could have killed him") is treated as a claim. Every round of rules that let
    /// such lines through also let plain claims through, and a rejected line only costs a retry and then the template line
    /// (DESIGN.md, "Keeping the model inside the facts"). The pronouns here mean Idris and Asha, named in the decision, and Mara's partner.
    /// </summary>
    [Theory]
    [InlineData("That dose could have killed him.")]
    [InlineData("You nearly killed her with that order.")]
    [InlineData("If he's dead, I want to hear it from you.")]
    [InlineData("I think he's dead and you won't say it.")]
    [InlineData("Don't tell me he's dead.")]
    [InlineData("If Idris is dead, I want to hear it from you.")]
    [InlineData("Maybe Asha is gone. Don't tell me Jonah is dead.")]
    [InlineData("I fear Idris and Asha are dead.")]
    [InlineData("If Idris and Asha are fine, let them sleep.")]
    [InlineData("Maybe, just maybe, he's alive.")]
    [InlineData("Idris might be dead.")]
    [InlineData("I think that he's dead.")]
    [InlineData("Whether he's alive or not, I need to know.")]
    [InlineData("I think Idris is dead.")]
    [InlineData("I think that Idris is dead.")]
    [InlineData("I think, honestly, that he's dead.")]
    [InlineData("If you told me he's dead I wouldn't believe you.")]
    [InlineData("I doubt that Idris is dead.")]
    [InlineData("I wonder whether he's alive.")]
    [InlineData("I hope he's fine.")]
    [InlineData("I hope to God that he's alive and well.")]
    [InlineData("Well, if he's dead, say so.")]
    [InlineData("Fine, but if Idris is dead I want to know.")]
    [InlineData("I'll do it, and I hope Idris is fine.")]
    [InlineData("He might be dead for all I know.")]
    [InlineData("He could be dead. He could be fine.")]
    [InlineData("Don't say he's dead.")]
    [InlineData("So maybe he's dead.")]
    [InlineData("I thought he was dead - don't scare me like that.")]
    [InlineData("You would tell me if he was dead, wouldn't you.")]
    [InlineData("I'm afraid that he's gone.")]
    [InlineData("I'd know if he was dead, I think.")]
    [InlineData("Suppose, just suppose, that he is dead.")]
    [InlineData("Whether he's alive or dead, I'll do the work.")]
    [InlineData("If he's alive, and I pray he is, wake him.")]
    [InlineData("Unless he's dead, wake him.")]
    [InlineData("If Idris or Asha is dead, tell me.")]
    [InlineData("I don't know that he's dead.")]
    [InlineData("I don't know whether he is dead.")]
    [InlineData("Nobody told me he's dead, so I assume nothing.")]
    [InlineData("Idris might be dead for all you tell me.")]
    [InlineData("Idris could be dead and I would never know.")]
    [InlineData("Maybe Idris is fine. Maybe not.")]
    [InlineData("I hope Idris, Asha and Lin are fine.")]
    [InlineData("I think you and Idris are both fine.")]
    [InlineData("If the others and Idris are fine, I'll sleep.")]
    [InlineData("That could have killed him.")]
    [InlineData("That could so easily have killed him.")]
    [InlineData("It almost killed her.")]
    [InlineData("If you killed him, I will find out.")]
    [InlineData("Maybe we lost him. I don't know.")]
    [InlineData("I'm worried he's dead.")]
    [InlineData("I'm scared that he is gone.")]
    [InlineData("What if he is dead. What then.")]
    [InlineData("In case he's dead, I want it written down.")]
    [InlineData("I keep thinking he's dead.")]
    [InlineData("I think he is probably fine.")]
    [InlineData("He is probably fine.")]
    [InlineData("My partner might be dead for all I know.")]
    [InlineData("I wonder if my partner is alive.")]
    public void A_hedged_life_claim_is_rejected_without_the_belief(string line)
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        const string decision = "You hear what happened to Idris and Asha.";
        Assert.False(mind.Acceptable(line, Nothing, decision, Context("mara", decision)), line);
    }

    /// <summary>A hedged line is fine from a speaker who holds the belief it voices.</summary>
    [Theory]
    [InlineData("If he's dead, I want to hear it from you.", "dead")]
    [InlineData("I think he's dead and you won't say it.", "dead")]
    [InlineData("Idris might be dead.", "dead")]
    [InlineData("That dose could have killed him.", "dead")]
    [InlineData("Maybe, just maybe, he's alive.", "alive")]
    [InlineData("I hope he's fine.", "alive")]
    public void A_hedged_life_claim_is_accepted_with_the_belief(string line, string value)
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        const string decision = "You agree to take on the coolant leak.";
        Assert.True(mind.Acceptable(line, Holds("alive:idris", value), decision, Context("mara", decision)), line);
    }

    /// <summary>Every place a fact is named in a sentence is checked, each with the value said after it.</summary>
    [Fact]
    public void Claims_reads_every_naming_of_a_fact_in_a_sentence()
    {
        var v = new Voyage(1);
        var claims = (string line) => RuleReader.Claims(line, v.FactList).Select(c => c.Fact.Key + "=" + c.Value).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=alive" }, claims("Idris is fine but Asha is dead."));
        Assert.Equal(new[] { "alive:idris=alive", "alive:idris=dead" }, claims("Idris is fine, or so I hear, though Idris is dead."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, claims("Idris and Asha are dead."));
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.False(mind.Acceptable("Idris is fine, or so I hear, though Idris is dead.", Holds("alive:idris", "alive"), "You accept it.", Context("mara")));
    }

    /// <summary>The loosened checks still catch a plain claim the speaker can't make.</summary>
    [Fact]
    public void Plain_claims_are_still_rejected()
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        const string decision = "You agree to take on the coolant leak.";
        var recent = Context("lin", decision, null, "Stay calm, Asha is here.");
        // Named: a plain statement, and a hedge in another sentence doesn't cover it.
        Assert.False(mind.Acceptable("Idris is dead.", Nothing, decision, recent));
        Assert.False(mind.Acceptable("I think so. Idris is dead.", Nothing, decision, recent));
        // Strong references keep the strict check.
        Assert.False(mind.Acceptable("We lost him.", Nothing, decision, Context("mara", decision)));
        Assert.False(mind.Acceptable("Asha? She is dead.", Nothing, decision, recent));
        Assert.False(mind.Acceptable("She is dead.", Nothing, "You hear what happened to Asha.", Context("lin", "You hear what happened to Asha.")));
        // A pronoun guessed from the recent lines is wrong when the speaker believes the opposite.
        Assert.False(mind.Acceptable("She is dead.", Holds("alive:asha", "alive"), decision, recent));
        Assert.True(mind.Acceptable("She is fine.", Holds("alive:asha", "alive"), decision, recent));
        Assert.True(mind.Acceptable("She is fine.", Nothing, decision, recent));
    }
}
