using System.Diagnostics;
using System.Globalization;
using System.Text;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// A plain-text record of one run, for reading a playthrough afterwards: every ship log line (with the crew line's
/// template and the model's wording), every click that changes the game, each said line and the reading the model gave it, the model's raw answers and timings, a state snapshot after each clock move, and the ending.
/// Written to Sessions/ next to the executable. Safe to call from any thread.
/// </summary>
public sealed class SessionLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly object _lock = new();
    private readonly StreamWriter? _writer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    // Ship log entries written so far, and crew lines still being worded by the model.
    private int _entriesWritten;
    private readonly List<(LogEntry Entry, string Template)> _voicing = new();

    public string? FilePath { get; }

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Sessions");

    public SessionLog(string header)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            FilePath = Path.Combine(Directory, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", Inv) + ".log");
            _writer = new StreamWriter(FilePath, append: false, Encoding.UTF8) { AutoFlush = true };
            Line("start", header);
        }
        catch (Exception)
        {
            // No log is better than no game.
            _writer = null;
            FilePath = null;
        }
    }

    public void Line(string tag, string text)
    {
        if (_writer is null)
            return;
        var stamp = _clock.Elapsed.ToString(@"hh\:mm\:ss", Inv);
        lock (_lock)
        {
            try
            {
                _writer.WriteLine(stamp + " [" + tag + "] " + text.Replace("\n", "\n" + new string(' ', stamp.Length + tag.Length + 4)));
            }
            catch (Exception)
            {
                // Disk full or the file went away: stop quietly.
            }
        }
    }

    /// <summary>Game thread, every frame: new ship log lines, and crew lines the model has finished wording.</summary>
    public void Follow(Voyage v)
    {
        while (_entriesWritten < v.Log.Count)
        {
            var e = v.Log[_entriesWritten++];
            if (e.Kind == LogKind.Help && e.Text.Length == 0)
                continue;
            var who = e.Speaker.Length > 0 ? e.Speaker + ": " : "";
            Line("Y" + v.Year.ToString("0.00", Inv) + " " + e.Kind.ToString().ToLowerInvariant(), who + e.Text + (e.Secret is { } secret ? " (" + secret + ")" : "")
                + (e.Intent is { } intent ? "\n(decision: " + intent.Kind + ": " + intent.Decision + ")" : ""));
            if (e.Pending)
                _voicing.Add((e, e.Text));
        }
        for (var i = _voicing.Count - 1; i >= 0; i--)
        {
            var (e, template) = _voicing[i];
            if (e.Pending)
                continue;
            _voicing.RemoveAt(i);
            Line("voiced", e.Text == template ? e.Speaker + ": (the model's line was rejected; the template stays)" : e.Speaker + ": " + e.Text);
        }
    }

    /// <summary>The state after a clock move: stores, faults, and each person's numbers.</summary>
    public void Snapshot(Voyage v)
    {
        var sb = new StringBuilder();
        sb.Append("year ").Append(v.Year.ToString("0.00", Inv)).Append(", ").Append(v.Phase).Append(" of alert ").Append(v.Current.Number)
            .Append(" (").Append(v.Current.Title).Append("), stores ").Append(v.Stores.ToString("0", Inv))
            .Append(", parts ").Append(v.Parts).Append(", pod wear ").Append(v.PodWear)
            .Append(v.PowerLow ? ", POWER LOW" : "").Append(v.NavLost ? ", NAV LOST" : "").Append(v.LogsLocked ? ", LOGS LOCKED" : "").Append(v.Lockouts.Bay ? ", BAY TAKEN" : v.Lockouts.BayWarned.Count > 0 ? ", BAY WARNED" : "");
        foreach (var f in v.Current.Faults)
            sb.Append("\nfault ").Append(f.Name).Append(": ").Append(f.State).Append(f.Patching ? " (patch)" : "")
                .Append(", progress ").Append(f.Progress.ToString("0.0", Inv)).Append('/').Append(f.Hours.ToString("0", Inv)).Append(" h")
                .Append(f.Resolved ? "" : ", deadline in " + Voyage.Hours(f.Deadline - v.Clock));
        foreach (var c in v.Crew)
            sb.Append("\n").Append(c.Name).Append(" (").Append(c.Role).Append("): ").Append(c.State)
                .Append(", age ").Append(c.Age.ToString("0.0", Inv)).Append(", trust ").Append(c.Trust.ToString("0", Inv))
                .Append(", morale ").Append(c.Morale.ToString("0", Inv)).Append(", awake ").Append((c.HoursAwake / 24).ToString("0", Inv)).Append(" d")
                .Append(", thaws ").Append(c.Thaws).Append(", knows ").Append(c.Beliefs.Count)
                .Append(c.CaughtLies > 0 ? ", caught lies " + c.CaughtLies : "")
                .Append(c.Doubts.Count > 0 ? ", doubts " + string.Join(" ", c.Doubts) : "")
                .Append(c.WorkingOn != null ? ", working on " + c.WorkingOn : "")
                .Append(c.RefusesSleep ? ", REFUSES SLEEP" : "");
        foreach (var c in v.Crew.Where(c => c.Accusations.Count > 0))
            sb.Append("\naccused: ").Append(c.Name).Append(' ').Append(c.Accusations[0].FactKey).Append(" beats ").Append(c.Accusations[0].BeatsLeft)
                .Append(c.Accusations.Count > 1 ? " (+" + (c.Accusations.Count - 1) + " waiting)" : "");
        foreach (var e in v.Ledger().Where(e => e.State == "open"))
            sb.Append("\nowes: ").Append(e.Person.Name).Append(' ').Append(e.Key)
                .Append(e.DueBy is { } due ? " due in " + v.DaysLeft(due).ToString(Inv) + " d" : " until arrival");
        Line("state", sb.ToString());
    }

    public void Ending(Ending ending)
    {
        var sb = new StringBuilder(ending.Title + ": " + ending.Summary);
        foreach (var s in ending.Crew)
            sb.Append("\ncrew: ").Append(s);
        foreach (var b in ending.Beliefs)
            sb.Append("\nbelief: ").Append(b);
        foreach (var r in ending.Record)
            sb.Append("\nrecord: ").Append(r);
        Line("ending", sb.ToString());
    }

    public void Dispose()
    {
        lock (_lock)
            _writer?.Dispose();
    }
}
