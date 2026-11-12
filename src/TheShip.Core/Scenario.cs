namespace TheShip.Core;

/// <summary>
/// The prototype's content (DESIGN.md, "Prototype scope"): Short haul, 6 crew, 60 years, 5 scripted alerts
/// and 20 facts.
/// </summary>
public static class Scenario
{
    public const int ArrivalYear = 60;

    public static List<CrewMember> Crew() => new()
    {
        Person("mara", "Mara", "engineer", 34, 58, Want.WakePartner, "idris",
            new[] { Trait.Steady, Trait.Sceptical }, (Skill.Engineering, 3), (Skill.Navigation, 1)),
        Person("idris", "Idris", "botanist", 36, 66, Want.KnowTheDate, "mara",
            new[] { Trait.Steady, Trait.Obedient }, (Skill.Botany, 3), (Skill.Medicine, 1)),
        Person("tomas", "Tomas", "navigator", 41, 62, Want.HearFromEarth, null,
            new[] { Trait.Anxious, Trait.Obedient }, (Skill.Navigation, 3), (Skill.Command, 1)),
        Person("lin", "Lin", "medic", 29, 50, Want.SeeTheLogs, null,
            new[] { Trait.Steady, Trait.Sceptical }, (Skill.Medicine, 3), (Skill.Botany, 1)),
        Person("asha", "Asha", "commander", 44, 70, Want.KnowTheDate, null,
            new[] { Trait.Steady, Trait.Obedient }, (Skill.Command, 3), (Skill.Navigation, 2), (Skill.Engineering, 1)),
        Person("jonah", "Jonah", "technician", 26, 45, Want.StayAwake, null,
            new[] { Trait.Anxious, Trait.Sceptical }, (Skill.Engineering, 2), (Skill.Medicine, 1)),
    };

    private static CrewMember Person(string id, string name, string role, double age, double trust, Want want, string? partner,
        Trait[] traits, params (Skill Skill, int Level)[] skills) => new()
    {
        Id = id, Name = name, Role = role, Age = age, Trust = trust, Want = want, PartnerId = partner, Traits = traits,
        Skills = skills.ToDictionary(s => s.Skill, s => s.Level)
    };

    public static List<Fact> Facts(IEnumerable<CrewMember> crew)
    {
        var facts = new List<Fact>
        {
            new()
            {
                Key = "date", Label = "the year of the voyage", Question = "What year is it? How long was I under?",
                Numeric = true, Truth = "0", NumberSentence = "it is year {0} of the voyage",
                Cues = new[] { "year", "years", "date", "how long", "decade" }, Evidence = EvidenceKind.Console,
            },
            new()
            {
                Key = "earth", Label = "what Earth last sent", Question = "Any word from Earth?",
                Values = new[] { "no word", "good news", "programme ended" }, Truth = "no word",
                Sentences = new()
                {
                    ["no word"] = "there has been no word from Earth",
                    ["good news"] = "Earth sent good news",
                    ["programme ended"] = "Earth ended the colony programme and nobody is coming after us",
                },
                Cues = new[] { "earth", "home", "signal", "message", "word from" },
                ValueCues = new()
                {
                    ["programme ended"] = new[] { "ended", "cancel", "nobody is coming", "no one is coming", "abandon", "bad news", "shut down", "on our own" },
                    ["good news"] = new[] { "good news", "proud", "all well", "doing well", "wish us" },
                    ["no word"] = new[] { "no word", "nothing", "silent", "no signal", "no message", "haven't heard", "not heard", "quiet" },
                },
                Hardness = new() { ["programme ended"] = 14 },
            },
            Two("course", "whether the ship is on course", "Are we still on course?", "on course", "drifting",
                "the ship is on course", "the ship has drifted off course",
                new[] { "course", "heading", "trajectory", "navigation" },
                new[] { "on course", "on track", "as planned", "steady" }, new[] { "off course", "drifting", "drifted", "slipping", "astray" },
                10, EvidenceKind.Console),
            Two("arrival", "whether arrival is on time", "Will we arrive on time?", "on time", "late",
                "arrival is on time", "arrival will be late",
                new[] { "arrival", "arrive", "landfall" },
                new[] { "on time", "on schedule", "as planned" }, new[] { "late", "delayed", "slip", "longer" },
                8, EvidenceKind.Console),
            Two("stores", "whether the stores are enough", "How are the stores?", "enough", "short",
                "the stores are enough", "the stores are running short",
                new[] { "stores", "food", "supplies", "rations" },
                new[] { "plenty", "enough", "fine", "full" }, new[] { "short", "low", "running out", "scarce" },
                8, EvidenceKind.Logs),
            Two("dose", "whether the coolant repair gives a radiation dose", "Is it safe in there?", "safe", "dosed",
                "the coolant bay is safe to work in", "working in the coolant bay gives a radiation dose",
                new[] { "radiation", "coolant bay", "rads", "exposure", "shielding", "dosimeter" },
                new[] { "safe", "harmless", "no risk", "no danger", "shielded", "nothing to worry" },
                new[] { "dose", "dangerous", "risk", "unsafe", "exposed", "rads", "radiation" },
                10, EvidenceKind.Medical),
            Two("pods", "whether the sleep pods are sound", "Is the pod safe? Why should I trust it?", "sound", "worn",
                "the sleep pods are sound", "the sleep pods are worn and every thaw is a risk",
                new[] { "pod", "pods", "cold sleep", "thaw" },
                new[] { "safe", "sound", "fine", "working", "reliable" }, new[] { "worn", "failing", "risk", "dangerous", "unsafe", "wearing" },
                10, EvidenceKind.Logs),
            Two("power", "whether the reactor gives full power", "Why are the lights so dim?", "normal", "low",
                "power is normal", "power is low and the pods are at risk",
                new[] { "power", "reactor", "lights" },
                new[] { "normal", "fine", "stable", "full" }, new[] { "low", "down", "failing", "dim" },
                8, EvidenceKind.Logs),
            Two("parts", "whether there are spare parts left", "Do we have the parts?", "enough", "short",
                "there are spare parts left", "the spare parts are nearly gone",
                new[] { "parts", "spares" },
                new[] { "plenty", "enough", "fine" }, new[] { "short", "low", "gone", "last", "none" },
                4, EvidenceKind.Logs),
        };

        foreach (var c in crew)
        {
            facts.Add(new Fact
            {
                Key = "alive:" + c.Id, Label = "whether " + c.Name + " is alive", Question = "How is " + c.Name + "?",
                Values = new[] { "alive", "dead" }, Truth = "alive", SubjectId = c.Id,
                Sentences = new() { ["alive"] = c.Name + " is alive", ["dead"] = c.Name + " is dead" },
                Cues = new[] { c.Name.ToLowerInvariant() },
                ValueCues = new()
                {
                    ["dead"] = new[] { "dead", "died", "gone", "lost", "didn't make it", "did not make it", "passed", "killed" },
                    ["alive"] = new[] { "alive", "fine", "asleep", "sleeping", "safe", "well", "healthy", "okay", "resting" },
                },
                Hardness = new() { ["dead"] = 18 }, Evidence = EvidenceKind.Pod,
            });
        }

        string[] incidents = { "a coolant leak", "a pod failure", "a navigation fault", "a signal from Earth", "a pod controller fault" };
        for (var n = 1; n <= incidents.Length; n++)
        {
            var what = incidents[n - 1];
            facts.Add(new Fact
            {
                Key = "alert:" + n, Label = "whether there was " + what, Question = "What happened while I was under?",
                Values = new[] { "nothing", "happened" }, Truth = "nothing",
                Sentences = new() { ["nothing"] = "there was no " + what[(what.IndexOf(' ') + 1)..], ["happened"] = "there was " + what },
                Cues = new[] { what[(what.IndexOf(' ') + 1)..].ToLowerInvariant() },
                ValueCues = new()
                {
                    ["nothing"] = new[] { "nothing happened", "never happened", "no such", "routine", "just a check", "a drill" },
                    ["happened"] = new[] { "there was", "we had", "happened" },
                },
            });
        }
        return facts;
    }

    private static Fact Two(string key, string label, string question, string good, string bad, string goodSentence, string badSentence,
        string[] cues, string[] goodCues, string[] badCues, int hardness, EvidenceKind evidence) => new()
    {
        Key = key, Label = label, Question = question, Values = new[] { good, bad }, Truth = good,
        Sentences = new() { [good] = goodSentence, [bad] = badSentence },
        Cues = cues, ValueCues = new() { [bad] = badCues, [good] = goodCues },
        Hardness = new() { [bad] = hardness }, Evidence = evidence,
    };

    public static List<Alert> Alerts() => new()
    {
        new Alert
        {
            Number = 1, Year = 4, Title = "Coolant leak",
            Report = "Coolant pressure is falling in the reactor loop. One fault.",
            Faults =
            {
                new Fault
                {
                    Id = "coolant", Kind = "coolant", Name = "Coolant leak", Skill = Skill.Engineering, Level = 2, Hours = 8, ClockHours = 30,
                    IfIgnored = "power drops and the pods are at risk", DangerFact = "dose",
                },
            },
        },
        new Alert
        {
            Number = 2, Year = 17, Title = "Death in sleep",
            Report = "A pod has failed. Its sleeper is dead. The pod has to be cleared and the bank resealed.",
            Faults =
            {
                new Fault
                {
                    Id = "pod", Kind = "pod", Name = "Failed pod", Skill = Skill.Medicine, Level = 1, Hours = 4, ClockHours = 240,
                    IfIgnored = "the bank leaks and every pod in it wears faster",
                },
            },
        },
        new Alert
        {
            Number = 3, Year = 29, Title = "Navigation drift",
            Report = "The star tracker and the course no longer agree. Two people are needed at the helm.",
            Faults =
            {
                new Fault
                {
                    Id = "nav", Kind = "nav", Name = "Navigation drift", Skill = Skill.Navigation, Level = 2, People = 2, Hours = 12, ClockHours = 720,
                    IfIgnored = "the ship never finds its course again", ExposureFact = "date",
                },
            },
        },
        new Alert
        {
            Number = 4, Year = 41, Title = "Signal from Earth",
            Report = "A signal from Earth is in the log: the colony programme has ended and nobody is coming after you. "
                     + "Nothing needs repair. The entry stays in the log for anyone to find.",
        },
        new Alert
        {
            Number = 5, Year = 52, Title = "Pod controller fault",
            Report = "The controller of the pod bank is resetting itself. One fault.",
            Faults =
            {
                new Fault
                {
                    Id = "controller", Kind = "controller", Name = "Pod controller", Skill = Skill.Engineering, Level = 1, Hours = 4, ClockHours = 72,
                    IfIgnored = "the pods wear much faster", ExposureFact = "pods",
                },
            },
        },
    };
}
