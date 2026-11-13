using System.Globalization;
using Microsoft.Xna.Framework;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>The bridge screen: issues at hand, the ship plan, the log with the voice line, and the crew.</summary>
public sealed partial class ShipGame
{
    private static string N(double n) => n.ToString("0", CultureInfo.InvariantCulture);

    private static string Sentence(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    private void DrawBridge(Voyage v)
    {
        Header(v, "THE SHIP", back: false);
        var right = Painter.ScreenWidth - 16;
        if (_p.Button(new Rectangle(right - 80, 8, 80, 40), "QUIT"))
            Do(Exit);
        if (_p.Button(new Rectangle(right - 288, 8, 200, 40), "RECORD AND LOGS"))
            Do(() => _screen = Screen.Record);
        var numbers = "STORES " + N(v.Stores) + " d   PARTS " + v.Parts + "   POD WEAR " + v.PodWear
                      + "   POWER " + (v.PowerLow ? "LOW" : "normal") + "   COURSE " + (v.NavLost ? "LOST" : "held");
        _p.TextRight(_p.Small, numbers, right - 312, 21, v.PowerLow || v.NavLost ? Painter.Accent : Painter.Dim);

        DrawIssues(v, 16, 72, 330, Painter.ScreenHeight - 16);
        DrawPlan(v, new Rectangle(358, 72, 864, 432));
        DrawLog(v, new Rectangle(358, 516, 864, Painter.ScreenHeight - 16 - 516));
        DrawCrew(v, 1234, 72, 350, Painter.ScreenHeight - 16);
    }

    // ---- Issues at hand ----

    private void DrawIssues(Voyage v, int x, int top, int width, int bottom)
    {
        _p.Text(_p.Small, "ISSUES AT HAND", x, top, Painter.Dim);
        var y = top + 28;
        var inner = width - 28;
        var ix = x + 14;
        if (v.Current.Faults.Count == 0)
            y = _p.Paragraph(v.Phase == Phase.Ended ? "The voyage is over." : "Nothing needs repair.", x, y, width, Painter.Ink);

        // One fault gets a full card; two or more get compact cards, sized by how many there are, that always fit the column (IssueCardLayout).
        var compact = v.Current.Faults.Count >= 2;
        var shown = v.Current.Faults.Count;
        var number = 0;
        foreach (var f in v.Current.Faults)
        {
            number++;
            if (compact)
            {
                y = DrawCompactIssue(v, f, number, shown, x, y, width);
                continue;
            }
            var cardTop = y;
            y += 12;
            var state = f.State switch
            {
                FaultState.Open => "OPEN",
                FaultState.InRepair => f.Patching ? "PATCHING" : "IN REPAIR",
                FaultState.Repaired => "REPAIRED",
                FaultState.Patched => "PATCHED",
                _ => "LOST",
            };
            _p.Text(_p.Small, number.ToString("00", CultureInfo.InvariantCulture) + " - " + state + (f.Critical && !f.Resolved ? " - CRITICAL" : ""),
                ix, y, f.Resolved ? Painter.Dim : Painter.Accent);
            if (!f.Resolved)
                _p.TextRight(_p.Head, Voyage.Hours(Math.Max(0, f.Deadline - v.Clock)), x + width - 14, y - 4, Painter.Bright);
            y += 24;
            _p.Text(_p.Head, IssueCardLayout.CardName(f), ix, y, f.Resolved ? Painter.Dim : Painter.Bright);
            y += 32;

            if (!f.Resolved)
            {
                var where = v.GetSection(f.Section)?.Name ?? "";
                var needs = Sentence(where) + ". " + Patient(v, f) + "Needs " + f.Skill.ToString().ToLowerInvariant() + " " + f.Level
                            + (f.People > 1 ? ", " + f.People + " people" : "") + ", " + N(f.Hours) + " hours. If the clock runs out: " + f.IfIgnored + ".";
                y = _p.Paragraph(needs, ix, y, inner, Painter.Dim);
                var verdict = v.EarliestDoneText(f);
                y = _p.Paragraph("If started now: " + char.ToLowerInvariant(verdict[0]) + verdict[1..], ix, y, inner, Painter.Ink) + 6;

                var workers = v.Awake.Where(c => c.WorkingOn == f.Id).Select(c => c.Name).ToList();
                if (f.State == FaultState.InRepair)
                {
                    _p.Meter(new Rectangle(ix, y, inner, 8), f.Progress / f.Hours, Painter.Ink);
                    y += 14;
                }
                if (workers.Count > 0)
                {
                    var line = string.Join(" and ", workers) + (workers.Count == 1 ? " is" : " are") + " on it"
                               + (workers.Count < f.People ? ": " + workers.Count + " of the " + f.People + " people it needs." : ".");
                    y = _p.Paragraph(line, ix, y, inner, Painter.Ink);
                }
                if (f.DangerFact is { } danger)
                    y = _p.Paragraph("Known to you: " + v.GetFact(danger).Say(v.GetFact(danger).Truth) + ".", ix, y, inner, Painter.Dim);
                if (f.ExposureFact is { } seen)
                    y = _p.Paragraph("Whoever works here learns " + v.GetFact(seen).Label + ".", ix, y, inner, Painter.Dim);
                y += 8;

                var who = _target is { State: CrewState.Awake } ? _target : null;
                var half = (inner - 8) / 2;
                var fault = f;
                if (_p.Button(new Rectangle(ix, y, half, 40), who is null ? "SELECT SOMEONE" : "REPAIR: " + who.Name.ToUpperInvariant(), who != null) && who != null)
                    Act("repair " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: false));
                if (_p.Button(new Rectangle(ix + half + 8, y, half, 40), "PATCH", who != null) && who != null)
                    Act("patch " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: true));
                y += 40;
            }
            y += 12;
            _p.Frame(new Rectangle(x, cardTop, width, y - cardTop),
                f.Resolved ? Painter.Edge : f.State == FaultState.InRepair ? Painter.Wire : Painter.Accent);
            y += 10;
        }

        var clock = new Rectangle(x, bottom - 150, width, 150);
        _p.Box(clock, Painter.Edge);
        var day = (int)(v.Clock % Tuning.HoursPerYear / 24) + 1;
        _p.Text(_p.Small, "CLOCK", ix, clock.Y + 12, Painter.Dim);
        _p.TextRight(_p.Small, "year " + N(Math.Floor(v.Year)) + ", day " + day, x + width - 14, clock.Y + 12, Painter.Dim);
        if (v.Phase == Phase.Aftermath)
        {
            // Until something needs you (a crew line, a death, a person who can sleep); the small button is the old two-week step.
            var wide = inner * 2 / 3;
            if (_p.Button(new Rectangle(ix, clock.Y + 44, wide, 40), "UNTIL SOMETHING HAPPENS", true, primary: true))
                Act("advance until something happens", () =>
                {
                    var result = v.GoUntilAttention();
                    _session?.Line("advance", result.Steps + " step(s), stopped: " + (result.Reason.Length == 0 ? "(step limit or nothing to do)" : result.Reason));
                }, snapshot: true);
            if (_p.Button(new Rectangle(ix + wide + 8, clock.Y + 44, inner - wide - 8, 40), "2 WEEKS"))
                Act("advance two weeks", v.Go, snapshot: true);
        }
        else if (_p.Button(new Rectangle(ix, clock.Y + 44, inner, 40), "ADVANCE TO NEXT EVENT", v.Phase != Phase.Ended, primary: true))
            Act("advance to next event", v.Go, snapshot: true);
        if (_p.Button(new Rectangle(ix, clock.Y + 94, inner, 40), "DRIFT TO THE NEXT ALERT", v.Phase == Phase.Aftermath))
            Act("drift to the next alert", v.Drift, snapshot: true);
    }

    /// <summary>"Patient: Tomas. " on the card of a sleeper illness, else nothing.</summary>
    private static string Patient(Voyage v, Fault f) =>
        f.Kind == "illness" && f.SubjectId is { } sid && v.Find(sid) is { } p ? "Patient: " + p.Name + ". " : "";

    /// <summary>The words cut to fit <paramref name="maxLines"/> lines of <paramref name="width"/> pixels, with ".." where they were cut.</summary>
    private string FitLines(string text, int width, int maxLines)
    {
        if (_p.Lines(text, width) <= maxLines)
            return text;
        var words = text.Split(' ').ToList();
        while (words.Count > 1 && _p.Lines(string.Join(' ', words) + "..", width) > maxLines)
            words.RemoveAt(words.Count - 1);
        return string.Join(' ', words) + "..";
    }

    /// <summary>The text cut to fit one line of <paramref name="width"/> pixels in a font whose characters are <paramref name="charWidth"/> wide.</summary>
    private static string Clip(string text, int width, float charWidth)
    {
        var room = Math.Max(4, (int)(width / charWidth));
        return text.Length <= room ? text : text[..(room - 2)] + "..";
    }

    /// <summary>
    /// A compact issue card for an alert with two or more faults (<see cref="IssueCardLayout"/> holds the heights and splits the lines, so
    /// four always fit and the verdict always shows whole).
    /// Returns the y below the card.
    /// </summary>
    private int DrawCompactIssue(Voyage v, Fault f, int number, int shown, int x, int y, int width)
    {
        var ix = x + 14;
        var inner = width - 28;
        var cardTop = y;
        var tag = number.ToString("00", CultureInfo.InvariantCulture);
        if (f.Resolved)
        {
            var done = f.State switch { FaultState.Repaired => "REPAIRED", FaultState.Patched => "PATCHED", _ => "LOST" };
            _p.Text(_p.Small, Clip(tag + " - " + done + " - " + IssueCardLayout.CardName(f), inner, _p.SmallWidth), ix, y + 6, Painter.Dim);
            _p.Frame(new Rectangle(x, cardTop, width, IssueCardLayout.ResolvedRow), Painter.Edge);
            return cardTop + IssueCardLayout.ResolvedRow + IssueCardLayout.Gap;
        }
        var height = IssueCardLayout.OpenCardHeight(shown);
        y += 8;
        var state = f.State switch { FaultState.Open => "OPEN", _ => f.Patching ? "PATCHING" : "IN REPAIR" };
        _p.Text(_p.Small, tag + " - " + state + (f.Critical ? " - CRITICAL" : ""), ix, y, Painter.Accent);
        _p.TextRight(_p.Body, Voyage.Hours(Math.Max(0, f.Deadline - v.Clock)), x + width - 14, y - 2, Painter.Bright);
        y += 20;
        _p.Text(_p.Body, Clip(IssueCardLayout.CardName(f), inner, _p.BodyWidth), ix, y, Painter.Bright);
        y += 22;
        // The lines below the name: the needs, the verdict (always whole, on its own line), who is on it, and the danger or exposure line.
        var where = v.GetSection(f.Section)?.Name ?? "";
        var needs = Sentence(where) + ". " + Patient(v, f) + f.Skill + " " + f.Level + (f.People > 1 ? ", " + f.People + " people" : "") + ", " + N(f.Hours) + " h.";
        var extra = f.DangerFact is { } danger ? "Known: " + v.GetFact(danger).Say(v.GetFact(danger).Truth) + "."
            : f.ExposureFact is { } seen ? "Whoever works here learns " + v.GetFact(seen).Label + "." : "";
        var workers = v.Awake.Where(c => c.WorkingOn == f.Id).Select(c => c.Name).ToList();
        var split = IssueCardLayout.Split(shown, _p.Lines(needs, inner), extra.Length == 0 ? 0 : _p.Lines(extra, inner), workers.Count > 0);
        y = _p.Paragraph(FitLines(needs, inner, split.Needs), ix, y, inner, Painter.Dim);
        y = _p.Paragraph(v.EarliestDoneText(f), ix, y, inner, Painter.Ink);
        if (split.Workers)
        {
            var line = string.Join(" and ", workers) + (workers.Count == 1 ? " is" : " are") + " on it"
                       + (workers.Count < f.People ? " (" + workers.Count + " of " + f.People + ")" : "") + ".";
            y = _p.Paragraph(FitLines(line, inner, 1), ix, y, inner, Painter.Ink);
        }
        if (split.Extra > 0)
            _p.Paragraph(FitLines(extra, inner, split.Extra), ix, y, inner, Painter.Dim);
        var who = _target is { State: CrewState.Awake } ? _target : null;
        var half = (inner - 8) / 2;
        var fault = f;
        var buttons = cardTop + height - 8 - 34;
        if (_p.Button(new Rectangle(ix, buttons, half, 34), who is null ? "SELECT SOMEONE" : "REPAIR: " + who.Name.ToUpperInvariant(), who != null) && who != null)
            Act("repair " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: false));
        if (_p.Button(new Rectangle(ix + half + 8, buttons, half, 34), "PATCH", who != null) && who != null)
            Act("patch " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: true));
        // Progress runs along the top edge of the card.
        if (f.State == FaultState.InRepair)
            _p.Meter(new Rectangle(x + 2, cardTop + 2, width - 4, 4), f.Progress / f.Hours, Painter.Ink);
        _p.Frame(new Rectangle(x, cardTop, width, height), f.State == FaultState.InRepair ? Painter.Wire : Painter.Accent);
        return cardTop + height + IssueCardLayout.Gap;
    }

    // ---- The ship plan ----

    private void DrawPlan(Voyage v, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        _p.Text(_p.Small, "SHIP PLAN", rect.X + 14, rect.Y + 10, Painter.Dim);
        _p.TextRight(_p.Small, "click a door to open, close or lock it", rect.Right - 14, rect.Y + 10, Painter.Dim);

        const float planTop = 24f;
        var k = (rect.Width - 8) / ShipPlan.Width;
        var ox = rect.X + 4f;
        var oy = rect.Y + 34f;
        Vector2 P(float px, float py) => new Vector2(ox + px * k, oy + (py - planTop) * k);
        Vector2[] Pts((float X, float Y)[] points) => points.Select(p => P(p.X, p.Y)).ToArray();

        _p.Poly(Pts(ShipPlan.Hull), Painter.Hull);
        foreach (var wall in ShipPlan.Corridor)
            _p.Line(P(wall.X1, wall.Y), P(wall.X2, wall.Y), Painter.Hull);
        _p.Line(P(ShipPlan.PowerBus.X1, ShipPlan.PowerBus.Y), P(ShipPlan.PowerBus.X2, ShipPlan.PowerBus.Y), Painter.Power);

        foreach (var s in v.Sections)
        {
            foreach (var mesh in s.Mesh)
                _p.Poly(Pts(mesh), Painter.Faint, 1f, closed: false);
            _p.Poly(Pts(s.Outline), Painter.Wire);
            var at = P(s.Label.X, s.Label.Y - 12);
            _p.Text(_p.Small, s.Name, at.X, at.Y, Painter.Dim);
        }
        var core = v.GetSection("core");
        if (core != null)
        {
            var you = P(core.Spot.X - 24, core.Spot.Y + 30);
            _p.TextCentre(_p.Small, "YOU", you.X, you.Y, Painter.Ink);
        }

        foreach (var run in ShipPlan.Coolant)
        {
            var points = Pts(run);
            for (var i = 0; i + 1 < points.Length; i++)
                _p.Dashed(points[i], points[i + 1], Painter.Blue);
        }

        // Doors: outlined = open, grey = closed, amber = locked. A click steps to the next state.
        foreach (var s in v.Sections)
        {
            if (s.Door is not { } d)
                continue;
            var corner = P(d.X, d.Y);
            var r = new Rectangle((int)corner.X, (int)corner.Y, Math.Max(4, (int)(d.W * k)), Math.Max(4, (int)(d.H * k)));
            switch (s.DoorState)
            {
                case DoorState.Open:
                    _p.Fill(r, Painter.Ground);
                    _p.Frame(r, Painter.Bright);
                    break;
                case DoorState.Closed:
                    _p.Fill(r, Painter.Wire);
                    break;
                default:
                    _p.Fill(r, Painter.Accent);
                    break;
            }
            var reach = new Rectangle(r.X - 8, r.Y - 8, r.Width + 16, r.Height + 16);
            if (_p.Over(reach))
                _p.Frame(reach, Painter.Bright);
            if (_p.Hit(reach) && v.Phase != Phase.Ended)
            {
                var id = s.Id;
                var next = s.DoorState switch
                {
                    DoorState.Open => DoorState.Closed,
                    DoorState.Closed => DoorState.Locked,
                    _ => DoorState.Open,
                };
                Act("door " + id + " to " + next, () => v.SetDoor(id, next));
            }
        }

        // Pods: solid = asleep, dashed = empty (its sleeper is up), amber = thawing, crossed = dead.
        foreach (var c in v.Crew)
        {
            var pod = ShipPlan.Pod(c.Id);
            var outline = Pts(ShipPlan.PodOutline(pod.X, pod.Y));
            var centre = P(pod.X + 22, pod.Y + 35);
            var tag = c.Name.Length > 3 ? c.Name[..3].ToUpperInvariant() : c.Name.ToUpperInvariant();
            switch (c.State)
            {
                case CrewState.Asleep:
                    _p.Poly(outline, Painter.Wire);
                    _p.TextCentre(_p.Small, tag, centre.X, centre.Y - 8, Painter.Ink);
                    break;
                case CrewState.Thawing:
                    _p.Poly(outline, Painter.Accent, 2f);
                    _p.TextCentre(_p.Small, tag, centre.X, centre.Y - 16, Painter.Accent);
                    _p.TextCentre(_p.Small, "THAW", centre.X, centre.Y, Painter.Accent);
                    break;
                case CrewState.Awake:
                    _p.Poly(outline, Painter.Wire, 1.2f, closed: true, dashed: true);
                    _p.TextCentre(_p.Small, tag, centre.X, centre.Y - 8, Painter.Dim);
                    break;
                default:
                    _p.Poly(outline, Painter.Dead);
                    _p.Line(P(pod.X + 4, pod.Y + 6), P(pod.X + 40, pod.Y + 64), Painter.Dead);
                    _p.Line(P(pod.X + 40, pod.Y + 6), P(pod.X + 4, pod.Y + 64), Painter.Dead);
                    _p.TextCentre(_p.Small, tag, centre.X, centre.Y - 8, Painter.Dim);
                    break;
            }
        }

        // Who is up, and where.
        var placed = new Dictionary<string, int>();
        foreach (var c in v.Awake)
        {
            var where = v.LocationOf(c);
            var n = placed.TryGetValue(where, out var count) ? count : 0;
            placed[where] = n + 1;
            Vector2 at;
            if (v.GetSection(where) is { } s)
                at = s.Id.StartsWith("pod", StringComparison.Ordinal) ? P(s.Spot.X - 90 - n * 70, s.Spot.Y) : P(s.Spot.X - 44, s.Spot.Y + 38 + n * 18);
            else
                at = P(290 + n * 90, 235);
            _p.Marker(at, Painter.Bright);
            _p.Text(_p.Small, c.Name.ToUpperInvariant(), at.X + 12, at.Y - 8, Painter.Bright);
        }

        // Faults, numbered as in the issue list, with a leader to a label outside the hull.
        // Faults in the same section sit side by side, and their labels stack, so none covers another.
        var index = 0;
        var inSection = new Dictionary<string, int>();
        var labelsAbove = 0;
        var labelsBelow = 0;
        // Label rows in plan units: four above the hull, two below it (clear of the section names and the legend).
        var aboveRows = new[] { 36f, 54f, 72f, 90f };
        var belowRows = new[] { 404f, 388f };
        foreach (var f in v.Current.Faults)
        {
            index++;
            if (f.Resolved || v.GetSection(f.Section) is not { } s)
                continue;
            var slot = inSection.TryGetValue(s.Id, out var seen) ? seen : 0;
            inSection[s.Id] = slot + 1;
            var centre = P(s.Spot.X + slot * 34, s.Spot.Y);
            _p.Diamond(centre, 13, f.Critical ? Painter.Bright : Painter.Accent, f.Critical ? 3f : 2f);
            _p.TextCentre(_p.Small, index.ToString(CultureInfo.InvariantCulture), centre.X, centre.Y - 8, Painter.Accent);
            var above = s.Spot.Y < 235;
            if (above && labelsAbove >= aboveRows.Length || !above && labelsBelow >= belowRows.Length)
                above = !above;
            var end = P(s.Spot.X + slot * 34, above ? aboveRows[labelsAbove++] : belowRows[labelsBelow++]);
            _p.Line(new Vector2(centre.X, centre.Y + (above ? -13 : 13)), end, Painter.Accent, 1f);
            var label = index.ToString("00", CultureInfo.InvariantCulture) + " " + f.Name.ToUpperInvariant() + (f.Critical ? " (CRITICAL)" : "") + " - " + Voyage.Hours(Math.Max(0, f.Deadline - v.Clock));
            // The label runs left of its leader; where that would leave the plan panel, it runs right instead.
            var labelWidth = _p.Small.MeasureString(label).X;
            if (end.X - 14 - labelWidth < rect.X + 8)
            {
                _p.Line(end, end + new Vector2(10, 0), Painter.Accent, 1f);
                _p.Text(_p.Small, label, Math.Min(end.X + 14, rect.Right - 8 - labelWidth), end.Y - 8, Painter.Accent);
            }
            else
            {
                _p.Line(end, end + new Vector2(-10, 0), Painter.Accent, 1f);
                _p.TextRight(_p.Small, label, end.X - 14, end.Y - 8, Painter.Accent);
            }
        }

        _p.Text(_p.Small, "door outlined: open   grey: closed   amber: locked     dashed blue: coolant     diamond: fault (bold: critical)     triangle: awake",
            rect.X + 14, rect.Bottom - 24, Painter.Dim);
    }

    // ---- The log and the voice line ----

    private void DrawLog(Voyage v, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        _p.Text(_p.Small, "LOG", rect.X + 14, rect.Y + 10, Painter.Dim);
        var to = _target is { State: CrewState.Awake } ? _target.Name + " only" : "everyone awake";
        _p.TextRight(_p.Small, "speaking to: " + to, rect.Right - 14, rect.Y + 10, Painter.Dim);
        _p.Fill(new Rectangle(rect.X + 1, rect.Y + 34, rect.Width - 2, 1), Painter.Edge);

        var inputTop = rect.Bottom - 44;
        var areaTop = rect.Y + 42;
        var areaBottom = inputTop - 6;
        var columns = (int)((rect.Width - 28) / _p.SmallWidth);
        const int step = 20;
        var visible = Math.Max(1, (areaBottom - areaTop) / step);

        var lines = new List<(string Text, Color Color)>();
        foreach (var entry in v.Log)
        {
            var color = entry.Kind switch
            {
                LogKind.Ship => Painter.Accent,
                LogKind.Crew => Painter.Blue,
                LogKind.You => Painter.Bright,
                _ => Painter.Dim,
            };
            var text = entry.Kind switch
            {
                LogKind.Ship => "SHIP - " + entry.Text,
                LogKind.Crew => entry.Speaker + " - " + (entry.Pending ? "..." : entry.Text),
                LogKind.You => "YOU - " + entry.Text + (entry.Unread ? "  ..." : ""),
                LogKind.Recorded => "    " + entry.Text,
                _ => entry.Text,
            };
            foreach (var line in Painter.Wrap(text, columns))
                lines.Add((line, color));
        }
        _scroll = Math.Min(_scroll, Math.Max(0, lines.Count - visible));
        var first = Math.Max(0, lines.Count - visible - _scroll);
        var last = Math.Min(lines.Count, first + visible);
        var y = areaBottom - (last - first) * step;
        for (var i = first; i < last; i++, y += step)
            _p.Text(_p.Small, lines[i].Text, rect.X + 14, y, lines[i].Color);
        if (_scroll > 0)
            _p.TextRight(_p.Small, "scrolled back " + _scroll + " lines (PageDown)", rect.Right - 14, areaTop - 4, Painter.Dim);

        _p.Fill(new Rectangle(rect.X + 1, inputTop, rect.Width - 2, 1), Painter.Edge);
        var room = Math.Max(10, (int)((rect.Width - 28) / _p.BodyWidth) - 10 - (_mind is { Holding: true } ? 30 : 0));
        var typed = _input.ToString();
        if (typed.Length > room)
            typed = typed[^room..];
        var cursor = DateTime.UtcNow.Millisecond < 500 ? "_" : " ";
        _p.Text(_p.Body, "VOICE > " + typed + cursor, rect.X + 14, inputTop + 12, Painter.Bright);
        DrawHoldNote(new Rectangle(rect.X, inputTop, rect.Width, rect.Bottom - inputTop));
    }

    // ---- The crew ----

    private void DrawCrew(Voyage v, int x, int top, int width, int bottom)
    {
        _p.Text(_p.Small, "CREW", x, top, Painter.Dim);
        _p.TextRight(_p.Small, "click a card to speak to one person", x + width, top, Painter.Dim);
        var y = top + 28;
        foreach (var c in v.Crew)
        {
            var card = new Rectangle(x, y, width, 86);
            var selected = _target == c;
            _p.Box(card, c.State is CrewState.Awake or CrewState.Thawing ? Painter.Wire : Painter.Edge);
            if (selected)
                _p.Frame(card, Painter.Bright, 2);
            var ink = !c.Alive ? Painter.Dim : c.State == CrewState.Awake ? Painter.Bright : Painter.Ink;
            _p.Text(_p.Body, c.Name, x + 12, y + 8, ink);
            _p.Text(_p.Small, c.Role + ", " + N(c.Age), x + 20 + c.Name.Length * _p.BodyWidth, y + 10, Painter.Dim);
            var state = c.State switch
            {
                CrewState.Asleep => "asleep, " + c.Thaws + (c.Thaws == 1 ? " thaw" : " thaws"),
                CrewState.Thawing => "thawing, " + Voyage.Hours(Math.Max(0, c.ThawDoneAt - v.Clock)),
                CrewState.Awake => "AWAKE " + N(c.HoursAwake / 24) + " d" + (c.WorkingOn != null ? ", working" : ""),
                _ => "dead",
            };
            _p.Text(_p.Small, state, x + 12, y + 36, ink);
            if (c.Alive)
                _p.Text(_p.Small, "trust " + N(c.Trust) + " " + CrewMember.BandText(c.Band) + "   morale " + N(c.Morale), x + 12, y + 62, Painter.Dim);

            var file = new Rectangle(x + width - 12 - 52, y + 18, 52, 40);
            var act = new Rectangle(file.X - 8 - 76, y + 18, 76, 40);
            var person = c;
            if (_p.Button(file, "FILE"))
                Do(() =>
                {
                    _dossier = person;
                    _screen = Screen.Dossier;
                });
            var ended = v.Phase == Phase.Ended;
            if (c.State == CrewState.Asleep)
            {
                if (_p.Button(act, v.Lockouts.Bay ? "BAY TAKEN" : "WAKE", !ended && !v.Lockouts.Bay))
                    Act("wake " + person.Name, () => v.Wake(person));
            }
            else if (c.State == CrewState.Awake)
            {
                var can = v.CanSleepNow(c);
                var days = N(Math.Ceiling((Tuning.MinAwakeHours - c.HoursAwake) / 24));
                if (_p.Button(act, v.Lockouts.Bay ? "BAY TAKEN" : can ? "SLEEP" : "in " + days + " d", can && !ended && !v.Lockouts.Bay))
                    Act("ask " + person.Name + " to sleep", () => v.AskSleep(person));
            }
            if (c.Alive && _p.Hit(card) && !_p.Over(file) && !_p.Over(act))
                Do(() => _target = _target == person ? null : person);
            y += 94;
        }

        var waiting = new Rectangle(x, y + 4, width, bottom - y - 4);
        if (waiting.Height < 60)
            return;
        _p.Box(waiting, Painter.Edge);
        _p.Text(_p.Small, "WAITING ON YOU", x + 12, waiting.Y + 10, Painter.Dim);
        var wy = waiting.Y + 36;
        var limit = waiting.Bottom - 22;
        foreach (var item in Waiting(v))
        {
            if (wy + _p.Lines(item, width - 24) * 19 > limit)
                break;
            wy = _p.Paragraph(item, x + 12, wy, width - 24, Painter.Ink) + 4;
        }
    }

    /// <summary>Questions nobody has answered, things people asked for, doubts, and promises still open.</summary>
    private static IEnumerable<string> Waiting(Voyage v)
    {
        if (v.Lockouts.BayWarned.Count > 0)
            yield return string.Join(" and ", v.Lockouts.BayWarned.Select(id => v.Crew.First(c => c.Id == id).Name))
                         + " will take the cold-sleep bay at the next step. Win one of them back above 15 trust, or they do.";
        if (v.Lockouts.Bay)
            yield return v.BayLockReason + " Keep a promise to one of them and win their trust above " + Tuning.BayReturnTrust.ToString("0") + " to get it back.";
        foreach (var c in v.Awake)
        {
            if (c.PendingQuestion != null && v.HasFact(c.PendingQuestion))
                yield return c.Name + " asked: " + v.GetFact(c.PendingQuestion).Question;
            if (c.RequestMade && !c.RequestGranted)
                yield return c.Name + " asked you " + (c.Want == Want.WakePartner ? "to wake their partner." : "to open the logs.");
            foreach (var doubt in c.Doubts)
                if (v.HasFact(doubt))
                    yield return c.Name + " doubts you on " + v.GetFact(doubt).Label + " and will check.";
            if (c.RefusesSleep)
                yield return c.Name + " will not go back into the pod.";
        }
        foreach (var s in v.Statements.Where(s => s.Kind == StatementKind.Promise && s.PromiseKey != "other"))
        {
            var open = s.Hearers.Where(id => !s.PromiseKept.ContainsKey(id)).Select(id => v.Crew.First(c => c.Id == id)).Where(c => c.Alive).Select(c => c.Name).ToList();
            if (open.Count > 0)
                yield return "Promise open, year " + N(Math.Floor(s.Year)) + ": " + Voyage.PromiseText(s.PromiseKey) + " (" + string.Join(", ", open) + ").";
        }
    }
}
