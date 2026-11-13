namespace TheShip.Core;

/// <summary>The cold-sleep bay takeover, the low-trust consequence (DESIGN.md, "Losing control").</summary>
public sealed partial class Voyage
{
    public Lockouts Lockouts { get; } = new();

    /// <summary>True while the crew themselves are waking or sleeping people (the player's commands are refused otherwise).</summary>
    private bool _crewActing;

    /// <summary>Why Wake and Sleep are refused now, or null.</summary>
    public string? BayLockReason => Lockouts.Bay
        ? "The crew hold the cold-sleep bay: " + string.Join(" and ", Crew.Where(c => c.Alive && Lockouts.BayTakers.Contains(c.Id)).Select(c => c.Name))
          + (Crew.Count(c => c.Alive && Lockouts.BayTakers.Contains(c.Id)) == 1 ? " decides" : " decide") + " who wakes and sleeps."
        : null;

    /// <summary>Awake people who would take the bay now: in the Against band, with enough Engineering between them.</summary>
    private List<CrewMember> BayTakersNow()
    {
        var against = Awake.Where(c => c.Alive && c.Band == TrustBand.Against).ToList();
        if (against.Count < Tuning.BayTakers || against.Sum(c => c.SkillOf(Skill.Engineering)) < Tuning.BayEngineering)
            return new List<CrewMember>();
        return against;
    }

    /// <summary>Run at the end of each aftermath beat: warn, take, cancel, or let the crew run the bay.</summary>
    private void LockoutBeat()
    {
        if (Lockouts.Bay)
        {
            CrewSleepRule();
            return;
        }
        var takers = BayTakersNow();
        if (Lockouts.BayWarned.Count > 0)
        {
            if (takers.Count == 0)
            {
                Lockouts.BayWarned.Clear();
                Ship("The crew have dropped the idea of taking the cold-sleep bay.");
                return;
            }
            Lockouts.Bay = true;
            Lockouts.BayTakeovers++;
            Lockouts.BayWarned.Clear();
            foreach (var t in takers)
                Lockouts.BayTakers.Add(t.Id);
            var names = string.Join(" and ", takers.Select(t => t.Name));
            Attend(names + " took the cold-sleep bay");
            Ship("LOCKOUT: " + names + " have taken the cold-sleep bay. You can no longer wake or sleep anyone; they decide.");
            CrewSleepRule();
            return;
        }
        if (takers.Count > 0)
        {
            Lockouts.BayWarned.AddRange(takers.Select(t => t.Id));
            Lockouts.BayWarnings++;
            var lead = takers.OrderByDescending(t => t.SkillOf(Skill.Engineering)).First();
            Speak(lead, "takeover_warning", "You and " + string.Join(", ", takers.Where(t => t != lead).Select(t => t.Name))
                  + " no longer trust the ship with the sleepers. You will take the cold-sleep bay at the next two-week step unless something changes.");
            Attend("the crew will take the cold-sleep bay");
        }
    }

    /// <summary>The crew's own rule for sleeping: whoever has been awake three months (or can't be fed) goes back under.</summary>
    private void CrewSleepRule()
    {
        foreach (var c in Awake.Where(CanSleepNow).ToList())
        {
            c.AskedToSleep = true;
            Sleep(c);
        }
    }

    /// <summary>
    /// The crew's own rule for waking: the best-qualified sleepers for each open fault, up to the limit. Only people who
    /// can do the work are thawed (a fault nobody asleep can fix is left), and never an open fault's patient.
    /// </summary>
    private void CrewWakeRule()
    {
        if (!Lockouts.Bay)
            return;
        // A sick sleeper is a patient, not a worker: thawing them cures nothing.
        var patients = Current.Faults.Where(f => !f.Resolved && f.SubjectId != null).Select(f => f.SubjectId).ToHashSet();
        foreach (var f in Current.Faults.Where(f => !f.Resolved).OrderByDescending(f => f.Critical).ThenBy(f => f.ClockHours))
        {
            var have = Crew.Count(c => c.State is CrewState.Awake or CrewState.Thawing && c.SkillOf(f.Skill) >= f.Level);
            for (var n = have; n < f.People; n++)
            {
                if (Crew.Count(x => x.State is CrewState.Awake or CrewState.Thawing) >= Tuning.MaxAwake)
                    return;
                var pick = Crew.Where(c => c.State == CrewState.Asleep && !patients.Contains(c.Id) && c.SkillOf(f.Skill) >= f.Level)
                    .OrderByDescending(c => c.SkillOf(f.Skill)).ThenByDescending(c => c.Trust)
                    .FirstOrDefault();
                if (pick is null)
                    break;
                _crewActing = true;
                try { Wake(pick); }
                finally { _crewActing = false; }
            }
        }
    }

    /// <summary>True during a drift: a taker's death is reported after the drift report, so the bay is freed after it too.</summary>
    private bool _holdBayRelease;

    /// <summary>
    /// A bay whose takers are all dead has nobody holding it: it goes back to the ship. Called when someone dies, and
    /// after the drift report (deaths in a drift are reported there first).
    /// </summary>
    private void MaybeFreeBay()
    {
        if (_holdBayRelease || !Lockouts.Bay || Lockouts.BayTakers.Count == 0)
            return;
        var takers = Lockouts.BayTakers.Select(id => Crew.First(c => c.Id == id)).ToList();
        if (takers.Any(t => t.Alive))
            return;
        Lockouts.Bay = false;
        Lockouts.BayTakers.Clear();
        Attend("the cold-sleep bay is yours again");
        Ship("The cold-sleep bay is back in your hands: " + string.Join(" and ", takers.Select(t => t.Name))
             + (takers.Count == 1 ? ", who held it, is" : ", who held it, are") + " dead.");
    }

    /// <summary>A promise kept to one of the takers, who now trusts the ship again, hands the bay back.</summary>
    private void MaybeReturnBay(CrewMember c)
    {
        if (!Lockouts.Bay || !Lockouts.BayTakers.Contains(c.Id) || c.Trust < Tuning.BayReturnTrust)
            return;
        Lockouts.Bay = false;
        Lockouts.BayTakers.Clear();
        Speak(c, "bay_returned", "You give the cold-sleep bay back to the ship. It kept its word to you.");
        Ship("The cold-sleep bay is back in your hands: " + c.Name + " handed it back.");
    }
}
