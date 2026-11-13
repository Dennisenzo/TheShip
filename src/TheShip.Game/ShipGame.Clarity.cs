using System.Globalization;
using Microsoft.Xna.Framework;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// What shows the player what they are doing (UI-PLAN.md, steps 3 and 4): the next-step line, the numbers in the header with
/// their change flashes, the picker for a repair with nobody selected, and what is under the mouse for cross-highlighting.
/// </summary>
public sealed partial class ShipGame
{
    // ---- The next useful step ----

    /// <summary>One line that names something the player can do now. The first rule that matches wins.</summary>
    private static string NextStep(Voyage v)
    {
        if (v.Phase == Phase.Ended)
            return "The voyage is over.";
        var accused = v.Awake.FirstOrDefault(c => v.OpenAccusation(c) != null);
        if (accused != null && v.OpenAccusation(accused)!.PromiseKey != null)
            return v.PromiseAccusationShort(accused, v.OpenAccusation(accused)!) + " Keep it, or tell " + accused.Them + " the truth.";
        if (accused != null)
            return accused.Name + " caught a lie. Answer " + accused.Them + ": admit it, or " + accused.They + " will check everything.";
        var asker = v.Awake.FirstOrDefault(c => c.PendingQuestion != null && v.HasFact(c.PendingQuestion));
        if (asker != null)
            return asker.Name + " is waiting for an answer.";
        // A condition matters only while the fault still waits on that person (nobody else has taken it on).
        var bargainer = v.Awake.FirstOrDefault(v.AwaitsWord);
        if (bargainer != null)
            return bargainer.Name + " wants your word before " + Voyage.StartsText(bargainer) + ".";
        var thawing = v.Crew.FirstOrDefault(c => c.State == CrewState.Thawing);
        var awake = v.Awake.ToList();
        // While the crew hold the cold-sleep bay, waking and sleeping are theirs: a line that would say to wake or sleep someone
        // says that instead (the clock and talk are left).
        const string bay = "The crew hold the cold-sleep bay. Talk, keep a promise, or advance the clock.";

        if (v.Phase == Phase.Alert)
        {
            var open = v.Current.Faults.Where(f => !f.Resolved).ToList();
            if (open.Count == 0)
                return "Nothing needs repair. Advance the clock.";
            if (awake.Count == 0 && thawing is null)
                return v.Lockouts.Bay ? bay : "Nobody is awake. Wake someone from the crew list.";
            var idle = awake.FirstOrDefault(c => c.WorkingOn is null);
            var unstaffed = open.Any(f => awake.Count(c => c.WorkingOn == f.Id) < f.People);
            if (idle != null && unstaffed)
                return "Select " + idle.Name + ", then Repair or Patch.";
            if (thawing != null)
                return thawing.Name + " thaws in " + Voyage.Hours(Math.Max(0, thawing.ThawDoneAt - v.Clock)) + ". Advance the clock.";
            return !unstaffed ? "Work is under way. Advance the clock." : v.Lockouts.Bay ? bay : "A fault has nobody on it. Wake someone, or advance the clock.";
        }

        if (awake.Count == 0 && thawing is null)
            return "Everyone is asleep. Drift to the next alert.";
        if (thawing != null)
            return thawing.Name + " thaws in " + Voyage.Hours(Math.Max(0, thawing.ThawDoneAt - v.Clock)) + ". Advance the clock.";
        // A pod refuser who was asked (by the ship, or by the crew holding the bay) stays up through the drift: say so only when
        // the drift would go now. One who can sleep but wasn't asked yet must be asked first (outside the bay).
        var refuser = awake.FirstOrDefault(c => c.RefusesSleep);
        if (refuser != null && awake.All(c => c.AskedToSleep))
            return refuser.Name + " will not go back into the pod. Talk, or drift without them.";
        if (refuser != null && !refuser.AskedToSleep && v.CanSleepNow(refuser) && !v.Lockouts.Bay)
            return refuser.Name + " will not go back into the pod. Ask them to sleep and drift without them, or talk.";
        var waiting = awake.FirstOrDefault(c => !v.CanSleepNow(c));
        if (waiting is null)
            return v.Lockouts.Bay ? bay : "Everyone awake can sleep. Ask them, then drift.";
        var days = Math.Ceiling((Tuning.MinAwakeHours - waiting.HoursAwake) / 24);
        return waiting.Name + " can sleep in " + N(days) + " days. Advance the clock, or talk.";
    }

    /// <summary>Who hears the next typed line.</summary>
    private List<CrewMember> Hearers(Voyage v) =>
        _target is { State: CrewState.Awake } one ? new List<CrewMember> { one } : v.Awake.ToList();

    // ---- Numbers that flash when they change ----

    private sealed class Seen
    {
        public double Value;
        public double Delta;
        public double At = double.NegativeInfinity;
    }

    private readonly Dictionary<string, Seen> _seen = new();

    /// <summary>
    /// Follows a shown number. When it changes, <paramref name="flash"/> runs from 1 to 0 and <paramref name="delta"/> holds
    /// the change ("-42") for a moment. While <paramref name="hold"/> is true a change is not taken up yet, and the value
    /// returned is the one from before it: a transition is still running, or the line that explains the change is not in the log.
    /// </summary>
    private double Track(string key, double value, bool hold, out float flash, out string delta)
    {
        if (!_seen.TryGetValue(key, out var seen))
        {
            seen = new Seen { Value = value };
            _seen[key] = seen;
        }
        else if (!hold && Math.Abs(value - seen.Value) >= 0.5)
        {
            seen.Delta = value - seen.Value;
            seen.Value = value;
            seen.At = _now;
        }
        flash = Motion.Still ? 0f : 1f - Motion.Progress(_now, seen.At, Motion.Flash);
        delta = !Motion.Still && _now - seen.At < Motion.DeltaShown ? (seen.Delta > 0 ? "+" : "") + N(seen.Delta) : "";
        return hold ? seen.Value : value;
    }

    /// <summary>A crew line that is still being worded: the change it explains is not shown before it.</summary>
    private static bool LinePending(Voyage v, CrewMember c)
    {
        for (var i = v.Log.Count - 1; i >= 0 && i >= v.Log.Count - 30; i--)
            if (v.Log[i].Pending && v.Log[i].CrewId == c.Id)
                return true;
        return false;
    }

    private static TrustBand BandOf(double trust) =>
        trust >= 60 ? TrustBand.Follows : trust >= 30 ? TrustBand.WantsReasons : trust >= 15 ? TrustBand.Refuses : TrustBand.Against;

    private static string Manner(TrustBand band) => band switch
    {
        TrustBand.Follows => "Follows a reasonable request without asking why.",
        TrustBand.WantsReasons => "Wants reasons, and checks what they can.",
        TrustBand.Refuses => "Refuses orders and acts on their own judgement.",
        _ => "Works against you.",
    };

    /// <summary>The header's numbers, right to left: label, shown text, whether it warns, the key and value that flash, and the tooltip.</summary>
    private List<(string Label, string Text, bool Warn, string Key, double Value, string Tip)> Stats(Voyage v) => new()
    {
        ("COURSE", v.NavLost ? "LOST" : "held", v.NavLost, "course", v.NavLost ? 1 : 0,
            "The course to the destination. With the course lost the ship never arrives."),
        ("POWER", v.PowerLow ? "LOW" : "normal", v.PowerLow, "power", v.PowerLow ? 1 : 0,
            "The reactor's output. On low power, people asleep in the pods are at risk."),
        ("POD WEAR", ShownPodWear(v).ToString(CultureInfo.InvariantCulture), false, "wear", v.PodWear,
            "Wear of the cold-sleep pods. Every thaw adds to it, and worn pods fail."),
        ("PARTS", ShownParts(v).ToString(CultureInfo.InvariantCulture), v.Parts == 0, "parts", v.Parts,
            "Spare parts. A repair needs one; with none left a fault can only be patched."),
        ("STORES", N(ShownStores(v)) + " d", v.Stores <= 0, "stores", v.Stores,
            "Food, in person-days. Each person awake eats one a day."),
    };

    /// <summary>How far the header's numbers reach left of their right edge (<see cref="DrawStats"/>).</summary>
    private float StatsWidth(Voyage v) =>
        Stats(v).Sum(s => _p.Small.MeasureString(s.Text).X + _p.Small.MeasureString(s.Label).X + 7 + 20) - 20;

    /// <summary>The ship's numbers in the header, each with its own colour, tooltip and change flash. Drawn from the right edge leftwards.</summary>
    private void DrawStats(Voyage v, int right)
    {
        var hold = _transition is not null;
        var x = (float)right;
        foreach (var s in Stats(v))
            Stat(s.Label, s.Text, s.Warn, s.Key, s.Value, s.Tip);
        return;

        void Stat(string label, string text, bool warn, string key, double value, string tip)
        {
            Track(key, value, hold, out var flash, out var delta);
            var textWidth = _p.Small.MeasureString(text).X;
            var labelWidth = _p.Small.MeasureString(label).X;
            x -= textWidth;
            var ink = warn ? Painter.Accent : Painter.Ink;
            _p.Text(_p.Small, text, x, 21, Color.Lerp(ink, Painter.Bright, flash));
            if (flash > 0f)
                _p.Fill(new Rectangle((int)x, 40, (int)textWidth, 2), Painter.Fade(Painter.Bright, flash));
            if (delta.Length > 0)
                _p.Text(_p.Small, delta, x, 38, Painter.Accent);
            x -= labelWidth + 7;
            _p.Text(_p.Small, label, x, 21, Painter.Dim);
            _p.Tip(new Rectangle((int)x - 4, 10, (int)(labelWidth + 7 + textWidth) + 8, 38), tip);
            x -= 20;
        }
    }

    // ---- Repair with nobody selected: a picker ----

    /// <summary>The picker that asks who a repair or patch goes to, and where it opens.</summary>
    private (Fault Fault, bool Patch, int X, int Y)? _picker;

    private void DrawPicker(Voyage v)
    {
        if (_picker is not { } pick)
            return;
        var awake = v.Awake.ToList();
        if (awake.Count == 0 || pick.Fault.Resolved)
            return; // closed in the next update (StepOverlays)
        const int width = 300;
        var height = 44 + awake.Count * 46 + 6;
        var panel = new Rectangle(pick.X, Math.Min(pick.Y, Painter.ScreenHeight - 16 - height), width, height);
        _p.Box(panel, Painter.Bright);
        _p.Text(_p.Small, (pick.Patch ? "PATCH" : "REPAIR") + ": WHO?", panel.X + 14, panel.Y + 12, Painter.Dim);
        var y = panel.Y + 40;
        var skill = pick.Fault.Skill;
        foreach (var c in awake)
        {
            var person = c;
            var fault = pick.Fault;
            var patch = pick.Patch;
            var label = c.Name.ToUpperInvariant() + "  " + skill.ToString().ToLowerInvariant() + " " + c.SkillOf(skill)
                        + (c.WorkingOn != null && c.WorkingOn != fault.Id ? ", busy" : "");
            if (_p.Button(new Rectangle(panel.X + 10, y, width - 20, 40), label, selected: c.SkillOf(skill) >= fault.Level,
                    tip: "This needs " + skill.ToString().ToLowerInvariant() + " " + fault.Level + "."))
            {
                Do(() => _picker = null);
                Act((patch ? "patch " : "repair ") + fault.Name + " by " + person.Name, () => v.Assign(person, fault, patch));
            }
            y += 46;
        }
        // A click anywhere else closes it.
        if (_p.Click && !_p.Over(panel))
            Do(() => _picker = null);
    }

    /// <summary>
    /// Closes what has lapsed, once per update and never in a draw: a picker whose fault is resolved or with nobody awake
    /// to pick, and a banner whose time is up.
    /// </summary>
    private void StepOverlays()
    {
        if (_picker is { } pick && (pick.Fault.Resolved || _voyage is null || !_voyage.Awake.Any()))
            _picker = null;
        if (_banner is not null && _bannerUntil <= _now)
            _banner = null;
    }

    // ---- What is under the mouse, for cross-highlighting ----

    /// <summary>The fault and the person the mouse was over in the last frame, wherever they were drawn (card, plan, pod).</summary>
    private string? _hotFault;
    private string? _hotCrew;
    private string? _hotFaultNext;
    private string? _hotCrewNext;

    /// <summary>The box around a set of points.</summary>
    private static Rectangle Bounds(IReadOnlyList<Vector2> points)
    {
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        return new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
    }
}
