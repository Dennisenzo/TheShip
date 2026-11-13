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

    /// <summary>The sentence every civil tone carries: at Follows and WantsReasons a person never insults the ship.</summary>
    public const string CivilTone = "You never insult the ship or call it names.";

    /// <summary>How a person in this trust band speaks to the ship.</summary>
    public static string Attitude(TrustBand band) => band switch
    {
        TrustBand.Follows => "You trust the ship and do what it reasonably asks. You speak to it as to a trusted colleague: you may grumble about the work or the situation. " + CivilTone,
        TrustBand.WantsReasons => "You want reasons from the ship before you act, and you check what you can. You are guarded but civil with it and push back when something does not add up. " + CivilTone,
        TrustBand.Refuses => "You no longer take the ship's word and act on your own judgement. You are cold and short with it.",
        _ => "You believe the ship is a danger to the crew. You are openly hostile to it.",
    };

    /// <summary>The tone toward the ship, repeated next to the feeling in the user turn.</summary>
    private static string Tone(TrustBand band) => band switch
    {
        TrustBand.Follows => "civil, like a trusted colleague; grumbling is fine, insults are not",
        TrustBand.WantsReasons => "guarded but civil; push back, no insults",
        TrustBand.Refuses => "cold and short",
        _ => "openly hostile",
    };

    /// <summary>
    /// Who the speaker is, what they believe and how they feel about the ship. Facts come only from the speaker's own beliefs
    /// (never the truth), so a line can't state what they don't know; the user turn adds only lines they said or heard.
    /// </summary>
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
            foreach (var (id, bond) in sheet.Bonds)
                sb.AppendLine(Bond(c, voyage, id, bond));
        }
        sb.AppendLine(Attitude(c.Band));
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
                      + "or other crew that is not in the list above. Don't say or guess whether anyone is alive, dead, safe or hurt unless it is in your list; ask the ship instead. One to three spoken sentences. Avoid stock filler such as \"let me check the diagnostics\". "
                      + "No stage directions, no name before the line.");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// A bond as the speaker knows it: past tense once they believe that person is dead (from their own beliefs only, never
    /// the truth, so a death they have not heard of stays hidden).
    /// </summary>
    private static string Bond(CrewMember c, Voyage voyage, string id, string bond)
    {
        if (!(c.Beliefs.TryGetValue("alive:" + id, out var b) && b.Value == "dead"))
            return bond;
        var name = voyage.Find(id)?.Name ?? id;
        // The bond's first clause, put in the past: "Idris is your partner. You chose..." -> "Idris was your partner".
        var first = bond.Split(new[] { ". ", "; ", ", ", " and " }, StringSplitOptions.None)[0].TrimEnd('.');
        var past = first.StartsWith(name + " is ", StringComparison.Ordinal) ? first.Replace(" is ", " was ")
            : name + " was one of the people closest to you";
        return past + ". You know " + name + " is dead.";
    }

    /// <summary>The feeling a kind of decision carries, so a flat \"I agree\" still has colour. A trusting person is puzzled, not angry, at a dodge, a gap or a lock.</summary>
    private static string Feeling(string kind, TrustBand band) => kind switch
    {
        "dodge" or "gap" or "locked" when band is TrustBand.Follows => "puzzled and a little uneasy",
        "dodge" or "gap" or "locked" when band is TrustBand.WantsReasons => "uneasy, wanting a straight answer",
        "comply" or "bargain_struck" or "work_done" or "accept" or "confirmed" or "promise_kept" or "request_granted" => "steady, a little relief",
        "bargain" => "guarded, bargaining",
        "bargain_refused" => "cold and short",
        "work_patched" or "comply_pushed" or "work_failed" or "work_no_parts" or "injured" or "injured_warned" or "refuse_danger" or "ask_danger" => "tense and wary",
        "accusation_closed" or "caught" or "left_behind" or "takeover_warning" or "refuse_trust" or "altered" or "gap" or "locked" or "promise_broken" or "dodge" => "angry, hurt or suspicious",
        "corrected" => "guarded, relieved to be told, still stung that it was said wrong first",
        "accusation_admitted" => "guarded, grudgingly glad of the truth, not forgiving",
        "reunion" => "warm, relieved and steadier, glad to have your partner beside you",
        "bay_returned" => "guarded, but willing to trust the ship again",
        "hard_truth" or "found" or "noticed_dose" => "shaken",
        "witnessed" => "shaken, having just watched someone die",
        "ask_again" => "impatient, wanting an answer",
        "sleep_yes" or "sleep_no" or "sleep_no_pod" or "refuse_answer" => "weary, with the pod on your mind",
        "wake_question" or "request_partner" or "request_logs" => "anxious to know",
        _ => "",
    };

    /// <summary>
    /// The last few lines this person saw, oldest first: their own lines and the ship's lines they heard (awake and spoken to);
    /// lines said to others or while they slept are not theirs.
    /// </summary>
    public static List<LogEntry> Recent(CrewMember speaker, Voyage voyage, LogEntry? current = null) =>
        voyage.Log.Where(e => e != current && !e.Pending
                && ((e.Kind == LogKind.Crew && e.CrewId == speaker.Id) || (e.Kind == LogKind.You && e.Hearers?.Contains(speaker.Id) == true)))
            .TakeLast(RecentLines).ToList();

    /// <summary>The turn for a crew line: the last few lines of the log (this person's and the ship's) and what the game decided now.</summary>
    public static string CrewUser(CrewIntent intent, CrewMember? speaker = null, Voyage? voyage = null, LogEntry? current = null)
    {
        var sb = new StringBuilder();
        if (speaker != null && voyage != null)
        {
            var recent = Recent(speaker, voyage, current);
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
        var band = speaker?.Band ?? TrustBand.WantsReasons;
        var feeling = Feeling(intent.Kind, band);
        if (feeling.Length > 0)
            sb.Append("\nYou feel: ").Append(feeling).Append('.');
        if (speaker != null)
            sb.Append("\nYour tone toward the ship: ").Append(Tone(band)).Append('.');
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
            if (voyage.BargainOf(h) is { } bargain)
                sb.Append(" (wants the ship's word to: ").Append(bargain.Key).Append(')');
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
