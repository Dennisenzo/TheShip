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

    public static readonly Color Ground = new(11, 17, 20);
    public static readonly Color Panel = new(15, 24, 28);
    public static readonly Color Room = new(15, 26, 31);
    public static readonly Color Edge = new(35, 53, 61);
    public static readonly Color Track = new(27, 42, 48);
    public static readonly Color Faint = new(34, 55, 63);
    public static readonly Color Hull = new(44, 70, 80);
    public static readonly Color Wire = new(108, 151, 164);
    public static readonly Color Dim = new(157, 182, 189);
    public static readonly Color Ink = new(213, 227, 230);
    public static readonly Color Bright = new(234, 243, 245);
    public static readonly Color Accent = new(242, 176, 76);
    public static readonly Color Blue = new(124, 196, 232);
    public static readonly Color Power = new(140, 122, 78);
    public static readonly Color Dead = new(74, 95, 103);

    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;

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
        Small = fonts.GetFont(15);
        Body = fonts.GetFont(17);
        Head = fonts.GetFont(22);
        Big = fonts.GetFont(32);
        SmallWidth = Math.Max(1, Small.MeasureString("M").X);
        BodyWidth = Math.Max(1, Body.MeasureString("M").X);
        HeadWidth = Math.Max(1, Head.MeasureString("M").X);
    }

    public void Fill(Rectangle r, Color color) => _batch.Draw(_pixel, r, color);

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

    public void Frame(Rectangle r, Color color, int thickness = 1)
    {
        Fill(new Rectangle(r.X, r.Y, r.Width, thickness), color);
        Fill(new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
        Fill(new Rectangle(r.X, r.Y, thickness, r.Height), color);
        Fill(new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
    }

    /// <summary>A panel: filled, with a one-pixel edge.</summary>
    public void Box(Rectangle r, Color edge)
    {
        Fill(r, Panel);
        Frame(r, edge);
    }

    public void Line(Vector2 a, Vector2 b, Color color, float thickness = 1.5f)
    {
        var d = b - a;
        var length = d.Length();
        if (length < 0.01f)
            return;
        _batch.Draw(_pixel, a, null, color, MathF.Atan2(d.Y, d.X), new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public void Dashed(Vector2 a, Vector2 b, Color color, float dash = 7f, float gap = 5f, float thickness = 1.5f, float offset = 0f)
    {
        var d = b - a;
        var length = d.Length();
        if (length < 0.01f)
            return;
        var unit = d / length;
        // A positive offset moves the dashes along the line (the coolant crawls).
        var first = offset % (dash + gap);
        if (first > 0f)
            first -= dash + gap;
        for (var at = first; at < length; at += dash + gap)
        {
            var from = Math.Max(0f, at);
            var to = Math.Min(length, at + dash);
            if (to > from)
                Line(a + unit * from, a + unit * to, color, thickness);
        }
    }

    public void Poly(IReadOnlyList<Vector2> points, Color color, float thickness = 1.5f, bool closed = true, bool dashed = false)
    {
        var count = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            if (dashed)
                Dashed(a, b, color, 3f, 4f, thickness);
            else
                Line(a, b, color, thickness);
        }
    }

    /// <summary>A diamond outline around a point: the fault marker.</summary>
    public void Diamond(Vector2 centre, float radius, Color color, float thickness = 2f)
    {
        Poly(new[]
        {
            centre + new Vector2(0, -radius), centre + new Vector2(radius, 0), centre + new Vector2(0, radius), centre + new Vector2(-radius, 0),
        }, color, thickness);
    }

    private const int GlyphSize = 14;
    private const int GlyphScale = 4;
    private Texture2D? _person;

    /// <summary>A small person: a round head over rounded shoulders, symmetric about the centre. An awake person.</summary>
    public void Marker(Vector2 centre, Color color)
    {
        _person ??= MakePersonGlyph();
        var box = new Rectangle((int)MathF.Round(centre.X) - GlyphSize / 2, (int)MathF.Round(centre.Y) - GlyphSize / 2, GlyphSize, GlyphSize);
        _batch.Draw(_person, box, color);
    }

    /// <summary>The glyph, drawn once at 4x with 4x4 sub-pixel coverage, as a premultiplied white mask; the batch tints it and scales it smoothly.</summary>
    private Texture2D MakePersonGlyph()
    {
        const int size = GlyphSize * GlyphScale;
        var data = PersonGlyphCoverage();
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

    /// <summary>Coverage 0..1 of the glyph at GlyphScale x its size (also used by the tests/preview).</summary>
    internal static float[] PersonGlyphCoverage()
    {
        const int size = GlyphSize * GlyphScale;
        const int sub = 4;
        var data = new float[size * size];
        const float mid = GlyphSize / 2f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var hit = 0;
            for (var sy = 0; sy < sub; sy++)
            for (var sx = 0; sx < sub; sx++)
            {
                var px = (x + (sx + 0.5f) / sub) / GlyphScale - mid;
                var py = (y + (sy + 0.5f) / sub) / GlyphScale;
                // Head: circle r 3.2 centred at y 3.4. Shoulders: upper half of an ellipse 12 wide, 6.2 tall, base at y 14.
                var head = px * px + (py - 3.4f) * (py - 3.4f) <= 3.2f * 3.2f;
                var ey = (py - 14f) / 6.2f;
                var body = py <= 14f && (px / 6f) * (px / 6f) + ey * ey <= 1f;
                if (head || body) hit++;
            }
            data[y * size + x] = hit / (float)(sub * sub);
        }
        return data;
    }

    public void Text(SpriteFontBase font, string text, float x, float y, Color color) =>
        font.DrawText(_batch, Printable(text), new Vector2(MathF.Round(x), MathF.Round(y)), color);

    public void TextRight(SpriteFontBase font, string text, float right, float y, Color color)
    {
        var shown = Printable(text);
        font.DrawText(_batch, shown, new Vector2(MathF.Round(right - font.MeasureString(shown).X), MathF.Round(y)), color);
    }

    public void TextCentre(SpriteFontBase font, string text, float centre, float y, Color color)
    {
        var shown = Printable(text);
        font.DrawText(_batch, shown, new Vector2(MathF.Round(centre - font.MeasureString(shown).X / 2), MathF.Round(y)), color);
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
    /// A button. Solid when <paramref name="primary"/>, outlined otherwise, greyed when disabled. Returns true when it
    /// was clicked this frame.
    /// </summary>
    public bool Button(Rectangle r, string label, bool enabled = true, bool primary = false, bool selected = false, string? tip = null)
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
        else if (primary || selected)
        {
            var fill = Color.Lerp(primary ? Accent : Ink, Bright, hot);
            Fill(r, pressed ? Color.Lerp(fill, Ground, 0.25f) : fill);
            ink = Ground;
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

    /// <summary>The model may write characters the bundled font lacks: map the common ones, replace the rest.</summary>
    public static string Printable(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                '‘' or '’' => "'",
                '“' or '”' => "\"",
                '–' or '—' => "-",
                '…' => "...",
                '\t' => " ",
                _ => ch is >= ' ' and <= 'ÿ' ? ch.ToString() : "?",
            });
        }
        return sb.ToString();
    }

    /// <summary>Word wrap for the monospaced font: by character count.</summary>
    public static List<string> Wrap(string text, int columns)
    {
        var lines = new List<string>();
        columns = Math.Max(8, columns);
        foreach (var paragraph in Printable(text).Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                var piece = word;
                while (piece.Length > columns)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    lines.Add(piece[..columns]);
                    piece = piece[columns..];
                }
                if (line.Length > 0 && line.Length + 1 + piece.Length > columns)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }
                if (line.Length > 0)
                    line.Append(' ');
                line.Append(piece);
            }
            lines.Add(line.ToString());
        }
        // A non-breaking space keeps a number with its unit on one line; it is drawn as an ordinary space.
        for (var i = 0; i < lines.Count; i++)
            lines[i] = lines[i].Replace('\u00A0', ' ');
        return lines;
    }
}
