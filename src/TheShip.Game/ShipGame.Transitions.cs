using System.Globalization;
using Microsoft.Xna.Framework;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// Time-skip transitions (UI-PLAN.md, step 2). The simulation jumps at once; the interface takes a snapshot before a clock
/// move, and afterwards shows the numbers moving from the snapshot to the new state. New log lines are held back until the
/// transition is over, so the outcome is not readable before the animation gets there. Nothing here changes the game.
/// </summary>
public sealed partial class ShipGame
{
    /// <summary>The three sizes of a clock move: to the next event in an alert, weeks in the aftermath, years to the next alert.</summary>
    private enum SkipKind { Hours, Weeks, Drift }

    /// <summary>What the interface showed before a clock move.</summary>
    private sealed class Before
    {
        public double Clock;
        public double Stores;
        public int Parts;
        public int PodWear;
        public int LogCount;
        public int AlertNumber;
        public Phase Phase;
        public readonly Dictionary<string, (CrewState State, double Age, double HoursAwake, double ThawLeft)> Crew = new();
        public readonly Dictionary<string, (double Left, double Progress)> Faults = new();
    }

    private sealed class Transition
    {
        public SkipKind Kind;
        public Before From = new();
        /// <summary>When the current beat started.</summary>
        public double Start;
        /// <summary>A drift has three beats: 0 the years roll, 1 the report waits until the player does something, 2 the new alert appears.</summary>
        public int Beat;
        public string Report = "";
        public string Banner = "";
        /// <summary>A drift's own end, before the next alert started (the report's numbers and deaths).</summary>
        public DriftOutcome? Drift;
    }

    /// <summary>Where the ship plan sits on the bridge; the drift plays over it.</summary>
    private static readonly Rectangle PlanRect = new(358, 72, 864, 456);

    private Transition? _transition;
    /// <summary>What the last "until something happens" click did; null after any other clock move.</summary>
    private AdvanceResult? _lastAdvance;
    /// <summary>Why the clock stopped, shown over the log for a few seconds or until the next click.</summary>
    private string? _banner;
    private double _bannerUntil;

    private static Before Snapshot(Voyage v)
    {
        var before = new Before
        {
            Clock = v.Clock, Stores = v.Stores, Parts = v.Parts, PodWear = v.PodWear, LogCount = v.Log.Count,
            AlertNumber = v.Current.Number, Phase = v.Phase,
        };
        foreach (var c in v.Crew)
            before.Crew[c.Id] = (c.State, c.Age, c.HoursAwake, Math.Max(0, c.ThawDoneAt - v.Clock));
        foreach (var f in v.Current.Faults)
            before.Faults[f.Id] = (Math.Max(0, f.Deadline - v.Clock), f.Progress);
        return before;
    }

    /// <summary>Called right after a clock move ran, with the snapshot taken right before it.</summary>
    private void BeginTransition(SkipKind kind, Before before)
    {
        if (_voyage is not { } v)
            return;
        var passed = Voyage.Hours(v.Clock - before.Clock);
        var banner = kind != SkipKind.Weeks ? ""
            : _lastAdvance is null ? (v.Clock > before.Clock ? passed + " pass." : "")
            : _lastAdvance.Reason.Length > 0 ? "Stopped: " + _lastAdvance.Reason + "."
            : v.Clock > before.Clock ? passed + " pass. Nothing needs you." : "";
        var moved = v.Clock != before.Clock || v.Current.Number != before.AlertNumber || v.Phase != before.Phase;
        if (!moved || Motion.Still)
        {
            // A refused move (the log says why), or the tour: nothing to animate.
            ShowBanner(banner);
            return;
        }
        var report = "";
        if (kind == SkipKind.Drift)
        {
            var line = v.Log.Skip(before.LogCount).FirstOrDefault(e => e.Kind == LogKind.Ship && e.Text.StartsWith("DRIFT:", StringComparison.Ordinal));
            report = line is null ? "" : line.Text["DRIFT:".Length..].Trim();
        }
        // A transition plays over the bridge only. A clock click held behind a line being read can land after the player
        // opened another screen: the clock move they asked for brings them back to the bridge to see it.
        _screen = Screen.Bridge;
        _picker = null;
        _transition = new Transition { Kind = kind, From = before, Start = _now, Banner = banner, Report = report, Drift = kind == SkipKind.Drift ? v.LastDrift : null };
        if (kind == SkipKind.Drift)
            _sounds.Drift();
    }

    private void ShowBanner(string text)
    {
        if (text.Length == 0)
            return;
        _banner = text;
        _bannerUntil = _now + Motion.Seconds(Motion.Banner);
    }

    private void EndTransition()
    {
        if (_transition is not { } t)
            return;
        _transition = null;
        // A drift skipped before its alert's title showed (Esc, a key, a click) still sounds the new alert's alarm.
        if (t is { Kind: SkipKind.Drift, Beat: < 2 } && _voyage is { Phase: Phase.Alert })
            SoundAlarm();
        ShowBanner(t.Banner);
    }

    /// <summary>The report's CONTINUE: on to the new alert, or to the ending after the last drift.</summary>
    private void ContinueDrift()
    {
        if (_transition is not { Kind: SkipKind.Drift, Beat: 1 } t)
            return;
        if (_voyage is not { Phase: not Phase.Ended })
        {
            EndTransition();
            return;
        }
        t.Beat = 2;
        t.Start = _now;
        SoundAlarm();
    }

    /// <summary>The drift report's width on the plan.</summary>
    private const int ReportWidth = 620;

    /// <summary>
    /// The transition's own box, which takes a click on it: the year while it rolls, the drift report while it waits,
    /// the alert's title while it shows. Anywhere else a click ends the transition and goes through to the bridge
    /// (the user's decision, 2026-11-13: no step that only confirms; the report stays up until the player does something).
    /// </summary>
    private Rectangle? TransitionBlock(Voyage v)
    {
        if (_transition is not { Kind: SkipKind.Drift } t)
            return null;
        var centre = PlanRect.X + PlanRect.Width / 2;
        if (t.Beat == 0)
            return new Rectangle(centre - 150, PlanRect.Y + PlanRect.Height / 2 - 62, 300, 124);
        if (t.Beat == 1)
            return new Rectangle(centre - ReportWidth / 2, PlanRect.Y + 48, ReportWidth, 100 + ReportRowHeight * 4 + (DriftReportOf(v, t).Lost.Count > 0 ? 28 : 0) + 6 + _p.Lines(ReportText(t), ReportWidth - 48) * 19 + 76);
        if (t.Beat == 2 && (float)(_now - t.Start) < Motion.Seconds(Motion.AlertTitle))
        {
            var width = Math.Max(360, (int)(v.Current.Title.Length * _p.HeadWidth) + 64);
            return new Rectangle(centre - width / 2, PlanRect.Y + PlanRect.Height / 2 - 48, width, 96);
        }
        return null;
    }

    /// <summary>Height of one row of the drift report's table: the numbers, then the before and after bars.</summary>
    private const int ReportRowHeight = 44;

    /// <summary>The drift report's rows and who died in the drift itself (not in the alert that followed it).</summary>
    private static (List<ReportRow> Rows, IReadOnlyList<string> Lost) DriftReportOf(Voyage v, Transition t) =>
        ScreenRules.DriftReport(t.From.Stores, t.From.Parts, t.From.PodWear, t.From.Crew.Values.Count(c => c.State != CrewState.Dead), v.Crew.Count, t.Drift);

    private static string ReportText(Transition t) => t.Report.Length > 0 ? t.Report : "The years pass.";

    /// <summary>A click on the transition's box: on to its next beat, or to its end.</summary>
    private void SkipTransition()
    {
        if (_transition is not { } t)
            return;
        if (t.Kind != SkipKind.Drift || t.Beat == 2)
            EndTransition();
        else if (t.Beat == 0)
        {
            t.Beat = 1;
            t.Start = _now;
        }
        else
            ContinueDrift();
    }

    /// <summary>How long the appearing of the new alert takes: its title, then one fault after the other.</summary>
    private static float RevealSeconds(Voyage v) => Motion.AlertTitle + Motion.FaultReveal * v.Current.Faults.Count + 0.4f;

    /// <summary>Moves the transition on; called once per update.</summary>
    private void StepTransition()
    {
        if (_transition is not { } t || _voyage is not { } v)
            return;
        if (_screen != Screen.Bridge)
        {
            // Left the bridge some other way while it ran: it is over (the log holds what it showed).
            EndTransition();
            return;
        }
        switch (t.Kind)
        {
            case SkipKind.Hours:
                if (Motion.Progress(_now, t.Start, Motion.SkipHours) >= 1f)
                    EndTransition();
                break;
            case SkipKind.Weeks:
                if (Motion.Progress(_now, t.Start, Motion.SkipWeeks) >= 1f)
                    EndTransition();
                break;
            default:
                if (t.Beat == 0 && Motion.Progress(_now, t.Start, Motion.DriftRoll) >= 1f)
                {
                    t.Beat = 1;
                    t.Start = _now;
                }
                else if (t.Beat == 2 && Motion.Progress(_now, t.Start, RevealSeconds(v)) >= 1f)
                    EndTransition();
                break;
        }
    }

    /// <summary>How far the shown numbers have moved from the snapshot to the real state, 0 to 1.</summary>
    private float SkipT
    {
        get
        {
            if (_transition is not { } t)
                return 1f;
            return t.Kind switch
            {
                SkipKind.Hours => Motion.EaseOut(Motion.Progress(_now, t.Start, Motion.SkipHours)),
                SkipKind.Weeks => Motion.EaseOut(Motion.Progress(_now, t.Start, Motion.SkipWeeks)),
                _ => t.Beat == 0 ? Motion.EaseOut(Motion.Progress(_now, t.Start, Motion.DriftRoll)) : 1f,
            };
        }
    }

    /// <summary>The years are rolling or the drift report is up: the new alert is not shown yet.</summary>
    private bool Drifting => _transition is { Kind: SkipKind.Drift, Beat: < 2 };

    // ---- What the screens show while a transition runs (the real values otherwise) ----

    private double ShownClock(Voyage v) => _transition is { } t ? Motion.Lerp(t.From.Clock, v.Clock, SkipT) : v.Clock;

    private double ShownStores(Voyage v) => _transition is { } t ? Motion.Lerp(t.From.Stores, v.Stores, SkipT) : v.Stores;

    private int ShownPodWear(Voyage v) => _transition is { } t ? (int)Math.Round(Motion.Lerp(t.From.PodWear, v.PodWear, SkipT)) : v.PodWear;

    private int ShownParts(Voyage v) => _transition is { } t && SkipT < 1f ? t.From.Parts : v.Parts;

    /// <summary>Hours left on a fault's clock.</summary>
    private double ShownLeft(Voyage v, Fault f)
    {
        var left = Math.Max(0, f.Deadline - v.Clock);
        return _transition is { } t && t.From.Faults.TryGetValue(f.Id, out var was) ? Motion.Lerp(was.Left, left, SkipT) : left;
    }

    private double ShownProgress(Fault f) =>
        _transition is { } t && t.From.Faults.TryGetValue(f.Id, out var was) ? Motion.Lerp(was.Progress, f.Progress, SkipT) : f.Progress;

    private double ShownAge(CrewMember c) =>
        _transition is { } t && t.From.Crew.TryGetValue(c.Id, out var was) ? Motion.Lerp(was.Age, c.Age, SkipT) : c.Age;

    private double ShownHoursAwake(CrewMember c) =>
        _transition is { } t && t.From.Crew.TryGetValue(c.Id, out var was) && c.HoursAwake >= was.HoursAwake
            ? Motion.Lerp(was.HoursAwake, c.HoursAwake, SkipT)
            : c.HoursAwake;

    private double ShownThawLeft(Voyage v, CrewMember c)
    {
        var left = Math.Max(0, c.ThawDoneAt - v.Clock);
        return _transition is { } t && t.From.Crew.TryGetValue(c.Id, out var was) && was.State == CrewState.Thawing
            ? Motion.Lerp(was.ThawLeft, left, SkipT)
            : left;
    }

    /// <summary>A pod on the plan keeps its state from before a drift until the report is up.</summary>
    private CrewState ShownState(CrewMember c) =>
        _transition is { Kind: SkipKind.Drift, Beat: 0 } t && t.From.Crew.TryGetValue(c.Id, out var was) ? was.State : c.State;

    /// <summary>Log lines written by the clock move stay back until the transition is over (a drift: until the report is closed).</summary>
    private int ShownLogCount(Voyage v) =>
        _transition is { } t && !(t.Kind == SkipKind.Drift && t.Beat == 2) ? Math.Min(t.From.LogCount, v.Log.Count) : v.Log.Count;

    /// <summary>How many of the alert's faults are shown: none while drifting, then one after the other.</summary>
    private int FaultsRevealed(Voyage v)
    {
        if (_transition is not { Kind: SkipKind.Drift } t)
            return int.MaxValue;
        if (t.Beat < 2)
            return 0;
        var since = (float)(_now - t.Start) - Motion.Seconds(Motion.AlertTitle);
        var each = Motion.Seconds(Motion.FaultReveal);
        return since < 0 ? 0 : each <= 0f ? int.MaxValue : 1 + (int)(since / each);
    }

    // ---- Drawing ----

    /// <summary>The strip over the log's title that says why the clock stopped.</summary>
    private void DrawBanner(Rectangle log)
    {
        if (_banner is null)
            return;
        var left = (float)(_bannerUntil - _now);
        if (left <= 0f)
            return; // cleared in the next update (StepOverlays)
        var strip = new Rectangle(log.X + 1, log.Y + 1, log.Width - 2, 33);
        _p.Fill(strip, Painter.Fade(Painter.Accent, Math.Min(1f, left / 0.5f)));
        _p.Text(_p.Body, Clip(_banner, strip.Width - 28, _p.BodyWidth), strip.X + 14, strip.Y + 7, Painter.Ground);
    }

    /// <summary>The transition's own drawing, on top of the bridge. Hours and weeks only move the numbers; a drift plays over the plan.</summary>
    private void DrawTransition(Voyage v)
    {
        if (_transition is not { } t)
            return;
        // A click on the transition's box moves it on a beat; a click anywhere else ends it, and the screen below had it
        // too (DrawScene): it goes through. The end is queued first, so a clock move clicked now starts a transition of its own.
        if (_p.Click)
        {
            if (TransitionBlock(v) is { } block && _p.Over(block))
                Do(SkipTransition);
            else
                _actions.Insert(0, EndTransition);
        }
        if (t.Kind != SkipKind.Drift)
            return;

        // The side columns and the log step back; the plan stays lit.
        var dim = t.Beat switch
        {
            0 => 0.78f * Motion.Progress(_now, t.Start, 0.4f),
            1 => 0.78f,
            _ => 0.78f * (1f - Motion.Progress(_now, t.Start, 0.6f)),
        };
        var ground = Painter.Fade(Painter.Ground, dim);
        var plan = PlanRect;
        _p.Fill(new Rectangle(0, 57, plan.X - 6, Painter.ScreenHeight - 57), ground);
        _p.Fill(new Rectangle(plan.Right + 6, 57, Painter.ScreenWidth - plan.Right - 6, Painter.ScreenHeight - 57), ground);
        _p.Fill(new Rectangle(plan.X - 6, plan.Bottom + 6, plan.Width + 12, Painter.ScreenHeight - plan.Bottom - 6), ground);

        var centre = plan.X + plan.Width / 2;
        if (t.Beat == 0)
        {
            // Streaks pass behind the year as the ship drifts.
            var linear = Motion.Progress(_now, t.Start, Motion.DriftRoll);
            var strength = MathF.Sin(linear * MathF.PI);
            for (var i = 0; i < 18; i++)
            {
                var y = plan.Y + 44 + i * 53 % (plan.Height - 88);
                var length = 40 + i * 29 % 80;
                var speed = 500 + i * 137 % 400;
                var x = plan.Right - (float)((_now * speed + i * 211) % (plan.Width + length));
                var x1 = Math.Max(x, plan.X + 2);
                var x2 = Math.Min(x + length, plan.Right - 2);
                if (x2 > x1)
                    _p.Line(new Vector2(x1, y), new Vector2(x2, y), Painter.Fade(Painter.Wire, 0.55f * strength), 1f);
            }
            var box = TransitionBlock(v)!.Value;
            _p.Fill(box, Painter.Fade(Painter.Ground, 0.9f));
            _p.Frame(box, Painter.Wire);
            _p.TextCentre(_p.Small, "DRIFTING - YEAR", centre, box.Y + 16, Painter.Dim);
            _p.TextCentre(_p.Big, Math.Floor(ShownClock(v) / Tuning.HoursPerYear).ToString("0", CultureInfo.InvariantCulture), centre, box.Y + 42, Painter.Bright);
            _p.TextCentre(_p.Small, "of " + Scenario.ArrivalYear, centre, box.Y + 90, Painter.Dim);
        }
        else if (t.Beat == 1)
        {
            const int width = ReportWidth;
            var text = ReportText(t);
            var card = TransitionBlock(v)!.Value;
            _p.Box(card, Painter.Bright);
            var x = card.X + 24;
            var y = card.Y + 20;
            _p.Text(_p.Small, "DRIFT REPORT", x, y, Painter.Dim);
            y += 26;
            _p.Text(_p.Head, "Year " + N(Math.Floor(t.From.Clock / Tuning.HoursPerYear)) + " to year " + N(Math.Floor(v.Year)), x, y, Painter.Bright);
            y += 44;
            // A small table: each row has its label, the numbers before and after, and two bars on one scale (before above, after below).
            var tableWidth = width - 48;
            var (rows, lost) = DriftReportOf(v, t);
            foreach (var row in rows)
            {
                var unit = row.Label == "STORES" ? " d" : "";
                var changeInk = !row.Changed ? Painter.Ink : row.Harm && row.Worse ? Painter.Danger : row.Worse ? Painter.Accent : Painter.Blue;
                _p.Text(_p.Small, row.Label, x, y, Painter.Dim);
                _p.TextRight(_p.Small, N(row.Was) + unit + "  to  " + N(row.Now) + unit, x + tableWidth, y, changeInk);
                var bar = new Rectangle(x, y + 22, tableWidth, 5);
                _p.Fill(bar, Painter.Track);
                _p.Fill(new Rectangle(bar.X, bar.Y, (int)(bar.Width * row.WasFill), bar.Height), Painter.Wire);
                var after = new Rectangle(x, y + 29, tableWidth, 5);
                _p.Fill(after, Painter.Track);
                _p.Fill(new Rectangle(after.X, after.Y, (int)(after.Width * row.NowFill), after.Height), changeInk);
                y += ReportRowHeight;
            }
            if (lost is { Count: > 0 } dead)
            {
                _p.Text(_p.Small, "LOST", x, y, Painter.Danger);
                _p.Text(_p.Small, string.Join(", ", dead), x + 80, y, Painter.Danger);
                y += 28;
            }
            y = _p.Paragraph(text, x, y + 6, width - 48, Painter.Ink) + 16;
            // A click anywhere on the card is CONTINUE (on to the new alert); a click or key elsewhere closes it and goes through.
            _ = _p.Button(new Rectangle(x, y, 200, 44), "CONTINUE", primary: true);
            _p.Text(_p.Small, "or carry on: any click or key", x + 216, y + 14, Painter.Dim);
        }
        else
        {
            // The new alert: its title first, then the faults appear on the plan and in the issue list.
            if (TransitionBlock(v) is { } box)
            {
                var title = v.Current.Title.ToUpperInvariant();
                _p.Fill(box, Painter.Fade(Painter.Ground, 0.92f));
                _p.Frame(box, Painter.Accent, 2);
                _p.TextCentre(_p.Small, "ALERT " + v.Current.Number + " OF " + v.Alerts.Count, centre, box.Y + 16, Painter.Accent);
                _p.TextCentre(_p.Head, title, centre, box.Y + 46, Painter.Bright);
            }
        }
    }
}
