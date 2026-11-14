using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>The drawing kit's text rules (<see cref="TextLayout"/>, linked from the Game project).</summary>
public class TextLayoutTests
{
    [Fact]
    public void A_line_break_is_kept_and_starts_a_new_line()
    {
        Assert.Equal("a\nb", TextLayout.Printable("a\nb"));
        Assert.Equal("a\nb", TextLayout.Printable("a\r\nb"));
        var lines = TextLayout.Wrap("Mara starts the repair when: [ ] answer: How is Idris?\nAnswer: true costs 6 trust.\nYour word: wake Idris.", 45);
        Assert.Equal(new[] { "Mara starts the repair when: [ ] answer: How", "is Idris?", "Answer: true costs 6 trust.", "Your word: wake Idris." }, lines);
        Assert.DoesNotContain(lines, l => l.Contains('\n'));
    }

    [Fact]
    public void A_blank_line_between_paragraphs_is_an_empty_row()
    {
        Assert.Equal(new[] { "First.", "", "Second." }, TextLayout.Wrap("First.\n\nSecond.", 44));
    }

    [Fact]
    public void Characters_the_font_lacks_are_mapped_or_replaced()
    {
        Assert.Equal("'a' \"b\" - ... x", TextLayout.Printable("‘a’ “b” — …\tx"));
        Assert.Equal("?", TextLayout.Printable("\u0007"));
        Assert.Equal("?", TextLayout.Printable("一"));
        // A non-breaking space keeps "12 h" together and is drawn as a space.
        Assert.Equal(new[] { "abcdef", "12 h" }, TextLayout.Wrap("abcdef 12 h", 8));
    }
}
