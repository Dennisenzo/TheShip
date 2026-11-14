using Xunit;
using TheShip.Game;

namespace TheShip.Tests;

/// <summary>The maths of soft lines: the cross-section, the round disc, and dash runs that continue round corners.</summary>
public class LineProfileTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void The_cross_section_is_symmetric_full_in_the_middle_and_zero_at_both_edges(int width)
    {
        var c = LineProfile.Cross(width);
        Assert.Equal(LineProfile.Texels(width), c.Length);
        for (var i = 0; i < c.Length; i++)
            Assert.Equal(c[i], c[c.Length - 1 - i], 5);
        Assert.Equal(0f, c[0]);
        Assert.Equal(1f, c[c.Length / 2]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void The_cross_section_carries_as_much_light_as_the_line_is_wide(int width)
    {
        // Integrated over the quad's extent the coverage equals the width: a soft line is as heavy as a hard one of the same thickness.
        var c = LineProfile.Cross(width);
        var area = c.Sum() * LineProfile.Extent(width) / c.Length;
        Assert.Equal(width, area, 1);
    }

    [Fact]
    public void The_falloff_is_one_pixel_wide_and_linear()
    {
        Assert.Equal(1f, LineProfile.Coverage(0f, 2f));
        Assert.Equal(1f, LineProfile.Coverage(0.5f, 2f));
        Assert.Equal(0.5f, LineProfile.Coverage(1f, 2f), 5);
        Assert.Equal(0f, LineProfile.Coverage(1.5f, 2f));
        Assert.Equal(0.75f, LineProfile.Coverage(0.75f, 2f), 5);
    }

    [Fact]
    public void A_disc_is_round_symmetric_and_as_thick_as_the_line_through_its_middle()
    {
        const int width = 4;
        var d = LineProfile.Disc(width);
        var n = LineProfile.Texels(width);
        Assert.Equal(n * n, d.Length);
        // The middle row is the line's cross-section.
        var cross = LineProfile.Cross(width);
        for (var x = 0; x < n; x++)
            Assert.Equal(cross[x], d[n / 2 * n + x], 4);
        // Symmetric in both axes and the diagonal, empty in the corners.
        Assert.Equal(d[3 * n + 7], d[7 * n + 3], 5);
        Assert.Equal(d[3 * n + 7], d[(n - 1 - 3) * n + 7], 5);
        Assert.Equal(0f, d[0]);
    }

    [Fact]
    public void Dashes_start_at_the_shift_and_repeat_every_period()
    {
        var runs = LineProfile.Dashes(30f, 7f, 5f, 0f);
        Assert.Equal(new[] { (0f, 7f), (12f, 19f), (24f, 30f) }, runs);
        var moved = LineProfile.Dashes(30f, 7f, 5f, 3f);
        Assert.Equal((3f, 10f), moved[0]);
        Assert.Equal((0f, 1f), LineProfile.Dashes(30f, 7f, 5f, -6f)[0]);
    }

    [Fact]
    public void The_pattern_continues_across_a_corner()
    {
        // Two segments of 10 and 10: the dashes of the second start where the pattern of a path of 20 puts them.
        var whole = LineProfile.Dashes(20f, 3f, 4f, 0f);
        var first = LineProfile.Dashes(10f, 3f, 4f, 0f);
        var second = LineProfile.Dashes(10f, 3f, 4f, 0f - 10f);
        var joined = first.Concat(second.Select(r => (From: r.From + 10f, To: r.To + 10f))).ToList();
        Assert.Equal(whole.Count, joined.Count);
        for (var i = 0; i < whole.Count; i++)
        {
            Assert.Equal(whole[i].From, joined[i].From, 4);
            Assert.Equal(whole[i].To, joined[i].To, 4);
        }
    }

    [Fact]
    public void Round_ends_shorten_a_whole_dash_but_not_one_cut_by_the_segment()
    {
        var runs = LineProfile.Dashes(30f, 7f, 5f, -3f, 1f);
        Assert.Equal((0f, 3f), runs[0]);      // cut at the start of the segment: stays, only its far end is trimmed
        Assert.Equal((10f, 15f), runs[1]);    // whole: both ends trimmed
        Assert.Equal((22f, 27f), runs[2]);    // whole
        var end = LineProfile.Dashes(30f, 7f, 5f, 2f, 1f);
        Assert.Equal((27f, 30f), end[^1]);    // cut at the end of the segment: stays flat there
    }

    [Fact]
    public void A_dash_shorter_than_its_caps_becomes_a_dot_in_its_middle()
    {
        var runs = LineProfile.Dashes(30f, 2f, 8f, 5f, 1.5f);
        Assert.Equal(runs[0].From, runs[0].To);
        Assert.Equal(6f, runs[0].From, 4);
    }
}
