using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheShip.Game;

/// <summary>Draws the sky and the vignette on the ground, before any panel. One sprite per star, no shaders.</summary>
internal sealed class Backdrop
{
    private Star[] _stars = StarField.Generate(0);
    private Texture2D? _dot;
    private Texture2D? _vignette;
    private double _phase;
    private double _last = double.NaN;
    private float _brightness = 1f;
    private float _drift;

    /// <summary>Off in a tour unless <c>--tour-time</c> is given, so the shots of earlier steps stay comparable.</summary>
    public bool Enabled { get; set; } = true;

    public void Seed(int seed) => _stars = StarField.Generate(seed);

    /// <summary>
    /// Move the sky to <paramref name="now"/>. <paramref name="fixedTime"/> (the tour with --tour-time) puts it at exactly that time, with the
    /// mood and the drift already settled, so a shot always shows the same sky.
    /// </summary>
    public void Step(double now, MusicMood mood, float drift, bool fixedTime)
    {
        var target = StarField.Brightness(mood);
        if (fixedTime || double.IsNaN(_last))
        {
            _phase = now;
            _brightness = target;
            _drift = drift;
            _last = now;
            return;
        }
        var dt = (float)Math.Clamp(now - _last, 0, 0.25);
        _last = now;
        _brightness = StarField.Follow(_brightness, target, dt, StarField.MoodFollowSeconds);
        _drift = StarField.Follow(_drift, drift, dt, StarField.DriftFollowSeconds);
        _phase += dt * StarField.SpeedFactor(_drift);
    }

    public void Draw(Painter p)
    {
        if (!Enabled)
            return;
        _dot ??= MakeDot(p.Device);
        _vignette ??= MakeVignette(p.Device);
        foreach (var star in _stars)
        {
            var size = star.Big ? 5 : 3;
            var stretch = StarField.Stretch(star, _drift);
            var x = StarField.X(star, _phase, Painter.ScreenWidth);
            var y = star.Y * Painter.ScreenHeight;
            if (y < HeaderHeight || Covered(p, x, y, size / 2f + 3f + StarField.Stretch(star, _drift)))
                continue;
            p.Stamp(_dot, x, y, size, size, stretch, Painter.Fade(Painter.Bright, StarField.AlphaOf(star, _brightness)));
        }
        p.Image(_vignette, new Rectangle(0, 0, Painter.ScreenWidth, Painter.ScreenHeight), Color.White);
    }

    /// <summary>The header is text on bare ground: no stars there (the panels and filled cards cover the sky by being drawn after it).</summary>
    private const int HeaderHeight = 60;

    private static bool Covered(Painter p, float x, float y, float reach)
    {
        foreach (var r in p.Covered)
            if (x >= r.X - reach && x <= r.Right + reach && y >= r.Y - reach && y <= r.Bottom + reach)
                return true;
        return false;
    }

    /// <summary>A soft round dot, premultiplied white; drawn at 3 or 5 device pixels it has a bright core and a feathered edge.</summary>
    private static Texture2D MakeDot(GraphicsDevice device)
    {
        const int n = 16;
        var texels = new Color[n * n];
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var r = MathF.Sqrt(MathF.Pow((x + 0.5f) / n - 0.5f, 2) + MathF.Pow((y + 0.5f) / n - 0.5f, 2)) * 2f;
                var a = MathF.Pow(Math.Clamp(1f - r, 0f, 1f), 1.4f);
                var v = (byte)Math.Round(a * 255f);
                texels[y * n + x] = new Color(v, v, v, v);
            }
        var texture = new Texture2D(device, n, n);
        texture.SetData(texels);
        return texture;
    }

    /// <summary>The vignette: black with alpha from <see cref="StarField.Vignette"/>, one small texture stretched over the whole layout.</summary>
    private static Texture2D MakeVignette(GraphicsDevice device)
    {
        const int w = 64, h = 36;
        var texels = new Color[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var a = (byte)Math.Round(StarField.Vignette((x + 0.5f) / w, (y + 0.5f) / h) * 255f);
                texels[y * w + x] = new Color((byte)0, (byte)0, (byte)0, a);
            }
        var texture = new Texture2D(device, w, h);
        texture.SetData(texels);
        return texture;
    }
}
