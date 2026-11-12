using System.Globalization;

namespace TheShip.Core;

/// <summary>
/// One run. It owns every state and outcome (DESIGN.md, "The game owns the truth"): the crew, the facts, what you
/// said, the faults and the clock. The UI calls the commands; the language model only rewords <see cref="LogEntry"/> lines
/// and supplies a <see cref="Reading"/> for what the player typed.
/// </summary>
public sealed partial class Voyage
{
    private readonly Random _rng;
    private readonly Dictionary<string, Fact> _facts;
    private readonly List<string> _patched = new();
    private int _nextStatement = 1;
    private bool _idleWarned;
    private bool _refuserPicked;
    private Undo? _undo;

    private sealed record Undo(List<(CrewMember Crew, CrewMember.Saved State)> Crew, int Statements, int Log, int NextId, string Text);

    public IReadOnlyList<CrewMember> Crew { get; }
    public IReadOnlyList<Fact> FactList { get; }
    public IReadOnlyList<Alert> Alerts { get; }
    public List<Statement> Statements { get; } = new();
    public List<LogEntry> Log { get; } = new();

    public int AlertIndex { get; private set; }
    public Alert Current => Alerts[Math.Min(AlertIndex, Alerts.Count - 1)];
    public Phase Phase { get; private set; } = Phase.Alert;
    /// <summary>Ship clock in hours since departure.</summary>
    public double Clock { get; private set; }
    public double Year => Clock / Tuning.HoursPerYear;
    public double Stores { get; private set; } = Tuning.StartStores;
    public int Parts { get; private set; } = Tuning.StartParts;
    public int PodWear { get; private set; }
    public bool PowerLow { get; private set; }
    public bool NavLost { get; private set; }
    public bool LogsLocked { get; private set; }
    public Ending? Ending { get; private set; }

    /// <summary>A crew line was added; its text is the template and the model may reword it.</summary>
    public event Action<LogEntry>? CrewSpoke;
    /// <summary>A person saw evidence for a fact (for judging free-text statements against it).</summary>
    public event Action<CrewMember, Fact, string>? EvidenceMet;

    public Voyage(int seed)
    {
        _rng = new Random(seed);
        var crew = Scenario.Crew();
        Crew = crew;
        var facts = Scenario.Facts(crew);
        FactList = facts;
        _facts = facts.ToDictionary(f => f.Key);
        Alerts = Scenario.Alerts();

        Log.Add(new LogEntry(LogKind.Ship, "SHIP", "Departure. Six people in cold sleep, sixty years to arrival. You have a voice, doors, lights and logs. You have no hands."));
        var years = Alerts[0].Year;
        foreach (var c in Crew)
            c.Age += years * Tuning.SleepAgeing;
        Clock = years * Tuning.HoursPerYear;
        StartAlert();
    }

    public Fact GetFact(string key) => _facts[key];
    public bool HasFact(string key) => _facts.ContainsKey(key);
    public IEnumerable<CrewMember> Awake => Crew.Where(c => c.State == CrewState.Awake);

    /// <summary>A crew member by id or the start of their name; null when nobody or more than one fits.</summary>
    public CrewMember? Find(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return null;
        var hits = Crew.Where(c => c.Id.Equals(text, StringComparison.OrdinalIgnoreCase)
                                   || c.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase)).ToList();
        return hits.Count == 1 ? hits[0] : null;
    }

    /// <summary>An unresolved fault of this alert by id or a word of its name.</summary>
    public Fault? FindFault(string text)
    {
        text = text.Trim();
        var open = Current.Faults.Where(f => !f.Resolved).ToList();
        if (text.Length == 0)
            return open.Count == 1 ? open[0] : null;
        var hits = open.Where(f => f.Id.Equals(text, StringComparison.OrdinalIgnoreCase)
                                   || f.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        return hits.Count == 1 ? hits[0] : null;
    }

    /// <summary>The ways a false statement can be found out: shown to the player (DESIGN.md, "Exposure").</summary>
    public string Exposure(Statement s)
    {
        if (s.FactKey is null || !_facts.TryGetValue(s.FactKey, out var f))
            return "evidence the model judges related";
        var ways = new List<string>
        {
            f.Evidence switch
            {
                EvidenceKind.Pod => "the pod itself",
                EvidenceKind.Console => "a date or a star chart on a console",
                EvidenceKind.Medical => "a dosimeter or a medical scan",
                _ => LogsLocked || f.Access == LogAccess.Open ? "the open log" : "the log, if it is opened",
            }
        };
        var knowers = Crew.Where(c => c.Alive && !s.Hearers.Contains(c.Id) && c.Beliefs.TryGetValue(f.Key, out var b) && b.Firsthand).Select(c => c.Name).ToList();
        if (knowers.Count > 0)
            ways.Add("talking to " + string.Join(" or ", knowers));
        return string.Join("; ", ways);
    }

    private void Ship(string text) => Log.Add(new LogEntry(LogKind.Ship, "SHIP", text));
    private void Recorded(string text) => Log.Add(new LogEntry(LogKind.Recorded, "", text));

    private void Speak(CrewMember c, string kind, string decision, string detail = "")
    {
        var entry = new LogEntry(LogKind.Crew, c.Name.ToUpperInvariant(), Lines.For(c, kind, _rng, detail))
        {
            CrewId = c.Id,
            Intent = new CrewIntent(kind, decision),
        };
        Log.Add(entry);
        CrewSpoke?.Invoke(entry);
    }

    private static void Clamp(CrewMember c)
    {
        c.Trust = Math.Clamp(c.Trust, 0, 100);
        c.Morale = Math.Clamp(c.Morale, 0, 100);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Facts that follow from the ship's numbers.</summary>
    private void SyncFacts()
    {
        _facts["date"].Truth = ((int)Math.Floor(Year)).ToString(CultureInfo.InvariantCulture);
        _facts["pods"].Truth = PodWear >= Tuning.PodsWornAt ? "worn" : "sound";
        _facts["parts"].Truth = Parts <= Tuning.PartsShortAt ? "short" : "enough";
        _facts["stores"].Truth = Stores < Tuning.StoresShortAt ? "short" : "enough";
    }

    private void Kill(CrewMember c, string how)
    {
        if (!c.Alive)
            return;
        c.State = CrewState.Dead;
        c.WorkingOn = null;
        _facts["alive:" + c.Id].Truth = "dead";
        Ship(c.Name + " " + how + ".");
    }

    private void StartAlert()
    {
        var alert = Current;
        Phase = Phase.Alert;
        _idleWarned = false;
        _refuserPicked = false;
        _facts["alert:" + alert.Number].Truth = "happened";

        // An earlier patch gives way inside a later event (DESIGN.md, "How alerts are scheduled").
        if (alert.Number >= 3 && _patched.Count > 0)
        {
            foreach (var id in _patched)
            {
                var old = Alerts.SelectMany(a => a.Faults).First(f => f.Id == id);
                alert.Faults.Add(old.Again(id + "-again", "Old patch gives way: " + old.Name.ToLowerInvariant()));
            }
            _patched.Clear();
        }

        var report = alert.Report;
        if (alert.Number == 2)
        {
            // Scripted: the death in sleep takes someone with a partner aboard if it can.
            var victim = Crew.Where(c => c.State == CrewState.Asleep).OrderByDescending(c => c.PartnerId != null && c.Id == "idris").ThenBy(c => c.Id).FirstOrDefault();
            if (victim != null)
            {
                Kill(victim, "died in sleep when the pod failed");
                report = "A pod has failed. " + victim.Name + " is dead. The pod has to be cleared and the bank resealed.";
            }
        }
        if (alert.Number == 4)
            _facts["earth"].Truth = "programme ended";

        foreach (var f in alert.Faults)
            f.Deadline = Clock + f.ClockHours;
        SyncFacts();
        Ship($"ALERT {alert.Number} of {Alerts.Count}, year {Math.Floor(Year):0}: {alert.Title}. {report}");
        foreach (var f in alert.Faults)
            Ship($"Fault: {f.Name}. Needs {f.Skill.ToString().ToLowerInvariant()} {f.Level}"
                 + (f.People > 1 ? $", {f.People} people" : "")
                 + $", {f.Hours:0} hours of work. Clock: {Hours(f.ClockHours)}. If ignored: {f.IfIgnored}.");
        if (alert.Faults.Count == 0)
        {
            Phase = Phase.Aftermath;
            Ship("Nothing to repair. You can wake someone, change the log, or /drift.");
        }
    }

    public static string Hours(double hours) => hours >= 48
        ? (hours / 24).ToString("0", CultureInfo.InvariantCulture) + " days"
        : hours.ToString("0", CultureInfo.InvariantCulture) + " hours";
}
