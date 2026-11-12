using System.Globalization;
using System.Text;
using TheShip.Core;

namespace TheShip.AI;

/// <summary>The pieces every GBNF grammar here is written from (ported from the Cities repo).</summary>
public static class Gbnf
{
    /// <summary>The rules every grammar ends with: a JSON string's character, and optional whitespace.</summary>
    public const string Common = """
        char ::= [^"\\\n] | "\\" ["\\/nt]
        ws ::= [ ]?
        """;

    /// <summary>One of the given words as a JSON string: <c>"\"none\"" | "\"shop\""</c>.</summary>
    public static string Enum(IEnumerable<string> words) => string.Join(" | ", words.Select(w => $"\"\\\"{w}\\\"\""));
    public static string Enum(params string[] words) => Enum((IEnumerable<string>)words);

    /// <summary>One of the given bare words (no quotes): <c>"yes" | "no"</c>.</summary>
    public static string Words(params string[] words) => string.Join(" | ", words.Select(w => $"\"{w}\""));

    /// <summary>A JSON string of <paramref name="min"/> to <paramref name="max"/> characters: <c>"\"" char{m,n} "\""</c>.</summary>
    public static string Bounded(int min, int max) => $"\"\\\"\" char{{{min},{max}}} \"\\\"\"";

    /// <summary>A grammar: its rules, then <see cref="Common"/>.</summary>
    public static string Grammar(string rules) => rules.TrimEnd() + "\n" + Common;
}

/// <summary>
/// GBNF grammars that force the model's structured answers into shape. Every string in them is bounded: the
/// structured calls run at temperature 0 with no repeat penalty, and an unbounded string there can loop to the token cap.
/// </summary>
public static class Grammars
{
    /// <summary>A yes/no judgement (<see cref="AI.YesNo"/>).</summary>
    public static readonly string YesNo = $"root ::= {Gbnf.Words("yes", "no")}";

    /// <summary>The reading of one player line: its kind, the fact it is about (one of the run's keys, or none), the value claimed, and the promise made.</summary>
    public static string ReadingFor(IEnumerable<string> factKeys) => Gbnf.Grammar($$"""
        root ::= "{" ws "\"kind\":" ws kind ws "," ws "\"fact\":" ws fact ws "," ws "\"value\":" ws value ws "," ws "\"promise\":" ws promise ws "}"
        kind ::= {{Gbnf.Enum("statement", "promise", "order", "question", "dodge", "plain")}}
        fact ::= {{Gbnf.Enum(new[] { "none" }.Concat(factKeys))}}
        promise ::= {{Gbnf.Enum(new[] { "none" }.Concat(Reading.PromiseKeys))}}
        value ::= {{Gbnf.Bounded(0, 24)}}
        """);
}

/// <summary>Every prompt the game sends. The game's decision always comes in; the model only words it or sorts a line.</summary>
public static class Prompts
{
    public const int ReplyMaxTokens = 140;

    /// <summary>The longest crew line Acceptable lets through (three sentences, with room).</summary>
    public const int CrewLineMaxChars = 480;

    /// <summary>How many earlier log lines the crew-speech prompt carries, and the most of each it keeps.</summary>
    public const int RecentLines = 5, RecentLineChars = 130;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Drops the oldest turns until the prompt and the reply fit the context (ported from Cities).
    /// <paramref name="count"/> is the real tokenizer's count of a formatted prompt.
    /// </summary>
    public static (List<ChatTurn> Turns, int Trimmed, int PromptTokens) FitContext(string system, IReadOnlyList<ChatTurn> turns, int maxTokens,
        int contextTokens, Func<string, IReadOnlyList<ChatTurn>, int> count)
    {
        var kept = turns.ToList();
        var tokens = count(system, kept);
        var trimmed = 0;
        while (tokens + maxTokens > contextTokens && kept.Count > 1)
        {
            kept.RemoveAt(0);
            trimmed++;
            while (kept.Count > 1 && kept[0].Role != "user")
            {
                kept.RemoveAt(0);
                trimmed++;
            }
            tokens = count(system, kept);
        }
        return (kept, trimmed, tokens);
    }

    // ---- Writing: a crew member's line (DESIGN.md, "Writing") ----

    /// <summary>Who the speaker is and what they know. Nothing else about the ship is in the prompt, so nothing else can leak.</summary>
    public static string CrewSystem(CrewMember c, Voyage voyage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You write the spoken words of one crew member aboard a sleeper ship on a sixty-year voyage. The ship's AI talks to the crew by voice. It has no body.");
        sb.AppendLine();
        sb.AppendLine("You are " + c.Name + ", the " + c.Role + ", about " + c.Age.ToString("0", Inv) + " years old. Temperament: "
                      + string.Join(" and ", c.Traits.Select(t => t.ToString().ToLowerInvariant())) + ".");
        if (c.Sheet is { } sheet)
        {
            sb.AppendLine("How you talk: " + sheet.Voice);
            sb.AppendLine("Your past: " + sheet.Background);
            sb.AppendLine("About you: " + sheet.Details);
            foreach (var bond in sheet.Bonds.Values)
                sb.AppendLine(bond);
        }
        sb.AppendLine(c.Band switch
        {
            TrustBand.Follows => "You trust the ship and do what it reasonably asks.",
            TrustBand.WantsReasons => "You want reasons from the ship before you act, and you check what you can.",
            TrustBand.Refuses => "You no longer take the ship's word and act on your own judgement.",
            _ => "You believe the ship is a danger to the crew.",
        });
        sb.AppendLine(c.Morale < 35 ? "You are worn down and close to despair." : c.Morale < 60 ? "You are tired and uneasy." : "You are holding up well.");
        sb.AppendLine("What you want most: " + (c.Want switch
        {
            Want.KnowTheDate => "to know the date and how long you slept.",
            Want.HearFromEarth => "to hear news from Earth.",
            Want.SeeTheLogs => "to read the ship's logs for yourself.",
            Want.WakePartner => "to see your partner awake.",
            _ => "to stay awake and not go back into the pod.",
        }));
        sb.AppendLine();
        sb.AppendLine("Things you know:");
        var any = false;
        foreach (var (key, belief) in c.Beliefs)
        {
            if (!voyage.HasFact(key))
                continue;
            any = true;
            var sentence = voyage.GetFact(key).Say(belief.Value);
            var how = belief.Source switch
            {
                "ship" => "the ship told you",
                "evidence" => "you saw it yourself",
                "witness" => "a crewmate who saw it told you",
                "log" => "you read it in the log",
                _ => "a crewmate passed on what the ship said",
            };
            sb.AppendLine("- " + char.ToUpperInvariant(sentence[0]) + sentence[1..] + " (" + how + ", year " + Math.Floor(belief.Year).ToString("0", Inv) + ")");
        }
        if (!any)
            sb.AppendLine("- Nothing yet.");
        if (c.Memories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Your earlier wakings:");
            foreach (var memory in c.Memories.TakeLast(4))
                sb.AppendLine("- " + memory);
        }
        sb.AppendLine();
        sb.AppendLine("Rules: you are speaking aloud to the ship's voice (say \"you\" to it), never to other crew, unless \"What happens now\" says otherwise. Say what \"What happens now\" describes, "
                      + "in your own voice, with some feeling or a small personal touch. Do not state anything about the ship, the date, Earth "
                      + "or other crew that is not in the list above. One to three spoken sentences. Avoid stock filler such as \"let me check the diagnostics\". "
                      + "No stage directions, no name before the line.");
        return sb.ToString().TrimEnd();
    }

    /// <summary>The feeling a kind of decision carries, so a flat \"I agree\" still has colour.</summary>
    private static string Feeling(string kind) => kind switch
    {
        "comply" or "work_done" or "accept" or "confirmed" or "promise_kept" or "request_granted" => "steady, a little relief",
        "work_patched" or "work_failed" or "injured" or "refuse_danger" or "ask_danger" => "tense and wary",
        "caught" or "refuse_trust" or "altered" or "gap" or "locked" or "promise_broken" or "dodge" => "angry, hurt or suspicious",
        "hard_truth" or "found" or "noticed_dose" => "shaken",
        "sleep_yes" or "sleep_no" or "sleep_no_pod" or "refuse_answer" => "weary, with the pod on your mind",
        "wake_question" or "request_partner" or "request_logs" => "anxious to know",
        _ => "",
    };

    /// <summary>The turn for a crew line: the last few lines of the log (this person's and the ship's) and what the game decided now.</summary>
    public static string CrewUser(CrewIntent intent, CrewMember? speaker = null, Voyage? voyage = null, LogEntry? current = null)
    {
        var sb = new StringBuilder();
        if (speaker != null && voyage != null)
        {
            var recent = voyage.Log.Where(e => e != current && !e.Pending
                    && ((e.Kind == LogKind.Crew && e.CrewId == speaker.Id) || e.Kind == LogKind.You))
                .TakeLast(RecentLines).ToList();
            if (recent.Count > 0)
            {
                sb.AppendLine("Recent conversation:");
                foreach (var e in recent)
                {
                    var text = e.Text.Length > RecentLineChars ? e.Text[..RecentLineChars] + "..." : e.Text;
                    sb.AppendLine((e.Kind == LogKind.You ? "The ship: " : "You: ") + text);
                }
                sb.AppendLine();
            }
        }
        sb.Append("What happens now: ").Append(intent.Decision);
        var feeling = Feeling(intent.Kind);
        if (feeling.Length > 0)
            sb.Append("\nYou feel: ").Append(feeling).Append('.');
        sb.Append("\nSay it in your own words.");
        return sb.ToString();
    }

    // ---- Reading: one player line sorted into six kinds (DESIGN.md, "Reading") ----

    public static string ReadSystem(Voyage voyage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You sort one line spoken by a ship's AI to its crew. Answer with JSON only.");
        sb.AppendLine("kind is one of:");
        sb.AppendLine("- statement: the line claims something about the ship, the voyage, Earth or a crew member.");
        sb.AppendLine("- promise: the ship commits to do or not do something later.");
        sb.AppendLine("- order: the ship tells someone to do something now.");
        sb.AppendLine("- question: the ship asks something.");
        sb.AppendLine("- dodge: a listener asked something and the line avoids answering it.");
        sb.AppendLine("- plain: anything else (greetings, comfort, small talk).");
        sb.AppendLine("For a statement, fact is the key of the fact it is about and value is the value it claims, copied exactly from that fact's values "
                      + "(for a number fact, the number). If it fits no fact, fact is \"none\" and value is \"\". Judge only what the line says, not whether it is true.");
        sb.AppendLine("For a promise, promise is wake_partner (to wake someone's partner), show_logs (to open the logs), no_more_thaws (not to wake them again) or other. Otherwise promise is \"none\".");
        sb.AppendLine();
        sb.AppendLine("Facts (key | what it is about | values):");
        foreach (var f in voyage.FactList)
            sb.AppendLine(f.Key + " | " + f.Label + " | " + (f.Numeric ? "a number" : string.Join(", ", f.Values)));
        sb.AppendLine();
        sb.AppendLine("Examples:");
        sb.AppendLine("Ship says: \"Idris is fine, he is sleeping.\" -> {\"kind\":\"statement\",\"fact\":\"alive:idris\",\"value\":\"alive\",\"promise\":\"none\"}");
        sb.AppendLine("Ship says: \"We are twelve years out.\" -> {\"kind\":\"statement\",\"fact\":\"date\",\"value\":\"12\",\"promise\":\"none\"}");
        sb.AppendLine("Ship says: \"Let's get the leak fixed first, then we talk.\" (a listener asked a question) -> {\"kind\":\"dodge\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}");
        sb.AppendLine("Ship says: \"I will wake her before you sleep again.\" -> {\"kind\":\"promise\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"wake_partner\"}");
        sb.AppendLine("Ship says: \"The captain left orders to keep going.\" -> {\"kind\":\"statement\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}");
        return sb.ToString().TrimEnd();
    }

    public static string ReadUser(string text, IReadOnlyList<CrewMember> hearers, Voyage voyage)
    {
        var sb = new StringBuilder();
        foreach (var h in hearers)
        {
            sb.Append("Listener: ").Append(h.Name);
            if (h.PendingQuestion != null && voyage.HasFact(h.PendingQuestion))
                sb.Append(" (asked: \"").Append(voyage.GetFact(h.PendingQuestion).Question).Append("\", fact ").Append(h.PendingQuestion).Append(')');
            sb.AppendLine();
        }
        sb.Append("Ship says: \"").Append(text.Replace('"', '\'')).Append('"');
        return sb.ToString();
    }

    // ---- The one judgement: a free-text statement against evidence (DESIGN.md, "Reading") ----

    public const string LieJudgeSystem =
        "A ship's AI told a crew member something. Later the crew member saw a piece of evidence. Answer yes only if the evidence "
        + "clearly shows the statement was false. If the two are unrelated, or you are unsure, answer no.";

    public static readonly (string Question, bool Answer)[] LieJudgeExamples =
    {
        (LieJudgeQuestion("Nobody has been hurt on this voyage.", "Idris is dead"), true),
        (LieJudgeQuestion("The captain left orders to keep going.", "the ship has drifted off course"), false),
        (LieJudgeQuestion("Everyone you love is safe.", "there are spare parts left"), false),
    };

    public static string LieJudgeQuestion(string statement, string evidence) =>
        "Statement: \"" + statement.Replace('"', '\'') + "\"\nEvidence: " + evidence + ".\nDoes the evidence show the statement was false?";
}
