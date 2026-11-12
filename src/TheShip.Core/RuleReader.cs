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
