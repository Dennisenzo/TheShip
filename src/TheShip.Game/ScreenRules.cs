namespace TheShip.Game;

/// <summary>Which small picture the ending's title sits over.</summary>
public enum EndingLook { Landfall, LandfallWithout, Adrift, QuietShip }

/// <summary>One row of the drift report's table: a number before and after, and the scale its bars are drawn on.</summary>
internal readonly record struct ReportRow(string Label, double Was, double Now, double Max, bool HigherIsWorse, bool Harm)
{
    public bool Changed => Math.Abs(Now - Was) > 0.5;
    /// <summary>The change is for the worse (stores down, wear up, a death).</summary>
    public bool Worse => HigherIsWorse ? Now > Was + 0.5 : Now < Was - 0.5;
    public float WasFill => ScreenRules.Fill(Was, Max);
    public float NowFill => ScreenRules.Fill(Now, Max);
}

/// <summary>The outline of a crew member's wireframe head, in the 240 x 200 space of the dossier's bust.</summary>
internal readonly record struct HeadLook(float Cheek, float Jaw, float Chin, float Brow, int Hair)
{
    public const int HairStyles = 4;
}

/// <summary>Pure rules of the ending, drift report and dossier screens (GRAPHICS-PLAN.md step 8).</summary>
internal static class ScreenRules
{
    /// <summary>The picture for an ending, from its title.</summary>
    public static EndingLook LookOf(string title)
    {
        if (title.StartsWith("Adrift", StringComparison.OrdinalIgnoreCase))
            return EndingLook.Adrift;
        if (title.Contains("without you", StringComparison.OrdinalIgnoreCase))
            return EndingLook.LandfallWithout;
        return title.StartsWith("Landfall", StringComparison.OrdinalIgnoreCase) ? EndingLook.Landfall : EndingLook.QuietShip;
    }

    /// <summary>How much of a bar is filled, 0 to 1 (a missing scale fills nothing).</summary>
    public static float Fill(double value, double max) => max <= 0 ? 0f : (float)Math.Clamp(value / max, 0, 1);

    /// <summary>
    /// The drift report's rows. Each bar's scale is the larger of the two values and a fixed floor, so a small change is
    /// visible and the bars of one row compare with each other. Alive is harm: a drop is a death.
    /// </summary>
    public static List<ReportRow> DriftRows(double storesWas, double storesNow, int partsWas, int partsNow, int wearWas, int wearNow, int aliveWas, int aliveNow, int crew)
    {
        static double Scale(double a, double b, double floor) => Math.Max(Math.Max(a, b), floor);
        return new List<ReportRow>
        {
            new("STORES", storesWas, storesNow, Scale(storesWas, storesNow, 1), false, false),
            new("SPARE PARTS", partsWas, partsNow, Scale(partsWas, partsNow, Core.Tuning.StartParts), false, false),
            new("POD WEAR", wearWas, wearNow, Scale(wearWas, wearNow, 20), true, false),
            new("ALIVE", aliveWas, aliveNow, Math.Max(crew, 1), false, true),
        };
    }

    /// <summary>
    /// The drift report's rows and the names it lists as lost, from the snapshot before the drift click and the drift's own end
    /// (<see cref="Core.Voyage.LastDrift"/>), never the state after it: by then the next alert has started, and its scripted
    /// events (alert 2's pod death) belong to that alert, not to the drift. With no outcome (none recorded) nothing changed.
    /// </summary>
    public static (List<ReportRow> Rows, IReadOnlyList<string> Lost) DriftReport(double storesWas, int partsWas, int wearWas, int aliveWas, int crew, Core.DriftOutcome? end)
    {
        end ??= new Core.DriftOutcome(storesWas, partsWas, wearWas, aliveWas, Array.Empty<string>());
        return (DriftRows(storesWas, end.Stores, partsWas, end.Parts, wearWas, end.PodWear, aliveWas, end.Alive, crew), end.Deaths);
    }

    /// <summary>
    /// A head for a person, the same every time: width of cheek and jaw, chin depth, brow height and a hair style, all drawn from
    /// the id (FNV-1a hash). Values are offsets in layout pixels, symmetric about the head's centre line.
    /// </summary>
    public static HeadLook HeadOf(string id)
    {
        var h = 2166136261u;
        foreach (var ch in id)
            h = (h ^ ch) * 16777619u;
        int Take(int range)
        {
            var v = (int)(h % (uint)range);
            h = (h / (uint)range) * 2654435761u + 40503u;
            return v;
        }
        return new HeadLook(Cheek: Take(9) - 4, Jaw: Take(15) - 7, Chin: Take(9) - 4, Brow: Take(7) - 3, Hair: Take(HeadLook.HairStyles));
    }

    /// <summary>The same point on the other side of the head's centre line.</summary>
    public static (float X, float Y) Mirror((float X, float Y) p) => (240 - p.X, p.Y);

    /// <summary>Right-side points of the head: brow, cheek, jaw, chin.</summary>
    public static (float X, float Y)[] HeadRight(HeadLook l) => new (float X, float Y)[]
    {
        (158, 38 + l.Brow),
        (174 + l.Cheek, 78),
        (164 + l.Jaw, 122),
        (140 + l.Jaw * 0.5f, 146 + l.Chin),
    };

    /// <summary>The head outline, clockwise from the top: the crown, the right side down, then the left side up.</summary>
    public static (float X, float Y)[] HeadOutline(HeadLook l)
    {
        var r = HeadRight(l);
        var list = new List<(float X, float Y)> { (120, 22 + l.Brow) };
        list.AddRange(r);
        for (var i = r.Length - 1; i >= 0; i--)
            list.Add(Mirror(r[i]));
        return list.ToArray();
    }

    /// <summary>The hair as open polylines on the head (style 0 is none: a shaved head).</summary>
    public static List<(float X, float Y)[]> Hair(HeadLook l)
    {
        var top = 22f + l.Brow;
        var right = HeadRight(l);
        var b = right[0];
        var c = right[1];
        var lines = new List<(float X, float Y)[]>();
        switch (l.Hair)
        {
            case 1: // cropped: a line across the forehead
                lines.Add(new[] { Mirror((b.X - 6, b.Y + 6)), (120f, top + 14), (b.X - 6, b.Y + 6) });
                break;
            case 2: // side parting
                lines.Add(new[] { Mirror((c.X - 4, c.Y - 14)), Mirror((b.X, b.Y + 4)), (100f, top + 8), (b.X, b.Y + 18), (c.X - 4, c.Y - 14) });
                lines.Add(new[] { (100f, top + 8), (96f, top + 30) });
                break;
            case 3: // a fringe in points
                lines.Add(new[] { Mirror((b.X, b.Y + 4)), (100f, top + 22), (112f, top + 10), (124f, top + 24), (136f, top + 10), (148f, top + 22), (b.X, b.Y + 4) });
                break;
        }
        return lines;
    }
}
