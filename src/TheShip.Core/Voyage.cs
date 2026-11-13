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

    public IReadOnlyList<CrewMember> Crew { get; }
    public IReadOnlyList<Fact> FactList { get; }
    public IReadOnlyList<Alert> Alerts { get; }
    public IReadOnlyList<Section> Sections { get; } = ShipPlan.Sections();
    public List<Statement> Statements { get; } = new();
    public List<LogEntry> Log { get; } = new();

    public int AlertIndex { get; private set; }
    public Alert Current => Alerts[Math.Min(AlertIndex, Alerts.Count - 1)];
    public Phase Phase { get; private set; } = Phase.Alert;
    /// <summary>Ship clock in hours since departure.</summary>
    public double Clock { get; private set; }
    public double Year => Clock / Tuning.HoursPerYear;
    public double Stores { get; internal set; } = Tuning.StartStores;
    public int Parts { get; private set; } = Tuning.StartParts;
    public int PodWear { get; internal set; }
    public bool PowerLow { get; private set; }
    public bool NavLost { get; private set; }
    public bool LogsLocked { get; private set; }
    public Ending? Ending { get; private set; }

    /// <summary>A crew line was added; its text is the template and the model may reword it.</summary>
    public event Action<LogEntry>? CrewSpoke;
    /// <summary>A person saw evidence for a fact (for judging free-text statements against it).</summary>
    public event Action<CrewMember, Fact, string>? EvidenceMet;

    public Variation Variation { get; }

    public Voyage(int seed) : this(seed, Variation.Classic) { }

    public Voyage(int seed, Variation variation)
    {
        Variation = variation;
        _rng = new Random(seed);
        var crew = Scenario.Crew();
        Crew = crew;
        var facts = Scenario.Facts(crew);
        FactList = facts;
        _facts = facts.ToDictionary(f => f.Key);
        Alerts = Scenario.Alerts(variation);

        Log.Add(new LogEntry(LogKind.Ship, "SHIP", "Departure. Six people in cold sleep, sixty years to arrival. You have a voice, doors, lights and logs. You have no hands."));
        var years = Alerts[0].Year;
        foreach (var c in Crew)
            c.Age += years * Tuning.SleepAgeing;
        Clock = years * Tuning.HoursPerYear;
        StartAlert();
    }

    public Fact GetFact(string key) => _facts[key];
    public bool HasFact(string key) => _facts.ContainsKey(key);
    public Section? GetSection(string id) => Sections.FirstOrDefault(s => s.Id == id);

    /// <summary>Where a person is on the plan: their pod bank, the section of the fault they work on, or the corridor.</summary>
    public string LocationOf(CrewMember c)
    {
        if (c.State != CrewState.Awake)
            return ShipPlan.Pod(c.Id).Bank;
        var fault = c.WorkingOn is null ? null : Current.Faults.FirstOrDefault(f => f.Id == c.WorkingOn);
        return fault is { Section.Length: > 0 } ? fault.Section : "corridor";
    }

    private bool Locked(string sectionId) => GetSection(sectionId) is { DoorState: DoorState.Locked };

    /// <summary>Open, close or lock a door. A locked door stops repairs behind it and is seen by whoever tries it.</summary>
    public void SetDoor(string sectionId, DoorState state)
    {
        if (Phase == Phase.Ended || GetSection(sectionId) is not { Door: not null } section)
            return;
        section.DoorState = state;
        Ship("The door to the " + section.Name.ToLowerInvariant() + " is " + state.ToString().ToLowerInvariant() + ".");
    }
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
    private void Recorded(string text, string? secret = null) => Log.Add(new LogEntry(LogKind.Recorded, "", text) { Secret = secret });

    /// <summary>Crew lines held back while a step that can kill runs (<see cref="HoldSpeech"/>), with their speakers.</summary>
    private List<(CrewMember Speaker, LogEntry Entry)>? _heldSpeech;

    private void Speak(CrewMember c, string kind, string decision, string detail = "")
    {
        // The dead say nothing.
        if (!c.Alive)
            return;
        var entry = new LogEntry(LogKind.Crew, c.Name.ToUpperInvariant(), Lines.For(c, kind, _rng, detail))
        {
            CrewId = c.Id,
            Intent = new CrewIntent(kind, decision),
        };
        if (_heldSpeech != null)
            _heldSpeech.Add((c, entry));
        else
            Emit(c, entry);
    }

    private void Emit(CrewMember c, LogEntry entry)
    {
        Log.Add(entry);
        Attend(c.Name + " has something to say");
        CrewSpoke?.Invoke(entry);
    }

    /// <summary>
    /// Holds crew lines back for a step in which several people can die (time passing, a drift): only those still alive
    /// after the whole step say what they said in it. Returns whether this call started the hold (and must release it).
    /// </summary>
    private bool HoldSpeech()
    {
        if (_heldSpeech != null)
            return false;
        _heldSpeech = new();
        return true;
    }

    /// <summary>Ends a hold: the lines of those still alive are said, in order; the lines of those who died in the step are dropped.</summary>
    private void ReleaseSpeech()
    {
        var held = _heldSpeech;
        _heldSpeech = null;
        if (held is null)
            return;
        foreach (var (c, entry) in held)
            if (c.Alive)
                Emit(c, entry);
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
        _facts["parts"].Sentences["short"] = Parts <= 0 ? "the spare parts are gone" : "the spare parts are nearly gone";
        _facts["stores"].Truth = Stores < Tuning.StoresShortAt ? "short" : "enough";
    }

    /// <summary>
    /// Someone dies. A death outside the pods (awake or coming round from a thaw) happens in front of everyone awake:
    /// they learn it as evidence, the way they learn who is up when someone is thawed (<see cref="Meet"/>).
    /// <paramref name="witnessesSilent"/>: the caller has the witnesses say it in its own line (<c>LeftBehind</c>).
    /// </summary>
    private void Kill(CrewMember c, string how, bool quiet = false, bool witnessesSilent = false)
    {
        if (!c.Alive)
            return;
        var seen = c.State is CrewState.Awake or CrewState.Thawing;
        c.State = CrewState.Dead;
        c.WorkingOn = null;
        _facts["alive:" + c.Id].Truth = "dead";
        Attend(c.Name + " died");
        if (!quiet)
            Ship(c.Name + " " + how + ".");
        // A quiet death is part of a drift report: the witnesses take it in silently, the report says it.
        if (seen)
            foreach (var w in Awake.ToList())
                Learn(w, "alive:" + c.Id, "dead", "evidence", silent: witnessesSilent || quiet, witnessed: c.Name);
        DropIllnessOf(c);
    }

    /// <summary>A sick person who has died needs no more treatment: their illness fault (and any patch of it that would return) goes.</summary>
    private void DropIllnessOf(CrewMember c)
    {
        // Only alerts that have started: a later alert's patient is still to be chosen.
        foreach (var alert in Alerts.Take(AlertIndex + 1))
            foreach (var f in alert.Faults.Where(f => f.Kind == "illness" && f.SubjectId == c.Id).ToList())
            {
                // History stays (repaired, patched, lost); only a patch that would return and work still open go.
                _patched.Remove(f.Id);
                if (f.Resolved)
                    continue;
                foreach (var w in Crew.Where(w => w.WorkingOn == f.Id))
                    w.WorkingOn = null;
                alert.Faults.Remove(f);
            }
        if (Phase == Phase.Alert)
            CheckAlertOver();
    }

    private string? _alert2Victim;

    /// <summary>The sleeper illness needs a living sleeper in the victim's bank: the drawn patient, else another one, else no illness.</summary>
    private void ChoosePatient(Alert alert, string? victimId)
    {
        var f = alert.Faults.FirstOrDefault(x => x.Kind == "illness");
        if (f is null)
            return;
        var bank = ShipPlan.Pod(victimId ?? Variation.Alert2Victim).Bank;
        bool Fits(CrewMember c) => c.State == CrewState.Asleep && c.Id != victimId && ShipPlan.Pod(c.Id).Bank == bank;
        var patient = Crew.FirstOrDefault(c => c.Id == Variation.Alert2Patient && Fits(c))
                      ?? Crew.Where(Fits).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (patient is null)
        {
            alert.Faults.Remove(f);
            return;
        }
        f.SubjectId = patient.Id;
        f.Section = bank;
        f.ExposureFact = "alive:" + patient.Id;
    }

    /// <summary>The fault's name as the log shows it: a sleeper illness names its patient.</summary>
    public string FaultTitle(Fault f) =>
        f.Kind == "illness" && f.SubjectId is { } sid && Crew.FirstOrDefault(c => c.Id == sid) is { } patient ? f.Name + ": " + patient.Name : f.Name;

    private static bool IsSide(Fault f) => f.Id is "side3" or "side5";

    /// <summary>The side fault needs a living crew member with the skill; if nobody has it the other kind is tried, and if neither, there is none.</summary>
    private void ChooseSideFault(Alert alert)
    {
        var f = alert.Faults.FirstOrDefault(IsSide);
        if (f is null)
            return;
        bool Doable(Fault x) => Crew.Any(c => c.Alive && c.SkillOf(x.Skill) >= x.Level);
        if (Doable(f))
            return;
        var slack = Variation.SlackHours[alert.Number - 1];
        var other = Scenario.SideFault(f.Kind == "blight" ? Variation.PowerSag : Variation.Blight, slack, f.Id);
        alert.Faults.Remove(f);
        if (Doable(other))
            alert.Faults.Add(other);
    }

    private void StartAlert()
    {
        var alert = Current;
        Phase = Phase.Alert;
        _idleWarned = false;
        _refuserPicked = false;
        _facts["alert:" + alert.Number].Truth = "happened";

        // An earlier patch gives way inside a later event (DESIGN.md, "How alerts are scheduled").
        if (alert.Number >= 2 && _patched.Count > 0)
        {
            // The alert holds four faults at most and the drawn side fault gives way to a returning patch; when more patches are due than
            // there is room for, the Critical ones come first, then the shortest clock, and the rest stay outstanding for the next alert
            // (or the last drift).
            var room = Tuning.MaxFaultsPerAlert - alert.Faults.Count(f => !IsSide(f));
            var due = _patched.Select(id => Alerts.SelectMany(a => a.Faults).First(f => f.Id == id))
                .OrderByDescending(f => f.Critical).ThenBy(f => f.ClockHours).Take(Math.Max(0, room)).ToList();
            foreach (var old in due)
            {
                // A returning patch keeps its Critical flag: it is the player's own shortcut coming due.
                alert.Faults.Add(old.Again(old.Id + "-again", old.Name, Variation.SlackHours[alert.Number - 1]));
                _patched.Remove(old.Id);
            }
        }

        if (alert.Number is 3 or 5)
            ChooseSideFault(alert);
        // At most four faults, returning patches first: the drawn side fault is the one dropped.
        while (alert.Faults.Count > Tuning.MaxFaultsPerAlert && alert.Faults.LastOrDefault(IsSide) is { } extra)
            alert.Faults.Remove(extra);

        var report = alert.Report;
        if (alert.Faults.FirstOrDefault(IsSide) is { } sideFault)
            report += sideFault.Kind == "blight" ? " The hydroponics crop is failing." : " The reactor output is sagging.";
        if (alert.Number == 2)
        {
            // Scripted: the death in sleep takes someone with a partner aboard if it can.
            var victim = Crew.FirstOrDefault(c => c.State == CrewState.Asleep && c.Id == Variation.Alert2Victim)
                         ?? Crew.Where(c => c.State == CrewState.Asleep).OrderBy(c => c.Id).FirstOrDefault();
            // The illness fault's patient is chosen after the victim is gone; until then it names nobody, so the victim's death cannot drop it.
            foreach (var ill in alert.Faults.Where(x => x.Kind == "illness"))
                ill.SubjectId = null;
            if (victim != null)
            {
                Kill(victim, "died in sleep when the pod failed");
                report = "A pod has failed. " + victim.Name + " is dead. The pod has to be cleared and the bank resealed.";
                // Whoever clears the pod sees who was in it.
                foreach (var f in alert.Faults.Where(f => f.Kind == "pod"))
                {
                    f.ExposureFact = "alive:" + victim.Id;
                    f.Section = ShipPlan.Pod(victim.Id).Bank;
                    f.SubjectId = victim.Id;
                }
                _alert2Victim = victim.Id;
            }
            ChoosePatient(alert, victim?.Id);
            if (alert.Faults.FirstOrDefault(x => x.Kind == "illness")?.SubjectId is { } sickId)
                report += " " + Crew.First(c => c.Id == sickId).Name + ", asleep in the same bank, is sick.";
        }
        if (alert.Number == 4)
            _facts["earth"].Truth = "programme ended";
        // An alert with no faults of its own (the signal) says what it holds: nothing, or the old patches giving way inside it.
        var returned = alert.Faults.Where(f => f.Name != f.PlainName).Select(f => f.PlainName).ToList();
        if (returned.Count == alert.Faults.Count)
            report += returned.Count switch
            {
                0 => " Nothing needs repair.",
                1 => " Nothing new needs repair, but an old patch gives way: " + returned[0] + ".",
                _ => " Nothing new needs repair, but old patches give way: " + string.Join(", ", returned.Take(returned.Count - 1)) + " and " + returned[^1] + ".",
            };

        foreach (var f in alert.Faults)
            f.Deadline = Clock + f.ClockHours;
        SyncFacts();
        Ship($"ALERT {alert.Number} of {Alerts.Count}, year {Math.Floor(Year):0}: {alert.Title}. {report}");
        foreach (var f in alert.Faults)
            Ship($"Fault: {FaultTitle(f)}. Needs {f.Skill.ToString().ToLowerInvariant()} {f.Level}"
                 + (f.People > 1 ? $", {f.People} people" : "")
                 + $", {f.Hours:0} hours of work. Clock: {Hours(f.ClockHours)}. If ignored: {f.IfIgnored}.");
        if (alert.Faults.Count == 0)
        {
            Phase = Phase.Aftermath;
            Ship("Nothing to repair. You can wake someone, change the log, or drift.");
        }
    }

    public static string Hours(double hours) => hours >= 48
        ? (hours / 24).ToString("0", CultureInfo.InvariantCulture) + " days"
        : hours.ToString("0", CultureInfo.InvariantCulture) + " hours";
}
