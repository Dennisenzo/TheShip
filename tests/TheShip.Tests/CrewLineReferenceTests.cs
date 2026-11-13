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
    // Validator findings (fix/read-timeout-and-pronoun-false-rejects): a possessive or a counterfactual is no death,
    // "she" for a machine is no crewmate, and a fear, a condition or a guess states nothing.
    [InlineData("That dose could have killed him.")]
    [InlineData("You nearly killed her with that order.")]
    [InlineData("You lost her trust a long time ago.")]
    [InlineData("She's fine now. The pump will hold.")]
    [InlineData("She's holding.")]
    [InlineData("She's well within tolerance.")]
    [InlineData("She has gone quiet on me.")]
    [InlineData("She is still asleep, leave her.")]
    [InlineData("If he's dead, I want to hear it from you.")]
    [InlineData("I think he's dead and you won't say it.")]
    [InlineData("Don't tell me he's dead.")]
    [InlineData("He's gone back to sleep, then. Good.")]
    [InlineData("If Idris is dead, I want to hear it from you.")]
    [InlineData("Maybe Asha is gone. Don't tell me Jonah is dead.")]
    public void Ordinary_lines_are_not_rejected_by_the_reference_check(string line)
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        foreach (var speaker in new[] { "mara", "lin", "tomas" })
            Assert.True(mind.Acceptable(line, Nothing, "You agree to take on the coolant leak.",
                Context(speaker, "You agree to take on the coolant leak.", null, "Idris is in his pod.", "Stay calm, Asha is here.")), speaker + ": " + line);
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
