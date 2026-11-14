using Xunit;
using TheShip.Game;

namespace TheShip.Tests;

/// <summary>Drawing at the window's real size: snapping to device pixels, the font size for a scale, the character grid, the person glyph.</summary>
public class PixelGridTests
{
    [Theory]
    [InlineData(0.8f)]
    [InlineData(1f)]
    [InlineData(1.1f)]
    [InlineData(1.2f)]
    [InlineData(1.6f)]
    [InlineData(2.5f)]
    public void A_one_pixel_line_is_one_device_pixel_wide_at_every_scale_and_position(float scale)
    {
        for (var x = 0; x < 1600; x += 7)
        {
            var (start, end) = PixelGrid.Span(x, x + 1f / scale, scale, 0.5f);
            Assert.Equal(1, end - start);
        }
    }

    [Fact]
    public void Thickness_is_the_scaled_width_and_never_below_one()
    {
        Assert.Equal(1, PixelGrid.Thickness(1, 0.4f));
        Assert.Equal(2, PixelGrid.Thickness(1, 1.6f));
        Assert.Equal(3, PixelGrid.Thickness(2, 1.6f));
        Assert.Equal(2, PixelGrid.Thickness(1.5f, 1f));
    }

    [Fact]
    public void A_span_with_width_is_never_thinner_than_a_pixel_and_an_empty_one_stays_empty()
    {
        var (a, b) = PixelGrid.Span(10.1f, 10.2f, 1f, 0f);
        Assert.Equal(1, b - a);
        var (c, d) = PixelGrid.Span(10f, 10f, 1f, 0f);
        Assert.Equal(c, d);
    }

    [Fact]
    public void Adjacent_spans_share_their_edge_so_there_is_no_gap_or_overlap()
    {
        foreach (var scale in new[] { 0.8f, 1.2f, 1.6f, 1.333f })
            for (var x = 0; x < 1000; x += 13)
            {
                var (_, firstEnd) = PixelGrid.Span(x, x + 13, scale, 3.5f);
                var (secondStart, _) = PixelGrid.Span(x + 13, x + 26, scale, 3.5f);
                Assert.Equal(firstEnd, secondStart);
            }
    }

    [Fact]
    public void A_device_edge_maps_back_to_layout_and_forth()
    {
        foreach (var scale in new[] { 0.8f, 1.2f, 1.6f })
            for (var d = 0; d < 2560; d += 37)
                Assert.Equal(d, PixelGrid.Device(PixelGrid.Layout(d, scale, 7.5f), scale, 7.5f));
    }

    [Theory]
    [InlineData(15, 1f, 15)]
    [InlineData(15, 1.2f, 18)]
    [InlineData(15, 1.6f, 24)]
    [InlineData(17, 1.6f, 27)]
    [InlineData(22, 1.6f, 35)]
    [InlineData(32, 1.6f, 51)]
    [InlineData(15, 0.8f, 12)]
    [InlineData(15, 1.1f, 16)]
    public void The_font_size_follows_the_scale_leaning_down(int size, float scale, int expected) =>
        Assert.Equal(expected, PixelGrid.FontPixels(size, scale));

    [Fact]
    public void A_font_is_never_much_larger_than_the_layout_expects_and_never_tiny()
    {
        foreach (var size in new[] { 15, 17, 22, 32 })
            for (var scale = 0.3f; scale < 3.5f; scale += 0.013f)
            {
                var pixels = PixelGrid.FontPixels(size, scale);
                Assert.True(pixels <= size * scale + 0.25f + 1e-3f || pixels == 6, size + " at " + scale);
                Assert.True(pixels >= 6);
            }
    }

    [Fact]
    public void Characters_sit_on_the_layout_grid_so_a_line_is_as_wide_as_the_layout_says()
    {
        // The fonts' own advance is a whole pixel (7 at 15 px, 9 at 18 px); the layout's is 7. At 1.2 the grid is 8.4 px.
        const float layoutAdvance = 7f;
        foreach (var scale in new[] { 0.8f, 1.2f, 1.6f })
            for (var columns = 1; columns <= 160; columns += 9)
            {
                var start = 100.3f;
                var end = PixelGrid.GlyphX(start, columns, layoutAdvance, scale);
                Assert.True(Math.Abs(end - (start + columns * layoutAdvance * scale)) <= 0.5f);
            }
        // Steps are 8 or 9 device pixels at 1.2, never more.
        for (var i = 0; i < 100; i++)
        {
            var step = PixelGrid.GlyphX(0, i + 1, 7f, 1.2f) - PixelGrid.GlyphX(0, i, 7f, 1.2f);
            Assert.InRange(step, 8, 9);
        }
    }

    [Fact]
    public void A_run_is_one_call_only_when_the_baked_advance_is_the_layouts()
    {
        Assert.True(PixelGrid.AdvanceMatches(7, 7, 1f));
        Assert.False(PixelGrid.AdvanceMatches(9, 7, 1.2f));
        Assert.True(PixelGrid.AdvanceMatches(14, 7, 2f));
    }

    [Fact]
    public void The_person_glyph_is_soft_symmetric_and_shaped_like_a_head_over_shoulders()
    {
        var data = PersonGlyph.Coverage();
        const int size = PersonGlyph.Size * PersonGlyph.Scale;
        Assert.Equal(size * size, data.Length);
        Assert.All(data, c => Assert.InRange(c, 0f, 1f));
        float At(int x, int y) => data[y * size + x];
        // Mirror image about the vertical centre.
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size / 2; x++)
                Assert.Equal(At(x, y), At(size - 1 - x, y), 3);
        // The head's top only grazes the first row (a soft edge), its middle is full, the corners are empty.
        Assert.InRange(At(size / 2, 0), 0.01f, 0.5f);
        Assert.Equal(1f, At(size / 2, (int)(3.4f * PersonGlyph.Scale)));
        Assert.Equal(0f, At(0, 0));
        Assert.Equal(0f, At(0, size - 1));
        // The shoulders fill the base across their 12-unit width, and the edge has partial coverage (a soft edge).
        Assert.Equal(1f, At(size / 2, size - 1));
        Assert.Contains(data, c => c is > 0f and < 1f);
        // The gap between head and shoulders (a neck) is narrower than the shoulders.
        var neck = Enumerable.Range(0, size).Count(x => At(x, (int)(6.9f * PersonGlyph.Scale)) > 0.5f);
        var shoulders = Enumerable.Range(0, size).Count(x => At(x, size - 1) > 0.5f);
        Assert.True(neck < shoulders);
    }

    [Fact]
    public void Wrapped_long_log_lines_fit_their_width_at_every_scale()
    {
        // The log is 1000 units wide with 14 units of margin each side; the small font's layout advance is 7.
        const float advance = 7f;
        const int width = 1000 - 28;
        var text = string.Join(" ", Enumerable.Range(0, 60).Select(i => new string((char)('a' + i % 26), 1 + i % 11)));
        var columns = (int)(width / advance);
        foreach (var scale in new[] { 0.8f, 1.2f, 1.6f })
            foreach (var line in TextLayout.Wrap(text, columns))
            {
                var last = PixelGrid.GlyphX(0, Math.Max(0, line.Length - 1), advance, scale);
                // The last character starts inside the box and a character ends within one advance of its start.
                Assert.True(last + advance * scale <= width * scale + 1f, "scale " + scale + ": " + line.Length + " columns");
            }
    }
}
