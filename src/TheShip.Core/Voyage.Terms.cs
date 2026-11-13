namespace TheShip.Core;

/// <summary>What a worker knows about the risk of a fault when they start: from their beliefs, true or false (the ship's word is all they have).</summary>
public enum RiskKnowledge
{
    /// <summary>The fault has no danger fact.</summary>
    NoRisk,
    /// <summary>They hold no belief about the danger.</summary>
    Unaware,
    /// <summary>They believe the benign value ("safe").</summary>
    Reassured,
    /// <summary>They believe the hard value: they take precautions.</summary>
    Briefed,
}

/// <summary>The terms a person works a fault on, fixed when they start (DESIGN.md, "Work terms"). Built from beliefs only, never from the truth.</summary>
public sealed record WorkTerms(string FaultId, RiskKnowledge Risk, double RateFactor, double HarmShare, bool CauseBriefed = false, double OddsBonus = 0, double CauseRateFactor = 1)
{
    /// <summary>The work speed in force: the precautions (<see cref="RateFactor"/>) times the speed of knowing the cause.</summary>
    public double Speed => RateFactor * CauseRateFactor;
    public bool RiskBriefed => Risk == RiskKnowledge.Briefed;
}

/// <summary>Counters of what the work terms did in a run (the balance harness reports them; nothing in the game reads them).</summary>
public sealed class WorkTermStats
{
    /// <summary>Work starts on a fault with a danger fact, and how many of them began briefed (believing the hard value).</summary>
    public int RiskStarts, BriefedStarts;
    /// <summary>Work starts on any fault, and how many began with a belief about its cause (the repair odds bonus).</summary>
    public int Starts, CauseBonusStarts;
    /// <summary>Repair rolls where a cause bonus was in force, and those whose result it changed from failed to held.</summary>
    public int BonusRolls, BonusFlips;
    /// <summary>Workers hurt by an order they had no word on (trust charged), at the notice beat or by a failed repair.</summary>
    public int HurtUnbriefed;
    /// <summary>Failed-repair injuries to a lead who was briefed (no trust charged) and to one who was not.</summary>
    public int InjuriesBriefed, InjuriesNotBriefed;
    /// <summary>Workers who finished a danger job (a dose) briefed and not briefed.</summary>
    public int DosesBriefed, DosesNotBriefed;
}

public sealed partial class Voyage
{
    /// <summary>What the work terms did in this run.</summary>
    public WorkTermStats TermStats { get; } = new();

    /// <summary>The terms <paramref name="c"/> would work <paramref name="f"/> on, from what they believe now.</summary>
    public WorkTerms TermsFor(CrewMember c, Fault f) => TermsFor(c, f, null, null);

    /// <summary>
    /// The terms <paramref name="c"/> would work <paramref name="f"/> on if they believed <paramref name="value"/> of <paramref name="key"/>
    /// (what an answer would change; the stakes of a question). Terms already fixed on this fault stay as they are.
    /// </summary>
    public WorkTerms TermsIfBelieved(CrewMember c, Fault f, string key, string value) =>
        c.WorkingOn == f.Id && c.Terms is { } t && t.FaultId == f.Id ? t : TermsFor(c, f, key, value);

    private WorkTerms TermsFor(CrewMember c, Fault f, string? key, string? value)
    {
        string? Believed(string fact) => fact == key ? value : c.Beliefs.TryGetValue(fact, out var b) ? b.Value : null;
        var risk = RiskKnowledge.NoRisk;
        if (f.DangerFact is { } danger)
            risk = Believed(danger) is not { } held ? RiskKnowledge.Unaware
                : _facts[danger].HardnessOf(held) > 0 ? RiskKnowledge.Briefed : RiskKnowledge.Reassured;
        var briefed = risk == RiskKnowledge.Briefed;
        // Any belief about the cause, true or false, counts: the ship's word is all they have.
        var cause = f.CauseFact is { } cf && Believed(cf) != null;
        return new WorkTerms(f.Id, risk, briefed ? Tuning.PrecautionRate : 1, briefed ? Tuning.PrecautionHarm : 1, cause, cause ? Tuning.CauseOddsBonus : 0, cause ? Tuning.CauseRate : 1);
    }

    /// <summary>The terms in force: those fixed when they started this fault, else what they would be if they started now.</summary>
    public WorkTerms TermsOf(CrewMember c, Fault f) =>
        c.WorkingOn == f.Id && c.Terms is { } t && t.FaultId == f.Id ? t : TermsFor(c, f);

    /// <summary>The team's work terms and pair speed; with <paramref name="at"/>, as they will be at that clock (a thaw done by then counts as awake).</summary>
    private double TeamFactor(Fault f, IEnumerable<CrewMember> team, double? at = null)
    {
        var list = team.ToList();
        return list.Count == 0 ? 1 : list.Average(w => TermsOf(w, f).Speed * (at is { } t ? PairFactorAt(w, t) : PairFactor(w)));
    }

    /// <summary>Work speed from having the partner awake too (<see cref="Tuning.PairRate"/>), 1 otherwise.</summary>
    internal double PairFactor(CrewMember w) =>
        w.PartnerId is { } p && Crew.First(x => x.Id == p).State == CrewState.Awake && w.State == CrewState.Awake ? Tuning.PairRate : 1;

    /// <summary>The pair speed as it will be at clock <paramref name="at"/>: either of the two still thawing counts once their thaw is done by then.</summary>
    private double PairFactorAt(CrewMember w, double at)
    {
        bool UpBy(CrewMember c) => c.State == CrewState.Awake || c.State == CrewState.Thawing && c.ThawDoneAt <= at + 0.001;
        return w.PartnerId is { } p && UpBy(Crew.First(x => x.Id == p)) && UpBy(w) ? Tuning.PairRate : 1;
    }
}
