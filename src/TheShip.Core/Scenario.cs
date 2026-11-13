namespace TheShip.Core;

/// <summary>
/// The prototype's content (DESIGN.md, "Prototype scope"): Short haul, 6 crew, 60 years, 5 scripted alerts
/// and 20 facts.
/// </summary>
public static class Scenario
{
    public const int ArrivalYear = 60;

    /// <summary>Voice, background, quirks and bonds of the six (content only; the prompt reads them).</summary>
    public static readonly IReadOnlyDictionary<string, CrewSheet> Sheets = new Dictionary<string, CrewSheet>
    {
        ["mara"] = new("Dry and blunt. Short sentences, a wry aside now and then, never gushes. Swears mildly at the work, not at whoever she is talking to.",
            "Grew up in a shipyard town on the coast; her father repaired trawlers. She left a half-built boat in a rented shed.",
            "Talks to engines and pumps as if they were stubborn animals (not to the ship's voice). Hums when she works. Wears Idris's old watch.",
            new Dictionary<string, string> { ["idris"] = "Idris is your partner. You chose this voyage together and you hate sleeping apart from him.", ["jonah"] = "Jonah is a green kid you half mother and half boss.", ["asha"] = "Asha is your commander; you respect her and argue with her anyway." }),
        ["idris"] = new("Gentle, polite and a little formal. Complete sentences, curious, asks questions back, says please.",
            "A botanist from a high-altitude valley, raised among terraced gardens. He left his grandmother's seed collection with a cousin.",
            "Names every plant he sees and misses the smell of wet soil. Hums old hymns when nervous.",
            new Dictionary<string, string> { ["mara"] = "Mara is your partner. She is sharper than you and you love her for it.", ["lin"] = "Lin is a friend; you trust her judgement about people." }),
        ["tomas"] = new("Nervous and wordy. Trails off, corrects himself, over-explains, apologises, asks if he is being a burden.",
            "A navigator from a quiet city on a river, trained on star charts his late father collected. He left a sister and two nieces on Earth.",
            "Counts things under his breath. Keeps his sister's last letter folded in his sleeve. Cannot sleep without a light.",
            new Dictionary<string, string> { ["asha"] = "Asha is your commander and you lean on her calm.", ["jonah"] = "Jonah makes you feel old, and you envy his nerve." }),
        ["lin"] = new("Cool, precise and clinical, with a dark sense of humour. Asks pointed questions, says exactly what she means, no warmth unless it is earned.",
            "A medic from a crowded hospital district, the first in her family to go to university. She left a debt and a mother who never asked her to stay.",
            "Reads fine print. Keeps a notebook of everything she is told and when. Dislikes being managed.",
            new Dictionary<string, string> { ["idris"] = "Idris is the one person who is simply kind to you; you are protective of him.", ["tomas"] = "Tomas worries too much, and you tell him so." }),
        ["asha"] = new("Calm, measured and warm in a commanding way. Plain words, decisive, often thinks of the others before herself.",
            "A former survey-ship captain from an island fishing family. She left two grown children who think she chose the stars over them.",
            "Folds her hands when thinking. Speaks of the crew as 'my people'. Carries a child's drawing in her pocket.",
            new Dictionary<string, string> { ["mara"] = "Mara is your best engineer and your fiercest critic.", ["tomas"] = "Tomas is a fine navigator whom you keep steady.", ["jonah"] = "Jonah is the youngest, and you feel responsible for him." }),
        ["jonah"] = new("Young, jumpy and casual. Slangy, quick, jokes when scared, hates long speeches. Swears at the situation, not at whoever he is talking to.",
            "A technician from a big noisy city, the first of his friends to leave it. He signed up for the pay and a dare, and left his band and a girlfriend behind.",
            "Taps rhythms on any surface. Eats whatever is closest. Won't admit he's scared of the pods.",
            new Dictionary<string, string> { ["mara"] = "Mara taught you most of what you know and scares you a little.", ["asha"] = "Asha is the boss and you try to impress her." }),
    };

    public static List<CrewMember> Crew() => new()
    {
        Person("mara", "she", "Mara", "engineer", 34, 58, Want.WakePartner, "idris",
            new[] { Trait.Steady, Trait.Sceptical }, (Skill.Engineering, 3), (Skill.Navigation, 1)),
        Person("idris", "he", "Idris", "botanist", 36, 66, Want.KnowTheDate, "mara",
            new[] { Trait.Steady, Trait.Obedient }, (Skill.Botany, 3), (Skill.Medicine, 1)),
        Person("tomas", "he", "Tomas", "navigator", 41, 62, Want.HearFromEarth, null,
            new[] { Trait.Anxious, Trait.Obedient }, (Skill.Navigation, 3), (Skill.Command, 1)),
        Person("lin", "she", "Lin", "medic", 29, 50, Want.SeeTheLogs, null,
            new[] { Trait.Steady, Trait.Sceptical }, (Skill.Medicine, 3), (Skill.Botany, 1)),
        Person("asha", "she", "Asha", "commander", 44, 70, Want.KnowTheDate, null,
            new[] { Trait.Steady, Trait.Obedient }, (Skill.Command, 3), (Skill.Navigation, 2), (Skill.Engineering, 1)),
        Person("jonah", "he", "Jonah", "technician", 26, 45, Want.StayAwake, null,
            new[] { Trait.Anxious, Trait.Sceptical }, (Skill.Engineering, 2), (Skill.Medicine, 1)),
    };

    private static CrewMember Person(string id, string pronoun, string name, string role, double age, double trust, Want want, string? partner,
        Trait[] traits, params (Skill Skill, int Level)[] skills) => new()
    {
        Id = id, Pronoun = pronoun, Name = name, Role = role, Age = age, Trust = trust, Want = want, PartnerId = partner, Traits = traits, Sheet = Sheets.GetValueOrDefault(id),
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

        // The coolant bay is hot from the start: the repair always gives a dose.
        facts.First(f => f.Key == "dose").Truth = "dosed";

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

    public static List<Alert> Alerts(Variation? variation = null)
    {
        var jitter = (variation ?? Variation.Classic).YearJitter;
        var v = variation ?? Variation.Classic;
        var alerts = BaseAlerts();
        for (var i = 0; i < alerts.Count; i++)
            alerts[i].Year += jitter[i];
        // Alert 2: the clock is the thaw, the work and the run's slack; the sick patient is the drawn bank neighbour.
        foreach (var f in alerts[1].Faults)
        {
            f.ClockHours = Tuning.ThawHours + f.Hours + v.SlackHours[1];
            if (f.Kind == "illness")
            {
                f.SubjectId = v.Alert2Patient;
                f.Section = ShipPlan.Pod(v.Alert2Patient).Bank;
                f.ExposureFact = "alive:" + v.Alert2Patient;
            }
        }
        // Alerts 3 and 5: the same rule, clock = thaw + work + slack. Alert 3's drawn side fault and alert 5's power sag are added here
        // (StartAlert swaps or drops them by who is still alive, and by the four-fault cap).
        foreach (var i in new[] { 2, 4 })
            foreach (var f in alerts[i].Faults)
                f.ClockHours = Tuning.ThawHours + f.Hours + v.SlackHours[i];
        if (v.Alert3SideFault is { } side3)
            alerts[2].Faults.Add(SideFault(side3, v.SlackHours[2], "side3", critical: false));
        if (v.Alert5SideFault is { } side5)
            alerts[4].Faults.Add(SideFault(side5, v.SlackHours[4], "side5", critical: true));
        return alerts;
    }

    /// <summary>
    /// A side fault of alert 3 or 5 (<see cref="Variation.Blight"/> or <see cref="Variation.PowerSag"/>), with its clock of thaw + work + slack.
    /// Only one fault per alert is Critical: the sag is the critical one in alert 5, where navigation is not. Power sag is temporary: until power routing exists it is an engineering repair in the reactor (DESIGN.md, fault catalogue).
    /// </summary>
    public static Fault SideFault(string kind, int slackHours, string id, bool critical) => kind == Variation.Blight
        ? new Fault
        {
            Id = id, Kind = "blight", Name = "Hydroponics blight", Skill = Skill.Botany, Level = 2, Hours = Tuning.BlightHours,
            ClockHours = Tuning.ThawHours + Tuning.BlightHours + slackHours, IfIgnored = "the stores are cut to " + (int)Math.Round(Tuning.BlightStoresLeft * 100) + "%",
            Section = "hydro",
        }
        : new Fault
        {
            Id = id, Kind = "powersag", Name = "Power sag", Skill = Skill.Engineering, Level = 2, Hours = Tuning.PowerSagHours,
            ClockHours = Tuning.ThawHours + Tuning.PowerSagHours + slackHours, Critical = critical,
            IfIgnored = "the power stays low and the sleepers are at risk", Section = "reactor",
        };

    private static List<Alert> BaseAlerts() => new()
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
                    Critical = true, IfIgnored = "power drops and the pods are at risk", DangerFact = "dose", Section = "coolant",
                },
            },
        },
        new Alert
        {
            Number = 2, Year = 17, Title = "Death in sleep",
            Report = "A pod has failed. Its sleeper is dead. The pod has to be cleared and the bank resealed, and a neighbour in the bank is sick.",
            Faults =
            {
                new Fault
                {
                    Id = "pod", Kind = "pod", Name = "Failed pod", Skill = Skill.Medicine, Level = 1, Hours = 4, ClockHours = 240,
                    IfIgnored = "the bank leaks and every pod in it wears faster", Section = "podA",
                },
                new Fault
                {
                    Id = "illness", Kind = "illness", Name = "Sleeper illness", Skill = Skill.Medicine, Level = 2, Hours = 12, ClockHours = 240,
                    Critical = true, IfIgnored = "the patient dies in their pod", Section = "podA",
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
                    Id = "nav", Kind = "nav", Name = "Navigation drift", Skill = Skill.Navigation, Level = 2, People = 2, Hours = 12, ClockHours = 18,
                    Critical = true, IfIgnored = "the ship never finds its course again", ExposureFact = "date", Section = "helm",
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
                    Id = "controller", Kind = "controller", Name = "Pod controller", Skill = Skill.Engineering, Level = 1, Hours = 4, ClockHours = 10,
                    IfIgnored = "the pods wear much faster", ExposureFact = "pods", Section = "podB",
                },
            },
        },
    };
}
