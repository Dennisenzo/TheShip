using System;
using System.Collections.Generic;

namespace TheShip.Game;

/// <summary>
/// The ship plan's light (graphics step 5): how bright each thing on the plan is, as pure functions of the game's state and of one
/// time value, so a shot at a fixed <c>--tour-time</c> shows an exact phase. Nothing here draws; nothing reads the clock.
/// </summary>
internal static class PlanLight
{
    /// <summary>Smooth value noise in 0..1: a hash of whole steps of <c>t x rate</c>, eased between them. The same (time, seed) always gives the same value.</summary>
    public static float Noise(double t, int seed, double rate)
    {
        var x = t * rate;
        var i = (long)Math.Floor(x);
        var f = (float)(x - i);
        var s = f * f * (3f - 2f * f);
        return Hash(i, seed) * (1f - s) + Hash(i + 1, seed) * s;
    }

    private static float Hash(long i, int seed)
    {
        unchecked
        {
            var h = (uint)(i * 374761393L + seed * 668265263L + 1274126177L);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    /// <summary>A stable number from a name (so each section and pod has its own phase).</summary>
    public static int SeedOf(string id)
    {
        var h = 17;
        foreach (var c in id)
            h = unchecked(h * 31 + c);
        return h;
    }

    /// <summary>How strongly a section's own light shows, 0..1: a slow breath when powered; dim with sudden dips at low power.</summary>
    public static float SectionGlow(bool powerLow, double t, int seed)
    {
        if (!powerLow)
            return 0.9f + 0.1f * MathF.Sin((float)(t / 7.0 * Math.PI * 2) + seed % 7);
        var n = Noise(t, seed, 7);
        // Mostly dim, now and then almost out, now and then back for a moment.
        return 0.12f + 0.38f * n * n + (Noise(t, seed + 1, 2.2) > 0.82f ? 0.25f : 0f);
    }

    /// <summary>The wash of a section with an open fault: its alpha, and whether it is the red-orange of the last quarter of the clock.</summary>
    public readonly record struct Wash(float Alpha, bool Danger);

    /// <summary>
    /// An open fault washes its section in amber (steady, faint); a critical one pulses slowly; in the last quarter of its clock the wash is
    /// <c>Danger</c> and pulses faster. <paramref name="final"/> is <c>LogTones.IsFinal</c>.
    /// </summary>
    public static Wash FaultWash(bool critical, bool final, double t)
    {
        if (final)
            return new Wash(0.10f + 0.16f * Pulse(t, 1.0f), true);
        if (critical)
            return new Wash(0.07f + 0.15f * Pulse(t, 2.6f), false);
        return new Wash(0.09f, false);
    }

    /// <summary>0..1 swing once every <paramref name="period"/> seconds (the caller owns time, so <c>Motion.Speed</c> does not stop it).</summary>
    public static float Pulse(double t, float period) => 0.5f + 0.5f * MathF.Sin((float)(t / period * Math.PI * 2));

    /// <summary>A sleeping pod's brightness: breathes slowly (about 0.8..1); a worn pod (wear at <paramref name="wornAt"/> or more) also flickers, the more the worse.</summary>
    public static float PodBreath(int wear, int wornAt, double t, int seed)
    {
        var breath = 0.9f + 0.1f * MathF.Sin((float)(t / 5.5 * Math.PI * 2) + seed % 11);
        if (wear < wornAt)
            return breath;
        var strength = Math.Clamp((wear - wornAt + 1) / 8f, 0.25f, 1f);
        var dip = Noise(t, seed, 5) > 0.7f ? 0.55f * strength : 0f;
        return Math.Max(0.15f, breath - dip - 0.12f * strength * Noise(t, seed + 3, 13));
    }

    public const float BusSpacing = 18f;

    /// <summary>Plan units a second the power dots travel.</summary>
    public static float BusSpeed(bool powerLow) => powerLow ? 7f : 26f;

    public static float BusAlpha(bool powerLow) => powerLow ? 0.35f : 0.9f;

    /// <summary>How far the whole chain has moved along the bus, 0..<see cref="BusSpacing"/>.</summary>
    public static float BusOffset(double t, bool powerLow) => (float)(t * BusSpeed(powerLow) % BusSpacing);

    /// <summary>How fast the coolant crawls (layout px a second): 0 when the coolant is lost.</summary>
    public static float CoolantSpeed(bool lost) => lost ? 0f : 10f;

    /// <summary>The hull's outer glow, alpha 0..1: a slow breath, half as bright at low power.</summary>
    public static float HullGlow(bool powerLow, double t) => (0.75f + 0.25f * MathF.Sin((float)(t / 6.0 * Math.PI * 2))) * (powerLow ? 0.5f : 1f);

    /// <summary>Length of exhaust line <paramref name="i"/> in layout px (0..<paramref name="max"/>): it licks in and out; shorter at low power.</summary>
    public static float ExhaustLength(double t, int i, float max, bool powerLow)
    {
        var n = 0.55f + 0.45f * Noise(t, 100 + i * 7, 4.5);
        return max * n * (powerLow ? 0.45f : 1f);
    }

    public const float TickSpacing = 80f;
    public const float TickSpeed = 12f;

    /// <summary>Where the hull ticks sit: x positions in plan units between <paramref name="from"/> and <paramref name="to"/>, drifting left (the vessel moves right), each with its alpha (faded towards both ends).</summary>
    public static List<(float X, float Alpha)> Ticks(double t, float from, float to)
    {
        var result = new List<(float, float)>();
        var shift = (float)(t * TickSpeed % TickSpacing);
        const float fade = 70f;
        for (var x = from - shift; x <= to + TickSpacing; x += TickSpacing)
        {
            if (x < from || x > to)
                continue;
            var edge = Math.Min(x - from, to - x);
            result.Add((x, Math.Clamp(edge / fade, 0f, 1f)));
        }
        return result;
    }

    /// <summary>
    /// The horizontal spans that fill a convex polygon, one rectangle per run of rows with the same extent (rows are <paramref name="stride"/> tall,
    /// sampled at their middle): a rectangle section is a few rectangles, a chamfer a few more.
    /// </summary>
    public static List<(float X0, float Y0, float X1, float Y1)> Spans(IReadOnlyList<(float X, float Y)> poly, float stride)
    {
        var result = new List<(float, float, float, float)>();
        if (poly.Count < 3 || stride <= 0f)
            return result;
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var p in poly)
        {
            top = Math.Min(top, p.Y);
            bottom = Math.Max(bottom, p.Y);
        }
        (float X0, float Y0, float X1, float Y1)? open = null;
        for (var y = top; y < bottom - 0.01f; y += stride)
        {
            var yc = Math.Min(y + stride / 2f, bottom - 0.01f);
            var lo = float.MaxValue;
            var hi = float.MinValue;
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                if ((a.Y <= yc && b.Y > yc) || (b.Y <= yc && a.Y > yc))
                {
                    var x = a.X + (yc - a.Y) / (b.Y - a.Y) * (b.X - a.X);
                    lo = Math.Min(lo, x);
                    hi = Math.Max(hi, x);
                }
            }
            if (hi < lo)
                continue;
            var rowBottom = Math.Min(y + stride, bottom);
            if (open is { } o && Math.Abs(o.X0 - lo) < 1f && Math.Abs(o.X1 - hi) < 1f)
                open = (o.X0, o.Y0, o.X1, rowBottom);
            else
            {
                if (open is { } done)
                    result.Add(done);
                open = (lo, y, hi, rowBottom);
            }
        }
        if (open is { } last)
            result.Add(last);
        return result;
    }

    /// <summary>A convex polygon pushed outward by <paramref name="distance"/> (mitred corners): the line the hull glow follows.</summary>
    public static (float X, float Y)[] Offset(IReadOnlyList<(float X, float Y)> poly, float distance)
    {
        var n = poly.Count;
        var area = 0f;
        for (var i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            area += a.X * b.Y - b.X * a.Y;
        }
        // In a y-down frame a polygon with positive shoelace area runs clockwise on screen; the outward normal of edge d is then (d.Y, -d.X).
        var sign = area >= 0f ? 1f : -1f;
        var result = new (float X, float Y)[n];
        for (var i = 0; i < n; i++)
        {
            var prev = poly[(i + n - 1) % n];
            var cur = poly[i];
            var next = poly[(i + 1) % n];
            var n1 = Normal(prev, cur, sign);
            var n2 = Normal(cur, next, sign);
            var dot = 1f + n1.X * n2.X + n1.Y * n2.Y;
            var scale = distance / Math.Max(0.35f, dot);
            result[i] = (cur.X + (n1.X + n2.X) * scale, cur.Y + (n1.Y + n2.Y) * scale);
        }
        return result;
    }

    private static (float X, float Y) Normal((float X, float Y) a, (float X, float Y) b, float sign)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1e-5f)
            return (0f, 0f);
        return (sign * dy / len, -sign * dx / len);
    }
}
