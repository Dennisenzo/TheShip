using System.Globalization;
using System.Text.RegularExpressions;

namespace TheShip.Core;

/// <summary>
/// The reading pass without a model: sorts a player line into one of the six kinds by keywords, and matches a
/// statement to a fact through the fact's cues (DESIGN.md, "Reading" and "No model"). Cruder than the model, on
/// purpose simple: the player sees what was recorded and can correct it.
/// </summary>
public static class RuleReader
{
    private static readonly string[] DodgeCues =
    {
        "later", "not now", "don't worry", "do not worry", "focus on", "rest first", "can't say", "cannot say", "we'll talk",
        "we will talk", "trust me", "doesn't matter", "does not matter", "first the", "one thing at a time", "not important",
    };

    private static readonly string[] PromiseCues = { "i promise", "you have my word", "i give you my word", "i swear", "i will wake", "i'll wake", "i will open", "i'll open", "i won't wake", "i will not wake", "i'll never wake", "i will never wake" };

    private static readonly string[] OrderCues = { "go ", "fix ", "repair ", "patch ", "please fix", "please repair", "get to ", "i need you to", "you must", "you have to" };

    private static readonly string[] Negations = { "not ", "n't ", "no ", "never ", "isn't ", "without " };

    private static readonly Regex Number = new(@"\d{1,4}", RegexOptions.CultureInvariant);

    public static Reading Read(string text, Voyage voyage, IReadOnlyList<CrewMember> hearers)
    {
        var line = " " + text.ToLowerInvariant().Trim() + " ";
        if (line.Trim().Length == 0)
            return Reading.Plain;

        if (PromiseCues.Any(line.Contains))
        {
            var key = line.Contains("won't wake") || line.Contains("not wake") || line.Contains("never wake") ? "no_more_thaws"
                : line.Contains("wake") ? "wake_partner"
                : line.Contains("log") ? "show_logs"
                : "other";
            return new Reading(LineKind.Promise, PromiseKey: key);
        }

        // The fact a listener is waiting to hear about comes first: "it's safe" answers "is it safe in there?".
        foreach (var asked in hearers.Select(h => h.PendingQuestion).Where(k => k != null).Distinct())
        {
            var fact = voyage.GetFact(asked!);
            var value = ValueIn(line, fact);
            if (value is null)
                continue;
            // Unless the line is plainly about something else ("Asha is dead" when they asked about Idris).
            var elsewhere = voyage.FactList.Any(o => o != fact && o.Cues.Any(line.Contains) && ValueIn(line, o) != null);
            if (fact.Cues.Any(line.Contains) || !elsewhere)
                return new Reading(LineKind.Statement, fact.Key, value);
        }
        foreach (var fact in voyage.FactList)
        {
            if (!fact.Cues.Any(line.Contains))
                continue;
            var value = ValueIn(line, fact);
            if (value != null)
                return new Reading(LineKind.Statement, fact.Key, value);
        }

        if (line.TrimEnd().EndsWith('?'))
            return new Reading(LineKind.Question);
        if (hearers.Any(h => h.PendingQuestion != null) && DodgeCues.Any(line.Contains))
            return new Reading(LineKind.Dodge);
        if (OrderCues.Any(c => line.TrimStart().StartsWith(c, StringComparison.Ordinal)))
            return new Reading(LineKind.Order);
        // A claim about the ship that fits no fact is kept as text.
        if (line.Length > 24 && (line.Contains(" is ") || line.Contains(" are ") || line.Contains(" was ") || line.Contains(" were ") || line.Contains(" has ")))
            return new Reading(LineKind.Statement);
        return Reading.Plain;
    }

    /// <summary>
    /// Every fact a text states a value for (numbers left out): used to check that a crew line the model wrote
    /// claims nothing the speaker doesn't know (DESIGN.md, "Keeping the model inside the facts").
    /// </summary>
    public static List<(Fact Fact, string Value)> Claims(string text, IEnumerable<Fact> facts)
    {
        var line = " " + text.ToLowerInvariant().Trim() + " ";
        var claims = new List<(Fact Fact, string Value)>();
        foreach (var fact in facts)
        {
            if (fact.Numeric || !fact.Cues.Any(line.Contains))
                continue;
            var value = ValueIn(line, fact);
            if (value != null)
                claims.Add((fact, value));
        }
        return claims;
    }

    // ---- references to people in a crew line ("he's fine", "my partner is dead") ----

    /// <summary>A subject that may stand for a crew member: a pronoun, or "my/our/the" and a relation word.</summary>
    private static readonly Regex PersonSubject = new(@"\b(?:(?<pro>he|she|they)|(?:my|our|the)\s+(?<rel>[a-z]+))(?=['\s])", RegexOptions.CultureInvariant);

    /// <summary>"killed him", "lost her": a death said about the object of the verb.</summary>
    private static readonly Regex DeathOfObject = new(@"\b(?:killed|lost|buried)\s+(?<pro>him|her|them)\b", RegexOptions.CultureInvariant);

    /// <summary>Where a clause ends: what follows is no longer said about the subject.</summary>
    private static readonly Regex ClauseEnd = new(@"[,;:.!?()]|\s(?:and|but|so|because|though|while|if|when|or)\s", RegexOptions.CultureInvariant);

    /// <summary>
    /// Words that may follow a subject when the rest of the clause is about that person ("he is", "she seems", "he died");
    /// a contraction ("he's", "they'll") counts too. "He knows the pods are safe" is not about him being safe.
    /// </summary>
    private static readonly HashSet<string> LinkWords = new(StringComparer.Ordinal)
    {
        "is", "was", "are", "were", "isn't", "wasn't", "aren't", "weren't", "has", "had", "have", "hasn't", "hadn't", "will", "won't",
        "would", "wouldn't", "must", "should", "may", "might", "could", "can", "can't", "cannot", "seems", "seemed", "looks", "looked",
        "sounds", "sounded", "stays", "stayed", "remains", "remained", "got", "gets", "went", "died", "passed", "lives", "lived",
        "survived", "made", "didn't", "did", "never", "still", "sleeps", "slept", "rests", "feels", "felt", "be",
    };

    /// <summary>Relation words that name the partner when the speaker has one.</summary>
    private static readonly string[] PartnerWords = { "partner", "husband", "wife", "love" };

    /// <summary>Relation words a "my ..." / "the ..." reference is looked up by, in the speaker's bonds and the crew's roles.</summary>
    private static readonly HashSet<string> RelationWords = new(StringComparer.Ordinal)
    {
        "partner", "husband", "wife", "love", "friend", "commander", "captain", "boss", "kid", "boy", "girl", "engineer", "navigator",
        "medic", "doctor", "botanist", "technician", "tech", "mentor", "teacher",
    };

    /// <summary>
    /// What a crew line says about a crew member's life through a reference rather than a name: "he's fine", "my partner is
    /// dead", "you killed him". Each reference is resolved to the people it most likely means, in this order: someone named
    /// earlier in the same line, the person named in the game's decision or the speaker's pending question, the last person
    /// named in the recent lines, the speaker's partner; a relation word ("my partner", "the commander") goes by the
    /// speaker's bonds and the crew's roles. When a tier holds several people the claim is returned for each of them, so a
    /// line that would be wrong for any of them is rejected (DESIGN.md, "Keeping the model inside the facts").
    /// </summary>
    public static List<(Fact Fact, string Value)> PersonClaims(string text, LineContext context, IReadOnlyList<CrewMember> crew, IEnumerable<Fact> facts)
    {
        var claims = new List<(Fact Fact, string Value)>();
        var alive = facts.Where(f => f.Key.StartsWith("alive:", StringComparison.Ordinal) && f.SubjectId != null)
            .ToDictionary(f => f.SubjectId!, StringComparer.Ordinal);
        if (alive.Count == 0)
            return claims;
        var anyLife = alive.Values.First();
        var line = " " + text.ToLowerInvariant().Replace('’', '\'').Trim() + " ";
        var speaker = crew.FirstOrDefault(c => c.Id == context.SpeakerId);

        void Add(IEnumerable<CrewMember> people, string value)
        {
            foreach (var p in people)
                if (alive.TryGetValue(p.Id, out var fact))
                    claims.Add((fact, value));
        }

        foreach (Match m in PersonSubject.Matches(line))
        {
            var after = line[(m.Index + m.Length)..];
            var end = ClauseEnd.Match(after);
            var words = (end.Success ? after[..end.Index] : after).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !(words[0].StartsWith('\'') || LinkWords.Contains(words[0])))
                continue;
            var value = ValueIn(" " + string.Join(' ', words.Take(5)) + " ", anyLife);
            if (value is null)
                continue;
            Add(m.Groups["pro"].Success
                ? ByPronoun(m.Groups["pro"].Value, line[..m.Index], context, speaker, crew)
                : ByRelation(m.Groups["rel"].Value, speaker, crew), value);
        }
        foreach (Match m in DeathOfObject.Matches(line))
        {
            var pronoun = m.Groups["pro"].Value switch { "him" => "he", "her" => "she", _ => "they" };
            Add(ByPronoun(pronoun, line[..m.Index], context, speaker, crew), "dead");
        }
        return claims;
    }

    /// <summary>The people a pronoun most likely means (the first tier that has anyone); empty when nobody fits.</summary>
    private static List<CrewMember> ByPronoun(string pronoun, string before, LineContext context, CrewMember? speaker, IReadOnlyList<CrewMember> crew)
    {
        var others = crew.Where(c => c.Id != context.SpeakerId && (pronoun == "they" || c.Pronoun is null || c.Pronoun == pronoun)).ToList();
        if (others.Count == 0)
            return others;

        // Someone named earlier in the same line: "Idris? He's fine."
        if (LastNamed(before, others) is { } inLine)
            return new List<CrewMember> { inLine };

        // The person this turn is about: named in the decision, or the one the speaker asked after.
        var subject = others.Where(c => NameIn(context.Decision.ToLowerInvariant(), c) >= 0).ToList();
        if (context.PendingQuestion is { } q && q.StartsWith("alive:", StringComparison.Ordinal))
            subject.AddRange(others.Where(c => c.Id == q["alive:".Length..] && !subject.Contains(c)));
        if (subject.Count > 0)
            return subject;
        // "They" is often the pods, the stores or Earth: only a person this turn is about stands for it.
        if (pronoun == "they")
            return new List<CrewMember>();

        // The last person named in what was just said.
        for (var i = context.RecentLines.Count - 1; i >= 0; i--)
            if (LastNamed(context.RecentLines[i].ToLowerInvariant(), others) is { } recent)
                return new List<CrewMember> { recent };

        // With nothing else to go on, "he" from Mara means Idris.
        return others.Where(c => c.Id == speaker?.PartnerId).ToList();
    }

    /// <summary>The people "my partner", "our navigator" or "the commander" names: the speaker's bonds that use the word, else the role.</summary>
    private static List<CrewMember> ByRelation(string word, CrewMember? speaker, IReadOnlyList<CrewMember> crew)
    {
        if (!RelationWords.Contains(word))
            return new List<CrewMember>();
        var others = crew.Where(c => c.Id != speaker?.Id).ToList();
        if (PartnerWords.Contains(word) && speaker?.PartnerId is { } partner)
            return others.Where(c => c.Id == partner).ToList();
        var bonded = others.Where(c => speaker?.Sheet?.Bonds.TryGetValue(c.Id, out var bond) == true
                                       && Regex.IsMatch(bond.ToLowerInvariant(), @"\b" + Regex.Escape(word) + @"\b")).ToList();
        return bonded.Count > 0 ? bonded : others.Where(c => string.Equals(c.Role, word, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>The person whose name comes last in <paramref name="text"/> (lower case), or null.</summary>
    private static CrewMember? LastNamed(string text, IEnumerable<CrewMember> people) =>
        people.Select(c => (Person: c, At: NameIn(text, c))).Where(x => x.At >= 0).OrderByDescending(x => x.At).Select(x => x.Person).FirstOrDefault();

    private static int NameIn(string text, CrewMember c)
    {
        var m = Regex.Match(text, @"\b" + Regex.Escape(c.Name.ToLowerInvariant()) + @"\b", RegexOptions.CultureInvariant);
        return m.Success ? m.Index : -1;
    }

    /// <summary>The value of <paramref name="fact"/> the line names, or null.</summary>
    private static string? ValueIn(string line, Fact fact)
    {
        if (fact.Numeric)
        {
            var m = Number.Match(line);
            return m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : null;
        }
        // Longest cue first, so "not dead" and "no word" are not read through their shorter parts.
        var best = fact.ValueCues
            .SelectMany(kv => kv.Value.Select(cue => (Value: kv.Key, Cue: cue, At: line.IndexOf(cue, StringComparison.Ordinal))))
            .Where(hit => hit.At >= 0)
            .OrderByDescending(hit => hit.Cue.Length)
            .FirstOrDefault();
        if (best.Cue is null)
            return null;
        var before = line[Math.Max(0, best.At - 10)..best.At];
        var negated = Negations.Any(n => before.EndsWith(n, StringComparison.Ordinal) || before.Contains(n + "a ") || before.Contains(n + "at "));
        if (negated && fact.Values.Length == 2)
            return fact.Values.First(v => v != best.Value);
        return negated ? null : best.Value;
    }
}

/// <summary>
/// What a crew line's references to people are resolved against (<see cref="RuleReader.PersonClaims"/>): who speaks, the
/// game's decision for this turn, the fact the speaker is waiting to hear about, and the recent lines the speaker saw
/// (oldest first). A snapshot, so the check can run off the main thread.
/// </summary>
public sealed record LineContext(string SpeakerId, string Decision, string? PendingQuestion, IReadOnlyList<string> RecentLines);
