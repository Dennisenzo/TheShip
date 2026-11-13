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
            var elsewhere = voyage.FactList.Any(o => o != fact && Names(line, o) && ValueIn(line, o) != null);
            if (Names(line, fact) || !elsewhere)
                return new Reading(LineKind.Statement, fact.Key, value);
        }
        foreach (var fact in voyage.FactList)
        {
            if (!Names(line, fact))
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
        var claims = new List<(Fact Fact, string Value)>();
        var factList = facts.Where(f => !f.Numeric).ToList();
        // A guess, a fear, a condition or a counterfactual ("I think Idris is dead", "if he's dead", "that could have
        // killed him") counts as a claim too: every round of hedge rules let plain claims through, and a rejected line
        // only costs a retry and then the template line (DESIGN.md, "Keeping the model inside the facts").
        foreach (var sentence in SentenceSplit.Split(text.ToLowerInvariant().Replace('’', '\'')))
        {
            var line = " " + sentence.Trim() + " ";
            if (line.Trim().Length == 0)
                continue;
            // Every place a fact is named: "Idris is fine but Asha is dead".
            var hits = factList
                .SelectMany(f => f.Cues.SelectMany(c => Occurrences(line, c).Select(at => (Fact: f, At: at, Len: c.Length))))
                .OrderBy(h => h.At).ToList();
            if (hits.Count == 0)
                continue;
            var at = hits.Select(h => h.At).Distinct().ToList();
            // What follows each naming up to the next one, and whether it only joins it to the next ("Idris and Asha are dead").
            var after = at.Select((p, i) =>
            {
                var from = p + hits.Where(h => h.At == p).Max(h => h.Len);
                var to = i + 1 < at.Count ? at[i + 1] : line.Length;
                return from < to ? line[from..to] : "";
            }).ToList();
            var joined = at.Select((_, i) => i + 1 < at.Count && Joiner.IsMatch(after[i])).ToList();
            // Namings joined that way share the value said after the last of them; "neither ... nor" negates it.
            var groupOf = new int[at.Count];
            var groups = new List<(int Start, int End)>();
            for (var i = 0; i < at.Count; i++)
            {
                if (i > 0 && joined[i - 1])
                {
                    groupOf[i] = groupOf[i - 1];
                    continue;
                }
                var last = i;
                while (joined[last])
                    last++;
                var start = at[i];
                var lead = GroupLead.Match(line[..start]);
                if (lead.Success)
                    start = lead.Index;
                groupOf[i] = groups.Count;
                groups.Add((start, last + 1 < at.Count ? at[last + 1] : line.Length));
            }
            string Text(int g) => line[groups[g].Start..groups[g].End];

            foreach (var hit in hits)
            {
                var i = at.IndexOf(hit.At);
                var g = groupOf[i];
                var value = ValueIn(Text(g), hit.Fact);
                // A naming with no value of its own takes another's only through a true ellipsis: "Asha is dead and so is
                // Idris", "Asha is dead, Idris too", "Dead, Idris is". "Idris said Asha is dead" says nothing of Idris.
                if (value is null && !joined[i] && (i == 0 || !joined[i - 1]))
                {
                    if (g > 0 && (AlsoAfter.IsMatch(after[i]) || (SoBefore.IsMatch(line[..hit.At]) && CopulaOnly.IsMatch(after[i]))))
                        value = ValueIn(Text(g - 1), hit.Fact);
                    else if (g == 0 && CopulaOnly.IsMatch(after[i]))
                        value = ValueIn(line[..groups[0].Start], hit.Fact);
                }
                if (value != null && !claims.Contains((hit.Fact, value)))
                    claims.Add((hit.Fact, value));
            }
        }
        return claims;
    }

    /// <summary>What may stand between two namings that share the value after the last: "Idris, Asha and Lin are fine", "Idris or Asha", "Idris nor Asha".</summary>
    private static readonly Regex Joiner = new(@"^[\s,&]*(?:(?:and|or|nor)[\s,]*)?$", RegexOptions.CultureInvariant);

    /// <summary>A word in front of joined namings that belongs to them: "neither Idris nor Asha", "both Idris and Asha".</summary>
    private static readonly Regex GroupLead = new(@"\b(?:neither|either|both)\s+$", RegexOptions.CultureInvariant);

    /// <summary>"... and so is Idris", "... and neither was Asha": the naming repeats the value said before it.</summary>
    private static readonly Regex SoBefore = new(@"\b(?:so|neither|nor)\s+(?:is|was|are|were|has|had|did)\s+$", RegexOptions.CultureInvariant);

    /// <summary>"Idris too", "Idris is too", "Idris as well": the naming repeats the value said before it.</summary>
    private static readonly Regex AlsoAfter = new(@"^\s*(?:(?:is|was|are|were|has|had|did)\s+)?(?:too|also|as well)\b", RegexOptions.CultureInvariant);

    /// <summary>Nothing but a verb after the naming: "dead, Idris is", "and so is Idris".</summary>
    private static readonly Regex CopulaOnly = new(@"^\s*(?:(?:is|was|are|were|has|had|did)\b)?[\s.,!;:]*$", RegexOptions.CultureInvariant);

    /// <summary>Whether <paramref name="line"/> names <paramref name="fact"/> through one of its cues (as a word).</summary>
    private static bool Names(string line, Fact fact) => fact.Cues.Any(c => Occurrences(line, c).Any());

    /// <summary>
    /// Every place <paramref name="cue"/> stands as a word in <paramref name="line"/>: "lin" is not in "line" or "feeling",
    /// "dead" is not in "deadline", "safe" is not in "unsafe". A cue of four letters or more may take a plain ending
    /// ("abandon" in "abandoned", "thaw" in "thawed", "cancel" in "cancelled").
    /// </summary>
    private static IEnumerable<int> Occurrences(string line, string cue)
    {
        if (cue.Length == 0)
            yield break;
        for (var at = line.IndexOf(cue, StringComparison.Ordinal); at >= 0; at = line.IndexOf(cue, at + 1, StringComparison.Ordinal))
        {
            if (char.IsLetterOrDigit(cue[0]) && at > 0 && char.IsLetterOrDigit(line[at - 1]))
                continue;
            var end = at + cue.Length;
            if (!char.IsLetterOrDigit(cue[^1]) || end >= line.Length || !char.IsLetterOrDigit(line[end]))
            {
                yield return at;
                continue;
            }
            if (cue.Length >= 4 && char.IsLetter(cue[^1])
                && Endings.Any(e => string.CompareOrdinal(line, end, e, 0, e.Length) == 0
                                    && (end + e.Length >= line.Length || !char.IsLetterOrDigit(line[end + e.Length]))))
                yield return at;
        }
    }

    private static readonly string[] Endings = { "s", "es", "d", "ed", "ing", "led", "ling", "ped", "ping" };

    private static readonly Regex SentenceSplit = new(@"(?<=[.!?;])\s+", RegexOptions.CultureInvariant);

    // ---- references to people in a crew line ("he's fine", "my partner is dead") ----

    /// <summary>A subject that may stand for a crew member: a pronoun, or "my/our/the" and a relation word.</summary>
    private static readonly Regex PersonSubject = new(@"\b(?:(?<pro>he|she|they)|(?:my|our|the)\s+(?<rel>[a-z]+))(?=['\s])", RegexOptions.CultureInvariant);

    /// <summary>"killed him", "lost her": a death said about the object of the verb ("lost her trust" is told apart in code).</summary>
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
    public static List<(Fact Fact, string Value)> PersonClaims(string text, LineContext context, IReadOnlyList<CrewMember> crew, IEnumerable<Fact> facts) =>
        PersonReferences(text, context, crew, facts).Select(c => (c.Fact, c.Value)).ToList();

    /// <summary>
    /// <see cref="PersonClaims"/> with how sure the resolution is. A weak one (the pronoun went to the last person named in
    /// the recent lines) may well mean a machine ("she's holding" about the pump), so it only counts against the line when
    /// the speaker believes the opposite; a strong one (named in the line, the decision, the pending question, the partner,
    /// a relation word) must be known.
    /// </summary>
    public static List<PersonClaim> PersonReferences(string text, LineContext context, IReadOnlyList<CrewMember> crew, IEnumerable<Fact> facts)
    {
        var claims = new List<PersonClaim>();
        var alive = facts.Where(f => f.Key.StartsWith("alive:", StringComparison.Ordinal) && f.SubjectId != null)
            .ToDictionary(f => f.SubjectId!, StringComparer.Ordinal);
        if (alive.Count == 0)
            return claims;
        var anyLife = alive.Values.First();
        var line = " " + text.ToLowerInvariant().Replace('’', '\'').Trim() + " ";
        var speaker = crew.FirstOrDefault(c => c.Id == context.SpeakerId);

        void Add(IEnumerable<CrewMember> people, string value, bool weak)
        {
            foreach (var p in people)
                if (alive.TryGetValue(p.Id, out var fact))
                    claims.Add(new PersonClaim(fact, value, weak));
        }

        foreach (Match m in PersonSubject.Matches(line))
        {
            if (InQuestion(line, m.Index))
                continue;
            var after = line[(m.Index + m.Length)..];
            var end = ClauseEnd.Match(after);
            var words = (end.Success ? after[..end.Index] : after).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !(words[0].StartsWith('\'') || LinkWords.Contains(words[0])))
                continue;
            var said = " " + string.Join(' ', words.Take(5)) + " ";
            var value = ValueIn(said, anyLife);
            if (value is null)
                continue;
            // "If he's dead", "I think he's gone", "he might be dead" count too (see Claims).
            if (m.Groups["pro"].Success)
            {
                var (people, weak) = ByPronoun(m.Groups["pro"].Value, line[..m.Index], context, speaker, crew);
                Add(people, value, weak);
            }
            else
                Add(ByRelation(m.Groups["rel"].Value, speaker, crew), value, false);
        }
        foreach (Match m in DeathOfObject.Matches(line))
        {
            if (InQuestion(line, m.Index))
                continue;
            // "lost her trust": "her" is a possessive when a word other than a small linking word follows.
            if (m.Groups["pro"].Value == "her" && PossessiveNext(line[(m.Index + m.Length)..]))
                continue;
            var pronoun = m.Groups["pro"].Value switch { "him" => "he", "her" => "she", _ => "they" };
            var (people, weak) = ByPronoun(pronoun, line[..m.Index], context, speaker, crew);
            Add(people, "dead", weak);
        }
        return claims;
    }

    /// <summary>Whether the sentence around <paramref name="at"/> is a question ("He's fine, isn't he?"): it states nothing.</summary>
    private static bool InQuestion(string line, int at)
    {
        var end = line.IndexOfAny(new[] { '.', '!', '?' }, at);
        return end >= 0 && line[end] == '?';
    }

    /// <summary>Words that may follow an object pronoun ("lost her in the dark", "killed him too"); any other word makes "her" a possessive.</summary>
    private static readonly HashSet<string> AfterObject = new(StringComparer.Ordinal)
    {
        "in", "on", "at", "to", "with", "for", "from", "by", "into", "out", "off", "up", "down", "over", "under", "and", "but", "or", "so",
        "too", "as", "then", "now", "there", "here", "already", "again", "back", "all", "both", "when", "while", "because", "after",
        "before", "during", "just", "yesterday", "today", "last", "years", "months", "weeks", "days", "ago", "once", "twice", "myself",
        "yourself", "ourselves", "themselves", "himself", "herself", "instead", "anyway", "first", "finally", "either", "neither",
        "you", "i", "we", "he", "she", "they", "it", "that", "this", "the", "a", "an",
    };

    private static bool PossessiveNext(string after)
    {
        var m = Regex.Match(after, @"^\s+(?<w>[a-z']+)", RegexOptions.CultureInvariant);
        return m.Success && !AfterObject.Contains(m.Groups["w"].Value);
    }

    /// <summary>
    /// The people a pronoun most likely means (the first tier that has anyone); empty when nobody fits. Weak when it only
    /// comes from the recent lines.
    /// </summary>
    private static (List<CrewMember> People, bool Weak) ByPronoun(string pronoun, string before, LineContext context, CrewMember? speaker, IReadOnlyList<CrewMember> crew)
    {
        var others = crew.Where(c => c.Id != context.SpeakerId && (pronoun == "they" || c.Pronoun is null || c.Pronoun == pronoun)).ToList();
        if (others.Count == 0)
            return (others, false);

        // Someone named earlier in the same line: "Idris? He's fine."
        if (LastNamed(before, others) is { } inLine)
            return (new List<CrewMember> { inLine }, false);

        // The person this turn is about: named in the decision, or the one the speaker asked after.
        var subject = others.Where(c => NameIn(context.Decision.ToLowerInvariant(), c) >= 0).ToList();
        if (context.PendingQuestion is { } q && q.StartsWith("alive:", StringComparison.Ordinal))
            subject.AddRange(others.Where(c => c.Id == q["alive:".Length..] && !subject.Contains(c)));
        if (subject.Count > 0)
            return (subject, false);
        // "They" is often the pods, the stores or Earth: only a person this turn is about stands for it.
        if (pronoun == "they")
            return (new List<CrewMember>(), false);

        // The last person named in what was just said.
        for (var i = context.RecentLines.Count - 1; i >= 0; i--)
            if (LastNamed(context.RecentLines[i].ToLowerInvariant(), others) is { } recent)
                return (new List<CrewMember> { recent }, true);

        // With nothing else to go on, "he" from Mara means Idris. Kept strict: "He's fine, I assume." from Mara, who knew
        // nothing of Idris, is the line this check was written for.
        return (others.Where(c => c.Id == speaker?.PartnerId).ToList(), false);
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

    /// <summary>"Gone back to sleep" is not "gone": phrases where a value cue means something else.</summary>
    private static readonly string[] Idioms = { "gone back to sleep", "gone to sleep", "gone back under", "gone to bed", "gone back to bed" };

    private static bool NotACue(string line, int at, string cue) =>
        Idioms.Any(i => i.StartsWith(cue, StringComparison.Ordinal) && string.CompareOrdinal(line, at, i, 0, i.Length) == 0);

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
            .SelectMany(kv => kv.Value.Select(cue => (Value: kv.Key, Cue: cue,
                At: Occurrences(line, cue).Where(at => !NotACue(line, at, cue)).DefaultIfEmpty(-1).First())))
            .Where(hit => hit.At >= 0)
            .OrderByDescending(hit => hit.Cue.Length)
            .FirstOrDefault();
        if (best.Cue is null)
            return null;
        var negated = Negated(line[..best.At]);
        if (negated && fact.Values.Length == 2)
            return fact.Values.First(v => v != best.Value);
        return negated ? null : best.Value;
    }

    /// <summary>
    /// Whether a value cue that follows <paramref name="before"/> is denied: a negation right before it, past small words
    /// ("not dead", "not at all dead", "by no means safe"), or "neither ... nor" ahead of it ("Neither Idris nor Asha is dead").
    /// </summary>
    private static bool Negated(string before)
    {
        if (NeitherNor.IsMatch(before))
            return true;
        var words = Word.Matches(before).Select(m => m.Value).ToList();
        for (var i = words.Count - 1; i >= 0 && i >= words.Count - 6; i--)
        {
            var w = words[i];
            if (NegationWords.Contains(w) || w.EndsWith("n't", StringComparison.Ordinal))
                return true;
            if (!NegationFiller.Contains(w))
                return false;
        }
        return false;
    }

    private static readonly Regex Word = new(@"[a-z']+", RegexOptions.CultureInvariant);

    private static readonly Regex NeitherNor = new(@"\bneither\b.*\bnor\b", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> NegationWords = new(StringComparer.Ordinal) { "not", "no", "never", "without", "cannot", "nor" };

    /// <summary>Small words that may stand between a negation and what it denies: "not at all", "not really", "not a".</summary>
    private static readonly HashSet<string> NegationFiller = new(StringComparer.Ordinal)
    {
        "a", "an", "at", "all", "really", "quite", "actually", "even", "truly", "exactly", "entirely", "completely", "remotely",
        "very", "so", "that", "too", "in", "any", "way", "means", "the", "least", "slightest", "bit", "yet", "by",
    };
}

/// <summary>
/// What a crew line's references to people are resolved against (<see cref="RuleReader.PersonClaims"/>): who speaks, the
/// game's decision for this turn, the fact the speaker is waiting to hear about, and the recent lines the speaker saw
/// (oldest first). A snapshot, so the check can run off the main thread.
/// </summary>
public sealed record LineContext(string SpeakerId, string Decision, string? PendingQuestion, IReadOnlyList<string> RecentLines);

/// <summary>A life claim a crew line makes through a reference (<see cref="RuleReader.PersonReferences"/>); weak when the reference was a guess from the recent lines.</summary>
public sealed record PersonClaim(Fact Fact, string Value, bool Weak);
