using Xunit;
using TheShip.AI;
using TheShip.Core;

namespace TheShip.Tests;

/// <summary>
/// Edges of the claim reader the validator found (fix/claim-reader-edges): cues matched inside words, a naming with no
/// value borrowing another naming's, and negations the reader missed.
/// </summary>
public class ClaimReaderEdgeTests
{
    private static readonly Dictionary<string, Belief> Nothing = new();

    private static Dictionary<string, Belief> Holds(params (string Key, string Value)[] beliefs) =>
        beliefs.ToDictionary(b => b.Key, b => new Belief(b.Value, "ship", 4));

    private static List<string> Claims(Voyage v, string line) =>
        RuleReader.Claims(line, v.FactList).Select(c => c.Fact.Key + "=" + c.Value).OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static LineContext Context(string speaker) => new(speaker, "You accept it.", null, Array.Empty<string>());

    // ---- 1. cues are words ----

    [Theory]
    [InlineData("The cooling line is dead.")]
    [InlineData("The cooling lines are fine.")]
    [InlineData("I'm feeling fine.")]
    [InlineData("The deadline is close.")]
    [InlineData("I'll dwell on it later.")]
    public void A_cue_inside_another_word_is_no_naming_and_no_value(string line)
    {
        var v = new Voyage(1);
        Assert.DoesNotContain(Claims(v, line), c => c.StartsWith("alive:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_name_inside_a_word_adds_no_claim_beside_the_real_one()
    {
        var v = new Voyage(1);
        Assert.Equal(new[] { "alive:idris=alive" }, Claims(v, "Idris is feeling fine."));
        Assert.Equal(new[] { "alive:lin=dead" }, Claims(v, "Lin is dead."));
        Assert.Equal(new[] { "alive:lin=alive" }, Claims(v, "Lin's fine."));
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.True(mind.Acceptable("The cooling line is dead.", Nothing, "You accept it.", Context("mara")));
        Assert.True(mind.Acceptable("Idris is feeling fine.", Holds(("alive:idris", "alive")), "You accept it.", Context("mara")));
    }

    [Fact]
    public void A_value_cue_inside_another_word_is_no_value_but_a_plain_ending_still_counts()
    {
        var v = new Voyage(1);
        Assert.Empty(Claims(v, "Idris has a deadline."));
        Assert.Equal(new[] { "pods=worn" }, Claims(v, "The pods are unsafe."));
        Assert.Equal(new[] { "earth=programme ended" }, Claims(v, "Earth abandoned us."));
        Assert.Equal(new[] { "earth=programme ended" }, Claims(v, "Earth cancelled the programme."));
    }

    // ---- 2. a naming with no value of its own ----

    /// <summary>
    /// A naming with no value in its own span is claimed with every value the sentence gives that fact, before or after it,
    /// each with its own negation and, when denied, both readings: the line passes only if all of them are right.
    /// </summary>
    [Fact]
    public void A_naming_with_no_value_of_its_own_is_claimed_with_every_value_in_its_sentence()
    {
        var v = new Voyage(1);
        Assert.Equal(new[] { "alive:asha=alive", "alive:asha=dead", "alive:idris=alive" }, Claims(v, "Idris isn't dead but Asha is."));
        Assert.Equal(new[] { "alive:jonah=alive", "alive:tomas=alive", "alive:tomas=dead" }, Claims(v, "Jonah isn't gone, Tomas is."));
        Assert.Equal(new[] { "alive:jonah=dead", "alive:tomas=dead" }, Claims(v, "Tomas is at the helm, Jonah is dead."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Idris said Asha is dead."));
        Assert.Empty(Claims(v, "Idris asked about Asha."));
    }

    [Theory]
    [InlineData("You killed Idris with that order.", "alive:idris=dead")]
    [InlineData("We lost Idris years ago.", "alive:idris=dead")]
    [InlineData("I can't believe we lost Idris to a pod fault.", "alive:idris=dead")]
    [InlineData("Dead Idris can't fix it.", "alive:idris=dead")]
    [InlineData("Poor dead Idris never knew.", "alive:idris=dead")]
    [InlineData("It was you who killed Asha in her sleep.", "alive:asha=dead")]
    [InlineData("We're on low power again.", "power=low")]
    [InlineData("Low power again.", "power=low")]
    [InlineData("The failing pods worry me.", "pods=worn")]
    [InlineData("No signal from Earth.", "earth=no word")]
    [InlineData("No word from Earth.", "earth=no word")]
    [InlineData("There's been no word from Earth.", "earth=no word")]
    [InlineData("The ship is off course.", "course=drifting")]
    [InlineData("Nobody is coming, Earth made that clear.", "earth=programme ended")]
    public void A_value_said_before_the_naming_is_read(string line, string claim)
    {
        var v = new Voyage(1);
        Assert.Contains(claim, Claims(v, line));
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.False(mind.Acceptable(line, Nothing, "You accept it.", Context("mara")), line);
        var (key, value) = (claim[..claim.IndexOf('=')], claim[(claim.IndexOf('=') + 1)..]);
        var other = v.FactList.First(f => f.Key == key).Values.First(x => x != value);
        Assert.False(mind.Acceptable(line, Holds((key, other)), "You accept it.", Context("mara")), line);
    }

    [Fact]
    public void A_true_ellipsis_shares_the_value()
    {
        var v = new Voyage(1);
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Asha is dead and so is Idris."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Idris and Asha are dead."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead", "alive:lin=dead" }, Claims(v, "Idris, Asha and Lin are dead."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Asha is dead, Idris too."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Asha is dead, Idris as well."));
        Assert.Equal(new[] { "alive:jonah=dead" }, Claims(v, "So we have lost Jonah as well."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Asha is dead. So is Idris."));
        Assert.Contains("alive:lin=dead", Claims(v, "Mara is not safe; Lin is."));
        Assert.Equal(new[] { "alive:idris=dead" }, Claims(v, "Dead, Idris is."));
        // A denied value repeated: both readings, so no speaker can pass it (a false reject, by design).
        Assert.Equal(new[] { "alive:asha=alive", "alive:idris=alive", "alive:idris=dead" }, Claims(v, "Asha isn't dead and neither is Idris."));
    }

    [Theory]
    [InlineData("Idris and the pods are fine.", new[] { "alive:idris=alive", "pods=sound" })]
    [InlineData("Idris, Asha, the pods and the power are fine.", new[] { "alive:asha=alive", "alive:idris=alive", "pods=sound", "power=normal" })]
    [InlineData("Idris and his pod are fine.", new[] { "alive:idris=alive", "pods=sound" })]
    [InlineData("Neither the pods nor the power are fine.", new[] { "pods=worn", "power=low" })]
    public void Namings_joined_through_an_article_or_possessive_share_the_value(string line, string[] claims)
    {
        var v = new Voyage(1);
        Assert.Equal(claims, Claims(v, line));
    }

    [Fact]
    public void A_borrowed_value_still_rejects_and_the_right_beliefs_still_pass()
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        // Conservative: Tomas is claimed dead too, so a speaker who believes him alive is rejected.
        Assert.False(mind.Acceptable("Tomas is at the helm, Jonah is dead.", Holds(("alive:jonah", "dead"), ("alive:tomas", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Jonah isn't gone, Tomas is.", Holds(("alive:jonah", "alive"), ("alive:tomas", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Asha is dead and so is Idris.", Holds(("alive:asha", "dead")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Asha is dead. So is Idris.", Holds(("alive:asha", "dead"), ("alive:idris", "alive")), "You accept it.", Context("mara")));
        Assert.True(mind.Acceptable("Asha is dead and so is Idris.", Holds(("alive:asha", "dead"), ("alive:idris", "dead")), "You accept it.", Context("mara")));
        Assert.True(mind.Acceptable("You killed Idris with that order.", Holds(("alive:idris", "dead")), "You accept it.", Context("mara")));
        Assert.True(mind.Acceptable("Idris and the pods are fine.", Holds(("alive:idris", "alive"), ("pods", "sound")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Idris and the pods are fine.", Holds(("alive:idris", "alive"), ("pods", "worn")), "You accept it.", Context("mara")));
    }

    [Theory]
    [InlineData("These pods are risky.", "pods=worn")]
    [InlineData("The lights are dimming.", "power=low")]
    [InlineData("The lights have dimmed.", "power=low")]
    [InlineData("We're arriving late.", "arrival=late")]
    public void A_plain_ending_on_a_cue_still_counts(string line, string claim)
    {
        var v = new Voyage(1);
        Assert.Equal(new[] { claim }, Claims(v, line));
    }

    // ---- 3. negations ----

    [Fact]
    public void A_negation_past_small_words_and_neither_nor_deny_the_value()
    {
        var v = new Voyage(1);
        Assert.Equal(new[] { "alive:idris=alive" }, Claims(v, "Idris is not at all dead."));
        Assert.Equal(new[] { "alive:idris=alive" }, Claims(v, "Idris is not really dead."));
        Assert.Equal(new[] { "alive:idris=dead" }, Claims(v, "Idris is by no means fine."));
        Assert.Equal(new[] { "alive:asha=alive", "alive:idris=alive" }, Claims(v, "Neither Idris nor Asha is dead."));
        Assert.Equal(new[] { "alive:idris=dead" }, Claims(v, "Idris is dead, not fine."));
        Assert.Equal(new[] { "alive:idris=alive" }, Claims(v, "Idris is neither dead nor dying."));
        Assert.Equal(new[] { "alive:idris=dead" }, Claims(v, "Idris is no longer alive."));
        Assert.Equal(new[] { "alive:idris=dead" }, Claims(v, "Idris won't be fine."));
        // "neither ... nor" ends at the clause.
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Neither Idris nor Asha is here, both are dead."));
        Assert.Equal(new[] { "alive:asha=alive", "alive:idris=alive", "alive:lin=dead" }, Claims(v, "Neither Idris nor Asha is dead, but Lin is dead."));
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.True(mind.Acceptable("Neither Idris nor Asha is dead.", Holds(("alive:idris", "alive"), ("alive:asha", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Neither Idris nor Asha is dead.", Holds(("alive:idris", "dead"), ("alive:asha", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Idris is not at all dead.", Holds(("alive:idris", "dead")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Neither Idris nor Asha is here, both are dead.", Holds(("alive:idris", "alive"), ("alive:asha", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Idris is no longer alive.", Holds(("alive:idris", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Asha is dead, Idris as well.", Holds(("alive:asha", "dead"), ("alive:idris", "alive")), "You accept it.", Context("mara")));
    }

    [Fact]
    public void The_keyword_reader_reads_the_same_negations()
    {
        var v = new Voyage(1);
        var r = RuleReader.Read("Idris is not at all dead.", v, Array.Empty<CrewMember>());
        Assert.Equal(("alive:idris", "alive"), (r.FactKey, r.Value));
        r = RuleReader.Read("The cooling line is dead.", v, Array.Empty<CrewMember>());
        Assert.NotEqual("alive:lin", r.FactKey);
    }
}
