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
        var right = Painter.ScreenWidth - 16;
        var statsRight = right - (v.Phase == Phase.Ended ? 532 : 412);
        Header(v, "THE SHIP", back: false, limit: statsRight - StatsWidth(v) - 12);
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
        DrawStats(v, statsRight);

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
            y = DrawNoIssues(v, x, y, width);

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
                _p.TextRight(_p.Head, Voyage.Hours(ShownLeft(v, f)), x + width - 14, y - 4, LogTones.IsFinal(ShownLeft(v, f), f.ClockHours) ? Painter.Danger : Painter.Bright);
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
                if (v.CauseKnown(f) is { } cause)
                    y = _p.Paragraph("Cause: " + cause + ".", ix, y, inner, Painter.Dim);
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
            _p.Cover(card);
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
            : who.Name + " has " + skill + " " + who.SkillOf(fault.Skill) + "; this needs " + fault.Level + "."
              + (v.TermsText(who, fault) is { Length: > 0 } terms ? " " + terms : "");
        // A person whose only open condition is the price of this very piece of work: the button pushes past it (and says so).
        // While a question is open too, it is still Repair or Patch: the click has them say what they wait on.
        var bargain = who != null && v.OnlyPriceLeft(who) ? v.BargainOf(who) : null;
        var pushRepair = bargain is { } br && br.FaultId == fault.Id && !br.Patch;
        var pushPatch = bargain is { } bp && bp.FaultId == fault.Id && bp.Patch;
        var pushTip = who != null && v.BargainText(who) is { } bt ? bt : fit;
        if (_p.Button(new Rectangle(x, y, half, height), who is null ? "REPAIR" : pushRepair ? PushLabel : RepairLabel(v, who, fault), anyone, tip: pushRepair ? pushTip : fit))
        {
            if (who != null)
            {
                if (PushAllowed(pushRepair))
                    Act("repair " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: false));
            }
            else
                Do(() => _picker = (fault, false, pickerX, y));
        }
        if (_p.Button(new Rectangle(x + half + 8, y, half, height), pushPatch ? PushLabel : "PATCH", anyone, tip: pushPatch ? pushTip : fit + " A patch is quicker, and gives way again later."))
        {
            if (who != null)
            {
                if (PushAllowed(pushPatch))
                    Act("patch " + fault.Name + " by " + who.Name, () => v.Assign(who, fault, patch: true));
            }
            else
                Do(() => _picker = (fault, true, pickerX, y));
        }
    }

    private const string PushLabel = "WITHOUT YOUR WORD";

    /// <summary>Real seconds before a second click on a button that pushes past a bargain counts, so a double click can't push by accident.</summary>
    private const double PushGuardSeconds = 0.6;

    private double _lastPushClickAt = double.NegativeInfinity;

    /// <summary>
    /// False when this click would push past a bargain within <see cref="PushGuardSeconds"/> of the last Repair/Patch click for a
    /// person (game layer only; the rules are unchanged). Every such click is stamped, pushing or not: the first click of a double
    /// click makes the offer (or re-targets it to another fault) and must not let the second push.
    /// </summary>
    private bool PushAllowed(bool pushing)
    {
        var ok = !pushing || _now - _lastPushClickAt >= PushGuardSeconds;
        _lastPushClickAt = _now;
        return ok;
    }

    /// <summary>"Patient: Tomas. " on the card of a sleeper illness, else nothing.</summary>
    private static string Patient(Voyage v, Fault f) =>
        f.Kind == "illness" && f.SubjectId is { } sid && v.Find(sid) is { } p ? "Patient: " + p.Name + ". " : "";

    /// <summary>The words cut at a word boundary to fit <paramref name="maxLines"/> lines of <paramref name="width"/> pixels, ending in an ellipsis (<see cref="CardLayout.CutToLines"/>).</summary>
    private string FitLines(string text, int width, int maxLines) =>
        CardLayout.CutToLines(text, (int)(width / _p.SmallWidth), maxLines);

    /// <summary>The text cut at a word boundary to fit one line of <paramref name="width"/> pixels in a font whose characters are <paramref name="charWidth"/> wide (<see cref="CardLayout.CutAtWord"/>).</summary>
    private static string Clip(string text, int width, float charWidth) =>
        CardLayout.CutAtWord(text, Math.Max(4, (int)(width / charWidth)));

    /// <summary>
    /// The calm panel for an alert with nothing to repair: the plain fact, then what to do next (the next-step line less its first
    /// sentence). Returns the y below it.
    /// </summary>
    private int DrawNoIssues(Voyage v, int x, int y, int width)
    {
        var ended = v.Phase == Phase.Ended;
        var next = ended ? "" : NextStep(v);
        const string lead = "Nothing needs repair. ";
        if (next.StartsWith(lead, StringComparison.Ordinal))
            next = next[lead.Length..];
        else if (next == "Nothing needs repair.")
            next = "";
        var rows = next.Length == 0 ? 0 : _p.Lines(next, width - 28);
        var panel = new Rectangle(x, y, width, 20 + 24 + (rows > 0 ? 6 + rows * 19 : 0) + 16);
        _p.Box(panel, Painter.Edge);
        _p.Text(_p.Body, ended ? "The voyage is over." : "Nothing needs repair.", x + 14, y + 16, Painter.Ink);
        if (rows > 0)
            _p.Paragraph(next, x + 14, y + 20 + 24 + 6, width - 28, Painter.Dim);
        _p.Cover(panel);
        return panel.Bottom + IssueCardLayout.Gap;
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
            var doneLine = tag + " - " + done + " - " + IssueCardLayout.CardName(f);
            var doneShown = Clip(doneLine, inner, _p.SmallWidth);
            _p.Text(_p.Small, doneShown, ix, y + 6, Painter.Dim);
            _p.Tip(new Rectangle(x, cardTop, width, IssueCardLayout.ResolvedRow), doneShown == doneLine ? "" : doneLine);
            _p.Frame(new Rectangle(x, cardTop, width, IssueCardLayout.ResolvedRow), Painter.Edge);
            _p.Cover(new Rectangle(x, cardTop, width, IssueCardLayout.ResolvedRow));
            return cardTop + IssueCardLayout.ResolvedRow + IssueCardLayout.Gap;
        }
        y += 8;
        var state = f.State switch { FaultState.Open => "OPEN", _ => f.Patching ? "PATCHING" : "IN REPAIR" };
        _p.Text(_p.Small, tag + " - " + state + (f.Critical ? " - CRITICAL" : ""), ix, y, Painter.Accent);
        _p.TextRight(_p.Body, Voyage.Hours(ShownLeft(v, f)), x + width - 14, y - 2, LogTones.IsFinal(ShownLeft(v, f), f.ClockHours) ? Painter.Danger : Painter.Bright);
        y += 20;
        var cardName = IssueCardLayout.CardName(f);
        var shownName = Clip(cardName, inner, _p.BodyWidth);
        _p.Text(_p.Body, shownName, ix, y, Painter.Bright);
        y += 22;
        // The lines below the name: the needs, the verdict (always whole, on its own line), who is on it, and the danger or exposure line.
        var where = v.GetSection(f.Section)?.Name ?? "";
        var needs = SectionName(where) + ". " + Patient(v, f) + f.Skill + Nb + f.Level + (f.People > 1 ? ", " + f.People + Nb + "people" : "") + ", " + N(f.Hours) + Nb + "h.";
        var extra = f.DangerFact is { } danger ? "Known: " + v.GetFact(danger).Say(v.GetFact(danger).Truth) + "."
            : v.CauseKnown(f) is { } cause ? "Cause: " + cause + "."
            : f.ExposureFact is { } seen ? "Whoever works here learns " + v.GetFact(seen).Label + "." : "";
        var workers = v.Awake.Where(c => c.WorkingOn == f.Id).Select(c => c.Name).ToList();
        var columns = (int)(inner / _p.SmallWidth);
        var split = IssueCardLayout.Split(shown, _p.Lines(needs, inner), extra.Length == 0 ? 0 : _p.Lines(extra, inner), workers.Count > 0);
        // A "Cause:" or "Known:" line that would be cut does not show half: it moves to the tooltip, and its line goes back to the needs text.
        var extraMoved = extra.Length > 0 && (split.Extra == 0 || CardLayout.NeedsCut(extra, columns, split.Extra));
        if (extraMoved)
            split = IssueCardLayout.Split(shown, _p.Lines(needs, inner), 0, workers.Count > 0);
        var verdict = Glue(v.EarliestDoneText(f));
        // The card is as tall as its text (never more than the per-count maximum), with the buttons right below it.
        var textLines = split.Needs + _p.Lines(verdict, inner) + (split.Workers ? 1 : 0) + split.Extra;
        var height = Math.Min(IssueCardLayout.OpenCardHeight(shown), IssueCardLayout.CardTop + textLines * IssueCardLayout.LineHeight + IssueCardLayout.CardBottom);
        y = _p.Paragraph(FitLines(needs, inner, split.Needs), ix, y, inner, Painter.Dim);
        y = _p.Paragraph(verdict, ix, y, inner, Painter.Ink);
        var workersLine = "";
        if (split.Workers)
        {
            var line = string.Join(" and ", workers) + (workers.Count == 1 ? " is" : " are") + " on it"
                       + (workers.Count < f.People ? " (" + workers.Count + " of " + f.People + ")" : "") + ".";
            workersLine = line;
            y = _p.Paragraph(FitLines(line, inner, 1), ix, y, inner, Painter.Ink);
        }
        if (split.Extra > 0)
            _p.Paragraph(FitLines(extra, inner, split.Extra), ix, y, inner, Painter.Dim);
        // The full text is the card's tooltip whenever any of it was cut or moved (the buttons keep their own tips, asked for after).
        var cut = shownName != cardName || extraMoved || CardLayout.NeedsCut(needs, columns, split.Needs)
                  || (workersLine.Length > 0 && CardLayout.NeedsCut(workersLine, columns, 1));
        if (cut)
            _p.Tip(new Rectangle(x, cardTop, width, height), string.Join(Environment.NewLine, new[] { cardName, needs, verdict, workersLine, extra }.Where(t => t.Length > 0)));
        WorkButtons(v, f, ix, cardTop + height - 8 - 34, (inner - 8) / 2, 34, x + width + 8);
        // Progress runs along the top edge of the card.
        if (f.State == FaultState.InRepair)
            _p.Meter(new Rectangle(x + 2, cardTop + 2, width - 4, 4), ShownProgress(f) / f.Hours, Painter.Ink);
        var card = new Rectangle(x, cardTop, width, height);
        if (_p.Over(card))
            _hotFaultNext = f.Id;
        _p.Frame(card, Color.Lerp(f.State == FaultState.InRepair ? Painter.Wire : Painter.Accent, Painter.Bright, _p.Ease("issue:" + f.Id, _hotFault == f.Id ? 1f : 0f)));
        _p.Cover(card);
        return cardTop + height + IssueCardLayout.Gap;
    }

    // ---- The ship plan ----

    /// <summary>The short label of a fault on the plan: "02 COOLANT LEAK 12 h" (a returning patch has no prefix; a critical fault shows by its diamond's thick border).</summary>
    private static string PlanFaultLabel(int index, Fault f, double hoursLeft) =>
        index.ToString("00", CultureInfo.InvariantCulture) + " " + f.PlainName.ToUpperInvariant() + " "
        + (hoursLeft >= 48 ? (hoursLeft / 24).ToString("0", CultureInfo.InvariantCulture) + " d" : hoursLeft.ToString("0", CultureInfo.InvariantCulture) + " h");

    private void DrawPlan(Voyage v, Rectangle rect)
    {
        var spritesBefore = _p.Sprites;
        var lightTime = PlanClock();
        _p.Box(rect, Painter.Edge);
        _p.Text(_p.Small, "SHIP PLAN", rect.X + 14, rect.Y + 10, Painter.Dim);
        // The next useful step, where the eye already is.
        _p.TextRight(_p.Small, "NEXT: " + NextStep(v), rect.Right - 14, rect.Y + 10, Drifting ? Painter.Dim : Painter.Accent);

        // The speaker button sits in the header row, right of the title.
        MusicButton(new Rectangle(rect.X + 100, rect.Y + 6, 32, 22));

        // What is on the plan: the faults to label (numbered as in the issue list) and who is up where.
        var shownFaults = new List<(int Index, Fault Fault, Section Section, int Slot, string Label)>();
        var inSection = new Dictionary<string, int>();
        var index = 0;
        foreach (var f in v.Current.Faults)
        {
            index++;
            if (f.Resolved || index > FaultsRevealed(v) || v.GetSection(f.Section) is not { } fs)
                continue;
            var slot = inSection.TryGetValue(fs.Id, out var seen) ? seen : 0;
            inSection[fs.Id] = slot + 1;
            shownFaults.Add((index, f, fs, slot, PlanFaultLabel(index, f, ShownLeft(v, f))));
        }
        var standing = new List<(CrewMember Person, float PlanX, float PlanY)>();
        var placed = new Dictionary<string, int>();
        foreach (var c in v.Awake)
        {
            var where = v.LocationOf(c);
            var n = placed.TryGetValue(where, out var count) ? count : 0;
            placed[where] = n + 1;
            if (v.GetSection(where) is { } ws)
                standing.Add(ws.Id.StartsWith("pod", StringComparison.Ordinal) ? (c, (ws.Id == "podA" ? 400 : 580) + n * 80, 235) : (c, ws.Spot.X - 44, ws.Spot.Y + 38 + n * 18));
            else
                standing.Add((c, 290 + n * 90, 235));
        }
        var boxes = standing.Select(m => new PlanBox(m.PlanX, m.PlanY, -8, -9, _p.Small.MeasureString(m.Person.Name.ToUpperInvariant()).X + 28, 18)).ToList();
        if (v.GetSection("core") is { } youSection)
            boxes.Add(new PlanBox(youSection.Spot.X - 24, youSection.Spot.Y + 30, -_p.SmallWidth * 1.5f, 0, _p.SmallWidth * 3, 16));
        var fit = PlanLayout.Compute(v.Sections, shownFaults.Select(f => new PlanFault(f.Index, f.Section.Id, f.Slot, _p.Small.MeasureString(f.Label).X)).ToList(),
            boxes, new RectF(rect.X + 4 + ExhaustPx, rect.Y + 34, rect.Width - 8 - ExhaustPx, rect.Height - 40), _p.SmallWidth);
        var k = fit.K;
        Vector2 P(float px, float py) { var (x, y) = fit.P(px, py); return new Vector2(x, y); }
        Vector2[] Pts((float X, float Y)[] points) => points.Select(p => P(p.X, p.Y)).ToArray();
        DrawPlanLightBack(v, fit, lightTime, shownFaults.Select(f => (f.Fault, f.Section.Id)).ToList());

        _p.Poly(Pts(ShipPlan.Hull), Painter.Hull);
        foreach (var wall in ShipPlan.Corridor)
            _p.Line(P(wall.X1, wall.Y), P(wall.X2, wall.Y), Painter.Hull);
        _p.Line(P(ShipPlan.PowerBus.X1, ShipPlan.PowerBus.Y), P(ShipPlan.PowerBus.X2, ShipPlan.PowerBus.Y), Painter.Power, 1f);
        DrawPlanBus(v, fit, lightTime);

        var hotFault = _hotFault is null ? null : v.Current.Faults.FirstOrDefault(f => f.Id == _hotFault);
        foreach (var s in v.Sections)
        {
            foreach (var mesh in s.Mesh)
                _p.Poly(Pts(mesh), Painter.Faint, 1f, closed: false);
            var outline = Pts(s.Outline);
            // The section of the fault under the mouse (on its card or on the plan) lights up.
            var lit = _p.Ease("section:" + s.Id, hotFault != null && hotFault.Section == s.Id ? 1f : 0f);
            _p.Poly(outline, Color.Lerp(Painter.Wire, Painter.Bright, lit), 1.5f + lit);
            var name = fit.Names[s.Id];
            _p.Text(_p.Small, s.Name, name.X, name.Y, Painter.Dim);

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

        var coolantLost = CoolantLost(v);
        foreach (var run in ShipPlan.Coolant)
        {
            var points = Pts(run);
            _p.DashedPath(points, coolantLost ? Painter.Fade(Painter.Dead, 0.55f) : Painter.Blue, offset: (float)(lightTime * PlanLight.CoolantSpeed(coolantLost) % 12));
        }

        // Where the people who are up stand, worked out before the doors: a marker can sit over a door's click area
        // (the idle spot in the corridor does), and then the marker wins.
        var markers = new List<(CrewMember Person, Vector2 At, string Name, Rectangle Backing)>();
        foreach (var (c, planX, planY) in standing)
        {
            var at = P(planX, planY);
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
            if (s.DoorState == DoorState.Locked)
                DrawDoorTint(r, lightTime);
            switch (s.DoorState)
            {
                case DoorState.Open:
                    _p.Capsule(r, Painter.Bright, 1f, Painter.Ground);
                    break;
                case DoorState.Closed:
                    _p.Capsule(r, Painter.Wire);
                    break;
                default:
                    _p.Capsule(r, Painter.Accent);
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
                    var breath = PlanLightOn ? PlanLight.PodBreath(ShownPodWear(v), Tuning.PodsWornAt, lightTime, PlanLight.SeedOf(c.Id)) : 1f;
                    if (PlanLightOn)
                        FillPlanShape(fit, ShipPlan.PodOutline(pod.X, pod.Y), Painter.Fade(Painter.Blue, 0.07f * breath), 8f);
                    _p.Poly(outline, Painter.Fade(Painter.Wire, breath));
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
                    _p.Line(P(pod.X + 4, pod.Y + 6), P(pod.X + 40, pod.Y + 64), Painter.Danger, 1f);
                    _p.Line(P(pod.X + 40, pod.Y + 6), P(pod.X + 4, pod.Y + 64), Painter.Danger, 1f);
                    _p.TextCentre(_p.Small, tag, centre.X, centre.Y - 8, Painter.Dim);
                    break;
            }
        }

        // Faults, numbered as in the issue list: a diamond in its section, a bent leader out to a label in a row above or below the hull
        // (placed by PlanLayout so no label overlaps another and no leader runs through text).
        foreach (var (idx, f, section, slot, label) in shownFaults)
        {
            var placedLabel = fit.Labels.First(l => l.Index == idx);
            var centre = new Vector2(placedLabel.Diamond.X + placedLabel.Diamond.W / 2, placedLabel.Diamond.Y + placedLabel.Diamond.H / 2);
            var mark = new Rectangle((int)centre.X - 15, (int)centre.Y - 15, 30, 30);
            var text = new Rectangle((int)placedLabel.Text.X - 2, (int)placedLabel.Text.Y - 1, (int)placedLabel.Text.W + 4, (int)placedLabel.Text.H + 2);
            var tip = IssueCardLayout.CardName(f) + (f.Critical ? " (critical)" : "") + ": " + Voyage.Hours(ShownLeft(v, f)) + " left.";
            if (_p.Over(mark) || _p.Over(text))
                _hotFaultNext = f.Id;
            _p.Tip(mark, tip);
            _p.Tip(text, tip);
            var faultLit = _p.Ease("fault:" + f.Id, _hotFault == f.Id ? 1f : 0f);
            // A fault pulses, and faster once under a quarter of its clock is left.
            var urgent = LogTones.IsFinal(ShownLeft(v, f), f.ClockHours);
            var beat = 0.55f + 0.45f * Motion.Pulse(_now, urgent ? 0.5f : 1.8f);
            _p.Diamond(centre, 13 + 4 * faultLit, Painter.Fade(f.Critical || faultLit > 0.5f ? Painter.Bright : urgent ? Painter.Danger : Painter.Accent, beat), f.Critical ? 3f : 2f);
            _p.TextCentre(_p.Body, idx.ToString(CultureInfo.InvariantCulture), centre.X, centre.Y - 9, urgent ? Painter.Danger : Painter.Accent);
            var leader = urgent ? Painter.Danger : Painter.Accent;
            _p.Poly(placedLabel.Leader.Select(p => new Vector2(p.X, p.Y)).ToArray(), leader, 1f, closed: false);
            _p.Text(_p.Small, label, placedLabel.Text.X, placedLabel.Text.Y, leader);
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
        PlanSprites = _p.Sprites - spritesBefore;

    }

    // ---- The log and the voice line ----

    /// <summary>When each log entry first showed on screen (the clock of its fade-in), so an entry held back behind a pending line fades in when it is released, not before.</summary>
    private readonly Dictionary<int, double> _logAppeared = new();
    /// <summary>The log's length at the last clock move: entries from here on are new since then.</summary>
    private int _logMark;
    /// <summary>Which log entries are on screen: each waits for the one before it to be shown in full (<see cref="LogReveal"/>).</summary>
    private readonly LogReveal _reveal = new();
    private readonly List<RevealItem> _revealItems = new();
    /// <summary>Log entries up to the last one on screen (the player's own line shows early, below a crew line still being worded).</summary>
    private int LogVisible => _reveal.Extent;

    /// <summary>Called once per update: moves the reveal on. A key or a click that skips a transition also finishes the typing.</summary>
    private void StepLogReveal(double dt, bool skip)
    {
        if (_voyage is not { } v)
            return;
        for (var i = _revealItems.Count; i < v.Log.Count; i++)
            _revealItems.Add(default);
        for (var i = 0; i < v.Log.Count; i++)
        {
            var e = v.Log[i];
            // The length of what DrawLog shows ("SHIP - ...", "Mara - ..."), counted without building the string every update.
            var length = e.Kind switch
            {
                LogKind.Ship => "SHIP - ".Length + e.Text.Length,
                LogKind.Crew => (e.Speaker?.Length ?? 0) + " - ".Length + e.Text.Length,
                _ => 0,
            };
            _revealItems[i] = new RevealItem(e.Kind == LogKind.Crew, e.Pending, length, Own: e.Kind == LogKind.You);
        }
        _reveal.Update(_revealItems, ShownLogCount(v), dt, skip, Motion.Still);
    }

    /// <summary>True while log lines are still held back or typing (the tour waits for it).</summary>
    private bool LogDraining => _reveal.Draining;

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

        // Lines fade in from the frame they first show (an entry held back behind a pending line shows later than the ones after it);
        // lines since the last clock move have a mark in the margin.
        var count = LogVisible;

        var lines = new List<(string Text, Color Color, int Entry)>();
        var entryIndex = -1;
        foreach (var entry in v.Log.Take(count))
        {
            entryIndex++;
            if (!_reveal.IsOnScreen(entryIndex))
                continue;
            _logAppeared.TryAdd(entryIndex, _now);
            var head = entryIndex == _reveal.Shown && _reveal.HeadVisible;
            var color = Painter.LogColor(LogTones.Of(entry.Kind, entry.Text));
            var text = entry.Kind switch
            {
                LogKind.Ship => "SHIP - " + entry.Text,
                LogKind.Crew => entry.Speaker + " - " + (entry.Pending ? "..." : entry.Text),
                LogKind.You => "YOU - " + entry.Text + (entry.Unread ? "  ..." : ""),
                LogKind.Recorded => "    " + entry.Text,
                _ => entry.Text,
            };
            // The head line types out: wrap the whole text, show the first characters of it.
            var left = head && !entry.Pending ? _reveal.Chars : int.MaxValue;
            foreach (var line in Painter.Wrap(text, columns))
            {
                if (left <= 0)
                    break;
                lines.Add((line.Length > left ? line[..left] : line, color, entryIndex));
                left -= line.Length + 1;
            }
        }
        _scroll = Math.Min(_scroll, Math.Max(0, lines.Count - visible));
        var first = Math.Max(0, lines.Count - visible - _scroll);
        var last = Math.Min(lines.Count, first + visible);
        var y = areaBottom - (last - first) * step;
        for (var i = first; i < last; i++, y += step)
        {
            var fresh = Motion.Progress(_now, _logAppeared.GetValueOrDefault(lines[i].Entry, _now), Motion.LogFade);
            var isFresh = fresh < 1f;
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

    /// <summary>
    /// "REPAIR: MARA 97%": the chance the repair holds if this person takes it on now (a patch always holds, and comes back);
    /// "REPAIR: NO PARTS" when no spare parts are left and a repair would be refused.
    /// </summary>
    private static string RepairLabel(Voyage v, CrewMember who, Fault f) =>
        v.RepairChanceWith(who, f) is { } chance
            ? "REPAIR: " + who.Name.ToUpperInvariant() + " " + N(Math.Round(chance * 100)) + "%"
            : "REPAIR: NO PARTS";

    // ---- The crew ----

    private void DrawCrew(Voyage v, int x, int top, int width, int bottom)
    {
        _p.Text(_p.Small, "CREW", x, top, Painter.Dim);
        var alone = _target is { State: CrewState.Awake } ? _target : null;
        _p.TextRight(_p.Small, alone is null ? "click a card to speak to one person" : "you speak to " + alone.Name + " alone; repairs go to " + alone.Name,
            x + width, top, alone is null ? Painter.Dim : Painter.Accent);
        var up = v.Crew.Count(c => c.State is CrewState.Awake or CrewState.Thawing);
        var y = top + 28;
        var items = Waiting(v, _target).ToList();
        var deadCount = v.Crew.Count(c => ShownState(c) == CrewState.Dead);
        var column = CardLayout.Crew(bottom - y, v.Crew.Count - deadCount, deadCount, items.Count == 0);
        foreach (var c in v.Crew)
        {
            if (ShownState(c) == CrewState.Dead)
            {
                DrawDeadStrip(c, x, y, width);
                y += CardLayout.StripHeight + CardLayout.CrewGap;
                continue;
            }
            var card = new Rectangle(x, y, width, column.CardHeight);
            var selected = _target == c;
            var over = _p.Over(card);
            if (over)
            {
                _hotCrewNext = c.Id;
                _p.Hand = true;
            }
            // The card lights up under the mouse, and with its pod or marker on the plan.
            var lit = _p.Ease("crew:" + c.Id, over || _hotCrew == c.Id ? 1f : 0f);
            _p.Box(card, Color.Lerp(c.State is CrewState.Awake or CrewState.Thawing ? Painter.Wire : Painter.Edge, Painter.Bright, lit));
            if (selected)
            {
                _p.Frame(card, Painter.Bright, 2);
                _p.Fill(new Rectangle(card.X, card.Y, 4, card.Height), Painter.Accent);
            }
            // While the years roll the card keeps the state from before the drift, as the pod does (ShownState).
            var shown = ShownState(c);
            var ink = shown == CrewState.Awake ? Painter.Bright : Painter.Ink;
            _p.Text(_p.Body, c.Name, x + 12, y + 8, ink);
            _p.Text(_p.Small, c.Role + ", " + N(ShownAge(c)), x + 20 + c.Name.Length * _p.BodyWidth, y + 10, Painter.Dim);
            var state = shown switch
            {
                CrewState.Asleep => "asleep, " + c.Thaws + (c.Thaws == 1 ? " thaw" : " thaws"),
                CrewState.Thawing => "thawing, " + Voyage.Hours(ShownThawLeft(v, c)),
                _ => "AWAKE " + N(ShownHoursAwake(c) / 24) + " d" + (c.WorkingOn != null ? ", working" : ""),
            };
            _p.Text(_p.Small, state, x + 12, y + 28, ink);
            DrawCrewMeters(v, c, x + 12, y + card.Height - 40, width - 24);

            var file = new Rectangle(x + width - 12 - 52, y + 10, 52, 32);
            var act = new Rectangle(file.X - 8 - 76, y + 10, 76, 32);
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
                var wakeTip = v.Lockouts.Bay
                    ? v.BayLockReason
                    : up >= Tuning.MaxAwake
                    ? Tuning.MaxAwake + " people are up already; that is the most at once."
                    : "The thaw takes " + N(Tuning.ThawHours) + " hours and ages " + c.Name + " " + Tuning.ThawAgeMin + " to " + Tuning.ThawAgeMax + " years.";
                if (_p.Button(act, v.Lockouts.Bay ? "BAY TAKEN" : "WAKE", !ended && !v.Lockouts.Bay, tip: wakeTip, strong: true))
                    Act("wake " + person.Name, () => v.Wake(person));
            }
            else if (c.State == CrewState.Awake)
            {
                var can = v.CanSleepNow(c);
                var days = N(Math.Ceiling((Tuning.MinAwakeHours - c.HoursAwake) / 24));
                var sleepTip = v.Lockouts.Bay
                    ? v.BayLockReason
                    : can
                    ? "Ask " + c.Name + " to go back into the pod. They may refuse." + (v.SleepBreaksText(c) is { } breaks ? " " + breaks : "")
                    : "A pod takes a person back after " + N(Tuning.MinAwakeHours / 24) + " days awake at the earliest: " + days + " to go.";
                if (_p.Button(act, v.Lockouts.Bay ? "BAY TAKEN" : can ? "SLEEP" : "in " + days + " d", can && !ended && !v.Lockouts.Bay, tip: sleepTip))
                    Act("ask " + person.Name + " to sleep", () => v.AskSleep(person));
            }
            if (_p.Hit(card) && !_p.Over(file) && !_p.Over(act))
                Do(() => _target = _target == person ? null : person);
            y += column.CardHeight + CardLayout.CrewGap;
        }

        var waiting = new Rectangle(x, y + 4, width, Math.Min(column.WaitingHeight, bottom - y - 4));
        if (items.Count == 0)
        {
            // Nothing waits on the player: a calm strip, and the room goes to the cards above.
            _p.Box(waiting, Painter.Edge);
            _p.Text(_p.Small, "Nothing waits on you.", x + 12, waiting.Y + (waiting.Height - 16) / 2, Painter.Dim);
            return;
        }
        if (waiting.Height < 60)
            return;
        _p.Box(waiting, Painter.Edge);
        _p.Text(_p.Small, "WAITING ON YOU", x + 12, waiting.Y + 10, Painter.Dim);
        // With several people asking, the stakes of an answer are shown for the selected one.
        var asking = v.Awake.Where(a => v.StakesOf(a) != null).ToList();
        if (asking.Count > 1 && (_target is null || !asking.Contains(_target)))
            _p.TextRight(_p.Small, "select a person for stakes", x + width - 12, waiting.Y + 10, Painter.Dim);
        var wy = waiting.Y + 36;
        var limit = waiting.Bottom - 22;
        foreach (var (who, item, tip) in items)
        {
            // An item that doesn't fit is left out; a shorter one after it may still fit.
            var rows = _p.Lines(item, width - 24);
            if (wy + rows * 19 > limit)
                continue;
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
            // A short item (terms, an accusation, a checklist with its stakes and price) has its full text as the tooltip.
            if (tip != null)
                _p.Tip(row, tip);
            wy = _p.Paragraph(item, x + 12, wy, width - 24, Painter.Ink) + 4;
        }
    }

    /// <summary>A dead person: one line (name, role, "dead", FILE) so the living cards have the room.</summary>
    private void DrawDeadStrip(CrewMember c, int x, int y, int width)
    {
        var strip = new Rectangle(x, y, width, CardLayout.StripHeight);
        _p.Box(strip, Painter.Edge);
        _p.Text(_p.Body, c.Name, x + 12, y + 6, Painter.Dim);
        _p.Text(_p.Small, c.Role + ", " + N(ShownAge(c)), x + 20 + c.Name.Length * _p.BodyWidth, y + 8, Painter.Dim);
        var file = new Rectangle(x + width - 12 - 52, y + 4, 52, CardLayout.StripHeight - 8);
        _p.TextRight(_p.Small, "dead", file.X - 12, y + 8, Painter.Danger);
        var person = c;
        if (_p.Button(file, "FILE", tip: "The dossier: who " + c.Name + " was, what they knew and what you told them."))
            Do(() =>
            {
                _dossier = person;
                _screen = Screen.Dossier;
            });
    }

    private static readonly double[] TrustEdges = { 0, 15, 30, 60, 100 };
    private static readonly double[] MoraleEdges = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

    /// <summary>
    /// Trust and morale as two thin segmented meters side by side with their numbers (a change flashes and shows its size, but not before
    /// the line that explains it is in the log). Trust has the dossier's four bands as segments, in the band's ink; the band's name and
    /// manner are the tooltip. Morale is ten equal parts.
    /// </summary>
    private void DrawCrewMeters(Voyage v, CrewMember c, int x, int y, int width)
    {
        var hold = _transition is not null || LinePending(v, c);
        var trust = Track("trust:" + c.Id, c.Trust, hold, out var trustFlash, out var trustDelta);
        var morale = Track("morale:" + c.Id, c.Morale, hold, out var moraleFlash, out var moraleDelta);
        var band = BandOf(trust);
        const int gutter = 14;
        var half = (width - gutter) / 2;
        var moraleX = x + half + gutter;

        _p.Text(_p.Small, "trust " + N(trust), x, y, Color.Lerp(Painter.Dim, Painter.Bright, trustFlash));
        if (trustDelta.Length > 0)
            _p.Text(_p.Small, trustDelta, x + (7 + N(trust).Length) * _p.SmallWidth, y, Painter.Accent);
        _p.Text(_p.Small, "morale " + N(morale), moraleX, y, Color.Lerp(Painter.Dim, Painter.Bright, moraleFlash));
        if (moraleDelta.Length > 0)
            _p.Text(_p.Small, moraleDelta, moraleX + (8 + N(morale).Length) * _p.SmallWidth, y, Painter.Accent);

        var barY = y + 20;
        var inkTrust = BandInk(band);
        foreach (var seg in CardLayout.Segments(half, 2, TrustEdges, trust))
        {
            _p.Fill(new Rectangle(x + seg.X, barY, seg.Width, 6), Painter.Track);
            _p.Fill(new Rectangle(x + seg.X, barY, seg.Fill, 6), inkTrust);
        }
        foreach (var seg in CardLayout.Segments(half, 2, MoraleEdges, morale))
        {
            _p.Fill(new Rectangle(moraleX + seg.X, barY, seg.Width, 6), Painter.Track);
            _p.Fill(new Rectangle(moraleX + seg.X, barY, seg.Fill, 6), Painter.Ink);
        }
        _p.Tip(new Rectangle(x - 4, y - 2, half + 8, 32), "Trust in you: " + N(trust) + " of 100, " + CrewMember.BandText(band) + ". " + Manner(band));
        _p.Tip(new Rectangle(moraleX - 4, y - 2, half + 8, 32), "Morale: " + N(morale) + " of 100. How " + c.Name + " is holding up.");
    }

    /// <summary>The accusation item in two or three rows; <see cref="Voyage.AccusationText"/> is its tooltip.</summary>
    private static string AccusationShort(Voyage v, CrewMember c)
    {
        var a = v.OpenAccusation(c)!;
        if (a.PromiseKey != null)
            return v.PromiseAccusationShort(c, a) + " Keep it now or tell " + c.Them + " the truth (" + a.BeatsLeft * 2 + " weeks).";
        var label = v.HasFact(a.FactKey) ? v.GetFact(a.FactKey).Label : "something you said";
        return c.Name + " caught you lying about " + label + " (" + a.How + "). Tell " + c.Them + " what is true (" + a.BeatsLeft * 2 + " weeks).";
    }

    /// <summary>Questions nobody has answered, conditions, things people asked for, doubts, and promises still open; each with the person it is about, if one.</summary>
    /// <remarks>
    /// In order: the stakes of the selected person's open question (or of the only open question): what the true answer costs and earns,
    /// what a reassuring one buys, and where the answer can be checked (<see cref="Voyage.StakesText"/>); then a bargain's price
    /// (<see cref="Voyage.BargainText"/>: the selected person's, else the first bargainer's with a hint to select the others); then the bay
    /// warning and the bay lockout; then per awake person, the selected one first: their other open question, their request, what the ship
    /// owes them (<see cref="Voyage.LedgerLine"/>), their doubts and a pod refusal; last, promises to people asleep.
    /// </remarks>
    private static IEnumerable<(CrewMember? Who, string Text, string? Tip)> Waiting(Voyage v, CrewMember? selected)
    {
        var asking = v.Awake.Count(a => v.StakesOf(a) != null);
        var staked = v.Awake.Where(c => c.PendingQuestion != null && v.HasFact(c.PendingQuestion))
            .FirstOrDefault(c => c == selected || asking == 1);
        // The stakes come first: they are the longest item and the one the player is deciding on. When the question they are about is
        // on that person's checklist, the checklist comes first instead, with the stakes as a short sub-line (the full stakes are the
        // tooltip): the stakes alone take 7 of the 8 rows "Waiting on you" has with every card shown and pushed the checklist out.
        if (staked != null && !v.JobHoldsQuestion(staked))
            yield return (staked, v.StakesText(staked), null);
        // The conditions a wary person stated for their job, one checklist per person that ticks off as each is met.
        foreach (var c in v.Awake.OrderBy(a => a == selected ? 0 : 1))
            if (v.JobItem(c, withStakes: c == staked) is { } checklist)
                yield return (c, checklist.Text, checklist.Tip);
        // What a wary person wants before starting when no checklist carries it: the selected one's (or the only one's) bargain, ahead of the bay items.
        var bargainers = v.Awake.Where(c => v.BargainOf(c) != null && v.JobText(c) is null).ToList();
        // With several and none of them selected, the first one's, and a hint to select the others.
        var bargainer = bargainers.FirstOrDefault(c => c == selected) ?? bargainers.FirstOrDefault();
        if (bargainer != null && v.BargainText(bargainer) is { } bargainText)
        {
            var others = bargainers.Where(c => c != bargainer).Select(c => c.Name).ToList();
            yield return (bargainer, bargainText + (others.Count > 0 && bargainer != selected
                ? " Select " + string.Join(" or ", others) + " to see " + (others.Count == 1 ? "theirs." : "each of theirs.")
                : ""), null);
        }
        // A lie they caught: short form here (two rows), the full text is the tooltip. Selected person first.
        foreach (var c in v.Awake.OrderBy(a => a == selected ? 0 : 1).Where(a => v.OpenAccusation(a) != null))
            yield return (c, AccusationShort(v, c), v.AccusationText(c));
        if (v.Lockouts.BayWarned.Count > 0)
            yield return (null, string.Join(" and ", v.Lockouts.BayWarned.Select(id => v.Crew.First(c => c.Id == id).Name))
                         + " will take the cold-sleep bay at the next step. Win one of them back above 15 trust, or they do.", null);
        if (v.Lockouts.Bay)
            yield return (null, v.BayLockReason + (v.LogsLocked
                ? " The crew hold the logs too: no promise to them can be kept now."
                : " Promise one of them to open the logs, have every log open when it falls due, and win their trust above "
                  + Tuning.BayReturnTrust.ToString("0") + " to get it back."), null);
        // The terms the selected idle person would work their likely job on, in two rows (the full text is the row's tooltip), so they
        // still fit under a six-row stakes item or a four-row bargain.
        if (selected != null && v.TermsItem(selected) is { } terms)
            yield return (selected, terms.Short, terms.Full);
        var owed = v.Ledger().Where(e => e.State == "open").ToList();
        foreach (var c in v.Awake.OrderBy(a => a == selected ? 0 : 1).ToList())
        {
            if (c != staked && c.PendingQuestion != null && v.HasFact(c.PendingQuestion) && v.JobText(c) is null)
                yield return (c, c.Name + " asked: " + v.GetFact(c.PendingQuestion).Question, null);
            // A request the ship said no to is settled: it leaves the list.
            if (c.RequestOpen)
                yield return (c, c.Name + " asked you " + (c.Want == Want.WakePartner ? "to wake their partner." : "to open the logs."), null);
            // What the ship owes them comes before their doubts.
            foreach (var e in owed.Where(e => e.Person == c))
                yield return (c, v.LedgerLine(e), null);
            foreach (var doubt in c.Doubts)
                if (v.HasFact(doubt))
                    yield return (c, v.DoubtText(c, doubt), null);
            if (v.PodRefusalText(c) is { } refusal)
                yield return (c, refusal, null);
        }
        // Promises to people who are asleep (never to thaw them again).
        foreach (var e in owed.Where(e => e.Person.State != CrewState.Awake && e.Person.Alive))
            yield return (e.Person, v.LedgerLine(e), null);
    }

}
