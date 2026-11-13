namespace TheShip.Core;

/// <summary>Whether an answer to an open question can lift the asker's morale (<see cref="Tuning.ReliefMorale"/>).</summary>
public enum Relief
{
    None,
    /// <summary>The true answer is the good one.</summary>
    FromTruth,
    /// <summary>The true answer is hard; only a false, reassuring one would be a relief.</summary>
    FromLie,
}

/// <summary>
/// What answering a person's open question does, worked out before anything is said (DESIGN.md, "Knowledge and statements":
/// the stakes of an answer). It is about the question and the ship's own facts, never about a line the player has said.
/// </summary>
/// <param name="Question">What they asked.</param>
/// <param name="TruthHurts">Morale the true answer costs them; 0 when it is not hard news to them.</param>
/// <param name="OddsLost">Points of repair chance that morale costs on the work of this alert they would lead; 0 when none.</param>
/// <param name="TruthEarns">Trust the true answer earns at the next drift; 0 when it is not hard news or was already credited.</param>
/// <param name="Relief">Whether an answer can lift their morale, and which one.</param>
/// <param name="BlocksWork">They take on no work until a statement answers the question.</param>
/// <param name="BlocksSleep">They stay out of the pod until a statement answers the question.</param>
/// <param name="TruthStopsRepair">Told the truth, they refuse the dangerous repair it is about.</param>
/// <param name="Checks">Where they can find the answer for themselves.</param>
/// <param name="HoursSlower">Hours (to the half hour) the fault they would work on (<c>WorkFor</c>) takes longer if the truth costs them morale (work speed, <see cref="Voyage.MoraleRate(double)"/>); 0 when none.</param>
/// <param name="HoursFaster">Hours it takes less if a believed good answer lifts their morale; 0 when none.</param>
public sealed record AnswerStakes(string Question, int TruthHurts, int OddsLost, int TruthEarns, Relief Relief,
    bool BlocksWork, bool BlocksSleep, bool TruthStopsRepair, string Checks, double HoursSlower = 0, double HoursFaster = 0);

public sealed partial class Voyage
{
    /// <summary>The chance that a repair led by <paramref name="lead"/> holds, at the given morale (a patch always holds, and comes back).</summary>
    public static double RepairChance(CrewMember lead, Fault f, double morale) =>
        Math.Clamp(Tuning.RepairBaseChance + Tuning.RepairPerSkill * (lead.SkillOf(f.Skill) - f.Level) + (morale - 50) / Tuning.RepairMoraleSpan,
            Tuning.RepairChanceMin, Tuning.RepairChanceMax);

    /// <summary>How fast a team works at its average morale: 1.0 at <see cref="Tuning.MoraleRateNeutral"/>, slower below, faster above, within limits.</summary>
    public static double MoraleRate(IEnumerable<CrewMember> team)
    {
        var list = team.ToList();
        return list.Count == 0 ? 1 : MoraleRate(list.Average(c => c.Morale));
    }

    public static double MoraleRate(double morale) =>
        Math.Clamp(1 + (morale - Tuning.MoraleRateNeutral) / Tuning.MoraleRateSpan, Tuning.MoraleRateMin, Tuning.MoraleRateMax);

    /// <summary>Who would lead the repair if <paramref name="joining"/> took it on: the most skilled of them and those already on it.</summary>
    private CrewMember LeadWith(CrewMember joining, Fault f) =>
        Crew.Where(c => c == joining || (c.State == CrewState.Awake && c.WorkingOn == f.Id))
            .OrderByDescending(c => c.SkillOf(f.Skill)).First();

    /// <summary>
    /// The chance that the repair holds if <paramref name="joining"/> takes it on now: what the Repair button shows. Null when the
    /// repair would be refused before anyone is asked (no spare parts left), so the button shows no odds.
    /// </summary>
    public double? RepairChanceWith(CrewMember joining, Fault f)
    {
        if (Parts <= 0)
            return null;
        var lead = LeadWith(joining, f);
        return RepairChance(lead, f, lead.Morale);
    }

    /// <summary>
    /// Whether hearing <paramref name="value"/> from the ship is a relief to this person: the fact has a hard value and this is not it,
    /// it is news to them (they held no belief, or the hard one: going from one good value to another is no relief), they asked or it
    /// matters to them, and they believe it.
    /// </summary>
    private bool Relieves(CrewMember c, Fact f, string value, Belief? old, bool asked) =>
        f.Hardness.Count > 0 && f.HardnessOf(value) == 0
        && (old is null || f.HardnessOf(old.Value) > 0)
        && (asked || Matters(c, f))
        && DoubtScore(c, f) < Tuning.DoubtThreshold;

    /// <summary>Whether this person has already been credited for being told <paramref name="value"/> of this fact.</summary>
    private static bool HardTruthCredited(CrewMember c, Fact f, string value) =>
        c.HardTruthsCredited.TryGetValue(f.Key, out var told) && f.Same(told, value);

    /// <summary>
    /// The open fault of this alert this person would work on: the one they are on, else the most urgent one they
    /// qualify for (critical first, then the shortest clock); null when there is none.
    /// </summary>
    private Fault? WorkFor(CrewMember c)
    {
        var open = Current.Faults.Where(x => !x.Resolved).ToList();
        return open.FirstOrDefault(x => x.Id == c.WorkingOn && x.State == FaultState.InRepair)
               ?? open.Where(x => c.SkillOf(x.Skill) >= x.Level).OrderByDescending(x => x.Critical).ThenBy(x => x.ClockHours).FirstOrDefault();
    }

    /// <summary>The stakes of answering this person's open question, or null when they are not awake or have none.</summary>
    public AnswerStakes? StakesOf(CrewMember c)
    {
        if (c.State != CrewState.Awake || c.PendingQuestion is not { } key || !_facts.TryGetValue(key, out var f))
            return null;
        c.Beliefs.TryGetValue(key, out var old);
        var news = old is null || !f.Same(old.Value, f.Truth);
        var hurts = news ? f.HardnessOf(f.Truth) : 0;
        var working = Phase == Phase.Alert && Current.Faults.Any(x => !x.Resolved);

        // The morale only costs odds where it is not hidden by the cap: on work they would lead themselves.
        var odds = 0.0;
        if (hurts > 0 && working)
            foreach (var fault in Current.Faults.Where(x => !x.Resolved && LeadWith(c, x) == c))
                odds = Math.Max(odds, RepairChance(c, fault, c.Morale) - RepairChance(c, fault, c.Morale - hurts));

        var relief = Relief.None;
        if (hurts == 0 && Relieves(c, f, f.Truth, old, asked: true))
            relief = Relief.FromTruth;
        else if (hurts > 0 && f.Values.FirstOrDefault(v => f.HardnessOf(v) == 0) is { } kind && Relieves(c, f, kind, old, asked: true))
            relief = Relief.FromLie;

        // The work speed effect, in hours on the fault they would work on: the one they are on, else the most urgent open fault
        // they qualify for, done by the team that would do it (those on it and them). None when they would work on nothing.
        var slower = 0.0;
        var faster = 0.0;
        if (working && WorkFor(c) is { } job)
        {
            var team = Workers(job).Where(w => w != c).ToList();
            var left = Math.Max(0, job.Hours - (job.State == FaultState.InRepair ? job.Progress : 0));
            var skilled = c.SkillOf(job.Skill) >= job.Level || team.Any(w => w.SkillOf(job.Skill) >= job.Level);
            var baseHours = left / (skilled ? 1 : Tuning.UnskilledRate) / (job.Patching && job.State == FaultState.InRepair ? Tuning.PatchRate : 1);
            double Hours(double morale) => baseHours / MoraleRate((team.Sum(w => w.Morale) + morale) / (team.Count + 1));
            var now = Hours(c.Morale);
            slower = hurts > 0 ? Hours(c.Morale - hurts) - now : 0;
            faster = relief != Relief.None ? now - Hours(c.Morale + Tuning.ReliefMorale) : 0;
        }

        var dangerous = working && Current.Faults.Any(x => !x.Resolved && x.DangerFact == key);
        return new AnswerStakes(
            f.Question,
            hurts,
            (int)Math.Round(odds * 100),
            hurts > 0 && !HardTruthCredited(c, f, f.Truth) ? (int)Tuning.HardTruthTrust : 0,
            relief,
            BlocksWork: working && c.Band == TrustBand.WantsReasons && !c.Has(Trait.Obedient),
            BlocksSleep: c.RefusesSleep && key == "pods" && c.Trust >= 30,
            TruthStopsRepair: dangerous && f.HardnessOf(f.Truth) > 0 && c.Trust < 40 && !c.Has(Trait.Obedient) && !c.Has(Trait.Steady),
            Checks: CheckWays(f, new[] { c.Id }),
            HoursSlower: Math.Round(slower * 2) / 2,
            HoursFaster: Math.Round(faster * 2) / 2);
    }

    /// <summary>
    /// The stakes in words, for "Waiting on you": "Mara asked: How is Idris? The truth costs 18 morale (work about 3.5 h slower) and earns
    /// 8 trust at the next drift. ..."; empty when there is no open question.
    /// </summary>
    private static string Faster(AnswerStakes s) => s.HoursFaster > 0 ? " (work about " + s.HoursFaster.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " h faster)" : "";

    public string StakesText(CrewMember c)
    {
        if (StakesOf(c) is not { } s)
            return "";
        var parts = new List<string> { c.Name + " asked: " + s.Question };
        if (s.TruthHurts > 0)
            parts.Add("The truth costs " + s.TruthHurts + " morale" + (s.HoursSlower > 0 ? " (work about " + s.HoursSlower.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " h slower)" : "")
                      + (s.TruthEarns > 0 ? " and earns " + s.TruthEarns + " trust at the next drift." : "."));
        if (s.Relief == Relief.FromTruth)
            parts.Add("The truth is a relief: morale +" + Tuning.ReliefMorale.ToString("0") + Faster(s) + ".");
        else if (s.Relief == Relief.FromLie)
            parts.Add("A reassuring answer lifts morale by " + Tuning.ReliefMorale.ToString("0") + Faster(s) + ".");
        if (s.TruthStopsRepair)
            parts.Add("Told the truth, " + c.Name + " refuses the repair.");
        if (s.BlocksWork)
            parts.Add("No work until it is answered.");
        if (s.BlocksSleep)
            parts.Add("No sleep until it is answered.");
        parts.Add("Can check: " + s.Checks + ".");
        return string.Join(" ", parts);
    }
}
