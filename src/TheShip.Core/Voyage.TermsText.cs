using System.Globalization;

namespace TheShip.Core;

public sealed partial class Voyage
{
    private static string Num(double x) => x.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// The terms <paramref name="c"/> works <paramref name="f"/> on, in words: "If Mara starts now: Mara knows about the radiation dose: precautions,
    /// work 10% slower, half the dose, no trust lost if hurt. Mara has not been told the cause; telling them: 3% faster, +8% odds."
    /// Built from what they believe and the fault, never from the truth, so it never says whether anything told them was true.
    /// Empty when the fault has neither a risk nor a cause.
    /// </summary>
    public string TermsText(CrewMember c, Fault f)
    {
        var t = TermsOf(c, f);
        var parts = new List<string>();
        var name = c.Name;
        switch (t.Risk)
        {
            case RiskKnowledge.Briefed:
                parts.Add(name + " knows about the " + RiskLabel(f) + ": precautions, work " + Num((1 - Tuning.PrecautionRate) * 100) + "% slower, " + HarmShareText() + " the dose, no trust lost if hurt.");
                break;
            case RiskKnowledge.Reassured:
                parts.Add(name + " expects no harm: no precautions, full dose.");
                break;
            case RiskKnowledge.Unaware:
                parts.Add(name + " has not been told about the " + RiskLabel(f) + ": full dose, and if hurt it counts against you.");
                break;
        }
        // Knowing the cause speeds the work (Tuning.CauseRate) and adds to the odds (Tuning.CauseOddsBonus).
        var causeGain = Num((Tuning.CauseRate - 1) * 100) + "% faster, +" + Num(Tuning.CauseOddsBonus * 100) + "% odds";
        if (f.CauseFact is not null)
            parts.Add(t.CauseBriefed
                ? name + " knows the cause: " + causeGain + "."
                : name + " has not been told the cause; telling them: " + causeGain + ".");
        if (parts.Count == 0)
            return "";
        return (c.WorkingOn == f.Id ? name + " works on it: " : "If " + name + " starts now: ") + string.Join(" ", parts);
    }

    /// <summary>The risk as a short noun ("radiation dose"), to follow "the" in a sentence.</summary>
    private string RiskLabel(Fault f) => f.DangerFact is { } d && _facts.TryGetValue(d, out var fact) && fact.Noun is { } noun ? noun : "danger";

    /// <summary>How much of the harm a briefed worker takes, as words before "the dose": "half", or "40% of".</summary>
    private static string HarmShareText() =>
        Math.Abs(Tuning.PrecautionHarm - 0.5) < 1e-9 ? "half" : Num(Tuning.PrecautionHarm * 100) + "% of";

    /// <summary>What the ship knows of this fault's cause, once the log shows it (its alert has started); null otherwise.</summary>
    public string? CauseKnown(Fault f) =>
        f.CauseFact is { } cf && _facts.TryGetValue(cf, out var fact) && InLog(fact) ? fact.Say(fact.Truth) : null;

    /// <summary>
    /// The terms in two rows of "Waiting on you" (about 90 characters): "Mara, unbriefed: full dose, hurt costs trust; cause untold (+8% odds)."
    /// The long form (<see cref="TermsText"/>) is its tooltip. From belief and fault only, like the long form. Empty when there are no terms.
    /// </summary>
    public string TermsShort(CrewMember c, Fault f)
    {
        var t = TermsOf(c, f);
        var risk = t.Risk switch
        {
            RiskKnowledge.Briefed => "briefed: " + Num((1 - Tuning.PrecautionRate) * 100) + "% slower, " + HarmShareText() + " dose, no trust lost if hurt",
            RiskKnowledge.Reassured => "expects no harm: full dose",
            RiskKnowledge.Unaware => "unbriefed: full dose, hurt costs trust",
            _ => null,
        };
        var cause = f.CauseFact is null ? null
            : t.CauseBriefed ? "knows the cause"
            : "cause untold (+" + Num(Tuning.CauseOddsBonus * 100) + "% odds)";
        var parts = new[] { risk, cause }.Where(p => p != null).ToList();
        return parts.Count == 0 ? "" : c.Name + ", " + string.Join("; ", parts) + ".";
    }

    /// <summary>
    /// The terms item for "Waiting on you": the selected idle person's likely job, when it has a risk or a cause, as the short form shown
    /// and the full text for its tooltip; null otherwise.
    /// </summary>
    public (string Short, string Full)? TermsItem(CrewMember c) =>
        c.State == CrewState.Awake && Phase == Phase.Alert && c.WorkingOn is null && WorkFor(c) is { } job && TermsText(c, job) is { Length: > 0 } text
            ? (TermsShort(c, job), text) : null;
}
