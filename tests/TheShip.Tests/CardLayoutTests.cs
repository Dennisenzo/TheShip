using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>The cards' pure layout rules (GRAPHICS-PLAN.md step 6): where text is cut, the crew column's heights, the thin meters.</summary>
public class CardLayoutTests
{
    [Theory]
    [InlineData("Cause: the navigation drift is a drifting star tracker.", 24, "Cause: the navigation...")]
    [InlineData("Short enough", 20, "Short enough")]
    [InlineData("Exactly twelve", 14, "Exactly twelve")]
    [InlineData("a seal, then more words", 12, "a seal...")]
    [InlineData("Supercalifragilistic words", 12, "Supercali...")]
    public void A_text_is_cut_at_a_word_with_an_ellipsis_and_never_with_two_dots(string text, int max, string expected)
    {
        var cut = CardLayout.CutAtWord(text, max);
        Assert.Equal(expected, cut);
        Assert.True(cut.Length <= max);
        Assert.DoesNotContain("..", cut.Replace("...", ""));
    }

    [Fact]
    public void A_cut_never_ends_a_word_in_the_middle_when_a_word_boundary_fits()
    {
        var text = "Whoever works here learns the coolant is failing again";
        for (var max = 10; max < text.Length; max++)
        {
            var cut = CardLayout.CutAtWord(text, max);
            Assert.True(cut.Length <= max, cut);
            Assert.EndsWith("...", cut);
            var head = cut[..^3];
            Assert.True(text.StartsWith(head));
            // The next character in the text is a space: the cut is on a word boundary.
            Assert.True(head.Length == text.Length || text[head.Length] is ' ' or ',' or '.', $"\"{cut}\" from max {max}");
        }
    }

    [Fact]
    public void A_long_text_is_cut_to_the_lines_it_has()
    {
        var text = "Pod bank A. Needs engineering 2, 2 people, 12 h. If the clock runs out: the pods lose power and everyone in them is lost.";
        foreach (var lines in new[] { 1, 2 })
        {
            var cut = CardLayout.CutToLines(text, 42, lines);
            Assert.True(TextLayout.Wrap(cut, 42).Count <= lines, cut);
            Assert.True(CardLayout.NeedsCut(text, 42, lines));
            Assert.False(CardLayout.NeedsCut(cut, 42, lines));
            Assert.EndsWith("...", cut);
        }
        Assert.Equal("Short.", CardLayout.CutToLines("Short.", 42, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_crew_column_fits_six_people_with_zero_to_six_dead(bool waitingEmpty)
    {
        // From the first card (y 100) to the bottom of the column (y 884).
        const int available = 884 - 100;
        var previous = 0;
        for (var dead = 0; dead <= 6; dead++)
        {
            var living = 6 - dead;
            var col = CardLayout.Crew(available, living, dead, waitingEmpty);
            var cards = living * (col.CardHeight + CardLayout.CrewGap) + dead * (CardLayout.StripHeight + CardLayout.CrewGap);
            Assert.True(cards + 4 + col.WaitingHeight <= available, $"{dead} dead: {cards} + {col.WaitingHeight} > {available}");
            Assert.True(col.WaitingHeight >= (waitingEmpty ? CardLayout.WaitingStrip : CardLayout.WaitingMin));
            if (living > 0)
            {
                Assert.InRange(col.CardHeight, CardLayout.CardMin, waitingEmpty ? CardLayout.CardMaxEmpty : CardLayout.CardMaxWithItems);
                // Dead strips give the living cards room, never take it.
                Assert.True(col.CardHeight >= previous);
                previous = col.CardHeight;
            }
        }
    }

    [Fact]
    public void An_empty_waiting_panel_is_a_strip_and_the_cards_get_its_room()
    {
        var with = CardLayout.Crew(784, 6, 0, waitingEmpty: false);
        var without = CardLayout.Crew(784, 6, 0, waitingEmpty: true);
        Assert.Equal(CardLayout.WaitingStrip, without.WaitingHeight);
        Assert.True(without.CardHeight >= with.CardHeight);
        Assert.True(with.WaitingHeight > without.WaitingHeight);
    }

    [Fact]
    public void Meter_segments_follow_the_bands_and_fill_with_the_value()
    {
        double[] bands = { 0, 15, 30, 60, 100 };
        var segs = CardLayout.Segments(150, 2, bands, 45);
        Assert.Equal(4, segs.Count);
        Assert.Equal(150, segs[^1].X + segs[^1].Width);
        for (var i = 1; i < segs.Count; i++)
            Assert.Equal(segs[i - 1].X + segs[i - 1].Width + 2, segs[i].X);
        Assert.Equal(segs[0].Width, segs[0].Fill);
        Assert.Equal(segs[1].Width, segs[1].Fill);
        Assert.InRange(segs[2].Fill, 1, segs[2].Width - 1);
        Assert.Equal(0, segs[3].Fill);
        Assert.All(CardLayout.Segments(150, 2, bands, 0), s => Assert.Equal(0, s.Fill));
        Assert.All(CardLayout.Segments(150, 2, bands, 100), s => Assert.Equal(s.Width, s.Fill));
        Assert.Equal(10, CardLayout.Segments(157, 2, new double[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }, 50).Count);
    }
}
