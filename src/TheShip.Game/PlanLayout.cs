using TheShip.Core;

namespace TheShip.Game;

/// <summary>A rectangle in screen pixels (no MonoGame types, so the layout can be tested).</summary>
internal readonly record struct RectF(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;

    /// <summary>True when the two rectangles overlap, each grown by <paramref name="pad"/> (touching edges do not count at pad 0).</summary>
    public bool Intersects(RectF o, float pad = 0f) =>
        X - pad < o.Right + pad && o.X - pad < Right + pad && Y - pad < o.Bottom + pad && o.Y - pad < Bottom + pad;

    public static RectF Around(float cx, float cy, float half) => new(cx - half, cy - half, half * 2, half * 2);
}

/// <summary>A fault to show on the plan: its place in the issue list (1-based), its section, how many are already in that section, and its label's width in pixels.</summary>
internal readonly record struct PlanFault(int Index, string SectionId, int Slot, float TextWidth);

/// <summary>A box tied to a plan point: it sits at the point's screen position plus an offset in pixels, and is W x H pixels (or scaled with the plan when <see cref="Scaled"/>).</summary>
internal readonly record struct PlanBox(float PlanX, float PlanY, float OffX, float OffY, float W, float H, bool Scaled = false);

/// <summary>One fault's marker, leader and label, in screen pixels.</summary>
internal sealed record PlacedLabel(int Index, RectF Diamond, (float X, float Y)[] Leader, RectF Text, bool Above, bool TextLeft, int Conflicts);

/// <summary>Where everything of the ship plan goes: the fit, the fault labels and the section names. Pure (<see cref="PlanLayout.Compute"/>).</summary>
internal sealed class PlanLayoutResult
{
    public float K { get; init; }
    public float Ox { get; init; }
    public float HullTop { get; init; }
    public List<PlacedLabel> Labels { get; init; } = new();
    public Dictionary<string, RectF> Names { get; init; } = new();

    /// <summary>A plan point on the screen.</summary>
    public (float X, float Y) P(float px, float py) => (Ox + px * K, HullTop + (py - PlanLayout.HullY0) * K);
}

/// <summary>
/// The ship plan's layout (graphics step 4): fit by width and height, hull centred; fault labels in rows above and below the hull with
/// leaders that bend (up or down, then a short stub) so no label overlaps another, and no leader runs through a label; section names in
/// a corner, moved off the fault diamonds, leaders, pods and the people standing about.
/// </summary>
internal static class PlanLayout
{
    public static readonly float HullX0 = ShipPlan.Hull.Min(p => p.X);
    public static readonly float HullX1 = ShipPlan.Hull.Max(p => p.X);
    public static readonly float HullY0 = ShipPlan.Hull.Min(p => p.Y);
    public static readonly float HullY1 = ShipPlan.Hull.Max(p => p.Y);

    public const float RowHeight = 18f;
    public const float RowGap = 10f;
    public const float SlotStep = 34f;
    public const float DiamondHalf = 15f;
    public const float Margin = 8f;
    public const int MaxRows = 6;
    public const float JogStep = 24f;
    public const int JogSteps = 16;

    [ThreadStatic] private static PlanLayoutInputs? _lastInputs;
    [ThreadStatic] private static PlanLayoutResult? _lastResult;

    /// <summary>
    /// <see cref="Compute"/>, kept from the last call while its inputs are the same (the bridge draws the plan every frame, and the layout
    /// changes only when a fault, a label's hours, a person standing about or the panel changes). One cache per thread (the game draws on one).
    /// </summary>
    public static PlanLayoutResult Cached(IReadOnlyList<Section> sections, IReadOnlyList<PlanFault> faults, IReadOnlyList<PlanBox> boxes, RectF area, float charWidth, float lineHeight = 16f)
    {
        var inputs = new PlanLayoutInputs(sections, faults.ToArray(), boxes.ToArray(), area, charWidth, lineHeight);
        if (_lastResult is { } kept && inputs.Same(_lastInputs))
            return kept;
        _lastInputs = inputs;
        return _lastResult = Compute(sections, faults, boxes, area, charWidth, lineHeight);
    }

    /// <summary>What a layout depends on. Sections are compared by reference: their geometry is fixed for a voyage.</summary>
    private sealed record PlanLayoutInputs(IReadOnlyList<Section> Sections, PlanFault[] Faults, PlanBox[] Boxes, RectF Area, float CharWidth, float LineHeight)
    {
        public bool Same(PlanLayoutInputs? o) => o is not null && ReferenceEquals(Sections, o.Sections) && Area == o.Area && CharWidth == o.CharWidth
                                                 && LineHeight == o.LineHeight && Faults.AsSpan().SequenceEqual(o.Faults) && Boxes.AsSpan().SequenceEqual(o.Boxes);
    }

    /// <summary>The plan's fit and layout inside <paramref name="area"/> (the part of the panel below its header).</summary>
    public static PlanLayoutResult Compute(IReadOnlyList<Section> sections, IReadOnlyList<PlanFault> faults, IReadOnlyList<PlanBox> boxes, RectF area, float charWidth, float lineHeight = 16f)
    {
        var hullW = HullX1 - HullX0;
        var hullH = HullY1 - HullY0;
        var kw = (area.W - 2 * Margin) / hullW;
        var k = kw;
        Placement placed;
        Dictionary<string, RectF> names;
        var round = 0;
        while (true)
        {
            var ox0 = Ox(area, k);
            // Each fault leaves its section up or down (preferably the nearer side); the section names keep clear of that run.
            // A fault whose label finds no free place on its side is turned round and everything is placed again.
            var turns = new HashSet<int>();
            while (true)
            {
                names = PlaceNames(sections, faults, ToAbove(sections, faults, turns), boxes, k, ox0, hullH * k, charWidth, lineHeight);
                placed = PlaceLabels(sections, faults, ToAbove(sections, faults, turns), names, area, k, ox0, hullH * k, lineHeight);
                var bad = placed.Labels.FindIndex(l => l.Conflicts > 0);
                if (bad < 0 || !turns.Add(placed.Labels[bad].Index) || turns.Count > faults.Count)
                    break;
            }
            var need = (placed.Above + placed.Below) * RowHeight + (placed.Above > 0 ? RowGap : 0) + (placed.Below > 0 ? RowGap : 0);
            var fit = Math.Min(kw, (area.H - need) / hullH);
            if (fit >= k - 0.0005f || ++round >= 8)
                break;
            k = fit;
        }

        var ox = Ox(area, k);
        var hullPx = hullH * k;
        var minTop = area.Y + placed.Above * RowHeight + (placed.Above > 0 ? RowGap : 0);
        var maxTop = area.Bottom - placed.Below * RowHeight - (placed.Below > 0 ? RowGap : 0) - hullPx;
        var top = Math.Max(minTop, Math.Min(area.Y + (area.H - hullPx) / 2, maxTop));

        var result = new PlanLayoutResult { K = k, Ox = ox, HullTop = top };
        foreach (var l in placed.Labels)
            result.Labels.Add(l with
            {
                Diamond = Shift(l.Diamond, top),
                Leader = l.Leader.Select(p => (p.X, p.Y + top)).ToArray(),
                Text = Shift(l.Text, top),
            });

        foreach (var (id, r) in names)
            result.Names[id] = Shift(r, top);
        return result;
    }

    /// <summary>For each fault (in order): whether its leader runs up. The nearer side, unless the fault is in <paramref name="turned"/>.</summary>
    private static bool[] ToAbove(IReadOnlyList<Section> sections, IReadOnlyList<PlanFault> faults, HashSet<int> turned) =>
        faults.Select(f => (sections.FirstOrDefault(x => x.Id == f.SectionId)?.Spot.Y ?? 0) < (HullY0 + HullY1) / 2 != turned.Contains(f.Index)).ToArray();

    private static float Ox(RectF area, float k) => area.X + (area.W - (HullX1 - HullX0) * k) / 2 - HullX0 * k;

    private static RectF Shift(RectF r, float dy) => r with { Y = r.Y + dy };

    private static RectF Segment((float X, float Y) a, (float X, float Y) b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    // ---- fault labels ----

    private sealed record Placement(List<PlacedLabel> Labels, int Above, int Below);

    /// <summary>Places the labels with the hull's top edge at y = 0 (the caller shifts them). Rows above have negative y.</summary>
    private static Placement PlaceLabels(IReadOnlyList<Section> sections, IReadOnlyList<PlanFault> faults, bool[] aboveOf, Dictionary<string, RectF> names, RectF area, float k, float ox, float hullPx, float lineHeight)
    {
        var placed = new List<PlacedLabel>();
        var usedAbove = 0;
        var usedBelow = 0;
        for (var fi = 0; fi < faults.Count; fi++)
        {
            var f = faults[fi];
            var s = sections.FirstOrDefault(x => x.Id == f.SectionId);
            if (s is null)
                continue;
            var cx = ox + (s.Spot.X + f.Slot * SlotStep) * k;
            var cy = (s.Spot.Y - HullY0) * k;
            var diamond = RectF.Around(cx, cy, DiamondHalf);
            var preferAbove = aboveOf[fi];

            // Candidates, cheapest first: a nearer row, the preferred side, a shorter jog. The first without a conflict wins;
            // if none is free, the one with the fewest.
            var options = new List<(float Cost, PlacedLabel Label)>();
            for (var row = 0; row < MaxRows; row++)
                foreach (var above in new[] { preferAbove })
                    for (var jog = -JogSteps; jog <= JogSteps; jog++)
                        foreach (var left in new[] { true, false })
                        {
                            var cand = Candidate(f, diamond, above, row, left, jog * JogStep, hullPx, lineHeight);
                            if (cand.Text.X < area.X + Margin || cand.Text.Right > area.Right - Margin)
                                continue;
                            options.Add((row * 2f + (above == preferAbove ? 0 : 3f) + Math.Abs(jog) * 0.25f + (left ? 0 : 0.1f), cand));
                        }
            PlacedLabel? best = null;
            foreach (var (_, cand) in options.OrderBy(o => o.Cost))
            {
                var conflicts = Conflicts(cand, placed, names);
                if (best is null || conflicts < best.Conflicts)
                    best = cand with { Conflicts = conflicts };
                if (conflicts == 0)
                    break;
            }
            best ??= Candidate(f, diamond, preferAbove, 0, true, 0, hullPx, lineHeight) with { Conflicts = 99 };
            placed.Add(best);
            if (best.Above)
                usedAbove = Math.Max(usedAbove, RowOf(best, hullPx) + 1);
            else
                usedBelow = Math.Max(usedBelow, RowOf(best, hullPx) + 1);
        }
        return new Placement(placed, usedAbove, usedBelow);
    }

    private static float RowCentre(bool above, int row, float hullPx) =>
        above ? -(RowGap + RowHeight / 2 + row * RowHeight) : hullPx + RowGap + RowHeight / 2 + row * RowHeight;

    private static int RowOf(PlacedLabel l, float hullPx)
    {
        var centre = l.Text.Y + l.Text.H / 2;
        return (int)Math.Round(l.Above ? (-centre - RowGap - RowHeight / 2) / RowHeight : (centre - hullPx - RowGap - RowHeight / 2) / RowHeight);
    }

    private static PlacedLabel Candidate(PlanFault f, RectF diamond, bool above, int row, bool left, float jog, float hullPx, float lineHeight)
    {
        var cx = diamond.X + diamond.W / 2;
        var cy = diamond.Y + diamond.H / 2;
        var yr = RowCentre(above, row, hullPx);
        var y0 = cy + (above ? -13 : 13);
        var stub = left ? -10f : 10f;
        // A jog runs sideways in the gap between the hull and the first row, so the leader can climb past the labels of nearer rows.
        var yj = above ? -RowGap / 2 : hullPx + RowGap / 2;
        var xj = cx + jog;
        var leader = jog == 0
            ? new[] { (cx, y0), (cx, yr), (cx + stub, yr) }
            : new[] { (cx, y0), (cx, yj), (xj, yj), (xj, yr), (xj + stub, yr) };
        var x = left ? xj - 14 - f.TextWidth : xj + 14;
        return new PlacedLabel(f.Index, diamond, leader, new RectF(x, yr - lineHeight / 2, f.TextWidth, lineHeight), above, left, 0);
    }

    private static int Conflicts(PlacedLabel c, List<PlacedLabel> placed, Dictionary<string, RectF> names)
    {
        var n = 0;
        var mine = LeaderRects(c).ToList();
        foreach (var name in names.Values)
        {
            if (c.Text.Intersects(name, 2))
                n++;
            foreach (var r in mine)
                if (r.Intersects(name, 2))
                    n++;
        }
        foreach (var o in placed)
        {
            if (c.Text.Intersects(o.Text, 2))
                n++;
            foreach (var r in LeaderRects(o))
                if (c.Text.Intersects(r, 2))
                    n++;
            foreach (var r in mine)
                if (o.Text.Intersects(r, 2) || o.Diamond.Intersects(r))
                    n++;
            foreach (var r in LeaderRects(o))
                if (c.Diamond.Intersects(r))
                    n++;
        }
        return n;
    }

    private static IEnumerable<RectF> LeaderRects(PlacedLabel l)
    {
        for (var i = 0; i + 1 < l.Leader.Length; i++)
            yield return Segment(l.Leader[i], l.Leader[i + 1]);
    }

    // ---- section names ----

    /// <summary>Section names in the local frame (hull top at y = 0): in a corner, off the fault diamonds, the pods and the people standing about.</summary>
    private static Dictionary<string, RectF> PlaceNames(IReadOnlyList<Section> sections, IReadOnlyList<PlanFault> faults, bool[] above, IReadOnlyList<PlanBox> boxes, float k, float ox, float hullPx, float charWidth, float lineHeight)
    {
        (float X, float Y) P(float px, float py) => (ox + px * k, (py - HullY0) * k);
        var hard = new List<RectF>();
        var strips = new List<RectF>();
        for (var i = 0; i < faults.Count; i++)
            if (sections.FirstOrDefault(x => x.Id == faults[i].SectionId) is { } fs)
            {
                var (dx, dy) = P(fs.Spot.X + faults[i].Slot * SlotStep, fs.Spot.Y);
                hard.Add(RectF.Around(dx, dy, DiamondHalf + 4));
                strips.Add(above[i] ? new RectF(dx - 3, 0, 6, dy) : new RectF(dx - 3, dy, 6, hullPx - dy));
            }
        foreach (var crew in new[] { "mara", "idris", "tomas", "lin", "asha", "jonah" })
        {
            var pod = ShipPlan.Pod(crew);
            var (px, py) = P(pod.X, pod.Y);
            hard.Add(new RectF(px, py, 44 * k, 70 * k));
        }
        var soft = boxes.Select(b =>
        {
            var (bx, by) = P(b.PlanX, b.PlanY);
            return new RectF(bx + b.OffX, by + b.OffY, b.Scaled ? b.W * k : b.W, b.Scaled ? b.H * k : b.H);
        }).ToList();
        var names = new Dictionary<string, RectF>();
        foreach (var s in sections)
            names[s.Id] = PlaceName(s, P, names.Values.ToList(), hard, strips, soft, charWidth, lineHeight);
        return names;
    }

    private static RectF PlaceName(Section s, Func<float, float, (float X, float Y)> map, List<RectF> others, List<RectF> hard, List<RectF> strips, List<RectF> soft, float charWidth, float lineHeight)
    {
        var w = s.Name.Length * charWidth;
        var outline = s.Outline.Select(p => map(p.X, p.Y)).ToArray();
        var x0 = outline.Min(p => p.X);
        var x1 = outline.Max(p => p.X);
        var y0 = outline.Min(p => p.Y);
        var y1 = outline.Max(p => p.Y);
        var centreY = (s.Outline.Min(p => p.Y) + s.Outline.Max(p => p.Y)) / 2;
        var fromBottom = centreY < 225;
        var inset = 6f;

        IEnumerable<RectF> Candidates()
        {
            for (var y = 0f; y + lineHeight + 2 * inset <= y1 - y0; y += 4)
            {
                var ry = fromBottom ? y1 - inset - lineHeight - y : y0 + inset + y;
                yield return new RectF(x0 + inset, ry, w, lineHeight);
                yield return new RectF(x1 - inset - w, ry, w, lineHeight);
                yield return new RectF((x0 + x1) / 2 - w / 2, ry, w, lineHeight);
                for (var x = x0 + inset + 8; x + w <= x1 - inset - 8; x += 8)
                    yield return new RectF(x, ry, w, lineHeight);
            }
        }

        // The strictest placement that has room: clear of everything, then of the people, then of the leaders runs, then only of the diamonds and pods.
        RectF? firstInside = null;
        foreach (var tier in new[] { 0, 1, 2, 3 })
            foreach (var c in Candidates())
            {
                if (!Inside(outline, c, 3))
                    continue;
                firstInside ??= c;
                if (hard.Any(h => h.Intersects(c, 2)) || tier <= 2 && others.Any(h => h.Intersects(c, 2)))
                    continue;
                if (tier <= 1 && strips.Any(h => h.Intersects(c, 2)) || tier == 0 && soft.Any(h => h.Intersects(c, 1)))
                    continue;
                return c;
            }
        return firstInside ?? new RectF(x0 + inset, y0 + inset, w, lineHeight);
    }

    private static bool Inside((float X, float Y)[] polygon, RectF r, float pad)
    {
        foreach (var (x, y) in new[] { (r.X - pad, r.Y - pad), (r.Right + pad, r.Y - pad), (r.X - pad, r.Bottom + pad), (r.Right + pad, r.Bottom + pad) })
            if (!Contains(polygon, x, y))
                return false;
        return true;
    }

    private static bool Contains((float X, float Y)[] polygon, float x, float y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var (xi, yi) = polygon[i];
            var (xj, yj) = polygon[j];
            if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }
}
