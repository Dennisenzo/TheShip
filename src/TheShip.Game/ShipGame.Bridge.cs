using System.Globalization;
using Microsoft.Xna.Framework;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>The bridge screen: issues at hand, the ship plan, the log with the voice line, and the crew.</summary>
public sealed partial class ShipGame
{
    private static string N(double n) => n.ToString("0", CultureInfo.InvariantCulture);

    private static string Sentence(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    /// <summary>A section's name as a sentence start, but keeping single letters upper case: "POD BANK A" becomes "Pod bank A".</summary>
    private static string SectionName(string s) =>
        string.Join(' ', s.Split(' ').Select((w, i) => w.Length == 1 ? w.ToUpperInvariant() : i == 0 ? Sentence(w) : w.ToLowerInvariant()));

    /// <summary>A non-breaking space (<see cref="Painter.Wrap"/> shows it as a space but never breaks a line at it): "12 h" stays together.</summary>
    private const char Nb = '\u00A0';

    /// <summary>Keeps a number and its unit on one line ("12 h", "3 days").</summary>
    private static string Glue(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"(?<=\d) (?=(h|hours?|d|days?|years?)\b)", Nb.ToString());

    private void DrawBridge(Voyage v)
    {
        // What the mouse was over in the last frame lights up wherever else it is drawn in this one.
        _hotFault = _hotFaultNext;
        _hotCrew = _hotCrewNext;
        _hotFaultNext = null;
        _hotCrewNext = null;
        Header(v, "THE SHIP", back: false);
        var right = Painter.ScreenWidth - 16;
        if (_p.Button(new Rectangle(right - 80, 8, 80, 40), "QUIT"))
            Do(Exit);
        if (_p.Button(new Rectangle(right - 288, 8, 200, 40), "RECORD AND LOGS"))
            Do(() => _screen = Screen.Record);
        if (_p.Button(new Rectangle(right - 400, 8, 104, 40), "BRIEFING", tip: "What this voyage is, what you can do and what it costs."))
            Do(() =>
            {
                _briefing = true;
                _session?.Line("click", "briefing reopened");
            });
        DrawStats(v, right - (v.Phase == Phase.Ended ? 532 : 412));

        DrawIssues(v, 16, 72, 330, Painter.ScreenHeight - 16);
        DrawPlan(v, PlanRect);
        var log = new Rectangle(358, 540, 864, Painter.ScreenHeight - 16 - 540);
        DrawLog(v, log);
        DrawBanner(log);
        DrawCrew(v, 1234, 72, 350, Painter.ScreenHeight - 16);
    }

    // ---- Issues at hand ----

    private void DrawIssues(Voyage v, int x, int top, int width, int bottom)
    {
        _p.Text(_p.Small, "ISSUES AT HAND", x, top, Painter.Dim);
        var y = top + 28;
        var inner = width - 28;
        var ix = x + 14;
        if (v.Current.Faults.Count == 0 && !Drifting)
            y = _p.Paragraph(v.Phase == Phase.Ended ? "The voyage is over." : "Nothing needs repair.", x, y, width, Painter.Ink);

        // One fault gets a full card; two or more get compact cards, sized by how many there are, that always fit the column (IssueCardLayout).
        var compact = v.Current.Faults.Count >= 2;
        var shown = v.Current.Faults.Count;
        var number = 0;
        // After a drift the faults of the new alert appear one after the other (FaultsRevealed).
        foreach (var f in v.Current.Faults.Take(FaultsRevealed(v)))
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
                _p.TextRight(_p.Head, Voyage.Hours(ShownLeft(v, f)), x + width - 14, y - 4, Painter.Bright);
            y += 24;
            _p.Text(_p.Head, IssueCardLayout.CardName(f), ix, y, f.Resolved ? Painter.Dim : Painter.Bright);
            y += 32;

            if (!f.Resolved)
            {
                var where = v.GetSection(f.Section)?.Name ?? "";
                var needs = SectionName(where) + ". " + Patient(v, f) + "Needs " + f.Skill.ToString().ToLowerInvariant() + Nb + f.Level
                            + (f.People > 1 ? ", " + f.People + Nb + "people" : "") + ", " + N(f.Hours) + Nb + "hours. If the clock runs out: " + f.IfIgnored + ".";
                y = _p.Paragraph(needs, ix, y, inner, Painter.Dim);
                var verdict = v.EarliestDoneText(f);
                y = _p.Paragraph(Glue("If started now: " + char.ToLowerInvariant(verdict[0]) + verdict[1..]), ix, y, inner, Painter.Ink) + 6;

                var workers = v.Awake.Where(c => c.WorkingOn == f.Id).Select(c => c.Name).ToList();
                if (f.State == FaultState.InRepair)
                {
                    _p.Meter(new Rectangle(ix, y, inner, 8), ShownProgress(f) / f.Hours, Painter.Ink);
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

                WorkButtons(v, f, ix, y, (inner - 8) / 2, 40, x + width + 8);
                y += 40;
            }
            y += 12;
            var card = new Rectangle(x, cardTop, width, y - cardTop);
            if (_p.Over(card) && !f.Resolved)
                _hotFaultNext = f.Id;
            var edge = f.Resolved ? Painter.Edge : f.State == FaultState.InRepair ? Painter.Wire : Painter.Accent;
            _p.Frame(card, Color.Lerp(edge, Painter.Bright, _p.Ease("issue:" + f.Id, _hotFault == f.Id ? 1f : 0f)));
            y += 10;
        }

        var clock = new Rectangle(x, bottom - 150, width, 150);
        _p.Box(clock, Painter.Edge);
        var day = (int)(ShownClock(v) % Tuning.HoursPerYear / 24) + 1;
        _p.Text(_p.Small, "CLOCK", ix, clock.Y + 12, Painter.Dim);
        _p.TextRight(_p.Small, "year " + N(Math.Floor(ShownClock(v) / Tuning.HoursPerYear)) + ", day " + day, x + width - 14, clock.Y + 12, Painter.Dim);
        if (v.Phase == Phase.Aftermath)
        {
            // Until something needs you (a crew line, a death, a person who can sleep); the small button is the old two-week step.
            var wide = inner * 2 / 3;
            if (_p.Button(new Rectangle(ix, clock.Y + 44, wide, 40), ClockLabel("advance until something happens", "UNTIL SOMETHING HAPPENS"), true, primary: true,
                    tip: "Moves the clock in two-week steps until a crew member speaks, someone can go back to sleep, or something goes wrong."))
                Act("advance until something happens", () =>
                {
                    var result = v.GoUntilAttention();
                    _lastAdvance = result;
                    _session?.Line("advance", result.Steps + " step(s), stopped: " + (result.Reason.Length == 0 ? "(step limit or nothing to do)" : result.Reason));
                }, snapshot: true, skip: SkipKind.Weeks);
            if (_p.Button(new Rectangle(ix + wide + 8, clock.Y + 44, inner - wide - 8, 40), ClockLabel("advance two weeks", "2 WEEKS", "WAITING")))
                Act("advance two weeks", v.Go, snapshot: true, skip: SkipKind.Weeks);
        }
        else if (_p.Button(new Rectangle(ix, clock.Y + 44, inner, 40), ClockLabel("advance to next event", "ADVANCE TO NEXT EVENT"), v.Phase != Phase.Ended, primary: true,
                     tip: v.Phase == Phase.Ended ? "The voyage is over." : "Moves the clock to the next thing that happens: a thaw done, a repair done, a fault's clock running out."))
            Act("advance to next event", v.Go, snapshot: true, skip: SkipKind.Hours);
        var driftTip = v.Phase == Phase.Alert ? "The alert is not over."
            : v.Phase == Phase.Ended ? "The voyage is over."
            : "Years pass until the next alert. Whoever is still awake stays awake, and eats.";
        if (_p.Button(new Rectangle(ix, clock.Y + 94, inner, 40), ClockLabel("drift to the next alert", "DRIFT TO THE NEXT ALERT"), v.Phase == Phase.Aftermath, tip: driftTip))
            Act("drift to the next alert", v.Drift, snapshot: true, skip: SkipKind.Drift);
    }

    /// <summary>A clock button whose click waits for a spoken line to be read says so (the small 2 WEEKS button in one word).</summary>
    private string ClockLabel(string what, string label, string waiting = "WAITING FOR YOUR LINE") => _held.Contains(what) ? waiting : label;

    /// <summary>
    /// Repair and Patch on an issue card. With a person selected they go to that person; with nobody selected they open a
    /// picker of who is awake (<see cref="DrawPicker"/>).
    /// </summary>
    private void WorkButtons(Voyage v, Fault fault, int x, int y, int half, int height, int pickerX)
    {
        var who = _target is { State: CrewState.Awake } ? _target : null;
        var anyone = v.Awake.Any();
        var skill = fault.Skill.ToString().ToLowerInvariant();
        var fit = who is null
            ? (anyone ? "Choose who to ask." : "Nobody is awake.")
            : who.Name + " has " + skill + " " + who.SkillOf(fault.Skill) + "; this needs " + fault.Level + ".";
        if (_p.Button(new Rectangle(x, y, half, height), who is null ? "REPAIR" : "REPAIR: " + who.Name.ToUpperInvariant(), anyone, tip: fit))
        {
            if (who != null)
                Act("repair " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: false));
            else
                Do(() => _picker = (fault, false, pickerX, y));
        }
        if (_p.Button(new Rectangle(x + half + 8, y, half, height), "PATCH", anyone, tip: fit + " A patch is quicker, and gives way again later."))
        {
            if (who != null)
                Act("patch " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: true));
            else
                Do(() => _picker = (fault, true, pickerX, y));
        }
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
        y += 8;
        var state = f.State switch { FaultState.Open => "OPEN", _ => f.Patching ? "PATCHING" : "IN REPAIR" };
        _p.Text(_p.Small, tag + " - " + state + (f.Critical ? " - CRITICAL" : ""), ix, y, Painter.Accent);
        _p.TextRight(_p.Body, Voyage.Hours(ShownLeft(v, f)), x + width - 14, y - 2, Painter.Bright);
        y += 20;
        _p.Text(_p.Body, Clip(IssueCardLayout.CardName(f), inner, _p.BodyWidth), ix, y, Painter.Bright);
        y += 22;
        // The lines below the name: the needs, the verdict (always whole, on its own line), who is on it, and the danger or exposure line.
        var where = v.GetSection(f.Section)?.Name ?? "";
        var needs = SectionName(where) + ". " + Patient(v, f) + f.Skill + Nb + f.Level + (f.People > 1 ? ", " + f.People + Nb + "people" : "") + ", " + N(f.Hours) + Nb + "h.";
        var extra = f.DangerFact is { } danger ? "Known: " + v.GetFact(danger).Say(v.GetFact(danger).Truth) + "."
            : f.ExposureFact is { } seen ? "Whoever works here learns " + v.GetFact(seen).Label + "." : "";
        var workers = v.Awake.Where(c => c.WorkingOn == f.Id).Select(c => c.Name).ToList();
        var split = IssueCardLayout.Split(shown, _p.Lines(needs, inner), extra.Length == 0 ? 0 : _p.Lines(extra, inner), workers.Count > 0);
        var verdict = Glue(v.EarliestDoneText(f));
        // The card is as tall as its text (never more than the per-count maximum), with the buttons right below it.
        var textLines = split.Needs + _p.Lines(verdict, inner) + (split.Workers ? 1 : 0) + split.Extra;
        var height = Math.Min(IssueCardLayout.OpenCardHeight(shown), IssueCardLayout.CardTop + textLines * IssueCardLayout.LineHeight + IssueCardLayout.CardBottom);
        y = _p.Paragraph(FitLines(needs, inner, split.Needs), ix, y, inner, Painter.Dim);
        y = _p.Paragraph(verdict, ix, y, inner, Painter.Ink);
        if (split.Workers)
        {
            var line = string.Join(" and ", workers) + (workers.Count == 1 ? " is" : " are") + " on it"
                       + (workers.Count < f.People ? " (" + workers.Count + " of " + f.People + ")" : "") + ".";
            y = _p.Paragraph(FitLines(line, inner, 1), ix, y, inner, Painter.Ink);
        }
        if (split.Extra > 0)
            _p.Paragraph(FitLines(extra, inner, split.Extra), ix, y, inner, Painter.Dim);
        WorkButtons(v, f, ix, cardTop + height - 8 - 34, (inner - 8) / 2, 34, x + width + 8);
        // Progress runs along the top edge of the card.
        if (f.State == FaultState.InRepair)
            _p.Meter(new Rectangle(x + 2, cardTop + 2, width - 4, 4), ShownProgress(f) / f.Hours, Painter.Ink);
        var card = new Rectangle(x, cardTop, width, height);
        if (_p.Over(card))
            _hotFaultNext = f.Id;
        _p.Frame(card, Color.Lerp(f.State == FaultState.InRepair ? Painter.Wire : Painter.Accent, Painter.Bright, _p.Ease("issue:" + f.Id, _hotFault == f.Id ? 1f : 0f)));
        return cardTop + height + IssueCardLayout.Gap;
    }

    // ---- The ship plan ----

    private void DrawPlan(Voyage v, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        _p.Text(_p.Small, "SHIP PLAN", rect.X + 14, rect.Y + 10, Painter.Dim);
        // The next useful step, where the eye already is.
        _p.TextRight(_p.Small, "NEXT: " + NextStep(v), rect.Right - 14, rect.Y + 10, Drifting ? Painter.Dim : Painter.Accent);

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

        var hotFault = _hotFault is null ? null : v.Current.Faults.FirstOrDefault(f => f.Id == _hotFault);
        foreach (var s in v.Sections)
        {
            foreach (var mesh in s.Mesh)
                _p.Poly(Pts(mesh), Painter.Faint, 1f, closed: false);
            var outline = Pts(s.Outline);
            // The section of the fault under the mouse (on its card or on the plan) lights up.
            var lit = _p.Ease("section:" + s.Id, hotFault != null && hotFault.Section == s.Id ? 1f : 0f);
            _p.Poly(outline, Color.Lerp(Painter.Wire, Painter.Bright, lit), 1.5f + lit);
            var at = P(s.Label.X, s.Label.Y - 12);
            _p.Text(_p.Small, s.Name, at.X, at.Y, Painter.Dim);

            var here = v.Awake.Where(c => v.LocationOf(c) == s.Id).Select(c => c.Name).ToList();
            var faults = v.Current.Faults.Where(f => !f.Resolved && f.Section == s.Id).Select(f => IssueCardLayout.CardName(f)).ToList();
            _p.Tip(Bounds(outline), SectionName(s.Name) + "."
                                    + (s.Door is null ? "" : " Door " + s.DoorState.ToString().ToLowerInvariant() + ".")
                                    + (here.Count > 0 ? " Here: " + string.Join(", ", here) + "." : "")
                                    + (faults.Count > 0 ? " Faults: " + string.Join(", ", faults) + "." : ""));
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
                _p.Dashed(points[i], points[i + 1], Painter.Blue, offset: Motion.Still ? 0f : (float)(_now * 10 % 12));
        }

        // Where the people who are up stand, worked out before the doors: a marker can sit over a door's click area
        // (the idle spot in the corridor does), and then the marker wins.
        var markers = new List<(CrewMember Person, Vector2 At, string Name, Rectangle Backing)>();
        var placed = new Dictionary<string, int>();
        foreach (var c in v.Awake)
        {
            var where = v.LocationOf(c);
            var n = placed.TryGetValue(where, out var count) ? count : 0;
            placed[where] = n + 1;
            Vector2 at;
            if (v.GetSection(where) is { } s)
                at = s.Id.StartsWith("pod", StringComparison.Ordinal) ? P((s.Id == "podA" ? 400 : 580) + n * 80, 235) : P(s.Spot.X - 44, s.Spot.Y + 38 + n * 18);
            else
                at = P(290 + n * 90, 235);
            var name = c.Name.ToUpperInvariant();
            markers.Add((c, at, name, new Rectangle((int)at.X - 8, (int)at.Y - 9, (int)(_p.Small.MeasureString(name).X + 28), 18)));
        }
        var overMarker = markers.Any(m => _p.Over(m.Backing));

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
            var next = s.DoorState switch
            {
                DoorState.Open => DoorState.Closed,
                DoorState.Closed => DoorState.Locked,
                _ => DoorState.Open,
            };
            var overDoor = _p.Over(reach) && !overMarker;
            var doorHot = _p.Ease("door:" + s.Id, overDoor ? 1f : 0f);
            if (doorHot > 0.01f)
                _p.Frame(reach, Painter.Fade(Painter.Bright, doorHot));
            if (overDoor && v.Phase != Phase.Ended)
                _p.Hand = true;
            if (!overMarker)
                _p.Tip(reach, "Door to the " + s.Name.ToLowerInvariant() + ": " + s.DoorState.ToString().ToLowerInvariant() + "."
                          + (v.Phase == Phase.Ended ? "" : " Click: " + (next == DoorState.Locked ? "lock" : next == DoorState.Closed ? "close" : "open") + ".")
                          + (s.DoorState == DoorState.Locked ? " A locked door stops work behind it, and whoever tries it sees it is locked." : ""));
            if (overDoor && _p.Click && v.Phase != Phase.Ended)
            {
                var id = s.Id;
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
            // A pod lights up with its person's crew card, and a click on it selects them like a click on the card.
            var podRect = Bounds(outline);
            var shown = ShownState(c);
            if (_p.Over(podRect))
            {
                _hotCrewNext = c.Id;
                if (c.Alive)
                    _p.Hand = true;
            }
            _p.Tip(podRect, c.Name + ", " + c.Role + ": " + (shown switch
            {
                CrewState.Asleep => "asleep",
                CrewState.Thawing => "thawing",
                CrewState.Awake => "awake, the pod is empty",
                _ => "dead",
            }) + ". Thawed " + c.Thaws + (c.Thaws == 1 ? " time." : " times."));
            if (c.Alive && _p.Hit(podRect))
            {
                var person = c;
                Do(() => _target = _target == person ? null : person);
            }
            var podLit = _p.Ease("pod:" + c.Id, _hotCrew == c.Id ? 1f : 0f);
            if (podLit > 0.01f)
                _p.Frame(new Rectangle(podRect.X - 4, podRect.Y - 4, podRect.Width + 8, podRect.Height + 8), Painter.Fade(Painter.Bright, podLit));
            switch (shown)
            {
                case CrewState.Asleep:
                    _p.Poly(outline, Painter.Wire);
                    _p.TextCentre(_p.Small, tag, centre.X, centre.Y - 8, Painter.Ink);
                    break;
                case CrewState.Thawing:
                    // A thawing pod breathes.
                    _p.Poly(outline, Painter.Fade(Painter.Accent, 0.45f + 0.55f * Motion.Pulse(_now, 1.2f)), 2f);
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
            if (f.Resolved || index > FaultsRevealed(v) || v.GetSection(f.Section) is not { } s)
                continue;
            var slot = inSection.TryGetValue(s.Id, out var seen) ? seen : 0;
            inSection[s.Id] = slot + 1;
            var centre = P(s.Spot.X + slot * 34, s.Spot.Y);
            var mark = new Rectangle((int)centre.X - 15, (int)centre.Y - 15, 30, 30);
            if (_p.Over(mark))
                _hotFaultNext = f.Id;
            _p.Tip(mark, IssueCardLayout.CardName(f) + (f.Critical ? " (critical)" : "") + ": " + Voyage.Hours(ShownLeft(v, f)) + " left.");
            var faultLit = _p.Ease("fault:" + f.Id, _hotFault == f.Id ? 1f : 0f);
            // A fault pulses, and faster once under a quarter of its clock is left.
            var urgent = ShownLeft(v, f) < 0.25 * f.ClockHours;
            var beat = 0.55f + 0.45f * Motion.Pulse(_now, urgent ? 0.5f : 1.8f);
            _p.Diamond(centre, 13 + 4 * faultLit, Painter.Fade(f.Critical || faultLit > 0.5f ? Painter.Bright : Painter.Accent, beat), f.Critical ? 3f : 2f);
            _p.TextCentre(_p.Small, index.ToString(CultureInfo.InvariantCulture), centre.X, centre.Y - 8, Painter.Accent);
            var above = s.Spot.Y < 235;
            if (above && labelsAbove >= aboveRows.Length || !above && labelsBelow >= belowRows.Length)
                above = !above;
            var end = P(s.Spot.X + slot * 34, above ? aboveRows[labelsAbove++] : belowRows[labelsBelow++]);
            _p.Line(new Vector2(centre.X, centre.Y + (above ? -13 : 13)), end, Painter.Accent, 1f);
            var label = index.ToString("00", CultureInfo.InvariantCulture) + " " + f.Name.ToUpperInvariant() + (f.Critical ? " (CRITICAL)" : "") + " - " + Voyage.Hours(ShownLeft(v, f));
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

        // Who is up, and where (placed above, before the doors).
        foreach (var (c, at, name, backing) in markers)
        {
            // Drawn after the fault leaders, on a panel-coloured backing, so no leader runs through a name.
            _p.Fill(backing, Painter.Panel);
            if (_p.Over(backing))
            {
                _hotCrewNext = c.Id;
                _p.Hand = true;
            }
            if (_p.Hit(backing))
            {
                var person = c;
                Do(() => _target = _target == person ? null : person);
            }
            var markerInk = _target == c ? Painter.Accent : Painter.Bright;
            _p.Marker(at, markerInk);
            _p.Text(_p.Small, name, at.X + 12, at.Y - 8, markerInk);
            // The selected person has a frame; so does the person whose card is under the mouse.
            if (_target == c || _hotCrew == c.Id)
                _p.Frame(new Rectangle(backing.X - 2, backing.Y - 2, backing.Width + 4, backing.Height + 4), _target == c ? Painter.Accent : Painter.Bright);
        }

        _p.Text(_p.Small, "rest the mouse on anything for what it is   door: outlined open, grey closed, amber locked", rect.X + 14, rect.Bottom - 20, Painter.Dim);
    }

    // ---- The log and the voice line ----

    /// <summary>How many log entries the last frame showed, and from which entry on (and since when) lines are fading in.</summary>
    private int _logKnown;
    private int _logFadeFrom;
    private double _logFadeAt;
    /// <summary>The log's length at the last clock move: entries from here on are new since then.</summary>
    private int _logMark;

    private void DrawLog(Voyage v, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        _p.Text(_p.Small, "LOG", rect.X + 14, rect.Y + 10, Painter.Dim);
        _p.TextRight(_p.Small, "type to speak; Enter says it", rect.Right - 14, rect.Y + 10, Painter.Dim);
        _p.Fill(new Rectangle(rect.X + 1, rect.Y + 34, rect.Width - 2, 1), Painter.Edge);

        var inputTop = rect.Bottom - 44;
        var areaTop = rect.Y + 42;
        var areaBottom = inputTop - 6;
        var columns = (int)((rect.Width - 28) / _p.SmallWidth);
        const int step = 20;
        var visible = Math.Max(1, (areaBottom - areaTop) / step);

        // Lines that are new since the last frame fade in; lines since the last clock move have a mark in the margin.
        var count = ShownLogCount(v);
        if (count > _logKnown)
        {
            _logFadeFrom = _logKnown;
            _logFadeAt = _now;
        }
        _logKnown = count;
        var fresh = Motion.Progress(_now, _logFadeAt, Motion.LogFade);

        var lines = new List<(string Text, Color Color, int Entry)>();
        var entryIndex = -1;
        foreach (var entry in v.Log.Take(count))
        {
            entryIndex++;
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
                lines.Add((line, color, entryIndex));
        }
        _scroll = Math.Min(_scroll, Math.Max(0, lines.Count - visible));
        var first = Math.Max(0, lines.Count - visible - _scroll);
        var last = Math.Min(lines.Count, first + visible);
        var y = areaBottom - (last - first) * step;
        for (var i = first; i < last; i++, y += step)
        {
            var isFresh = lines[i].Entry >= _logFadeFrom && fresh < 1f;
            _p.Text(_p.Small, lines[i].Text, rect.X + 14, y, isFresh ? Painter.Fade(lines[i].Color, 0.2f + 0.8f * fresh) : lines[i].Color);
            if (_logMark > 0 && lines[i].Entry >= _logMark)
                _p.Fill(new Rectangle(rect.X + 5, y + 2, 2, 15), Painter.Fade(Painter.Accent, 0.7f));
        }
        if (_scroll > 0)
            _p.TextRight(_p.Small, "scrolled back " + _scroll + " lines (PageDown)", rect.Right - 14, areaTop - 4, Painter.Dim);

        _p.Fill(new Rectangle(rect.X + 1, inputTop, rect.Width - 2, 1), Painter.Edge);
        // The prompt names who hears the line: one selected person, or everyone awake.
        var hearers = Hearers(v);
        var prompt = v.Phase == Phase.Ended ? "VOICE > "
            : hearers.Count == 0 ? "TO NOBODY > "
            : "TO " + string.Join(", ", hearers.Select(h => h.Name.ToUpperInvariant())) + " > ";
        var room = Math.Max(10, (int)((rect.Width - 28) / _p.BodyWidth) - prompt.Length - 2 - (_mind is { Holding: true } ? 30 : 0));
        var typed = _input.ToString();
        if (typed.Length > room)
            typed = typed[^room..];
        var cursor = _now % 1.0 < 0.5 ? "_" : " ";
        if (hearers.Count == 0 && typed.Length == 0 && v.Phase != Phase.Ended)
            _p.Text(_p.Body, "Nobody is awake to hear you.", rect.X + 14, inputTop + 12, Painter.Dim);
        else
        {
            _p.Text(_p.Body, prompt, rect.X + 14, inputTop + 12, hearers.Count == 0 ? Painter.Dim : Painter.Accent);
            _p.Text(_p.Body, typed + cursor, rect.X + 14 + prompt.Length * _p.BodyWidth, inputTop + 12, Painter.Bright);
        }
        DrawHoldNote(new Rectangle(rect.X, inputTop, rect.Width, rect.Bottom - inputTop));
    }

    // ---- The crew ----

    private void DrawCrew(Voyage v, int x, int top, int width, int bottom)
    {
        _p.Text(_p.Small, "CREW", x, top, Painter.Dim);
        var alone = _target is { State: CrewState.Awake } ? _target : null;
        _p.TextRight(_p.Small, alone is null ? "click a card to speak to one person" : "you speak to " + alone.Name + " alone; repairs go to " + alone.Name,
            x + width, top, alone is null ? Painter.Dim : Painter.Accent);
        var up = v.Crew.Count(c => c.State is CrewState.Awake or CrewState.Thawing);
        var y = top + 28;
        foreach (var c in v.Crew)
        {
            var card = new Rectangle(x, y, width, 86);
            var selected = _target == c;
            var over = _p.Over(card);
            if (over)
            {
                _hotCrewNext = c.Id;
                if (c.Alive)
                    _p.Hand = true;
            }
            // The card lights up under the mouse, and with its pod or marker on the plan.
            var lit = _p.Ease("crew:" + c.Id, (over && c.Alive) || _hotCrew == c.Id ? 1f : 0f);
            _p.Box(card, Color.Lerp(c.State is CrewState.Awake or CrewState.Thawing ? Painter.Wire : Painter.Edge, Painter.Bright, lit));
            if (selected)
            {
                _p.Frame(card, Painter.Bright, 2);
                _p.Fill(new Rectangle(card.X, card.Y, 4, card.Height), Painter.Accent);
            }
            // While the years roll the card keeps the state from before the drift, as the pod does (ShownState).
            var shown = ShownState(c);
            var ink = shown == CrewState.Dead ? Painter.Dim : shown == CrewState.Awake ? Painter.Bright : Painter.Ink;
            _p.Text(_p.Body, c.Name, x + 12, y + 8, ink);
            _p.Text(_p.Small, c.Role + ", " + N(ShownAge(c)), x + 20 + c.Name.Length * _p.BodyWidth, y + 10, Painter.Dim);
            var state = shown switch
            {
                CrewState.Asleep => "asleep, " + c.Thaws + (c.Thaws == 1 ? " thaw" : " thaws"),
                CrewState.Thawing => "thawing, " + Voyage.Hours(ShownThawLeft(v, c)),
                CrewState.Awake => "AWAKE " + N(ShownHoursAwake(c) / 24) + " d" + (c.WorkingOn != null ? ", working" : ""),
                _ => "dead",
            };
            _p.Text(_p.Small, state, x + 12, y + 36, ink);
            if (shown != CrewState.Dead)
            {
                // A change flashes and shows its size, but not before the line that explains it is in the log.
                var hold = _transition is not null || LinePending(v, c);
                var trust = Track("trust:" + c.Id, c.Trust, hold, out var trustFlash, out var trustDelta);
                var morale = Track("morale:" + c.Id, c.Morale, hold, out var moraleFlash, out var moraleDelta);
                var band = BandOf(trust);
                var tx = x + 12f;
                var trustText = "trust " + N(trust) + " " + CrewMember.BandText(band);
                _p.Text(_p.Small, trustText, tx, y + 62, Color.Lerp(Painter.Dim, Painter.Bright, trustFlash));
                _p.Tip(new Rectangle(x + 8, y + 58, (int)(trustText.Length * _p.SmallWidth) + 8, 24), Manner(band));
                tx += (trustText.Length + 1) * _p.SmallWidth;
                if (trustDelta.Length > 0)
                {
                    _p.Text(_p.Small, trustDelta, tx, y + 62, Painter.Accent);
                    tx += (trustDelta.Length + 1) * _p.SmallWidth;
                }
                var moraleText = "morale " + N(morale);
                _p.Text(_p.Small, moraleText, tx + _p.SmallWidth, y + 62, Color.Lerp(Painter.Dim, Painter.Bright, moraleFlash));
                if (moraleDelta.Length > 0)
                    _p.Text(_p.Small, moraleDelta, tx + (moraleText.Length + 2) * _p.SmallWidth, y + 62, Painter.Accent);
            }

            var file = new Rectangle(x + width - 12 - 52, y + 18, 52, 40);
            var act = new Rectangle(file.X - 8 - 76, y + 18, 76, 40);
            var person = c;
            if (_p.Button(file, "FILE", tip: "The dossier: who " + c.Name + " is, what they know and what you told them."))
                Do(() =>
                {
                    _dossier = person;
                    _screen = Screen.Dossier;
                });
            var ended = v.Phase == Phase.Ended;
            if (c.State == CrewState.Asleep)
            {
                var wakeTip = up >= Tuning.MaxAwake
                    ? Tuning.MaxAwake + " people are up already; that is the most at once."
                    : "The thaw takes " + N(Tuning.ThawHours) + " hours and ages " + c.Name + " " + Tuning.ThawAgeMin + " to " + Tuning.ThawAgeMax + " years.";
                if (_p.Button(act, "WAKE", !ended, tip: wakeTip))
                    Act("wake " + person.Name, () => v.Wake(person));
            }
            else if (c.State == CrewState.Awake)
            {
                var can = v.CanSleepNow(c);
                var days = N(Math.Ceiling((Tuning.MinAwakeHours - c.HoursAwake) / 24));
                var sleepTip = can
                    ? "Ask " + c.Name + " to go back into the pod. They may refuse."
                    : "A pod takes a person back after " + N(Tuning.MinAwakeHours / 24) + " days awake at the earliest: " + days + " to go.";
                if (_p.Button(act, can ? "SLEEP" : "in " + days + " d", can && !ended, tip: sleepTip))
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
        foreach (var (who, item) in Waiting(v))
        {
            var rows = _p.Lines(item, width - 24);
            if (wy + rows * 19 > limit)
                break;
            // An item about a person selects that person when clicked.
            var row = new Rectangle(x + 4, wy - 2, width - 8, rows * 19 + 4);
            if (who != null && _p.Over(row))
            {
                _p.Fill(row, Painter.Track);
                _p.Hand = true;
                _hotCrewNext = who.Id;
                if (_p.Click)
                {
                    var person = who;
                    Do(() => _target = person);
                }
            }
            wy = _p.Paragraph(item, x + 12, wy, width - 24, Painter.Ink) + 4;
        }
    }

    /// <summary>Questions nobody has answered, things people asked for, doubts, and promises still open; each with the person it is about, if one.</summary>
    private static IEnumerable<(CrewMember? Who, string Text)> Waiting(Voyage v)
    {
        foreach (var c in v.Awake)
        {
            if (c.PendingQuestion != null && v.HasFact(c.PendingQuestion))
                yield return (c, c.Name + " asked: " + v.GetFact(c.PendingQuestion).Question);
            if (c.RequestMade && !c.RequestGranted)
                yield return (c, c.Name + " asked you " + (c.Want == Want.WakePartner ? "to wake their partner." : "to open the logs."));
            foreach (var doubt in c.Doubts)
                if (v.HasFact(doubt))
                    yield return (c, c.Name + " doubts you on " + v.GetFact(doubt).Label + " and will check.");
            if (c.RefusesSleep)
                yield return (c, c.Name + " will not go back into the pod.");
        }
        foreach (var s in v.Statements.Where(s => s.Kind == StatementKind.Promise && s.PromiseKey != "other"))
        {
            var open = s.Hearers.Where(id => !s.PromiseKept.ContainsKey(id)).Select(id => v.Crew.First(c => c.Id == id)).Where(c => c.Alive).Select(c => c.Name).ToList();
            if (open.Count > 0)
                yield return (null, "Promise open, year " + N(Math.Floor(s.Year)) + ": " + Voyage.PromiseText(s.PromiseKey) + " (" + string.Join(", ", open) + ").");
        }
    }
}
