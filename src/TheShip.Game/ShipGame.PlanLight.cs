using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>Drawing of the ship plan's light (graphics step 5). The rules are in <see cref="PlanLight"/>; this only paints them, under the plan's outlines.</summary>
public sealed partial class ShipGame
{
    /// <summary>Layout px left of the hull kept for the engine exhaust.</summary>
    private const float ExhaustPx = 44f;
    /// <summary>The sprite count of the plan's last frame (own quads, lines and discs; text glyphs are not counted). Read by the tour.</summary>
    internal int PlanSprites { get; private set; }
    /// <summary>The plan's sprites with the light switched off, measured by the tour for comparison.</summary>
    internal bool PlanLightOn = true;

    /// <summary>
    /// The time of the plan's motion: <c>Painter.Now</c>, frozen at the start of a transition (nothing on the plan moves under the overlay);
    /// in a tour without <c>--tour-time</c> the motion is at rest (time 0), with it the plan is at exactly that time.
    /// </summary>
    private double PlanClock()
    {
        if (Motion.Still && _options.TourTime is null)
            return 0;
        return _transition is { } t ? t.Start : _p.Now;
    }

    private static bool CoolantLost(Voyage v) => v.Current.Faults.Any(f => f.Kind == "coolant" && f.State == FaultState.Lost);

    /// <summary>Fills a convex plan shape with rows of rectangles (rows <paramref name="rowPx"/> tall on the screen).</summary>
    private void FillPlanShape(PlanLayoutResult fit, IReadOnlyList<(float X, float Y)> poly, Color color, float rowPx = 8f)
    {
        foreach (var (x0, y0, x1, y1) in PlanLight.Spans(poly, rowPx / fit.K))
        {
            var (ax, ay) = fit.P(x0, y0);
            var (bx, by) = fit.P(x1, y1);
            var r = new Rectangle((int)MathF.Round(ax), (int)MathF.Round(ay), (int)MathF.Round(bx) - (int)MathF.Round(ax), (int)MathF.Round(by) - (int)MathF.Round(ay));
            if (r.Width > 0 && r.Height > 0)
                _p.Fill(r, color);
        }
    }

    /// <summary>What lies under everything else: the hull's glow, the engine exhaust, the ticks that say the ship moves right, each room's light and the wash of an open fault.</summary>
    private void DrawPlanLightBack(Voyage v, PlanLayoutResult fit, double t, IReadOnlyList<(Fault Fault, string Section)> openFaults)
    {
        if (!PlanLightOn)
            return;
        var low = v.PowerLow;
        Vector2 P(float x, float y) { var (px, py) = fit.P(x, y); return new Vector2(px, py); }

        // Rooms: their own light when powered (dim and flickering at low power), under an amber wash where a fault is open.
        var roomLight = Color.Lerp(Painter.Room, Painter.Wire, 0.55f);
        foreach (var s in v.Sections)
        {
            var glow = PlanLight.SectionGlow(low, t, PlanLight.SeedOf(s.Id));
            FillPlanShape(fit, s.Outline, Painter.Fade(roomLight, 0.42f * glow));
            PlanLight.Wash? wash = null;
            foreach (var (f, section) in openFaults)
            {
                if (section != s.Id)
                    continue;
                var w = PlanLight.FaultWash(f.Critical, LogTones.IsFinal(ShownLeft(v, f), f.ClockHours), t);
                if (wash is null || w.Danger && !wash.Value.Danger || w.Danger == wash.Value.Danger && w.Alpha > wash.Value.Alpha)
                    wash = w;
            }
            if (wash is { } washed)
                FillPlanShape(fit, s.Outline, Painter.Fade(washed.Danger ? Painter.Danger : Painter.Accent, washed.Alpha));
        }

        // The hull glow: a thin soft outline just outside the hull.
        var outer = PlanLight.Offset(ShipPlan.Hull, 3.5f / fit.K);
        var glowPoints = outer.Select(p => P(p.X, p.Y)).ToArray();
        var hullGlow = PlanLight.HullGlow(low, t);
        _p.Poly(glowPoints, Painter.Fade(Painter.Blue, 0.09f * hullGlow), 5f);

        // Engine exhaust at the stern (left): five lines licking out, fading outward.
        var ys = new[] { 160f, 205f, 265f, 310f };
        for (var i = 0; i < ys.Length; i++)
        {
            var from = P(ShipPlan.Hull[10].X, ys[i]);
            var length = PlanLight.ExhaustLength(t, i, ExhaustPx - 2f, low);
            var mid = from + new Vector2(-length * 0.45f, 0);
            var end = from + new Vector2(-length, 0);
            var strength = low ? 0.5f : 1f;
            _p.Line(from, mid, Painter.Fade(Painter.Ink, 0.6f * strength), 1.5f);
            _p.Line(mid, end, Painter.Fade(Painter.Ink, 0.28f * strength), 1f);
        }

        // Ticks along the hull, drifting towards the stern: the ship goes right.
        foreach (var (x, alpha) in PlanLight.Ticks(t, 60f, 760f))
        {
            var top = P(x, 80f);
            var bottom = P(x, 390f);
            var color = Painter.Fade(Painter.Wire, 0.4f * alpha);
            _p.Line(new Vector2(top.X, top.Y - 9f), new Vector2(top.X, top.Y - 6f), color, 1f);
            _p.Line(new Vector2(bottom.X, bottom.Y + 6f), new Vector2(bottom.X, bottom.Y + 9f), color, 1f);
        }
    }

    /// <summary>The power bus: a chain of dots running along it, slower and dimmer at low power.</summary>
    private void DrawPlanBus(Voyage v, PlanLayoutResult fit, double t)
    {
        if (!PlanLightOn)
            return;
        var low = v.PowerLow;
        var offset = PlanLight.BusOffset(t, low);
        var color = Color.Lerp(Painter.Power, Painter.Accent, 0.6f);
        var x0 = ShipPlan.PowerBus.X1;
        var x1 = ShipPlan.PowerBus.X2;
        for (var x = x0 + offset; x < x1; x += PlanLight.BusSpacing)
        {
            var edge = Math.Clamp(Math.Min(x - x0, x1 - x) / 24f, 0f, 1f);
            var (px, py) = fit.P(x, ShipPlan.PowerBus.Y);
            _p.Dot(new Vector2(px, py), Painter.Fade(color, PlanLight.BusAlpha(low) * edge), 3f);
        }
    }

    /// <summary>The tint round a locked doorway.</summary>
    private void DrawDoorTint(Rectangle door, double t)
    {
        if (!PlanLightOn)
            return;
        var area = new Rectangle(door.X - 5, door.Y - 5, door.Width + 10, door.Height + 10);
        _p.Fill(area, Painter.Fade(Painter.Accent, 0.12f + 0.08f * PlanLight.Pulse(t, 3.2f)));
    }
}
