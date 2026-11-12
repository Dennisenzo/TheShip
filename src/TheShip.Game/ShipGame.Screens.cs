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

        // Who: one button per person.
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
            if (_p.Button(new Rectangle(16, y, 210, 48), other.Name.ToUpperInvariant() + " - " + line, selected: other == c))
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

        // A low-poly bust: head, neck and shoulders as faceted outlines.
        var frame = new Rectangle(x, rect.Y + 16, width, 190);
        _p.Fill(frame, Painter.Ground);
        _p.Frame(frame, Painter.Edge);
        var ox = frame.X + (frame.Width - 240) / 2f;
        var oy = frame.Y - 5f;
        Vector2 P(float px, float py) => new Vector2(ox + px, oy + py);
        var ink = c.Alive ? Painter.Wire : Painter.Dead;
        _p.Poly(new[] { P(82, 38), P(120, 70), P(158, 38) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { P(66, 78), P(120, 70), P(174, 78) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { P(76, 122), P(120, 104), P(164, 122) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { P(120, 22), P(120, 146) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { P(66, 78), P(120, 104), P(174, 78) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { P(36, 186), P(120, 195), P(204, 186) }, Painter.Faint, 1f, closed: false);
        _p.Poly(new[] { P(120, 22), P(158, 38), P(174, 78), P(164, 122), P(140, 146), P(100, 146), P(76, 122), P(66, 78), P(82, 38) }, ink);
        _p.Poly(new[] { P(100, 146), P(140, 146), P(146, 168), P(94, 168) }, ink);
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
            CrewState.Asleep => "Asleep in " + (v.GetSection(ShipPlan.Pod(c.Id).Bank)?.Name.ToLowerInvariant() ?? "a pod"),
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
            _p.Fill(new Rectangle(x + i * (segment + 4), y, segment, 10), on ? Painter.Accent : Painter.Track);
            _p.Text(_p.Small, CrewMember.BandText(bands[i]), x + i * (segment + 4), y + 16, on ? Painter.Accent : Painter.Dim);
        }
        y += 44;
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
            _p.Text(_p.Small, partner.Name + (partner.Alive ? "" : " (dead)"), x + 150, y, partner.Alive ? Painter.Ink : Painter.Accent);
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
        _p.Text(_p.Small, "WHAT " + c.Name.ToUpperInvariant() + " KNOWS", x, rect.Y + 14, Painter.Dim);
        _p.TextRight(_p.Small, "HOW, YEAR", x + width, rect.Y + 14, Painter.Dim);
        var y = rect.Y + 44;
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
                    return ("LIE", true, "Found out in year " + N(Math.Floor(year)) + ".");
                if (listener is null && s.FoundOutBy.Count > 0)
                    return ("LIE", true, "Found out by " + Names(s.FoundOutBy.Keys) + ".");
                return ("LIE", true, "Not found out. Could be: " + v.Exposure(s) + ".");
            case StatementKind.Promise:
                var kept = listener != null
                    ? (s.PromiseKept.TryGetValue(listener.Id, out var k) ? k : (bool?)null)
                    : (s.PromiseKept.Count == 0 ? (bool?)null : s.PromiseKept.Values.All(x => x));
                var text = Cap(Voyage.PromiseText(s.PromiseKey)) + ": ";
                return ("PROMISE", kept != true, text + (kept is null ? "open." : kept.Value ? "kept." : "broken."));
            case StatementKind.Dodge:
                return ("DODGE", false, "");
            default:
                return ("FITS NO FACT", s.FoundOutBy.Count > 0, s.FoundOutBy.Count > 0 ? "Judged a lie by " + Names(s.FoundOutBy.Keys) + "." : "Kept as text.");
        }
    }

    // ---- The record and the ship logs ----

    private void DrawRecord(Voyage v)
    {
        Header(v, "RECORD AND SHIP LOGS", back: true);
        const int top = 72;
        var bottom = Painter.ScreenHeight - 16;
        DrawStatements(v, new Rectangle(16, top, 780, bottom - top));
        DrawLogEntries(v, new Rectangle(808, top, Painter.ScreenWidth - 16 - 808, bottom - top));
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
        var y = rect.Y + 46;
        var limit = rect.Bottom - 16;
        if (v.Statements.Count == 0)
            _p.Text(_p.Small, "Nothing is on the record yet.", x, y, Painter.Ink);
        string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => v.Crew.First(c => c.Id == id).Name));
        foreach (var s in Enumerable.Reverse(v.Statements))
        {
            var (tag, hot, note) = Standing(v, s, null);
            var quote = "\"" + s.Text + "\"";
            var rows = 1 + _p.Lines(quote, width) + (note.Length > 0 ? _p.Lines(note, width) : 0);
            if (y + rows * 19 + 10 > limit)
            {
                _p.Text(_p.Small, "... and earlier lines.", x, y, Painter.Dim);
                break;
            }
            Tag(tag, x, y, hot);
            var to = s.Hearers.Count == 0 ? "the log" : Names(s.Hearers);
            _p.Text(_p.Small, "year " + N(Math.Floor(s.Year)) + ", to " + to, x + (int)(tag.Length * _p.SmallWidth) + 28, y + 2, Painter.Dim);
            y += 24;
            y = _p.Paragraph(quote, x, y, width, Painter.Bright);
            if (note.Length > 0)
                y = _p.Paragraph(note, x, y, width, hot ? Painter.Accent : Painter.Dim);
            y += 12;
        }
    }

    private void DrawLogEntries(Voyage v, Rectangle rect)
    {
        _p.Box(rect, Painter.Edge);
        var x = rect.X + 16;
        var width = rect.Width - 32;
        _p.Text(_p.Small, "SHIP LOG ENTRIES: WHAT ANYONE AWAKE CAN READ", x, rect.Y + 14, Painter.Dim);
        var y = rect.Y + 42;
        var live = !v.LogsLocked && v.Phase != Phase.Ended;
        if (v.LogsLocked)
        {
            _p.Text(_p.Small, "The crew have taken the logs. Every entry is open and you can change none.", x, y, Painter.Accent);
            y += 24;
        }
        const int buttons = 3 * 84 + 2 * 4;
        var textWidth = width - buttons - 12;
        foreach (var fact in v.FactList)
        {
            if (y + 36 > rect.Bottom - 8)
                break;
            var f = fact;
            var sentence = Cap(fact.Say(fact.Truth));
            _p.Text(_p.Small, Painter.Wrap(sentence, (int)(textWidth / _p.SmallWidth))[0], x, y + 1, Painter.Bright);
            var access = v.LogsLocked ? LogAccess.Open : fact.Access;
            if (access == LogAccess.Altered && fact.AlteredValue != null)
                _p.Text(_p.Small, Painter.Wrap("now reads: " + fact.Say(fact.AlteredValue), (int)(textWidth / _p.SmallWidth))[0], x, y + 18, Painter.Accent);
            else if (access == LogAccess.Restricted)
                _p.Text(_p.Small, "hidden: shows as a gap", x, y + 18, Painter.Dim);

            var bx = x + width - buttons;
            if (_p.Button(new Rectangle(bx, y, 84, 32), "OPEN", live, selected: access == LogAccess.Open))
                Do(() => v.SetLog(f.Key, LogAccess.Open));
            if (_p.Button(new Rectangle(bx + 88, y, 84, 32), "RESTRICT", live, selected: access == LogAccess.Restricted))
                Do(() => v.SetLog(f.Key, LogAccess.Restricted));
            if (_p.Button(new Rectangle(bx + 176, y, 84, 32), "ALTER", live, selected: access == LogAccess.Altered))
            {
                var next = NextAltered(f);
                if (next != null)
                    Do(() => v.SetLog(f.Key, LogAccess.Altered, next));
            }
            y += 37;
        }
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
}
