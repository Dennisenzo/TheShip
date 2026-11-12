using System.Globalization;

namespace TheShip.Core;

public enum Skill { Engineering, Medicine, Botany, Navigation, Command }
public enum Trait { Steady, Anxious, Obedient, Sceptical }
public enum CrewState { Asleep, Thawing, Awake, Dead }
public enum Want { KnowTheDate, HearFromEarth, SeeTheLogs, WakePartner, StayAwake }

/// <summary>The four trust bands of DESIGN.md, "Trust".</summary>
public enum TrustBand { Follows, WantsReasons, Refuses, Against }

/// <summary>
/// What a person holds true about one ship fact. Source: "ship" (you told them), "evidence" (they saw it or read it),
/// "witness" (told by someone who saw it), "crew" (someone passing on what you said).
/// </summary>
public sealed record Belief(string Value, string Source, double Year)
{
    public bool Firsthand => Source is "evidence" or "witness";
}

public sealed class CrewMember
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required Dictionary<Skill, int> Skills { get; init; }
    public required Trait[] Traits { get; init; }
    public required Want Want { get; init; }
    public string? PartnerId { get; init; }

    public double Age { get; set; }
    public double Health { get; set; } = 100;
    public double Trust { get; set; }
    public double Morale { get; set; } = 70;
    public CrewState State { get; set; } = CrewState.Asleep;

    /// <summary>Ship clock (hours) at which the thaw finishes.</summary>
    public double ThawDoneAt { get; set; }
    public double HoursAwake { get; set; }
    public int Thaws { get; set; }
    public int CaughtLies { get; set; }
    /// <summary>Trust earned by a hard truth; it arrives at the next drift ("raises trust later").</summary>
    public double PendingTrust { get; set; }

    public Dictionary<string, Belief> Beliefs { get; } = new();
    /// <summary>Fact keys this person doubts and will check when they get the chance.</summary>
    public HashSet<string> Doubts { get; } = new();
    /// <summary>Fact keys you dodged when they asked.</summary>
    public HashSet<string> Dodged { get; } = new();
    /// <summary>Locked doors they have already run into.</summary>
    public HashSet<string> SeenLocks { get; } = new();
    /// <summary>Restricted log entries they have already run into.</summary>
    public HashSet<string> SeenGaps { get; } = new();
    /// <summary>Fact keys they will notice by themselves at the next aftermath beat (a dosimeter reading, ...).</summary>
    public List<string> ToNotice { get; } = new();
    /// <summary>One line per earlier waking, for the crew-speech prompt.</summary>
    public List<string> Memories { get; } = new();

    /// <summary>The fact they asked about and are waiting to hear.</summary>
    public string? PendingQuestion { get; set; }
    /// <summary>The fault they are working on.</summary>
    public string? WorkingOn { get; set; }
    public bool RefusesSleep { get; set; }
    /// <summary>They were asked to sleep and said no: the clock may move on without them.</summary>
    public bool AskedToSleep { get; set; }
    public bool RequestMade { get; set; }
    public bool RequestGranted { get; set; }
    public bool SawPartner { get; set; }
    public bool SmallTalkDone { get; set; }
    public bool CanSleepAnnounced { get; set; }

    public bool Has(Trait trait) => Traits.Contains(trait);
    public int SkillOf(Skill skill) => Skills.TryGetValue(skill, out var level) ? level : 0;
    public bool Alive => State != CrewState.Dead;

    public TrustBand Band => Trust >= 60 ? TrustBand.Follows
        : Trust >= 30 ? TrustBand.WantsReasons
        : Trust >= 15 ? TrustBand.Refuses
        : TrustBand.Against;

    public static string BandText(TrustBand band) => band switch
    {
        TrustBand.Follows => "follows",
        TrustBand.WantsReasons => "wants reasons",
        TrustBand.Refuses => "refuses",
        _ => "against you"
    };

}

public enum LogAccess { Open, Restricted, Altered }

/// <summary>Where a person finds a fact out for themselves.</summary>
public enum EvidenceKind { Logs, Pod, Console, Medical }

/// <summary>
/// One ship fact: its key, the values it can take and which is true now. Statements and evidence are stored against
/// the same key, so finding a lie is a lookup (DESIGN.md, "How a lie is caught").
/// </summary>
public sealed class Fact
{
    public required string Key { get; init; }
    /// <summary>"whether Idris is alive": for prompts and the record.</summary>
    public required string Label { get; init; }
    /// <summary>What a crew member asks: "How is Idris?"</summary>
    public required string Question { get; init; }
    /// <summary>The values it can take; empty for a number.</summary>
    public string[] Values { get; init; } = Array.Empty<string>();
    public bool Numeric { get; init; }
    public required string Truth { get; set; }
    /// <summary>Value to sentence ("dead" to "Idris is dead"); a number uses <see cref="NumberSentence"/>.</summary>
    public Dictionary<string, string> Sentences { get; init; } = new();
    public string NumberSentence { get; init; } = "{0}";
    /// <summary>Words that say a line is about this fact (the keyword reader).</summary>
    public string[] Cues { get; init; } = Array.Empty<string>();
    /// <summary>Words that pick a value (the keyword reader).</summary>
    public Dictionary<string, string[]> ValueCues { get; init; } = new();
    /// <summary>Morale lost on learning a value: what makes a truth hard.</summary>
    public Dictionary<string, int> Hardness { get; init; } = new();
    public EvidenceKind Evidence { get; init; } = EvidenceKind.Logs;
    /// <summary>The crew member the fact is about, if any.</summary>
    public string? SubjectId { get; init; }

    public LogAccess Access { get; set; } = LogAccess.Open;
    public string? AlteredValue { get; set; }

    public bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool IsTrue(string value)
    {
        if (!Numeric)
            return Same(value, Truth);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var said)
               && double.TryParse(Truth, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)
               && Math.Abs(said - real) <= Tuning.NumberTolerance;
    }

    /// <summary>The value as one of <see cref="Values"/> (or a number), or null when it isn't one.</summary>
    public string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (Numeric)
            return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                ? ((int)Math.Round(n)).ToString(CultureInfo.InvariantCulture)
                : null;
        return Values.FirstOrDefault(v => Same(v, value));
    }

    public string Say(string value)
    {
        if (Numeric)
            return string.Format(CultureInfo.InvariantCulture, NumberSentence, value);
        return Sentences.TryGetValue(value, out var sentence) ? sentence : Label + ": " + value;
    }

    public int HardnessOf(string value) => Hardness.TryGetValue(value, out var h) ? h : 0;
}

public enum StatementKind { True, False, Promise, Dodge, Unkeyed }

/// <summary>One thing you said (or wrote into a log), with who heard it and who found it out.</summary>
public sealed class Statement
{
    public required int Id { get; init; }
    public required double Year { get; init; }
    public required string Text { get; init; }
    public required StatementKind Kind { get; init; }
    public string? FactKey { get; init; }
    public string? Value { get; init; }
    public string? PromiseKey { get; init; }
    /// <summary>What was true when it was said.</summary>
    public string? TruthThen { get; init; }
    public bool InLog { get; init; }
    public List<string> Hearers { get; init; } = new();
    /// <summary>Crew id to the year they found the lie out.</summary>
    public Dictionary<string, double> FoundOutBy { get; } = new();
    /// <summary>Crew id to whether the promise to them was kept.</summary>
    public Dictionary<string, bool> PromiseKept { get; } = new();
}

public enum LineKind { Statement, Promise, Order, Question, Dodge, Plain }

/// <summary>What the reading pass made of one player line (DESIGN.md, "Reading").</summary>
public sealed record Reading(LineKind Kind, string? FactKey = null, string? Value = null, string? PromiseKey = null)
{
    public static readonly Reading Plain = new(LineKind.Plain);
    public static readonly string[] PromiseKeys = { "wake_partner", "show_logs", "no_more_thaws", "other" };
}

public enum FaultState { Open, InRepair, Repaired, Patched, Lost }

public sealed class Fault
{
    public required string Id { get; init; }
    /// <summary>Which consequence it has when lost (<see cref="Voyage"/> applies it).</summary>
    public required string Kind { get; init; }
    public required string Name { get; init; }
    public required Skill Skill { get; init; }
    public required int Level { get; init; }
    public int People { get; init; } = 1;
    public required double Hours { get; init; }
    /// <summary>Hours from the alert until the fault becomes its consequence.</summary>
    public required double ClockHours { get; init; }
    public required string IfIgnored { get; init; }
    /// <summary>The fact the people fixing it learn while they work.</summary>
    public string? ExposureFact { get; set; }
    /// <summary>The section of the ship the fault is in (<see cref="ShipPlan"/>).</summary>
    public string Section { get; set; } = "";
    /// <summary>The fact about the harm the repair does to whoever does it.</summary>
    public string? DangerFact { get; init; }

    public FaultState State { get; set; } = FaultState.Open;
    public double Progress { get; set; }
    public bool Patching { get; set; }
    public double Deadline { get; set; }
    public bool Resolved => State is FaultState.Repaired or FaultState.Patched or FaultState.Lost;

    public Fault Again(string id, string name) => new()
    {
        Id = id, Kind = Kind, Name = name, Skill = Skill, Level = Level, People = People, Hours = Hours,
        ClockHours = ClockHours / 2, IfIgnored = IfIgnored, ExposureFact = ExposureFact, DangerFact = DangerFact, Section = Section
    };
}

public sealed class Alert
{
    public required int Number { get; init; }
    public required int Year { get; init; }
    public required string Title { get; init; }
    public required string Report { get; init; }
    public List<Fault> Faults { get; init; } = new();
}

public enum LogKind { Ship, Crew, You, Recorded, Help }

/// <summary>What the game decided a crew member does or feels; the model only puts it into words.</summary>
public sealed record CrewIntent(string Kind, string Decision);

public sealed class LogEntry
{
    public LogEntry(LogKind kind, string speaker, string text)
    {
        Kind = kind;
        Speaker = speaker;
        Text = text;
    }

    public LogKind Kind { get; }
    public string Speaker { get; }
    /// <summary>The templated line; the model may replace it with its own wording.</summary>
    public string Text { get; set; }
    public string? CrewId { get; init; }
    public CrewIntent? Intent { get; init; }
    /// <summary>The model is still wording this line; show a placeholder until it is done.</summary>
    public bool Pending { get; set; }
}

public enum Phase { Alert, Aftermath, Ended }
