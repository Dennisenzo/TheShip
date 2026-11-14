namespace TheShip.Game;

/// <summary>
/// The maths of soft lines (no MonoGame, so it is tested). A line of <c>T</c> device pixels is drawn as a quad <c>T + 2</c> device pixels
/// across, textured with its cross-section: full coverage in the middle and a one-pixel linear falloff at each edge (a pixel-wide box filter, so
/// the same amount of light falls on the screen at any angle). The texture is premultiplied white, so the sprite batch's tint is the line's colour.
/// Round caps and joins are a disc with the same falloff, and dashes are runs along a path measured in one distance so the pattern continues
/// round a corner.
/// </summary>
internal static class LineProfile
{
    /// <summary>Texels per device pixel across the profile (linear sampling blends them).</summary>
    public const int Resolution = 8;

    /// <summary>Extra device pixels either side of the line that the quad covers, so the falloff and its zero edge fit.</summary>
    public const int Margin = 1;

    /// <summary>Coverage 0..1 of a pixel centred <paramref name="distance"/> from the middle of a line <paramref name="width"/> pixels wide.</summary>
    public static float Coverage(float distance, float width) => Math.Clamp(width / 2f - Math.Abs(distance) + 0.5f, 0f, 1f);

    /// <summary>The size in device pixels of the quad (and of the square of a disc) for a line of <paramref name="width"/> pixels.</summary>
    public static int Extent(int width) => width + 2 * Margin;

    /// <summary>
    /// Texels across a quad: odd, so one texel sits exactly on the middle of the line and the profile peaks at full coverage there
    /// (an even count would put the middle between two texels and a one-pixel line would peak at 94%).
    /// </summary>
    public static int Texels(int width) => Extent(width) * Resolution + 1;

    /// <summary>The cross-section of a line <paramref name="width"/> device pixels wide: <see cref="Texels"/> coverages, from one edge to the other.</summary>
    public static float[] Cross(int width)
    {
        var n = Texels(width);
        var data = new float[n];
        for (var i = 0; i < n; i++)
            data[i] = Coverage(((i + 0.5f) / n - 0.5f) * Extent(width), width);
        return data;
    }

    /// <summary>Coverage of a disc of <paramref name="width"/> pixels across, <see cref="Texels"/> texels square, row by row (a round cap or join).</summary>
    public static float[] Disc(int width)
    {
        var n = Texels(width);
        var data = new float[n * n];
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var dx = ((x + 0.5f) / n - 0.5f) * Extent(width);
            var dy = ((y + 0.5f) / n - 0.5f) * Extent(width);
            data[y * n + x] = Coverage(MathF.Sqrt(dx * dx + dy * dy), width);
        }
        return data;
    }

    /// <summary>
    /// The widest disc (in device pixels) given a texture of its own, a cap or join of an ordinary line. Wider discs (the planet, a thick dot at a large
    /// window) are drawn from one shared <see cref="BigDiscTexels"/> texture scaled by the batch, so no texture is built per window size.
    /// </summary>
    public const int MaxOwnDisc = 32;

    /// <summary>Texels across the shared texture of wide discs.</summary>
    public const int BigDiscTexels = 256;

    /// <summary>Whether a disc of <paramref name="width"/> device pixels gets its own exact texture (else the shared big one, scaled).</summary>
    public static bool OwnDisc(int width) => width <= MaxOwnDisc;

    /// <summary>
    /// The shared wide disc: <paramref name="n"/> texels square, a disc <c>n - 2</c> texels across with a one-texel falloff (premultiplied coverage, row by
    /// row). Scaled up, the edge falls off over about a device pixel.
    /// </summary>
    public static float[] BigDisc(int n = BigDiscTexels)
    {
        var data = new float[n * n];
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var dx = x + 0.5f - n / 2f;
            var dy = y + 0.5f - n / 2f;
            data[y * n + x] = Coverage(MathF.Sqrt(dx * dx + dy * dy), n - 2);
        }
        return data;
    }

    /// <summary>
    /// The runs of a dash pattern that fall on a segment of <paramref name="length"/>: dashes of length <paramref name="dash"/>, <paramref name="gap"/>
    /// apart, one starting at <paramref name="shift"/> (any value, taken modulo the period). Each run is shortened by <paramref name="cap"/> at both
    /// ends, since a round end adds that much (a run shorter than that becomes a dot at its middle, <c>From == To</c>). Runs are cut at the ends of the segment.
    /// </summary>
    public static List<(float From, float To)> Dashes(float length, float dash, float gap, float shift, float cap = 0f)
    {
        var runs = new List<(float From, float To)>();
        var period = dash + gap;
        if (length <= 0f || dash <= 0f || period <= 0f)
            return runs;
        var first = shift % period;
        if (first > 0f)
            first -= period;
        for (var at = first; at < length; at += period)
        {
            var from = Math.Max(0f, at);
            var to = Math.Min(length, at + dash);
            if (to <= from)
                continue;
            // Only a run that is whole at an end of the dash keeps its cut end flat to the segment; a trimmed dash shrinks by the cap.
            var a = from + (at >= 0f ? cap : 0f);
            var b = to - (at + dash <= length ? cap : 0f);
            if (b < a)
                a = b = (from + to) / 2f;
            runs.Add((a, b));
        }
        return runs;
    }
}
