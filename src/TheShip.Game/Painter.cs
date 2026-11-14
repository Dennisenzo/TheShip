using System.Text;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheShip.Game;

/// <summary>
/// The drawing kit of the interface: lines, outlines, panels, text and buttons on a white pixel, in the wireframe look
/// of the interface design. Everything is laid out in a 1600 x 900 space that the game scales to the window.
/// </summary>
internal sealed class Painter
{
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 900;

    // ---- The palette and what each colour means ----
    // Surfaces:  Ground = the window; Panel = a boxed panel (with a 1 px inner top highlight, see Box); Room = a ship section's fill;
    //            Track = the empty part of a meter and a hovered button; Edge = a panel's frame and a divider, also a selected button's fill.
    // Lines:     Hull and Faint = the ship's hull and mesh, decoration only (Faint is below 3:1 on a panel: never use it for text);
    //            Wire = a section outline, a frame that is live (an awake card, an outline button); Power = the power bus.
    // Text:      Dim = labels and secondary text; Ink = ordinary text, including ordinary SHIP lines; Bright = values, names, what you
    //            said, a hovered or selected thing. Blue = a crew member's spoken line, and the coolant lines.
    // Meaning:   Accent (amber) = "this needs you": an alert, a fault that is open, a question waiting, the next step, a trust or
    //            morale change, a LIE or PROMISE tag, the drift line, a locked door, a lockout; a solid amber button is the next step.
    //            Danger (red-orange) = harm: a death, a fault in the last quarter of its clock, a failed pod. Used sparingly,
    //            never for decoration. Dead = a dead person's pod and glyph (a dead card's text is Dim; Dead is readable, about 4.7:1).
    public static readonly Color Ground = new(11, 17, 20);
    public static readonly Color Panel = new(18, 28, 33);
    public static readonly Color Room = new(18, 30, 36);
    public static readonly Color Edge = new(35, 53, 61);
    public static readonly Color Track = new(27, 42, 48);
    public static readonly Color Faint = new(40, 64, 73);
    public static readonly Color Hull = new(44, 70, 80);
    public static readonly Color Wire = new(108, 151, 164);
    public static readonly Color Dim = new(157, 182, 189);
    public static readonly Color Ink = new(213, 227, 230);
    public static readonly Color Bright = new(234, 243, 245);
    public static readonly Color Accent = new(242, 176, 76);
    public static readonly Color Danger = new(238, 104, 78);
    public static readonly Color Blue = new(124, 196, 232);
    public static readonly Color Power = new(140, 122, 78);
    public static readonly Color Dead = new(110, 134, 143);

    /// <summary>The colour of a log line's tone (<see cref="LogTones.Of"/>).</summary>
    public static Color LogColor(LogTone tone) => tone switch
    {
        LogTone.Needs => Accent,
        LogTone.Danger => Danger,
        LogTone.You => Bright,
        LogTone.Crew => Blue,
        LogTone.Recorded => Dim,
        _ => Ink,
    };

    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;
    private readonly FontSystem _fonts;
    /// <summary>The layout fonts (what callers hold and measure with) and what is baked for each at the current scale.</summary>
    private readonly Dictionary<SpriteFontBase, FontFamily> _families = new();
    private sealed record FontFamily(int Size, float Advance, Dictionary<int, (SpriteFontBase Font, float Advance)> Baked);
    private static readonly string[] OneChar = Enumerable.Range(0, 256).Select(i => ((char)i).ToString()).ToArray();

    /// <summary>
    /// How the 1600 x 900 layout sits on the device: the view scale and the device position of the layout's top-left corner. Set each
    /// frame by <see cref="SetView"/>; <see cref="Fill"/>, <see cref="Frame"/> and text snap to whole device pixels with them.
    /// </summary>
    public float Scale { get; private set; } = 1f;
    public float OriginX { get; private set; }
    public float OriginY { get; private set; }
    /// <summary>The scale the fonts are baked for: <see cref="Scale"/>, except for a moment after it changed (a window drag).</summary>
    private float _fontScale = 1f;
    private double _scaleChangedAt = double.NegativeInfinity;

    public SpriteFontBase Small { get; }
    public SpriteFontBase Body { get; }
    public SpriteFontBase Head { get; }
    public SpriteFontBase Big { get; }
    public float SmallWidth { get; }
    public float BodyWidth { get; }
    public float HeadWidth { get; }

    /// <summary>The mouse in the 1600 x 900 space, and whether the left button was released since the last frame drawn.</summary>
    public Vector2 Mouse { get; set; }
    public bool Click { get; set; }
    /// <summary>The left button is held down.</summary>
    public bool Down { get; set; }
    /// <summary>Seconds since the game started, and seconds since the last frame drawn.</summary>
    public double Now { get; set; }
    public float Delta { get; set; }
    /// <summary>Set during a frame when the mouse is over something clickable: the game shows the hand cursor.</summary>
    public bool Hand { get; set; }
    /// <summary>Set during a frame when a disabled button was clicked: the game plays the refusal tone.</summary>
    public bool Denied { get; set; }

    private readonly Dictionary<string, float> _eased = new();
    private readonly HashSet<string> _easedThisFrame = new();
    private string? _tip;
    private Rectangle _tipRect;
    private Rectangle _tipLastRect;
    private double _tipSince;

    public Painter(SpriteBatch batch, Texture2D pixel, FontSystem fonts)
    {
        _batch = batch;
        _pixel = pixel;
        _fonts = fonts;
        Small = fonts.GetFont(15);
        Body = fonts.GetFont(17);
        Head = fonts.GetFont(22);
        Big = fonts.GetFont(32);
        SmallWidth = Math.Max(1, Small.MeasureString("M").X);
        BodyWidth = Math.Max(1, Body.MeasureString("M").X);
        HeadWidth = Math.Max(1, Head.MeasureString("M").X);
        foreach (var (font, size) in new[] { (Small, 15), (Body, 17), (Head, 22), (Big, 32) })
            _families[font] = new FontFamily(size, font.MeasureString("M").X, new());
    }

    /// <summary>
    /// Call at the start of a frame: the view's scale and origin. The fonts follow a new scale after it has been still for
    /// <see cref="PixelGrid.FontSettleSeconds"/> (baking at every size of a window drag would stall), or at once when
    /// <paramref name="immediate"/> (the tour). Until then the old fonts are drawn stretched to the layout size.
    /// </summary>
    public void SetView(float scale, float originX, float originY, bool immediate = false)
    {
        if (Math.Abs(scale - Scale) > 1e-4f)
            _scaleChangedAt = Now;
        Scale = scale;
        OriginX = originX;
        OriginY = originY;
        if (immediate || Math.Abs(scale - _fontScale) <= 1e-4f || Now - _scaleChangedAt >= PixelGrid.FontSettleSeconds)
            _fontScale = scale;
    }

    /// <summary>A rectangle in layout units snapped to whole device pixels and drawn there.</summary>
    public void Fill(Rectangle r, Color color)
    {
        var (x0, x1) = PixelGrid.Span(r.X, r.Right, Scale, OriginX);
        var (y0, y1) = PixelGrid.Span(r.Y, r.Bottom, Scale, OriginY);
        FillDevice(x0, y0, x1, y1, color);
    }

    /// <summary>A rectangle given in device pixels; the batch's transform maps its layout position back onto exactly those pixels.</summary>
    private void FillDevice(int x0, int y0, int x1, int y1, Color color)
    {
        if (x1 <= x0 || y1 <= y0)
            return;
        _batch.Draw(_pixel, new Vector2(PixelGrid.Layout(x0, Scale, OriginX), PixelGrid.Layout(y0, Scale, OriginY)), null, color, 0f,
            Vector2.Zero, new Vector2((x1 - x0) / Scale, (y1 - y0) / Scale), SpriteEffects.None, 0f);
    }

    /// <summary>A colour at an opacity of 0 to 1 (premultiplied, as the sprite batch blends).</summary>
    public static Color Fade(Color color, float alpha) => color * Math.Clamp(alpha, 0f, 1f);

    /// <summary>
    /// A value kept per <paramref name="id"/> that moves towards <paramref name="target"/> over <paramref name="seconds"/>:
    /// the only animation state of hover and highlight. An id not asked for during a frame is forgotten.
    /// </summary>
    public float Ease(string id, float target, float seconds = Motion.Hover)
    {
        _easedThisFrame.Add(id);
        var length = Motion.Seconds(seconds);
        var value = _eased.TryGetValue(id, out var was) ? was : 0f;
        if (length <= 0f)
            value = target;
        else
        {
            var step = Delta / length;
            value = value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
        }
        _eased[id] = value;
        return value;
    }

    /// <summary>A tooltip for whatever is in <paramref name="r"/>; the last one asked for under the mouse is the one shown.</summary>
    public void Tip(Rectangle r, string text)
    {
        if (text.Length == 0 || !Over(r))
            return;
        _tip = text;
        _tipRect = r;
    }

    /// <summary>Call at the start of a frame's drawing.</summary>
    public void BeginFrame()
    {
        _tip = null;
        Hand = false;
        Denied = false;
    }

    /// <summary>Draws the tooltip once the mouse has rested on its rectangle, and forgets eased values nobody asked for. Call last.</summary>
    public void EndFrame()
    {
        if (_tip is null)
            _tipLastRect = Rectangle.Empty;
        else
        {
            if (_tipRect != _tipLastRect)
            {
                _tipLastRect = _tipRect;
                _tipSince = Now;
            }
            if (Now - _tipSince >= Motion.Seconds(Motion.TipDelay))
            {
                var lines = Wrap(_tip, 44);
                var width = (int)(lines.Max(l => l.Length) * SmallWidth) + 20;
                var height = lines.Count * 19 + 14;
                var x = (int)Math.Clamp(Mouse.X + 16, 4, ScreenWidth - width - 4);
                var y = (int)Mouse.Y + 22;
                if (y + height > ScreenHeight - 4)
                    y = (int)Mouse.Y - height - 8;
                var box = new Rectangle(x, y, width, height);
                Fill(box, Ground);
                Frame(box, Bright);
                for (var i = 0; i < lines.Count; i++)
                    Text(Small, lines[i], x + 10, y + 7 + i * 19, Ink);
            }
        }
        foreach (var id in _eased.Keys.Where(id => !_easedThisFrame.Contains(id)).ToList())
            _eased.Remove(id);
        _easedThisFrame.Clear();
    }

    /// <summary>
    /// An outline inside <paramref name="r"/>. The outer edge is snapped to device pixels and the thickness is
    /// <c>max(1, round(thickness x scale))</c> device pixels, so every side has the same weight at every scale. The four sides do not
    /// overlap (a translucent frame has no darker corners).
    /// </summary>
    public void Frame(Rectangle r, Color color, int thickness = 1)
    {
        var (x0, x1) = PixelGrid.Span(r.X, r.Right, Scale, OriginX);
        var (y0, y1) = PixelGrid.Span(r.Y, r.Bottom, Scale, OriginY);
        var t = PixelGrid.Thickness(thickness, Scale);
        if (x1 - x0 <= 2 * t || y1 - y0 <= 2 * t)
        {
            FillDevice(x0, y0, x1, y1, color);
            return;
        }
        FillDevice(x0, y0, x1, y0 + t, color);
        FillDevice(x0, y1 - t, x1, y1, color);
        FillDevice(x0, y0 + t, x0 + t, y1 - t, color);
        FillDevice(x1 - t, y0 + t, x1, y1 - t, color);
    }

    /// <summary>A panel: filled, with a one-pixel edge.</summary>
    public void Box(Rectangle r, Color edge)
    {
        Fill(r, Panel);
        // A faint light falling from the top edge, about 20 px deep (not on a panel too low to hold it).
        var depth = Math.Min(TopLightDepth, r.Height / 2);
        if (depth > 2)
            Image(TopLight(), new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, depth), Fade(Bright, TopLightAlpha));
        Frame(r, edge);
        // A 1 px light edge just inside the top: the panel catches the light.
        Fill(new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, 1), Fade(Bright, 0.07f));
    }

    public const int TopLightDepth = 20;
    public const float TopLightAlpha = 0.045f;
    private Texture2D? _topLight;

    /// <summary>A 1 x 20 premultiplied white gradient, full at the top and nothing at the bottom.</summary>
    private Texture2D TopLight()
    {
        if (_topLight is not null)
            return _topLight;
        var texels = new Color[TopLightDepth];
        for (var i = 0; i < texels.Length; i++)
        {
            var a = 1f - (i + 0.5f) / texels.Length;
            var v = (byte)Math.Round(a * a * 255f);
            texels[i] = new Color(v, v, v, v);
        }
        _topLight = new Texture2D(_pixel.GraphicsDevice, 1, TopLightDepth);
        _topLight.SetData(texels);
        return _topLight;
    }

    /// <summary>The graphics device, for textures made by other drawing code.</summary>
    public GraphicsDevice Device => _pixel.GraphicsDevice;

    /// <summary>A premultiplied texture stretched over a layout rectangle (tint = colour x opacity).</summary>
    public void Image(Texture2D texture, Rectangle r, Color tint) =>
        _batch.Draw(texture, new Vector2(r.X, r.Y), null, tint, 0f, Vector2.Zero, new Vector2((float)r.Width / texture.Width, (float)r.Height / texture.Height), SpriteEffects.None, 0f);

    /// <summary>
    /// A soft dot centred on a layout point, <paramref name="deviceWidth"/> x <paramref name="deviceHeight"/> device pixels (plus <paramref name="stretch"/>
    /// layout pixels of width for a streak), snapped to whole device pixels so it is the same size at any window size.
    /// </summary>
    public void Stamp(Texture2D texture, float x, float y, int deviceWidth, int deviceHeight, float stretch, Color tint)
    {
        var w = deviceWidth + (int)MathF.Round(stretch * Scale);
        var left = (int)MathF.Round(OriginX + x * Scale - w / 2f);
        var top = (int)MathF.Round(OriginY + y * Scale - deviceHeight / 2f);
        _batch.Draw(texture, new Vector2(PixelGrid.Layout(left, Scale, OriginX), PixelGrid.Layout(top, Scale, OriginY)), null, tint, 0f, Vector2.Zero,
            new Vector2(w / Scale / texture.Width, deviceHeight / Scale / texture.Height), SpriteEffects.None, 0f);
    }

    private readonly Dictionary<int, Texture2D> _cross = new();
    private readonly Dictionary<int, Texture2D> _discs = new();

    /// <summary>
    /// A soft line from <paramref name="a"/> to <paramref name="b"/> with round ends. The thickness is <c>max(1, round(thickness x scale))</c>
    /// device pixels, as for <see cref="Frame"/>; the edges fall off over one pixel at any angle (<see cref="LineProfile"/>), so a diagonal does not stair-step.
    /// A horizontal or vertical line is snapped to whole pixels and drawn flat, like a frame.
    /// </summary>
    public void Line(Vector2 a, Vector2 b, Color color, float thickness = 1.5f) => SoftLine(a, b, color, PixelGrid.Thickness(thickness, Scale), true);

    private void SoftLine(Vector2 a, Vector2 b, Color color, int width, bool caps)
    {
        var d = b - a;
        var length = d.Length();
        var round = caps && width >= 2;
        if (length < 0.01f)
        {
            if (round)
                Disc(a, color, width);
            return;
        }
        if (Math.Abs(d.Y) < 1e-3f || Math.Abs(d.X) < 1e-3f)
        {
            var (from, to) = FlatLine(a, b, color, width, caps);
            if (round)
            {
                Disc(from, color, width);
                Disc(to, color, width);
            }
            return;
        }
        var unit = d / length;
        if (caps && !round)
        {
            // A one-pixel line has no disc; it runs half a pixel past its ends so two lines meeting at a corner leave no notch.
            var half = 0.5f / Scale;
            a -= unit * half;
            length += 2 * half;
        }
        var n = LineProfile.Texels(width);
        var texture = Cross(width);
        _batch.Draw(texture, a, null, color, MathF.Atan2(d.Y, d.X), new Vector2(0f, n / 2f),
            new Vector2(length, LineProfile.Extent(width) / Scale / n), SpriteEffects.None, 0f);
        if (round)
        {
            Disc(a, color, width);
            Disc(b, color, width);
        }
    }

    /// <summary>A horizontal or vertical line as a snapped rectangle of <paramref name="width"/> device pixels across the middle of the line.</summary>
    /// <returns>The two ends of the rectangle's middle line, in layout units, on the device pixels it was snapped to (where a round cap belongs).</returns>
    private (Vector2 From, Vector2 To) FlatLine(Vector2 a, Vector2 b, Color color, int width, bool caps)
    {
        var horizontal = Math.Abs(b.Y - a.Y) < 1e-3f;
        var from = horizontal ? Math.Min(a.X, b.X) : Math.Min(a.Y, b.Y);
        var to = horizontal ? Math.Max(a.X, b.X) : Math.Max(a.Y, b.Y);
        var along = horizontal ? (Scale, OriginX) : (Scale, OriginY);
        var s0 = PixelGrid.Device(from, along.Item1, along.Item2);
        var s1 = PixelGrid.Device(to, along.Item1, along.Item2);
        // A one-pixel line runs through its end pixel; a thicker one gets its round end from the discs.
        if (caps && width < 2)
            s1++;
        var across = horizontal ? (a.Y, OriginY) : (a.X, OriginX);
        var c0 = PixelGrid.Round(across.Item1 * Scale + across.Item2 - width / 2f);
        var middle = c0 + width / 2f;
        if (horizontal)
        {
            FillDevice(s0, c0, Math.Max(s1, s0 + 1), c0 + width, color);
            return (new Vector2(PixelGrid.Layout(s0, Scale, OriginX), (middle - OriginY) / Scale), new Vector2(PixelGrid.Layout(s1, Scale, OriginX), (middle - OriginY) / Scale));
        }
        FillDevice(c0, s0, c0 + width, Math.Max(s1, s0 + 1), color);
        return (new Vector2((middle - OriginX) / Scale, PixelGrid.Layout(s0, Scale, OriginY)), new Vector2((middle - OriginX) / Scale, PixelGrid.Layout(s1, Scale, OriginY)));
    }

    private void Disc(Vector2 centre, Color color, int width)
    {
        var n = LineProfile.Texels(width);
        _batch.Draw(Discs(width), centre, null, color, 0f, new Vector2(n / 2f), LineProfile.Extent(width) / Scale / n, SpriteEffects.None, 0f);
    }

    private Texture2D Cross(int width)
    {
        if (_cross.TryGetValue(width, out var cached))
            return cached;
        var data = LineProfile.Cross(width);
        var texels = new Color[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            var v = (byte)Math.Round(data[i] * 255f);
            texels[i] = new Color(v, v, v, v);
        }
        var texture = new Texture2D(_pixel.GraphicsDevice, 1, data.Length);
        texture.SetData(texels);
        return _cross[width] = texture;
    }

    private Texture2D Discs(int width)
    {
        if (_discs.TryGetValue(width, out var cached))
            return cached;
        var data = LineProfile.Disc(width);
        var n = LineProfile.Texels(width);
        var texels = new Color[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            var v = (byte)Math.Round(data[i] * 255f);
            texels[i] = new Color(v, v, v, v);
        }
        var texture = new Texture2D(_pixel.GraphicsDevice, n, n);
        texture.SetData(texels);
        return _discs[width] = texture;
    }

    /// <summary>
    /// A rounded bar (a door): a line along the long side of <paramref name="r"/> as thick as the short side, round at both ends. With a
    /// <paramref name="ring"/> it is an outline of that thickness instead, hollowed with <paramref name="hollow"/> (the same width on every side).
    /// </summary>
    public void Capsule(Rectangle r, Color color, float ring = 0f, Color? hollow = null)
    {
        var outer = Math.Max(2, PixelGrid.Thickness(Math.Min(r.Width, r.Height), Scale));
        var half = Math.Min(r.Width, r.Height) / 2f;
        Vector2 a, b;
        if (r.Height >= r.Width)
        {
            a = new Vector2(r.X + half, r.Y + half);
            b = new Vector2(r.X + half, r.Bottom - half);
        }
        else
        {
            a = new Vector2(r.X + half, r.Y + half);
            b = new Vector2(r.Right - half, r.Y + half);
        }
        SoftLine(a, b, color, outer, true);
        if (ring > 0f && hollow is { } inside)
        {
            // Outer and inner widths differ by an even number of pixels, so both sit on the same pixels and the ring is even.
            var inner = outer - 2 * PixelGrid.Thickness(ring, Scale);
            if (inner >= 1)
                SoftLine(a, b, inside, inner, true);
        }
    }

    /// <summary>A dashed line with round ends; a positive <paramref name="offset"/> moves the dashes along the line (the coolant crawls).</summary>
    public void Dashed(Vector2 a, Vector2 b, Color color, float dash = 7f, float gap = 5f, float thickness = 1.5f, float offset = 0f) =>
        DashRun(a, b, color, dash, gap, thickness, offset);

    private void DashRun(Vector2 a, Vector2 b, Color color, float dash, float gap, float thickness, float shift)
    {
        var d = b - a;
        var length = d.Length();
        if (length < 0.01f)
            return;
        var unit = d / length;
        var width = PixelGrid.Thickness(thickness, Scale);
        var cap = width >= 2 ? width / 2f / Scale : 0f;
        foreach (var (from, to) in LineProfile.Dashes(length, dash, gap, shift, cap))
            SoftLine(a + unit * from, a + unit * to, color, width, true);
    }

    /// <summary>A dashed polyline (or polygon) with one dash pattern running on round its corners; <paramref name="offset"/> moves the dashes along it.</summary>
    public void DashedPath(IReadOnlyList<Vector2> points, Color color, float dash = 7f, float gap = 5f, float thickness = 1.5f, float offset = 0f, bool closed = false)
    {
        var count = closed ? points.Count : points.Count - 1;
        var travelled = 0f;
        for (var i = 0; i < count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            DashRun(a, b, color, dash, gap, thickness, offset - travelled);
            travelled += (b - a).Length();
        }
    }

    /// <summary>A polyline or polygon: soft lines with round joins. A dashed one keeps one dash pattern running round its corners.</summary>
    public void Poly(IReadOnlyList<Vector2> points, Color color, float thickness = 1.5f, bool closed = true, bool dashed = false)
    {
        if (dashed)
        {
            DashedPath(points, color, 3f, 4f, thickness, 0f, closed);
            return;
        }
        var count = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < count; i++)
            Line(points[i], points[(i + 1) % points.Count], color, thickness);
    }

    /// <summary>A diamond outline around a point: the fault marker.</summary>
    public void Diamond(Vector2 centre, float radius, Color color, float thickness = 2f)
    {
        Poly(new[]
        {
            centre + new Vector2(0, -radius), centre + new Vector2(radius, 0), centre + new Vector2(0, radius), centre + new Vector2(-radius, 0),
        }, color, thickness);
    }

    private const int GlyphSize = PersonGlyph.Size;
    private const int GlyphScale = PersonGlyph.Scale;
    private Texture2D? _person;

    /// <summary>A small person: a round head over rounded shoulders, symmetric about the centre. An awake person.</summary>
    public void Marker(Vector2 centre, Color color)
    {
        _person ??= MakePersonGlyph();
        var left = MathF.Round(centre.X) - GlyphSize / 2;
        var top = MathF.Round(centre.Y) - GlyphSize / 2;
        var (x0, x1) = PixelGrid.Span(left, left + GlyphSize, Scale, OriginX);
        var (y0, y1) = PixelGrid.Span(top, top + GlyphSize, Scale, OriginY);
        _batch.Draw(_person, new Vector2(PixelGrid.Layout(x0, Scale, OriginX), PixelGrid.Layout(y0, Scale, OriginY)), null, color, 0f, Vector2.Zero,
            new Vector2((x1 - x0) / Scale / _person.Width, (y1 - y0) / Scale / _person.Height), SpriteEffects.None, 0f);
    }

    /// <summary>The glyph, drawn once at 4x with 4x4 sub-pixel coverage, as a premultiplied white mask; the batch tints it and scales it smoothly.</summary>
    private Texture2D MakePersonGlyph()
    {
        const int size = GlyphSize * GlyphScale;
        var data = PersonGlyph.Coverage();
        var texels = new Color[size * size];
        for (var i = 0; i < texels.Length; i++)
        {
            var a = (byte)Math.Round(data[i] * 255f);
            texels[i] = new Color(a, a, a, a);
        }
        var texture = new Texture2D(_pixel.GraphicsDevice, size, size);
        texture.SetData(texels);
        return texture;
    }

    public void Text(SpriteFontBase font, string text, float x, float y, Color color) => Run(font, Printable(text), x, y, color);

    public void TextRight(SpriteFontBase font, string text, float right, float y, Color color)
    {
        var shown = Printable(text);
        Run(font, shown, right - font.MeasureString(shown).X, y, color);
    }

    public void TextCentre(SpriteFontBase font, string text, float centre, float y, Color color)
    {
        var shown = Printable(text);
        Run(font, shown, centre - font.MeasureString(shown).X / 2, y, color);
    }

    /// <summary>
    /// Draws a line of text with a layout font (<see cref="Small"/>, <see cref="Body"/>, <see cref="Head"/>, <see cref="Big"/>) at the
    /// window's real size: a font baked at <see cref="PixelGrid.FontPixels"/>, drawn 1:1 on device pixels, every character on the
    /// layout's grid so widths, wrapping and alignment stay what the layout fonts say. A line break starts a line below.
    /// </summary>
    private void Run(SpriteFontBase layout, string shown, float x, float y, Color color)
    {
        if (shown.Length == 0)
            return;
        if (!_families.TryGetValue(layout, out var family))
        {
            layout.DrawText(_batch, shown, new Vector2(MathF.Round(x), MathF.Round(y)), color);
            return;
        }
        var pixels = PixelGrid.FontPixels(family.Size, _fontScale);
        if (!family.Baked.TryGetValue(pixels, out var baked))
        {
            var font = _fonts.GetFont(pixels);
            family.Baked[pixels] = baked = (font, font.MeasureString("M").X);
        }
        // Fonts for this scale: 1 baked pixel is 1 device pixel. A stale font (a window drag) is stretched to the layout size.
        var fresh = Math.Abs(_fontScale - Scale) <= 1e-4f;
        var k = fresh ? 1f / Scale : family.Size / (float)pixels;
        var row = 0;
        foreach (var line in shown.Split('\n'))
        {
            RunLine(baked.Font, baked.Advance, family.Advance, k, fresh, line, x, y + row * layout.LineHeight, color);
            row++;
        }
    }

    private void RunLine(SpriteFontBase font, float bakedAdvance, float layoutAdvance, float k, bool fresh, string line, float x, float y, Color color)
    {
        if (line.Length == 0)
            return;
        var startX = x * Scale + OriginX;
        var layoutY = PixelGrid.Layout(PixelGrid.Device(y, Scale, OriginY), Scale, OriginY);
        var scale = new Vector2(k, k);
        if (fresh && PixelGrid.AdvanceMatches(bakedAdvance, layoutAdvance, Scale))
        {
            // The baked advance is the layout's: one call for the whole run, starting on a device pixel.
            font.DrawText(_batch, line, new Vector2(PixelGrid.Layout(PixelGrid.Round(startX), Scale, OriginX), layoutY), color, scale: scale);
            return;
        }
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == ' ')
                continue;
            var deviceX = PixelGrid.GlyphX(startX, i, layoutAdvance, Scale);
            font.DrawText(_batch, ch < OneChar.Length ? OneChar[ch] : ch.ToString(), new Vector2(PixelGrid.Layout(deviceX, Scale, OriginX), layoutY), color, scale: scale);
        }
    }

    /// <summary>Wrapped text in the small font; returns the y below it.</summary>
    public int Paragraph(string text, int x, int y, int width, Color color, int lineHeight = 19)
    {
        foreach (var line in Wrap(text, (int)(width / SmallWidth)))
        {
            Text(Small, line, x, y, color);
            y += lineHeight;
        }
        return y;
    }

    public int Lines(string text, int width) => Wrap(text, (int)(width / SmallWidth)).Count;

    public bool Over(Rectangle r) => r.Contains((int)Mouse.X, (int)Mouse.Y);

    /// <summary>True when the rectangle was clicked this frame.</summary>
    public bool Hit(Rectangle r) => Click && Over(r);

    /// <summary>
    /// A button. Three weights: <paramref name="primary"/> is solid amber (the next step), <paramref name="strong"/> is solid light (the main
    /// action of a repeated row, such as WAKE on a crew card), everything else is an outline; <paramref name="selected"/> is an outline on a
    /// lighter fill (a state that is on). Greyed when disabled. Returns true when it was clicked this frame.
    /// </summary>
    public bool Button(Rectangle r, string label, bool enabled = true, bool primary = false, bool selected = false, string? tip = null, bool strong = false)
    {
        var over = enabled && Over(r);
        if (tip != null)
            Tip(r, tip);
        // Hover fades in and out; a held button is drawn a shade darker.
        var hot = Ease("button:" + r.X + ":" + r.Y + ":" + label, over ? 1f : 0f);
        var pressed = over && Down;
        if (over)
            Hand = true;
        if (!enabled && Hit(r))
            Denied = true;
        Color ink;
        if (!enabled)
        {
            Frame(r, Faint);
            ink = Dim;
        }
        else if (primary || strong)
        {
            var fill = Color.Lerp(primary ? Accent : Ink, Bright, hot);
            Fill(r, pressed ? Color.Lerp(fill, Ground, 0.25f) : fill);
            ink = Ground;
        }
        else if (selected)
        {
            Fill(r, pressed ? Track : Color.Lerp(Edge, Wire, 0.35f + 0.25f * hot));
            Frame(r, Bright);
            ink = Bright;
        }
        else
        {
            if (hot > 0.01f)
                Fill(r, Fade(pressed ? Edge : Track, hot));
            Frame(r, Color.Lerp(Wire, Bright, hot));
            ink = Bright;
        }
        TextCentre(Small, label, r.X + r.Width / 2f, r.Y + (r.Height - 16) / 2f, ink);
        return enabled && Hit(r);
    }

    /// <summary>A bar from 0 to 1.</summary>
    public void Meter(Rectangle r, double value, Color color)
    {
        Fill(r, Track);
        Fill(new Rectangle(r.X, r.Y, (int)(r.Width * Math.Clamp(value, 0, 1)), r.Height), color);
    }

    /// <summary>The model may write characters the bundled font lacks: map the common ones, replace the rest; a line break is kept (<see cref="TextLayout.Printable"/>).</summary>
    public static string Printable(string text) => TextLayout.Printable(text);

    /// <summary>Word wrap for the monospaced font: by character count, a new line at each line break (<see cref="TextLayout.Wrap"/>).</summary>
    public static List<string> Wrap(string text, int columns) => TextLayout.Wrap(text, columns);
}
