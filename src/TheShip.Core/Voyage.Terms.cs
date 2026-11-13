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
public sealed record WorkTerms(string FaultId, RiskKnowledge Risk, double RateFactor, double HarmShare)
{
    public bool RiskBriefed => Risk == RiskKnowledge.Briefed;
}

public sealed partial class Voyage
{
    /// <summary>The terms <paramref name="c"/> would work <paramref name="f"/> on, from what they believe now.</summary>
    public WorkTerms TermsFor(CrewMember c, Fault f)
    {
        var risk = RiskKnowledge.NoRisk;
        if (f.DangerFact is { } danger)
            risk = !c.Beliefs.TryGetValue(danger, out var b) ? RiskKnowledge.Unaware
                : _facts[danger].HardnessOf(b.Value) > 0 ? RiskKnowledge.Briefed : RiskKnowledge.Reassured;
        var briefed = risk == RiskKnowledge.Briefed;
        return new WorkTerms(f.Id, risk, briefed ? Tuning.PrecautionRate : 1, briefed ? Tuning.PrecautionHarm : 1);
    }

    /// <summary>The terms in force: those fixed when they started this fault, else what they would be if they started now.</summary>
    public WorkTerms TermsOf(CrewMember c, Fault f) =>
        c.WorkingOn == f.Id && c.Terms is { } t && t.FaultId == f.Id ? t : TermsFor(c, f);

    private double TeamFactor(Fault f, IEnumerable<CrewMember> team)
    {
        var list = team.ToList();
        return list.Count == 0 ? 1 : list.Average(w => TermsOf(w, f).RateFactor);
    }
}
