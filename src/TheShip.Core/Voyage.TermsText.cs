using System.Globalization;

namespace TheShip.Core;

public sealed partial class Voyage
{
    private static string Num(double x) => x.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// The terms <paramref name="c"/> works <paramref name="f"/> on, in words: "If Mara starts now: Mara knows the dose - precautions: work 10% slower,
    /// dose 8 health instead of 15, no trust lost if hurt. Mara has not been told the cause: telling Mara adds 8% to the repair odds."
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
                parts.Add(name + " knows the " + RiskLabel(f) + " - precautions: work " + Num((1 - Tuning.PrecautionRate) * 100) + "% slower, dose "
                          + Num(Tuning.DangerDose * Tuning.PrecautionHarm) + " health instead of " + Num(Tuning.DangerDose) + ", no trust lost if hurt.");
                break;
            case RiskKnowledge.Reassured:
                parts.Add(name + " expects no harm - no precautions: full pace, full dose of " + Num(Tuning.DangerDose) + " health.");
                break;
            case RiskKnowledge.Unaware:
                parts.Add(name + " has not been told about the " + RiskLabel(f) + " - no precautions: full dose of " + Num(Tuning.DangerDose)
                          + " health, and if hurt " + name + " counts it against you.");
                break;
        }
        if (f.CauseFact is not null)
            parts.Add(t.CauseBriefed
                ? name + " knows the cause: +" + Num(Tuning.CauseOddsBonus * 100) + "% repair odds."
                : name + " has not been told the cause: telling " + name + " adds " + Num(Tuning.CauseOddsBonus * 100) + "% to the repair odds.");
        if (parts.Count == 0)
            return "";
        return (c.WorkingOn == f.Id ? name + " works on it: " : "If " + name + " starts now: ") + string.Join(" ", parts);
    }

    private string RiskLabel(Fault f) => f.DangerFact is { } d && _facts.TryGetValue(d, out var fact) ? fact.Label : "danger";

    /// <summary>What the ship knows of this fault's cause, once the log shows it (its alert has started); null otherwise.</summary>
    public string? CauseKnown(Fault f) =>
        f.CauseFact is { } cf && _facts.TryGetValue(cf, out var fact) && InLog(fact) ? fact.Say(fact.Truth) : null;

    /// <summary>The terms item for "Waiting on you": the selected idle person's likely job, when it has a risk or a cause; null otherwise.</summary>
    public string? TermsItem(CrewMember c) =>
        c.State == CrewState.Awake && Phase == Phase.Alert && c.WorkingOn is null && WorkFor(c) is { } job && TermsText(c, job) is { Length: > 0 } text ? text : null;
}
