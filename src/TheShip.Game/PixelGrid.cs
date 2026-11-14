namespace TheShip.Game;

/// <summary>
/// The pure rules of drawing at the window's real size (no MonoGame, so they are unit-tested). The interface is laid out in a
/// 1600 x 900 space; the window shows it at a scale <c>s</c> with its top-left at an origin. A rectangle or a text position in layout
/// units is turned into whole device pixels here, so a "1 px" frame is one device pixel at every scale and text starts on a pixel.
/// </summary>
internal static class PixelGrid
{
    /// <summary>The fonts are re-baked only after the scale has been still for this long (a window drag changes it every frame).</summary>
    public const double FontSettleSeconds = 0.3;

    /// <summary>Round half up: the same answer for every sign, unlike banker's rounding.</summary>
    public static int Round(float value) => (int)MathF.Floor(value + 0.5f);

    /// <summary>The device pixel at a layout coordinate.</summary>
    public static int Device(float layout, float scale, float origin) => Round(layout * scale + origin);

    /// <summary>The device pixels a layout span covers, [Start, End): each edge rounded, and a span with width never thinner than 1 px.</summary>
    public static (int Start, int End) Span(float from, float to, float scale, float origin)
    {
        var start = Device(from, scale, origin);
        var end = Device(to, scale, origin);
        if (end <= start && to > from)
            end = start + 1;
        return (start, end);
    }

    /// <summary>A line's thickness in device pixels: at least 1.</summary>
    public static int Thickness(float layout, float scale) => Math.Max(1, Round(layout * scale));

    /// <summary>The layout coordinate of a device pixel edge (what to give the sprite batch so its transform lands exactly on it).</summary>
    public static float Layout(int device, float scale, float origin) => (device - origin) / scale;

    /// <summary>
    /// The pixel size to bake a font of layout size <paramref name="size"/> at for a view scale: the exact size, rounded with a lean
    /// downwards (a quarter pixel), so a glyph is never more than a quarter pixel taller than the layout expects.
    /// </summary>
    public static int FontPixels(float size, float scale) => Math.Max(6, (int)MathF.Floor(size * scale + 0.25f));

    /// <summary>
    /// Where character <paramref name="index"/> of a run starts, in device pixels. The fonts keep a whole-pixel advance per character,
    /// which at another size differs from the layout's advance (7 px at 15 px, 9 at 18, not 8.4); every character is therefore put
    /// on the layout's grid, <paramref name="layoutAdvance"/> x scale per character, so a line is as wide as the layout assumes.
    /// </summary>
    public static int GlyphX(float startDevice, int index, float layoutAdvance, float scale) => Round(startDevice + index * layoutAdvance * scale);

    /// <summary>True when a whole run can be drawn in one call: the baked font's advance already is the layout's, to within a thousandth of a pixel.</summary>
    public static bool AdvanceMatches(float bakedAdvance, float layoutAdvance, float scale) => Math.Abs(bakedAdvance - layoutAdvance * scale) < 0.001f;
}
