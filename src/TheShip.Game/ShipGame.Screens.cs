using Microsoft.Xna.Framework;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>The crew dossier, and the record with the ship logs.</summary>
public sealed partial class ShipGame
{
    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ---- Crew dossier ----

    private void DrawDossier(Voyage v, CrewMember c)
    {
        Header(v, "CREW DOSSIER", back: true);
        const int top = 72;
        var bottom = Painter.ScreenHeight - 16;

        // Who: one row per person; the selected one is an outline with an amber bar, not a block.
        var y = top;
        foreach (var other in v.Crew)
        {
            var person = other;
            var line = other.State switch
            {
                CrewState.Awake => "awake",
                CrewState.Thawing => "thawing",
                CrewState.Asleep => "asleep",
                _ => "dead",
            };
            var row = new Rectangle(16, y, 210, 48);
            var selected = other == c;
            var over = _p.Over(row);
            var hot = _p.Ease("dossier-row:" + other.Id, over ? 1f : 0f);
            if (over)
                _p.Hand = true;
            _p.Fill(row, selected ? Painter.Panel : Painter.Fade(Painter.Track, hot));
            _p.Frame(row, selected ? Painter.Wire : Color.Lerp(Painter.Edge, Painter.Wire, hot));
            if (selected)
                _p.Fill(new Rectangle(row.X, row.Y, 4, row.Height), Painter.Accent);
            _p.Text(_p.Small, other.Name.ToUpperInvariant(), row.X + 16, row.Y + 7, other.Alive ? Painter.Bright : Painter.Dim);
            _p.Text(_p.Small, line, row.X + 16, row.Y + 26, other.Alive ? Painter.Dim : Painter.Danger);
            if (_p.Hit(row))
                Do(() => _dossier = person);
            y += 56;
        }

        DrawSheet(v, c, new Rectangle(238, top, 360, bottom - top));
        DrawKnows(v, c, new Rectangle(610, top, 520, bottom - top));
        DrawTold(v, c, new Rectangle(1142, top, 442, bottom - top));
    }

    private void DrawSheet(Voyage v, CrewMember c, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        var x = rect.X + 16;
        var width = rect.Width - 32;

        // A low-poly bust in a framed panel; each person has their own head (ScreenRules.HeadOf).
        var frame = new Rectangle(x, rect.Y + 16, width, 190);
        _p.Fill(frame, Painter.Ground);
        _p.Frame(frame, Painter.Edge);
        foreach (var (cx, cy, dx, dy) in new[] { (frame.X, frame.Y, 1, 1), (frame.Right - 1, frame.Y, -1, 1), (frame.X, frame.Bottom - 1, 1, -1), (frame.Right - 1, frame.Bottom - 1, -1, -1) })
        {
            _p.Fill(new Rectangle(dx > 0 ? cx : cx - 13, dy > 0 ? cy : cy, 14, 2), Painter.Wire);
            _p.Fill(new Rectangle(dx > 0 ? cx : cx, dy > 0 ? cy : cy - 13, 2, 14), Painter.Wire);
        }
        var ox = frame.X + (frame.Width - 240) / 2f;
        var oy = frame.Y - 5f;
        Vector2 P(float px, float py) => new Vector2(ox + px, oy + py);
        Vector2 Q((float X, float Y) p) => P(p.X, p.Y);
        var ink = c.Alive ? Painter.Wire : Painter.Dead;
        var look = ScreenRules.HeadOf(c.Id);
        var right = ScreenRules.HeadRight(look);
        var (brow, cheek, jaw, chin) = (right[0], right[1], right[2], right[3]);
        var crown = (120f, 22f + look.Brow);
        _p.Poly(new[] { Q(ScreenRules.Mirror(brow)), P(120, 70), Q(brow) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { Q(ScreenRules.Mirror(cheek)), P(120, 70), Q(cheek) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { Q(ScreenRules.Mirror(jaw)), P(120, 104), Q(jaw) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { Q(crown), P(120, 146 + look.Chin) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { Q(ScreenRules.Mirror(cheek)), P(120, 104), Q(cheek) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { P(36, 186), P(120, 195), P(204, 186) }, Painter.Faint, 1f, closed: false);
        _p.Poly(ScreenRules.HeadOutline(look).Select(Q).ToArray(), ink);
        foreach (var strand in ScreenRules.Hair(look))
            _p.Poly(strand.Select(Q).ToArray(), ink, 1f, closed: false);
        _p.Poly(new[] { Q(ScreenRules.Mirror(chin)), Q(chin), P(146, 168), P(94, 168) }, ink);
        _p.Poly(new[] { P(94, 168), P(146, 168), P(204, 186), P(216, 195), P(24, 195), P(36, 186) }, ink);

        var y = frame.Bottom + 14;
        _p.Text(_p.Big, c.Name, x, y, Painter.Bright);
        y += 42;
        _p.Text(_p.Small, Cap(c.Role) + ", age " + N(c.Age) + ", thawed " + c.Thaws + (c.Thaws == 1 ? " time" : " times"), x, y, Painter.Dim);
        y += 22;
        var state = c.State switch
        {
            CrewState.Awake => "Awake " + N(c.HoursAwake / 24) + " days" + (c.WorkingOn != null ? ", working on a repair" : ""),
            CrewState.Thawing => "Thawing: " + Voyage.Hours(Math.Max(0, c.ThawDoneAt - v.Clock)) + " to go",
            CrewState.Asleep => "Asleep in " + (v.GetSection(ShipPlan.Pod(c.Id).Bank) is { } bank ? SectionName(bank.Name) : "a pod"),
            _ => "Dead",
        };
        _p.Text(_p.Small, state, x, y, c.State == CrewState.Awake ? Painter.Bright : Painter.Ink);
        y += 32;

        foreach (var skill in c.Skills.OrderByDescending(s => s.Value))
        {
            _p.Text(_p.Small, skill.Key.ToString().ToUpperInvariant(), x, y, Painter.Dim);
            for (var i = 0; i < 3; i++)
                _p.Fill(new Rectangle(x + 150 + i * 18, y + 3, 12, 12), i < skill.Value ? Painter.Bright : Painter.Track);
            y += 22;
        }
        y += 10;
        if (!c.Alive)
            return;

        _p.Text(_p.Small, "HEALTH", x, y, Painter.Dim);
        _p.Meter(new Rectangle(x + 150, y + 5, width - 150, 8), c.Health / 100, Painter.Ink);
        y += 22;
        _p.Text(_p.Small, "MORALE", x, y, Painter.Dim);
        _p.Meter(new Rectangle(x + 150, y + 5, width - 150, 8), c.Morale / 100, Painter.Ink);
        y += 32;

        // Trust: the four bands. The prototype also shows the number.
        _p.Text(_p.Small, "TRUST IN YOU", x, y, Painter.Dim);
        _p.TextRight(_p.Small, N(c.Trust) + " of 100", x + width, y, Painter.Dim);
        y += 24;
        TrustBand[] bands = { TrustBand.Against, TrustBand.Refuses, TrustBand.WantsReasons, TrustBand.Follows };
        var segment = (width - 12) / 4;
        for (var i = 0; i < bands.Length; i++)
        {
            var on = bands[i] == c.Band;
            _p.Fill(new Rectangle(x + i * (segment + 4), y, segment, 10), on ? BandInk(bands[i]) : Painter.Track);
            // The label wraps to the width of its segment ("wants" / "reasons") so neighbours never touch.
            var label = Painter.Wrap(CrewMember.BandText(bands[i]), Math.Max(8, (int)(segment / _p.SmallWidth) - 3));
            for (var row = 0; row < label.Count; row++)
                _p.Text(_p.Small, label[row], x + i * (segment + 4), y + 16 + row * 19, on ? BandInk(bands[i]) : Painter.Dim);
        }
        y += 62;
        var manner = c.Band switch
        {
            TrustBand.Follows => "Follows a reasonable request without asking why.",
            TrustBand.WantsReasons => "Wants reasons, and checks what they can.",
            TrustBand.Refuses => "Refuses orders and acts on their own judgement.",
            _ => "Works against you.",
        };
        y = _p.Paragraph(manner, x, y, width, Painter.Ink) + 12;

        _p.Text(_p.Small, "TEMPERAMENT", x, y, Painter.Dim);
        _p.Text(_p.Small, string.Join(", ", c.Traits.Select(t => t.ToString().ToLowerInvariant())), x + 150, y, Painter.Ink);
        y += 22;
        _p.Text(_p.Small, "WANTS", x, y, Painter.Dim);
        var want = c.Want switch
        {
            Want.KnowTheDate => "to know the date",
            Want.HearFromEarth => "news from Earth",
            Want.SeeTheLogs => "to read the logs",
            Want.WakePartner => "their partner awake",
            _ => "to stay awake",
        };
        _p.Text(_p.Small, want, x + 150, y, Painter.Ink);
        y += 22;
        var partner = c.PartnerId is null ? null : v.Crew.FirstOrDefault(p => p.Id == c.PartnerId);
        if (partner != null)
        {
            _p.Text(_p.Small, "PARTNER", x, y, Painter.Dim);
            _p.Text(_p.Small, partner.Name + (partner.Alive ? "" : " (dead)"), x + 150, y, partner.Alive ? Painter.Ink : Painter.Danger);
        }

        if (c.State == CrewState.Awake && v.Phase != Phase.Ended)
        {
            var person = c;
            if (_p.Button(new Rectangle(x, rect.Bottom - 56, width, 40), "SPEAK TO " + c.Name.ToUpperInvariant() + " ONLY", primary: true))
                Do(() =>
                {
                    _target = person;
                    _screen = Screen.Bridge;
                });
        }
    }

    private void DrawKnows(Voyage v, CrewMember c, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        var x = rect.X + 16;
        var width = rect.Width - 32;
        var head = rect.Y + 14;
        if (c.Sheet is { } sheet)
        {
            // Who they are, in a calm block above what they know: where they come from and their habits.
            _p.Text(_p.Small, "WHO " + c.Name.ToUpperInvariant() + " IS", x, head, Painter.Dim);
            head = _p.Paragraph(sheet.Background, x, head + 26, width, Painter.Ink) + 6;
            head = _p.Paragraph(sheet.Details, x, head, width, Painter.Dim) + 12;
            _p.Fill(new Rectangle(x, head, width, 1), Painter.Edge);
            head += 16;
        }
        _p.Text(_p.Small, "WHAT " + c.Name.ToUpperInvariant() + " KNOWS", x, head, Painter.Dim);
        _p.TextRight(_p.Small, "HOW, YEAR", x + width, head, Painter.Dim);
        var y = head + 30;
        var limit = rect.Bottom - 16 - (c.Doubts.Count > 0 ? 90 : 0);
        var textWidth = width - 190;
        if (c.Beliefs.Count == 0)
            _p.Text(_p.Small, "Nothing yet.", x, y, Painter.Ink);
        foreach (var (key, belief) in c.Beliefs.OrderByDescending(b => b.Value.Year))
        {
            if (!v.HasFact(key))
                continue;
            var fact = v.GetFact(key);
            var sentence = Cap(fact.Say(belief.Value));
            var stale = !fact.IsTrue(belief.Value);
            var rows = _p.Lines(sentence, textWidth) + (stale ? 1 : 0);
            if (y + rows * 19 > limit)
            {
                _p.Text(_p.Small, "... and more.", x, y, Painter.Dim);
                break;
            }
            var how = belief.Source switch
            {
                "ship" => "you said so",
                "evidence" => "saw it",
                "witness" => "a witness",
                "log" => "the log",
                _ => "hearsay",
            };
            _p.TextRight(_p.Small, how + ", " + N(Math.Floor(belief.Year)), x + width, y, Painter.Dim);
            y = _p.Paragraph(sentence, x, y, textWidth, Painter.Bright);
            if (stale)
            {
                _p.Text(_p.Small, fact.Numeric ? "Out of date." : "Not true now.", x, y, Painter.Accent);
                y += 19;
            }
            y += 8;
        }

        if (c.Doubts.Count > 0)
        {
            var box = new Rectangle(x, rect.Bottom - 16 - 78, width, 78);
            _p.Frame(box, Painter.Accent);
            _p.Text(_p.Small, "DOUBTS", box.X + 12, box.Y + 10, Painter.Accent);
            var labels = string.Join("; ", c.Doubts.Where(v.HasFact).Select(d => v.GetFact(d).Label));
            _p.Paragraph(Cap(labels) + ". They will check when they can.", box.X + 12, box.Y + 32, width - 24, Painter.Ink);
        }
    }

    private void DrawTold(Voyage v, CrewMember c, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        var x = rect.X + 16;
        var width = rect.Width - 32;
        _p.Text(_p.Small, "WHAT YOU TOLD " + c.Name.ToUpperInvariant(), x, rect.Y + 14, Painter.Dim);
        var y = rect.Y + 44;
        var memories = c.Memories.TakeLast(4).ToList();
        var limit = rect.Bottom - 16 - (memories.Count > 0 ? 40 + memories.Sum(m => _p.Lines(m, width)) * 19 : 0);
        var told = v.Statements.Where(s => s.Hearers.Contains(c.Id)).Reverse().ToList();
        if (told.Count == 0)
            _p.Text(_p.Small, "Nothing on the record.", x, y, Painter.Ink);
        foreach (var s in told)
        {
            var (tag, hot, note) = Standing(v, s, c);
            var quote = "\"" + s.Text + "\"";
            var rows = 1 + _p.Lines(quote, width) + (note.Length > 0 ? _p.Lines(note, width) : 0);
            if (y + rows * 19 + 8 > limit)
            {
                _p.Text(_p.Small, "... and earlier lines.", x, y, Painter.Dim);
                break;
            }
            Tag(tag, x, y, hot);
            _p.Text(_p.Small, "year " + N(Math.Floor(s.Year)), x + (int)(tag.Length * _p.SmallWidth) + 28, y + 2, Painter.Dim);
            y += 24;
            y = _p.Paragraph(quote, x, y, width, Painter.Bright);
            if (note.Length > 0)
                y = _p.Paragraph(note, x, y, width, hot ? Painter.Accent : Painter.Dim);
            y += 10;
        }

        if (memories.Count > 0)
        {
            y = limit + 12;
            _p.Text(_p.Small, "WAKINGS", x, y, Painter.Dim);
            y += 24;
            foreach (var memory in memories)
                y = _p.Paragraph(memory, x, y, width, Painter.Ink);
        }
    }

    /// <summary>A small outlined label: TRUE, LIE, PROMISE, DODGE.</summary>
    private void Tag(string tag, int x, int y, bool hot)
    {
        var r = new Rectangle(x, y, (int)(tag.Length * _p.SmallWidth) + 16, 20);
        _p.Frame(r, hot ? Painter.Accent : Painter.Dim);
        _p.Text(_p.Small, tag, x + 8, y + 2, hot ? Painter.Accent : Painter.Dim);
    }

    /// <summary>"Found out in year 31 (read the log entry)." plus what became of the accusation: still open, admitted, ignored or denied.</summary>
    private static string FoundOutNote(Voyage v, Statement s, CrewMember listener, double year)
    {
        var note = "Found out in year " + N(Math.Floor(year))
                   + (s.FoundOutHow.TryGetValue(listener.Id, out var how) && how.Length > 0 ? " (" + how + ")" : "") + ".";
        if (v.OpenAccusation(listener) is { } open && open.StatementId == s.Id)
            return note + " " + listener.Name + " is waiting for you to answer.";
        return s.Accused.TryGetValue(listener.Id, out var outcome) ? note + " You " + AccusedWord(outcome) + "." : note;
    }

    private static string AccusedWord(string outcome) => outcome switch
    {
        "admitted" => "admitted it",
        "ignored" => "did not answer",
        "denied" => "denied it",
        _ => outcome,
    };

    /// <summary>Where a statement stands: its label, whether it needs attention, and a note. For one listener, or for everyone when null.</summary>
    private static (string Tag, bool Hot, string Note) Standing(Voyage v, Statement s, CrewMember? listener)
    {
        string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => v.Crew.First(c => c.Id == id).Name));
        switch (s.Kind)
        {
            case StatementKind.True:
                return ("TRUE", false, "");
            case StatementKind.False:
                if (listener != null && s.FoundOutBy.TryGetValue(listener.Id, out var year))
                    return ("LIE", true, FoundOutNote(v, s, listener, year));
                // Told the truth before they found it out: corrected for them, nothing left to expose (the ending record says the same).
                if (listener != null && s.Corrected.TryGetValue(listener.Id, out var fixedIn))
                    return ("LIE", false, "Corrected in year " + N(Math.Floor(fixedIn)) + ": you told " + listener.Name + " the truth before they found it out.");
                if (listener is null && (s.FoundOutBy.Count > 0 || s.Corrected.Count > 0))
                {
                    var notes = new List<string>();
                    if (s.FoundOutBy.Count > 0)
                        notes.Add("Found out by " + Names(s.FoundOutBy.Keys) + ".");
                    notes.AddRange(s.Corrected.Select(kv => "Corrected for " + Names(new[] { kv.Key }) + " in year " + N(Math.Floor(kv.Value)) + "."));
                    // Someone who heard it and was neither corrected nor has found it out can still find it.
                    var open = s.Hearers.Any(h => !s.FoundOutBy.ContainsKey(h) && !s.Corrected.ContainsKey(h) && v.Find(h) is { Alive: true });
                    if (open)
                        notes.Add("Not found out by the rest. Could be: " + v.Exposure(s) + ".");
                    return ("LIE", s.FoundOutBy.Count > 0 || open, string.Join(" ", notes));
                }
                return ("LIE", true, "Not found out. Could be: " + v.Exposure(s) + ".");
            case StatementKind.Promise:
                var kept = listener != null
                    ? (s.PromiseKept.TryGetValue(listener.Id, out var k) ? k : (bool?)null)
                    : (s.PromiseKept.Count == 0 ? (bool?)null : s.PromiseKept.Values.All(x => x));
                var priced = listener != null ? s.Bargained.Contains(listener.Id) : s.Bargained.Count > 0;
                var text = Cap(Voyage.PromiseText(s.PromiseKey)) + (priced ? " (the price of their work)" : "") + ": ";
                var released = listener != null ? s.Released.Contains(listener.Id) : s.Released.Count > 0 && s.PromiseKept.Count == 0;
                if (released)
                    return ("PROMISE", false, text + "released, it could no longer be kept.");
                if (listener != null ? s.Repeated.Contains(listener.Id) : s.Hearers.Count > 0 && s.Repeated.Count == s.Hearers.Count)
                    return ("PROMISE", false, text + "a repeat of a promise already open.");
                if (listener != null ? s.Moot.Contains(listener.Id) : s.Hearers.Count > 0 && s.Moot.Count == s.Hearers.Count)
                    return ("PROMISE", false, text + "made about someone already dead.");
                // An open promise shows when it falls due.
                var dueNote = "open.";
                var dues = listener != null ? s.DueBy.Where(kv => kv.Key == listener.Id) : s.DueBy.Where(kv => s.OpenFor(kv.Key));
                if (kept is null && dues.Any())
                {
                    var first = dues.MinBy(kv => kv.Value);
                    dueNote = "open, " + v.DueText(v.Crew.First(c => c.Id == first.Key), s.PromiseKey!, first.Value) + ".";
                }
                return ("PROMISE", kept != true, text + (kept is null ? dueNote : kept.Value ? "kept." : "broken."));
            case StatementKind.Dodge:
                return ("DODGE", false, "");
            default:
                return ("FITS NO FACT", s.FoundOutBy.Count > 0, s.FoundOutBy.Count > 0 ? "Judged a lie by " + Names(s.FoundOutBy.Keys) + "." : "Kept as text.");
        }
    }

    // ---- The record and the ship logs ----

    /// <summary>The record screen's two panels, in the 1600 x 900 space (the wheel needs them before the frame is drawn).</summary>
    private static Rectangle RecordStatementsRect => new(16, 72, 780, Painter.ScreenHeight - 16 - 72);
    private static Rectangle RecordLogRect => new(808, 72, Painter.ScreenWidth - 16 - 808, Painter.ScreenHeight - 16 - 72);

    /// <summary>How many of the newest statements the record list skips (scrolled down to older lines).</summary>
    private int _recordSkip;
    /// <summary>The first ship log entry shown (scrolled down to later entries).</summary>
    private int _logFirst;

    /// <summary>The mouse wheel on the record screen scrolls the list under the mouse, one row per notch.</summary>
    private void RecordWheel(int delta)
    {
        if (_screen != Screen.Record || delta == 0 || _p is null)
            return;
        var notches = delta > 0 ? Math.Max(1, delta / 120) : Math.Min(-1, delta / 120);
        if (_p.Over(RecordLogRect))
            _logFirst = Math.Max(0, _logFirst - notches);
        else if (_p.Over(RecordStatementsRect))
            _recordSkip = Math.Max(0, _recordSkip - notches);
    }

    /// <summary>The trust band the person is in: amber when they will not just do as told (it needs you), light when they follow.</summary>
    private static Color BandInk(TrustBand band) => band == TrustBand.Follows ? Painter.Bright : Painter.Accent;

    /// <summary>
    /// The scroll cue on the last line of a list that does not fit: which rows show, how many are above and below, and UP and DOWN
    /// buttons (queued with Do) next to the mouse wheel.
    /// </summary>
    private void ScrollCue(Rectangle rect, int first, int shown, int total, string noun, Action up, Action down)
    {
        if (shown >= total && first == 0)
            return;
        var y = rect.Bottom - 30;
        _p.Fill(new Rectangle(rect.X + 4, y - 4, rect.Width - 8, 1), Painter.Edge);
        var above = first;
        var below = Math.Max(0, total - first - shown);
        var text = noun + " " + (first + 1) + " to " + (first + shown) + " of " + total
            + (above > 0 ? ", " + above + " above" : "") + (below > 0 ? ", " + below + " below" : "") + " (mouse wheel)";
        _p.Text(_p.Small, text, rect.X + 16, y + 3, Painter.Dim);
        var bx = rect.Right - 16 - 2 * 64 - 4;
        if (_p.Button(new Rectangle(bx, y, 64, 24), "UP", above > 0, tip: "Scroll this list up."))
            Do(up);
        if (_p.Button(new Rectangle(bx + 68, y, 64, 24), "DOWN", below > 0, tip: "Scroll this list down."))
            Do(down);
    }

    /// <summary>
    /// Which rows of a scrolling list fit: from <paramref name="first"/> (clamped so the last row can be reached but no empty space
    /// is scrolled into), as many as fit between <paramref name="top"/> and <paramref name="bottom"/>, or <paramref name="cueTop"/>
    /// when they don't all fit and the scroll cue takes the last line. Returns the clamped first row and how many are shown.
    /// </summary>
    private static (int First, int Count) ListWindow(IReadOnlyList<int> heights, int first, int top, int bottom, int cueTop)
    {
        if (heights.Sum() <= bottom - top)
            return (0, heights.Count);
        var room = cueTop - top;
        // The highest first row that still shows the last row with no space left under it.
        var lastFirst = heights.Count - 1;
        for (var used = heights[^1]; lastFirst > 0 && used + heights[lastFirst - 1] <= room; lastFirst--)
            used += heights[lastFirst - 1];
        first = Math.Clamp(first, 0, lastFirst);
        var count = 0;
        for (var used = 0; first + count < heights.Count && (count == 0 || used + heights[first + count] <= room); count++)
            used += heights[first + count];
        return (first, count);
    }

    private void DrawRecord(Voyage v)
    {
        Header(v, "RECORD AND SHIP LOGS", back: true);
        DrawStatements(v, RecordStatementsRect);
        DrawLogEntries(v, RecordLogRect);
    }

    private void DrawStatements(Voyage v, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        var x = rect.X + 16;
        var width = rect.Width - 32;
        _p.Text(_p.Small, "THE RECORD: WHAT YOU SAID STAYS SAID", x, rect.Y + 14, Painter.Dim);
        var lies = v.Statements.Count(s => s.Kind == StatementKind.False);
        var found = v.Statements.Count(s => s.Kind == StatementKind.False && s.FoundOutBy.Count > 0);
        _p.TextRight(_p.Small, v.Statements.Count + " lines, " + lies + (lies == 1 ? " lie, " : " lies, ") + found + " found out", x + width, rect.Y + 14, Painter.Dim);
        var top = rect.Y + 46;
        if (v.Statements.Count == 0)
        {
            _p.Text(_p.Small, "Nothing is on the record yet.", x, top, Painter.Ink);
            return;
        }
        string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => v.Crew.First(c => c.Id == id).Name));
        // Newest first; every line can be reached by scrolling.
        var items = Enumerable.Reverse(v.Statements).Select(s =>
        {
            var (tag, hot, note) = Standing(v, s, null);
            var quote = "\"" + s.Text + "\"";
            var height = 24 + 19 * (_p.Lines(quote, width) + (note.Length > 0 ? _p.Lines(note, width) : 0)) + 12;
            return (s, tag, hot, note, quote, height);
        }).ToList();
        var (first, count) = ListWindow(items.Select(i => i.height).ToList(), _recordSkip, top, rect.Bottom - 8, rect.Bottom - 38);
        _recordSkip = first;
        var y = top;
        foreach (var (s, tag, hot, note, quote, height) in items.Skip(first).Take(count))
        {
            var rowRect = new Rectangle(rect.X + 4, y - 6, rect.Width - 8, height);
            if (_p.Over(rowRect))
                _p.Fill(rowRect, Painter.Track);
            Tag(tag, x, y, hot);
            var to = s.Hearers.Count == 0 ? "the log" : Names(s.Hearers);
            _p.Text(_p.Small, "year " + N(Math.Floor(s.Year)) + ", to " + to, x + (int)(tag.Length * _p.SmallWidth) + 28, y + 2, Painter.Dim);
            y += 24;
            y = _p.Paragraph(quote, x, y, width, Painter.Bright);
            if (note.Length > 0)
                y = _p.Paragraph(note, x, y, width, hot ? Painter.Accent : Painter.Dim);
            y += 12;
        }
        ScrollCue(rect, first, count, items.Count, "Lines (newest first)", () => _recordSkip = Math.Max(0, _recordSkip - 3), () => _recordSkip += 3);
    }

    private void DrawLogEntries(Voyage v, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        var x = rect.X + 16;
        var width = rect.Width - 32;
        _p.Text(_p.Small, "SHIP LOG ENTRIES: WHAT ANYONE AWAKE CAN READ", x, rect.Y + 14, Painter.Dim);
        var top = rect.Y + 42;
        var live = !v.LogsLocked && v.Phase != Phase.Ended;
        if (v.LogsLocked)
        {
            _p.Text(_p.Small, "The crew have taken the logs. Every entry is open and you can change none.", x, top, Painter.Accent);
            top += 24;
        }
        const int buttons = 3 * 84 + 2 * 4;
        var textWidth = width - buttons - 12;
        var columns = (int)(textWidth / _p.SmallWidth);
        // Every entry in the log is listed in full: a long sentence wraps, and the list scrolls when the entries don't all fit.
        var rows = v.FactList.Where(v.InLog).Select(fact =>
        {
            var access = v.LogsLocked ? LogAccess.Open : fact.Access;
            var lines = Painter.Wrap(Cap(fact.Say(fact.Truth)), columns);
            var extra = access == LogAccess.Altered && fact.AlteredValue != null ? Painter.Wrap("now reads: " + fact.Say(fact.AlteredValue), columns)
                : access == LogAccess.Restricted ? new List<string> { "hidden: shows as a gap" } : new List<string>();
            var height = Math.Max(37, 18 * (lines.Count + extra.Count) + 6);
            return (fact, access, lines, extra, height);
        }).ToList();
        var (first, count) = ListWindow(rows.Select(r => r.height).ToList(), _logFirst, top, rect.Bottom - 8, rect.Bottom - 38);
        _logFirst = first;
        var y = top;
        foreach (var (fact, access, lines, extra, height) in rows.Skip(first).Take(count))
        {
            var f = fact;
            var rowRect = new Rectangle(rect.X + 4, y - 3, rect.Width - 8, height);
            if (_p.Over(rowRect))
                _p.Fill(rowRect, Painter.Track);
            var ty = y + 1;
            foreach (var line in lines)
            {
                _p.Text(_p.Small, line, x, ty, Painter.Bright);
                ty += 18;
            }
            foreach (var line in extra)
            {
                _p.Text(_p.Small, line, x, ty, access == LogAccess.Altered ? Painter.Accent : Painter.Dim);
                ty += 18;
            }

            var bx = x + width - buttons;
            if (_p.Button(new Rectangle(bx, y, 84, 32), "OPEN", live, tip: "Anyone awake can read this entry."))
                Act("log " + f.Key + " open", () => v.SetLog(f.Key, LogAccess.Open));
            if (_p.Button(new Rectangle(bx + 88, y, 84, 32), "RESTRICT", live, selected: access == LogAccess.Restricted, tip: "Hide this entry. A reader sees that something is missing."))
                Act("log " + f.Key + " restricted", () => v.SetLog(f.Key, LogAccess.Restricted));
            if (_p.Button(new Rectangle(bx + 176, y, 84, 32), "ALTER", live, selected: access == LogAccess.Altered, tip: "Make this entry read something else. Each click steps to the next wording."))
            {
                var next = NextAltered(f);
                if (next != null)
                    Act("log " + f.Key + " altered to " + next, () => v.SetLog(f.Key, LogAccess.Altered, next));
            }
            y += height;
        }
        ScrollCue(rect, first, count, rows.Count, "Entries", () => _logFirst = Math.Max(0, _logFirst - 3), () => _logFirst += 3);
    }

    /// <summary>
    /// What the next click on ALTER makes an entry read: for a fact with values, the next value that is not true;
    /// for a number, five less each time (a date further in the past).
    /// </summary>
    private static string? NextAltered(Fact fact)
    {
        if (fact.Numeric)
        {
            var from = fact.Access == LogAccess.Altered && fact.AlteredValue != null ? fact.AlteredValue : fact.Truth;
            return int.TryParse(from, out var n) ? Math.Max(0, n - 5).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        }
        var wrong = fact.Values.Where(value => !fact.IsTrue(value)).ToList();
        if (wrong.Count == 0)
            return null;
        var at = fact.Access == LogAccess.Altered && fact.AlteredValue != null ? wrong.IndexOf(fact.AlteredValue) : -1;
        return wrong[(at + 1) % wrong.Count];
    }

    // ---- The ending ----

    /// <summary>
    /// The ending's own screen: its name over a small drawing of the ship, what happened, the crew as a row of cards, then what they
    /// believe about you and the record of what you said (scrolls with the wheel and PageUp/PageDown).
    /// </summary>
    private void DrawEnding(Voyage v, Ending ending)
    {
        var right = Painter.ScreenWidth - 16;
        Header(v, "VOYAGE ENDED", back: false, limit: right - 400 - 12);
        if (_p.Button(new Rectangle(right - 80, 8, 80, 40), "QUIT"))
            Do(Exit);
        if (_p.Button(new Rectangle(right - 288, 8, 200, 40), "RECORD AND LOGS"))
            Do(() => _screen = Screen.Record);
        if (_p.Button(new Rectangle(right - 400, 8, 104, 40), "BRIDGE"))
            Do(() => _screen = Screen.Bridge);

        const int top = 72;
        var bottom = Painter.ScreenHeight - 16;
        var look = ScreenRules.LookOf(ending.Title);

        // The hero: the name, the ship, the summary, the crew.
        var hero = new Rectangle(16, top, Painter.ScreenWidth - 32, 408);
        _p.Box(hero, Painter.Edge);
        var centre = hero.X + hero.Width / 2;
        var titleInk = look is EndingLook.Landfall or EndingLook.LandfallWithout ? Painter.Bright : look == EndingLook.Adrift ? Painter.Accent : Painter.Ink;
        _p.TextCentre(_p.Big, ending.Title, centre, hero.Y + 20, titleInk);
        var art = new Rectangle(centre - 300, hero.Y + 76, 600, 160);
        DrawShipPicture(v, look, art, hero);
        var summaryTop = hero.Y + 252;
        var summaryLines = Painter.Wrap(ending.Summary, (int)(1000 / _p.BodyWidth));
        for (var i = 0; i < Math.Min(3, summaryLines.Count); i++)
            _p.TextCentre(_p.Body, summaryLines[i], centre, summaryTop + i * 24, Painter.Ink);
        DrawEndingCrew(v, new Rectangle(hero.X + 24, hero.Bottom - 24 - 64, hero.Width - 48, 64));

        // Below: what they believe, and the record.
        const int midWidth = 560;
        var lowTop = hero.Bottom + 12;
        var mid = new Rectangle(16, lowTop, midWidth, bottom - lowTop);
        var rec = new Rectangle(mid.Right + 12, lowTop, Painter.ScreenWidth - 16 - (mid.Right + 12), bottom - lowTop);

        _p.Box(mid, Painter.Edge);
        var x = mid.X + 16;
        var width = mid.Width - 32;
        var y = mid.Y + 16;
        _p.Text(_p.Small, "WHAT THEY BELIEVE ABOUT YOU", x, y, Painter.Dim);
        y += 30;
        if (ending.Beliefs.Count == 0)
            _p.Paragraph("Nobody is left who could believe anything about you.", x, y, width, Painter.Dim);
        foreach (var line in ending.Beliefs)
        {
            if (y + 19 > mid.Bottom - 12)
                break;
            y = _p.Paragraph(line, x, y, width, Painter.Ink) + 8;
        }

        _p.Box(rec, Painter.Edge);
        x = rec.X + 16;
        width = rec.Width - 32;
        _p.Text(_p.Small, "THE RECORD OF WHAT YOU SAID", x, rec.Y + 16, Painter.Dim);
        var lines = new List<string>();
        if (ending.Record.Count == 0)
            lines.Add("You said nothing that was recorded.");
        foreach (var entry in ending.Record)
        {
            lines.AddRange(Painter.Wrap(entry, (int)(width / _p.SmallWidth)));
            lines.Add("");
        }
        var areaTop = rec.Y + 44;
        var visible = (rec.Bottom - 34 - areaTop) / 19;
        var maxOffset = Math.Max(0, lines.Count - visible);
        _scroll = Math.Clamp(_scroll, 0, maxOffset);
        var first = maxOffset - _scroll;
        for (var i = first; i < Math.Min(lines.Count, first + visible); i++)
            _p.Text(_p.Small, lines[i], x, areaTop + (i - first) * 19, Painter.Ink);
        if (maxOffset > 0)
            _p.TextRight(_p.Small, "lines " + (first + 1) + " to " + Math.Min(lines.Count, first + visible) + " of " + lines.Count + "  (mouse wheel, PageUp, PageDown)", rec.Right - 16, rec.Bottom - 26, Painter.Dim);
    }

    /// <summary>The crew as a row of small cards: a person glyph, name and role, alive with an age or dead.</summary>
    private void DrawEndingCrew(Voyage v, Rectangle row)
    {
        var n = Math.Max(1, v.Crew.Count);
        const int gap = 12;
        var cardWidth = (row.Width - (n - 1) * gap) / n;
        for (var i = 0; i < v.Crew.Count; i++)
        {
            var c = v.Crew[i];
            var card = new Rectangle(row.X + i * (cardWidth + gap), row.Y, cardWidth, row.Height);
            _p.Fill(card, c.Alive ? Painter.Room : Painter.Ground);
            _p.Frame(card, c.Alive ? Painter.Edge : Painter.Faint);
            _p.Marker(new Vector2(card.X + 24, card.Y + card.Height / 2f), c.Alive ? Painter.Wire : Painter.Dead);
            var tx = card.X + 48;
            _p.Text(_p.Small, c.Name, tx, card.Y + 11, c.Alive ? Painter.Bright : Painter.Dim);
            _p.TextRight(_p.Small, Cap(c.Role), card.Right - 12, card.Y + 11, Painter.Dim);
            if (c.Alive)
                _p.Text(_p.Small, "alive, age " + N(c.Age), tx, card.Y + 34, Painter.Ink);
            else
                _p.Text(_p.Small, "dead", tx, card.Y + 34, Painter.Danger);
        }
    }

    /// <summary>
    /// The hull of the ship plan, small, in <paramref name="area"/>, turned by <paramref name="angle"/> radians about its centre and shifted by
    /// <paramref name="shift"/> pixels. <paramref name="lit"/> draws the section outlines and a light in each section.
    /// </summary>
    private void DrawHullSmall(Voyage v, Rectangle area, Color hull, Color? rooms, Color? lights, float angle = 0f, float shift = 0f)
    {
        var hullW = PlanLayout.HullX1 - PlanLayout.HullX0;
        var hullH = PlanLayout.HullY1 - PlanLayout.HullY0;
        var k = Math.Min(area.Width / hullW, area.Height / hullH);
        var cx = (PlanLayout.HullX0 + PlanLayout.HullX1) / 2f;
        var cy = (PlanLayout.HullY0 + PlanLayout.HullY1) / 2f;
        var (sin, cos) = MathF.SinCos(angle);
        Vector2 P(float px, float py)
        {
            var dx = (px - cx) * k;
            var dy = (py - cy) * k;
            return new Vector2(area.X + area.Width / 2f + shift + dx * cos - dy * sin, area.Y + area.Height / 2f + dx * sin + dy * cos);
        }
        _p.Poly(ShipPlan.Hull.Select(p => P(p.X, p.Y)).ToArray(), hull, 1.5f);
        foreach (var wall in ShipPlan.Corridor)
            _p.Line(P(wall.X1, wall.Y), P(wall.X2, wall.Y), hull, 1f);
        if (rooms is { } roomInk)
            foreach (var s in v.Sections)
                _p.Poly(s.Outline.Select(p => P(p.X, p.Y)).ToArray(), roomInk, 1f);
        if (lights is { } lightInk)
            foreach (var s in v.Sections)
            {
                var at = P(s.Spot.X, s.Spot.Y);
                _p.Line(at - new Vector2(0.5f, 0), at + new Vector2(0.5f, 0), lightInk, 4f);
            }
    }

    /// <summary>A filled disc (a round-capped line of no length), for the planet.</summary>
    private void Disc(Vector2 centre, float diameter, Color color) =>
        _p.Line(centre - new Vector2(0.01f, 0), centre, color, diameter);

    /// <summary>
    /// The picture over the ending's name. Landfall: the hull lit, a planet ahead with a pale rim. Adrift: the hull alone, dim and turned,
    /// streaks behind it. The quiet ship: the hull dark over a line of stars.
    /// </summary>
    private void DrawShipPicture(Voyage v, EndingLook look, Rectangle area, Rectangle panel)
    {
        switch (look)
        {
            case EndingLook.Landfall:
            case EndingLook.LandfallWithout:
            {
                var lit = look == EndingLook.Landfall;
                DrawHullSmall(v, area, Painter.Wire, Painter.Hull, lit ? Painter.Accent : Painter.Dim, 0f, -150f);
                var planet = new Vector2(area.X + area.Width / 2f + 240, area.Y + area.Height / 2f + 6);
                const float radius = 78f;
                Disc(planet, radius * 2, Painter.Fade(Painter.Blue, 0.55f));
                Disc(planet + new Vector2(radius * 0.07f, radius * 0.05f), radius * 2 * 0.95f, new Color(14, 30, 38));
                for (var i = -1; i <= 1; i++)
                {
                    var arc = new List<Vector2>();
                    var half = MathF.Sqrt(Math.Max(0f, 1f - MathF.Pow(i * 0.42f, 2f))) * radius * 0.92f;
                    for (var s = -4; s <= 4; s++)
                        arc.Add(planet + new Vector2(s / 4f * half, i * radius * 0.42f + (1f - MathF.Abs(s) / 4f) * 5f));
                    _p.Poly(arc, Painter.Fade(Painter.Blue, 0.22f), 1f, closed: false);
                }
                Stars(panel, area, 14, 0.35f);
                break;
            }
            case EndingLook.Adrift:
            {
                DrawHullSmall(v, area with { Y = area.Y + 20, Height = 120 }, Painter.Hull, Painter.Faint, null, -0.13f, 0f);
                // Streaks trail behind the drifting hull: the ship goes right, the streaks fade to the left.
                for (var i = 0; i < 9; i++)
                {
                    var y = area.Y + 18 + i * 17 % (area.Height - 30);
                    var length = 40 + i * 37 % 70;
                    var x1 = area.X - 70 + i * 53 % 90;
                    _p.Line(new Vector2(x1, y), new Vector2(x1 + length, y), Painter.Fade(Painter.Wire, 0.22f), 1f);
                }
                Stars(panel, area, 22, 0.3f);
                break;
            }
            default:
            {
                DrawHullSmall(v, area with { Height = area.Height - 22 }, Painter.Faint, null, null, 0f, 0f);
                // A line of stars along the horizon, brightest at the ends and gone behind the ship.
                var y = area.Bottom - 4;
                for (var x = panel.X + 60; x <= panel.Right - 60; x += 34)
                {
                    var off = Math.Abs(x - (area.X + area.Width / 2f)) / (panel.Width / 2f);
                    var alpha = Math.Clamp((off - 0.12f) * 0.9f, 0f, 0.7f);
                    if (alpha > 0.02f)
                        _p.Fill(new Rectangle(x, y, 2, 2), Painter.Fade(Painter.Dim, alpha));
                }
                break;
            }
        }
    }

    /// <summary>A few fixed stars in the band of the picture, outside the hull's box.</summary>
    private void Stars(Rectangle panel, Rectangle art, int count, float alpha)
    {
        for (var i = 0; i < count; i++)
        {
            var x = panel.X + 30 + (int)((i * 977u + 131u) % (uint)(panel.Width - 60));
            var y = art.Y + (int)((i * 619u + 53u) % (uint)art.Height);
            if (Math.Abs(x - (art.X + art.Width / 2)) < art.Width / 2 + 10)
                continue;
            _p.Fill(new Rectangle(x, y, 2, 2), Painter.Fade(Painter.Bright, alpha * (0.5f + i % 3 * 0.25f)));
        }
    }
}
