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

    [Fact]
    public void A_naming_with_no_value_of_its_own_borrows_no_other_namings_value()
    {
        var v = new Voyage(1);
        Assert.Equal(new[] { "alive:idris=alive" }, Claims(v, "Idris isn't dead but Asha is."));
        Assert.Equal(new[] { "alive:jonah=dead" }, Claims(v, "Tomas is at the helm, Jonah is dead."));
        Assert.Equal(new[] { "alive:asha=dead" }, Claims(v, "Idris said Asha is dead."));
        Assert.Empty(Claims(v, "Idris asked about Asha."));
    }

    [Fact]
    public void A_true_ellipsis_shares_the_value()
    {
        var v = new Voyage(1);
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Asha is dead and so is Idris."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Idris and Asha are dead."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead", "alive:lin=dead" }, Claims(v, "Idris, Asha and Lin are dead."));
        Assert.Equal(new[] { "alive:asha=dead", "alive:idris=dead" }, Claims(v, "Asha is dead, Idris too."));
        Assert.Equal(new[] { "alive:asha=alive", "alive:idris=alive" }, Claims(v, "Asha isn't dead and neither is Idris."));
        Assert.Equal(new[] { "alive:idris=dead" }, Claims(v, "Dead, Idris is."));
    }

    [Fact]
    public void A_borrowed_value_no_longer_rejects_a_line_the_speaker_may_say()
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.True(mind.Acceptable("Tomas is at the helm, Jonah is dead.", Holds(("alive:jonah", "dead"), ("alive:tomas", "alive")), "You accept it.", Context("mara")));
        Assert.True(mind.Acceptable("Idris said Asha is dead.", Holds(("alive:asha", "dead"), ("alive:idris", "alive")), "You accept it.", Context("mara")));
        // The plain claim in it still counts.
        Assert.False(mind.Acceptable("Idris said Asha is dead.", Holds(("alive:idris", "alive")), "You accept it.", Context("mara")));
        // A true ellipsis is still a claim about both.
        Assert.False(mind.Acceptable("Asha is dead and so is Idris.", Holds(("alive:asha", "dead")), "You accept it.", Context("mara")));
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
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        Assert.True(mind.Acceptable("Neither Idris nor Asha is dead.", Holds(("alive:idris", "alive"), ("alive:asha", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Neither Idris nor Asha is dead.", Holds(("alive:idris", "dead"), ("alive:asha", "alive")), "You accept it.", Context("mara")));
        Assert.False(mind.Acceptable("Idris is not at all dead.", Holds(("alive:idris", "dead")), "You accept it.", Context("mara")));
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
