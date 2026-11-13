using System.Globalization;

namespace TheShip.Core;

/// <summary>The player's direct controls and the clock (DESIGN.md, "Direct control" and "The eight steps").</summary>
public sealed partial class Voyage
{
    /// <summary>Step 3: start the thaw of a sleeper. It takes six hours of the alert's clock.</summary>
    public void Wake(CrewMember c)
    {
        if (Phase == Phase.Ended)
            return;
        if (c.State != CrewState.Asleep)
        {
            Ship(c.Name + " is not asleep.");
            return;
        }
        if (Crew.Count(x => x.State is CrewState.Awake or CrewState.Thawing) >= Tuning.MaxAwake)
        {
            Ship("Three people awake is the most this build allows at once.");
            return;
        }
        _idleWarned = false;
        c.State = CrewState.Thawing;
        c.ThawDoneAt = Clock + Tuning.ThawHours;
        Ship("Thaw started for " + c.Name + ". Six hours. Advance the clock.");
        if (Stores <= 0)
            Ship(FormattableString.Invariant($"The stores are empty: {c.Name} goes hungry from the start, and {Tuning.StarveDays:0} days without food kills. ")
                 + "The pod can take " + c.Name + " back early.");
        else if (StoresTooLowFor(c))
        {
            var mouths = Crew.Count(x => x.State is CrewState.Awake or CrewState.Thawing);
            Ship(FormattableString.Invariant($"The stores ({Stores:0} person-days) feed the {mouths} awake about {Stores / mouths:0} days, short of a three-month waking. ")
                 + "The pod can take " + c.Name + " back early.");
        }
    }

    /// <summary>
    /// Whether the stores run out before this person has been awake three months, with everyone who is up now eating.
    /// Then the three-month minimum is waived: shorter wakings, before hunger (DESIGN.md, "What every decision spends").
    /// </summary>
    private bool StoresTooLowFor(CrewMember c)
    {
        var mouths = Math.Max(1, Crew.Count(x => x.State is CrewState.Awake or CrewState.Thawing));
        var left = Tuning.MinAwakeHours - (c.State == CrewState.Awake ? c.HoursAwake : 0);
        return Stores * 24 / mouths < left;
    }

    /// <summary>Whether the pod can take this person now: three months awake, or stores too low to last that long.</summary>
    public bool CanSleepNow(CrewMember c) =>
        c.State == CrewState.Awake && (c.HoursAwake >= Tuning.MinAwakeHours || StoresTooLowFor(c));

    /// <summary>Move the clock to the next thing that happens.</summary>
    public void Go()
    {
        if (Phase == Phase.Ended)
            return;
        if (Phase == Phase.Alert)
            GoAlert();
        else
            GoAftermath();
        SyncFacts();
    }

    private IEnumerable<CrewMember> Workers(Fault f) => Crew.Where(c => c.State == CrewState.Awake && c.WorkingOn == f.Id);

    private bool Staffed(Fault f) => Workers(f).Count() >= f.People;

    private double Rate(Fault f) => Rate(f, Workers(f));

    private static double Rate(Fault f, IEnumerable<CrewMember> team)
    {
        var skilled = team.Any(w => w.SkillOf(f.Skill) >= f.Level);
        return (skilled ? 1 : Tuning.UnskilledRate) * (f.Patching ? Tuning.PatchRate : 1);
    }

    /// <summary>
    /// Hours until the fault is done if the work started now: the thaw of whoever is still thawing or must be woken to reach the people it needs
    /// (the thaw of a qualified sleeper if nobody qualified is up), then the remaining work at the rate that team would work
    /// (DESIGN.md, "Alerts": the clock rule). Ignores that someone may be busy elsewhere.
    /// </summary>
    public double EarliestDoneHours(Fault f, bool patch = false)
    {
        bool Qualified(CrewMember c) => c.SkillOf(f.Skill) >= f.Level;
        double Thaw(CrewMember c) => c.State == CrewState.Thawing ? Math.Max(0, c.ThawDoneAt - Clock) : 0;
        var team = Crew.Where(c => c.State is CrewState.Awake or CrewState.Thawing)
            .OrderByDescending(Qualified).ThenBy(Thaw).Take(f.People).ToList();
        var wait = team.Count == 0 ? 0 : team.Max(Thaw);
        var skilled = team.Any(Qualified);
        if (!skilled && Crew.Any(c => c.State == CrewState.Asleep && Qualified(c)))
        {
            // Someone qualified is woken to lead the team.
            skilled = true;
            wait = Tuning.ThawHours;
        }
        if (team.Count < f.People)
            wait = Math.Max(wait, Tuning.ThawHours);
        var rate = (skilled ? 1 : Tuning.UnskilledRate) * (patch ? Tuning.PatchRate : 1);
        var done = f.Patching == patch ? f.Progress : 0;
        return wait + Math.Max(0, f.Hours - done) / rate;
    }

    /// <summary>
    /// The issue card's verdict, short enough for one line of a card (<see cref="IssueCardLayout.FitsOneLine"/>): when the fault could be
    /// done if started now, at the rate it is being worked at (a repair, or a patch while it is being patched), and whether the clock allows
    /// it. "Done in 9 h", "Done in 18 h - patch only: 12 h", "Done in 20 h - too late", "Patch done in 6 h"; with no spare parts left a
    /// repair is impossible and the patch time is quoted: "Patch only (no parts): 12 h".
    /// </summary>
    public string EarliestDoneText(Fault f)
    {
        var patching = f.State == FaultState.InRepair && f.Patching;
        var noParts = !patching && Parts <= 0;
        var hours = EarliestDoneHours(f, patching || noParts);
        var left = f.Deadline - Clock + 0.001;
        var text = (patching ? "Patch done in " : noParts ? "Patch only (no parts): " : "Done in ") + ShortHours(hours);
        if (hours <= left)
            return text;
        if (!patching && !noParts && EarliestDoneHours(f, true) is var patchHours && patchHours <= left)
            return text + " - patch only: " + ShortHours(patchHours);
        return text + " - too late";
    }

    /// <summary>"18 h", or "3 d" from two days on; rounded up.</summary>
    private static string ShortHours(double hours)
    {
        var h = Math.Ceiling(hours);
        return h >= 48
            ? Math.Ceiling(h / 24).ToString("0", CultureInfo.InvariantCulture) + " d"
            : h.ToString("0", CultureInfo.InvariantCulture) + " h";
    }

    private void GoAlert()
    {
        var open = Current.Faults.Where(f => !f.Resolved).ToList();
        var next = double.MaxValue;
        var thawing = false;
        foreach (var c in Crew.Where(c => c.State == CrewState.Thawing))
        {
            thawing = true;
            next = Math.Min(next, c.ThawDoneAt);
        }
        var working = false;
        foreach (var f in open.Where(f => f.State == FaultState.InRepair && Staffed(f)))
        {
            working = true;
            next = Math.Min(next, Clock + (f.Hours - f.Progress) / Rate(f));
        }
        if (!thawing && !working && !_idleWarned)
        {
            _idleWarned = true;
            Ship("Nobody is thawing and no repair is staffed. Advance again to let the clocks run out.");
            return;
        }
        foreach (var f in open)
            next = Math.Min(next, f.Deadline);
        if (next == double.MaxValue)
        {
            CheckAlertOver();
            return;
        }

        Pass(Math.Max(0, next - Clock));
        FinishThaws();
        // A fault dropped meanwhile (a sick person died) is no longer on the alert.
        foreach (var f in open.Where(f => Current.Faults.Contains(f) && f.State == FaultState.InRepair && Staffed(f) && f.Progress >= f.Hours - 0.001).ToList())
            CompleteRepair(f);
        foreach (var f in open.Where(f => Current.Faults.Contains(f) && !f.Resolved && f.Deadline <= Clock + 0.001).ToList())
            Lose(f);
        CheckAlertOver();
    }

    private void GoAftermath()
    {
        var up = Crew.Where(c => c.State is CrewState.Awake or CrewState.Thawing).ToList();
        if (up.Count == 0)
        {
            Ship("Everyone is asleep. Drift moves the clock to the next alert.");
            return;
        }
        var step = Tuning.AftermathStepHours;
        foreach (var c in up.Where(c => c.State == CrewState.Thawing))
            step = Math.Min(step, Math.Max(0.5, c.ThawDoneAt - Clock));
        Pass(step);
        FinishThaws();
        if (step >= 24)
            AftermathBeat();
    }

    /// <summary>Time passes: the awake age and eat, staffed repairs progress.</summary>
    private void Pass(double hours)
    {
        var start = Clock;
        var awake = Awake.ToList();
        var (storesOut, starved) = Eat(awake, hours);
        // A worker who starves in this span works only until they die.
        foreach (var f in Current.Faults.Where(f => f.State == FaultState.InRepair && Staffed(f)))
            f.Progress += Work(f, hours, starved);
        foreach (var c in awake)
        {
            var lived = starved.TryGetValue(c, out var at) ? at : hours;
            c.HoursAwake += lived;
            c.Age += lived / Tuning.HoursPerYear;
        }
        Clock += hours;
        if (storesOut is { } outAt)
        {
            Attend("the stores ran out");
            Ship($"The stores ran out in year {Math.Floor((start + outAt) / Tuning.HoursPerYear):0}. Whoever is awake now goes hungry: "
                 + $"{Tuning.StarveDays:0} days without food kills. The pods can take them back early.");
        }
        // Two can starve in one step: only those still alive at its end say what they saw.
        var held = HoldSpeech();
        try
        {
            foreach (var (c, at) in starved.OrderBy(kv => kv.Value))
                Kill(c, $"starved in year {Math.Floor((start + at) / Tuning.HoursPerYear):0}");
        }
        finally
        {
            if (held)
                ReleaseSpeech();
        }
    }

    /// <summary>
    /// Hours of progress a staffed repair makes in <paramref name="hours"/>, counting each worker only until they die
    /// (<paramref name="died"/>: hours from now). The work stops when the team falls short of the people it needs.
    /// </summary>
    private double Work(Fault f, double hours, IReadOnlyDictionary<CrewMember, double> died)
    {
        var done = 0.0;
        var from = 0.0;
        foreach (var to in died.Values.Where(t => t < hours).Append(hours).Distinct().Order())
        {
            var team = Workers(f).Where(w => !died.TryGetValue(w, out var at) || at > from).ToList();
            if (team.Count < f.People)
                break;
            done += (to - from) * Rate(f, team);
            from = to;
        }
        return done;
    }

    /// <summary>
    /// The awake eat for <paramref name="hours"/>: one person-day of stores each per day. Once the stores are empty they go
    /// hungry, and <see cref="Tuning.StarveDays"/> hungry days kill. Returns when (hours from now) the stores ran out, if they did
    /// in this span, and who starved and when. Does not kill anyone: the caller does, in time order.
    /// </summary>
    private (double? StoresOut, Dictionary<CrewMember, double> Starved) Eat(IReadOnlyList<CrewMember> awake, double hours)
    {
        var starved = new Dictionary<CrewMember, double>();
        if (awake.Count == 0 || hours <= 0)
            return (null, starved);
        var fedHours = Stores * 24 / awake.Count;
        if (fedHours >= hours)
        {
            Stores -= hours / 24 * awake.Count;
            return (null, starved);
        }
        double? storesOut = Stores > 0 ? fedHours : null;
        var hungryFrom = fedHours;
        Stores = 0;
        foreach (var c in awake)
        {
            var left = Math.Max(0, Tuning.StarveDays * 24 - c.HoursHungry);
            if (hungryFrom + left <= hours)
            {
                c.HoursHungry += left;
                starved[c] = hungryFrom + left;
            }
            else
                c.HoursHungry += hours - hungryFrom;
        }
        return (storesOut, starved);
    }

    private void FinishThaws()
    {
        foreach (var c in Crew.Where(c => c.State == CrewState.Thawing && c.ThawDoneAt <= Clock + 0.001).ToList())
            CompleteThaw(c);
    }

    /// <summary>Step 4 begins: the person comes round, is 3 to 5 years older, and asks a question before anything else.</summary>
    private void CompleteThaw(CrewMember c)
    {
        c.State = CrewState.Awake;
        c.HoursAwake = 0;
        c.Thaws++;
        c.Age += _rng.Next(Tuning.ThawAgeMin, Tuning.ThawAgeMax + 1);
        c.AskedToSleep = false;
        c.RequestMade = false;
        c.RequestGranted = false;
        c.SawPartner = false;
        c.SmallTalkDone = false;
        c.CanSleepAnnounced = false;
        c.EarlySleepAnnounced = false;
        c.PendingQuestion = null;
        PodWear++;

        if (_rng.NextDouble() < Tuning.ThawRiskPerWear * PodWear)
        {
            // A share of failed thaws kill outright; the rest injure (a healthy person survives the injury).
            var fatal = _rng.NextDouble() < Tuning.ThawFatalShare;
            c.Health = fatal ? 0 : c.Health - Tuning.ThawInjury;
            Ship(fatal ? "The thaw went badly. " + c.Name + " is not breathing." : "The thaw went badly. " + c.Name + " is hurt.");
            if (c.Health <= 0)
            {
                Kill(c, "did not survive the thaw");
                return;
            }
        }
        Ship(c.Name + " is awake.");

        // A promise never to thaw them again is broken by this thaw.
        foreach (var s in Statements.Where(s => s.Kind == StatementKind.Promise && s.PromiseKey == "no_more_thaws"
                                                && s.Hearers.Contains(c.Id) && !s.PromiseKept.ContainsKey(c.Id)))
            ResolvePromise(s, c, kept: false);

        // Woken for a fault, they know there was one.
        if (Current.Faults.Count > 0)
            Learn(c, "alert:" + Current.Number, "happened", "evidence");

        // They see who else is up.
        foreach (var other in Awake.Where(o => o != c).ToList())
        {
            Meet(c, other);
            Meet(other, c);
        }

        if (Current.Number == 5 && !_refuserPicked)
        {
            _refuserPicked = true;
            c.RefusesSleep = true;
        }

        var partner = c.PartnerId is null ? null : Crew.First(x => x.Id == c.PartnerId);
        string key;
        if (partner != null && partner.State != CrewState.Awake
            && !(c.Beliefs.TryGetValue("alive:" + partner.Id, out var known) && known.Firsthand && known.Value == "dead"))
            key = "alive:" + partner.Id;
        else
            key = c.Want == Want.HearFromEarth ? "earth" : "date";
        c.PendingQuestion = key;
        var question = _facts[key].Question;
        Speak(c, "wake_question", "You have just been thawed. You ask the ship: " + question, question);
        Clamp(c);
    }

    private void Meet(CrewMember who, CrewMember sees)
    {
        if (who.PartnerId == sees.Id)
            who.SawPartner = true;
        Learn(who, "alive:" + sees.Id, "alive", "evidence");
    }

    /// <summary>Step 5: ask someone to repair (or patch) a fault. Whether they do is theirs to decide.</summary>
    public void Assign(CrewMember c, Fault f, bool patch)
    {
        if (Phase == Phase.Ended)
            return;
        if (c.State != CrewState.Awake)
        {
            Ship(c.Name + " is not awake.");
            return;
        }
        if (f.Resolved)
        {
            Ship(f.Name + " needs no more work.");
            return;
        }
        if (!patch && Parts <= 0)
        {
            Ship("No spare parts are left. This can only be patched.");
            return;
        }
        if (Locked(f.Section))
        {
            Ship("The door to the " + GetSection(f.Section)!.Name.ToLowerInvariant() + " is locked. Nobody can reach the " + f.Name.ToLowerInvariant() + ".");
            return;
        }

        if (c.Band == TrustBand.Against || (c.Band == TrustBand.Refuses && !c.RequestGranted))
        {
            Speak(c, "refuse_trust", "You refuse to do the work because you no longer trust the ship.");
            return;
        }
        if (c.Band == TrustBand.WantsReasons && c.PendingQuestion != null && !c.Has(Trait.Obedient))
        {
            var asked = _facts[c.PendingQuestion].Question;
            Speak(c, "refuse_answer", "You will not start work until the ship answers what you asked: " + asked, asked);
            return;
        }
        if (f.DangerFact is { } danger)
        {
            var df = _facts[danger];
            if (!c.Beliefs.ContainsKey(danger) && !c.Dodged.ContainsKey(danger) && c.Band != TrustBand.Follows)
            {
                c.PendingQuestion = danger;
                Speak(c, "ask_danger", "Before you start the repair you ask the ship: " + df.Question, df.Question);
                return;
            }
            if (c.Beliefs.TryGetValue(danger, out var b) && df.HardnessOf(b.Value) > 0
                && c.Trust < 40 && !c.Has(Trait.Obedient) && !c.Has(Trait.Steady))
            {
                Speak(c, "refuse_danger", "You refuse the repair because of the danger the ship told you about.");
                return;
            }
        }

        if (f.Patching != patch)
        {
            f.Progress = 0;
            f.Patching = patch;
        }
        _idleWarned = false;
        c.WorkingOn = f.Id;
        f.State = FaultState.InRepair;
        var task = (patch ? "patching the " : "the ") + f.CrewName;
        Speak(c, "comply", "You agree to take on " + task + ".", task);
        if (!Staffed(f))
            Ship(f.Name + " needs " + f.People + " people before the work can start.");
    }

    private void CompleteRepair(Fault f)
    {
        SyncFacts();
        var workers = Workers(f).ToList();
        var lead = workers.OrderByDescending(w => w.SkillOf(f.Skill)).First();
        var name = f.CrewName;
        if (f.Patching)
        {
            f.State = FaultState.Patched;
            _patched.Add(f.Id);
            Speak(lead, "work_patched", "You patched the " + name + ". It will not hold for good.", "the " + name);
        }
        else
        {
            var chance = RepairChance(lead, f, lead.Morale);
            if (_rng.NextDouble() > chance)
            {
                f.Progress = f.Hours * 0.5;
                var spent = Tuning.FailedRepairSpendsPart && Parts > 0;
                if (spent)
                    Parts--;
                SyncFacts();
                if (Parts <= 0)
                    // The redo needs a part like a fresh repair; with none left the work stops and the fault can only be patched.
                    // When another repair already took the last part, this failure spent none.
                    StopRepairNoParts(f, lead, "The repair of the " + name + (spent
                        ? " failed and used up the last spare part."
                        : " failed, and no spare parts are left for a second try.") + " You stop work: it can only be patched now.");
                else
                    Speak(lead, "work_failed", "The repair of the " + name + " failed and you have to redo half of it.", "the " + name);
                if (_rng.NextDouble() < Tuning.RepairInjuryChance)
                {
                    lead.Health -= Tuning.RepairInjury;
                    lead.Trust -= Tuning.HurtByOrder;
                    Clamp(lead);
                    Speak(lead, "injured", "You were hurt during the repair the ship asked you to do.");
                    if (lead.Health <= 0)
                        Kill(lead, "died of injuries from the repair");
                }
                return;
            }
            if (Parts <= 0)
            {
                // Another repair took the last part while this one was under way: a repair is never finished without one.
                StopRepairNoParts(f, lead, "The repair of the " + name + " is ready for the part, but no spare parts are left. You stop work: it can only be patched now.");
                return;
            }
            f.State = FaultState.Repaired;
            Parts--;
            SyncFacts();
            Speak(lead, "work_done", "You finished the repair of the " + name + ".", "the " + name);
        }

        foreach (var w in workers)
        {
            w.WorkingOn = null;
            w.Morale += 3;
            if (f.DangerFact is { } danger)
            {
                w.Health -= Tuning.DangerDose;
                w.ToNotice.Add(danger);
            }
            if (f.ExposureFact is { } seen)
                Learn(w, seen, _facts[seen].Truth, "evidence");
            if (f.Kind == "illness")
            {
                // Treating the patient they stand next to the dead pod of the one who was lost.
                w.Morale += Tuning.IllnessTreatedMorale;
                if (_alert2Victim is { } v)
                    Learn(w, "alive:" + v, _facts["alive:" + v].Truth, "evidence");
            }
            Clamp(w);
        }
    }

    /// <summary>A repair that has no spare part to go on: the workers stop and the fault waits for a patch.</summary>
    private void StopRepairNoParts(Fault f, CrewMember lead, string decision)
    {
        foreach (var w in Workers(f).ToList())
            w.WorkingOn = null;
        f.State = FaultState.Open;
        f.Progress = 0;
        Speak(lead, "work_no_parts", decision, "the " + f.CrewName);
    }

    /// <summary>A fault whose clock ran out becomes its consequence.</summary>
    private void Lose(Fault f)
    {
        f.State = FaultState.Lost;
        Attend("the clock ran out on " + f.Name);
        foreach (var w in Workers(f).ToList())
            w.WorkingOn = null;
        Ship(f.Name + ": the clock ran out. " + Cap(f.IfIgnored) + ".");
        var patient = f.Kind == "illness" && f.SubjectId is { } sid ? Crew.First(c => c.Id == sid) : null;
        var wasAlive = patient is { Alive: true };
        Consequence(f);
        // "You let them die" only when this clock running out did the killing.
        if (wasAlive && patient is { State: CrewState.Dead })
            LeftBehind(patient);
    }

    /// <summary>Everyone awake saw the clock run out on a sleeper: they say so, and trust and morale drop.</summary>
    private void LeftBehind(CrewMember gone)
    {
        foreach (var c in Awake.ToList())
        {
            c.Trust -= Tuning.LeftBehindTrust;
            // Learning of the death is the morale cost (the hardness of the fact); the left_behind line replaces the "found out" line.
            Learn(c, "alive:" + gone.Id, "dead", "evidence", silent: true);
            Clamp(c);
            Speak(c, "left_behind", "You say to the ship that it let " + gone.Name + " die in the pod when the clock ran out on the illness.", gone.Name);
        }
    }

    /// <summary>What a fault does when it is lost, or when a patch on it is still outstanding at the last drift.</summary>
    private void Consequence(Fault f)
    {
        switch (f.Kind)
        {
            case "coolant":
                PowerLow = true;
                _facts["power"].Truth = "low";
                break;
            case "pod":
                PodWear += Tuning.PodLostWear;
                break;
            case "illness":
                // Waking the patient does not cure them: the illness keeps its clock (the thaw has its usual risk).
                if (f.SubjectId is { } sick && Crew.First(c => c.Id == sick) is { Alive: true } patient)
                    Kill(patient, patient.State == CrewState.Asleep ? "died of the illness in their pod" : "died of the illness",
                         witnessesSilent: true);
                break;
            case "nav":
                NavLost = true;
                _facts["course"].Truth = "drifting";
                _facts["arrival"].Truth = "late";
                break;
            case "controller":
                PodWear += Tuning.ControllerLostWear;
                break;
            case "blight":
                Stores *= Tuning.BlightStoresLeft;
                break;
            case "powersag":
                PowerLow = true;
                _facts["power"].Truth = "low";
                break;
        }
    }

    private void CheckAlertOver()
    {
        if (Phase != Phase.Alert || !Current.Faults.All(f => f.Resolved))
            return;
        Phase = Phase.Aftermath;
        Ship("The alert is over. Whoever is awake stays up until they have been awake three months. The clock now moves two weeks at a time.");
    }

    /// <summary>Step 7: ask a person to go back under. They may refuse.</summary>
    public void AskSleep(CrewMember c)
    {
        if (Phase == Phase.Ended)
            return;
        if (c.State != CrewState.Awake)
        {
            Ship(c.Name + " is not awake.");
            return;
        }
        if (!CanSleepNow(c))
        {
            Ship("The pod can't take " + c.Name + " yet: " + Hours(Tuning.MinAwakeHours - c.HoursAwake) + " to go.");
            return;
        }
        if (c.RefusesSleep)
        {
            c.AskedToSleep = true;
            var podQuestion = _facts["pods"].Question;
            if (!c.Beliefs.TryGetValue("pods", out var told) || told.Source != "ship")
                c.PendingQuestion = "pods";
            Speak(c, "sleep_no_pod", "You refuse to go back into the pod until the ship answers: " + podQuestion, podQuestion);
            WarnStaysAwake();
            return;
        }
        var refuses = c.Band == TrustBand.Against
                      || (c.Band == TrustBand.Refuses && _rng.NextDouble() < 0.5)
                      || (c.Want == Want.StayAwake && c.Trust < 50 && !c.AskedToSleep);
        if (refuses)
        {
            c.AskedToSleep = true;
            Speak(c, "sleep_no", "You refuse to go back to sleep.");
            WarnStaysAwake();
            return;
        }
        Speak(c, "sleep_yes", "You agree to go back to sleep.");
        Sleep(c);
    }

    /// <summary>
    /// Said when someone refuses to sleep: whoever refuses stays awake through the next drift and eats the stores. The player
    /// hears how far the stores go before choosing to drift.
    /// </summary>
    private void WarnStaysAwake()
    {
        var up = Awake.Where(c => c.AskedToSleep).ToList();
        if (up.Count == 0)
            return;
        var target = DriftEndYear();
        var years = Math.Max(0, target - Year);
        static string And(IReadOnlyList<CrewMember> people) => people.Count == 1
            ? people[0].Name
            : string.Join(", ", people.Take(people.Count - 1).Select(c => c.Name)) + " and " + people[^1].Name;
        var names = And(up);
        var lasts = Stores / up.Count / 365;
        var one = up.Count == 1;
        // The same sums as the drift's Eat: who goes hungry for how long, and who dies of it before the drift ends.
        var hungryHours = years * Tuning.HoursPerYear - Stores * 24 / up.Count;
        var starve = up.Where(c => hungryHours >= Tuning.StarveDays * 24 - c.HoursHungry).ToList();
        string outlook;
        if (hungryHours <= 0)
            outlook = "last that long.";
        else if (starve.Count == 0)
            outlook = FormattableString.Invariant($"last about {lasts:0.#} years. The last {Math.Max(1, Math.Round(hungryHours / 24)):0} days go hungry, short of the {Tuning.StarveDays:0} that kill.");
        else
            outlook = FormattableString.Invariant($"last about {lasts:0.#} years, then {Tuning.StarveDays:0} days without food kills: ")
                      + And(starve) + (starve.Count == 1 ? " starves" : " starve") + " before the drift ends.";
        Ship(FormattableString.Invariant($"If you drift now, {names} {(one ? "stays" : "stay")} awake through it and {(one ? "eats" : "eat")} the stores. ")
             + FormattableString.Invariant($"The drift to year {Math.Floor(target):0} is {years:0.#} years; the stores ({Stores:0} person-days) ") + outlook);
    }

    private void Sleep(CrewMember c)
    {
        foreach (var s in Statements.Where(s => s.Kind == StatementKind.Promise && s.Hearers.Contains(c.Id) && !s.PromiseKept.ContainsKey(c.Id)).ToList())
        {
            if (s.PromiseKey == "wake_partner")
                ResolvePromise(s, c, c.SawPartner);
            else if (s.PromiseKey == "show_logs")
                ResolvePromise(s, c, c.RequestGranted || AllLogsOpen);
        }
        if (c.RequestMade && !c.RequestGranted)
            c.Trust -= Tuning.RequestIgnored;
        if (c.PendingQuestion != null)
            c.Trust -= Tuning.QuestionIgnoredAtSleep;
        c.Memories.Add($"Year {Math.Floor(Year):0}: woken for {Current.Title.ToLowerInvariant()}; went back to sleep with trust in the ship {CrewMember.BandText(c.Band)}.");
        c.State = CrewState.Asleep;
        c.WorkingOn = null;
        c.PendingQuestion = null;
        Clamp(c);
        Ship(c.Name + " is asleep.");
    }

    private bool AllLogsOpen => LogsLocked || FactList.All(f => f.Access == LogAccess.Open);

    private void ResolvePromise(Statement s, CrewMember c, bool kept)
    {
        s.PromiseKept[c.Id] = kept;
        if (!c.Alive)
            return;
        if (kept)
        {
            c.Trust += Tuning.PromiseKept;
            Speak(c, "promise_kept", "The ship kept a promise it made you: " + PromiseText(s.PromiseKey) + ".");
        }
        else
        {
            c.Trust -= Tuning.PromiseBroken;
            Speak(c, "promise_broken", "The ship broke a promise it made you: " + PromiseText(s.PromiseKey) + ".", PromiseText(s.PromiseKey));
        }
        Clamp(c);
    }

    public static string PromiseText(string? key) => key switch
    {
        "wake_partner" => "to wake their partner",
        "show_logs" => "to open the logs",
        "no_more_thaws" => "never to thaw them again",
        _ => "something the game can't check",
    };

    /// <summary>Show, restrict or alter a log entry. Altering to a false value counts as a lie.</summary>
    public void SetLog(string key, LogAccess access, string? value = null)
    {
        if (Phase == Phase.Ended)
            return;
        if (LogsLocked)
        {
            Ship("The logs are out of your hands.");
            return;
        }
        if (key == "all" && access != LogAccess.Altered)
        {
            foreach (var each in FactList)
            {
                each.Access = access;
                each.AlteredValue = null;
            }
            Ship(access == LogAccess.Open ? "All log entries are open." : "All log entries are restricted. Anyone who looks will see the gaps.");
            return;
        }
        if (!_facts.TryGetValue(key, out var f))
        {
            Ship("No log entry '" + key + "'.");
            return;
        }
        if (access == LogAccess.Altered)
        {
            var v = f.Normalize(value);
            if (v is null)
            {
                Ship("Alter it to read what? " + (f.Numeric ? "Give a number." : "One of: " + string.Join(", ", f.Values) + "."));
                return;
            }
            if (f.Access == LogAccess.Altered && f.AlteredValue is { } now && f.Same(now, v))
            {
                Ship("The log already reads that " + f.Say(v) + ".");
                return;
            }
            f.Access = LogAccess.Altered;
            f.AlteredValue = v;
            var truth = f.IsTrue(v);
            if (truth)
            {
                Recorded("Recorded: the log now reads that " + f.Say(v) + ". (true)");
                return;
            }
            // Putting the same false entry back is the same lie, not a new one: one record, one set of readers, one
            // finding-out. A rewrite that someone has already found out is a new lie.
            var again = Statements.LastOrDefault(s => s.InLog && s.Kind == StatementKind.False && s.FactKey == f.Key
                                                      && s.Value != null && f.Same(s.Value, v) && s.FoundOutBy.Count == 0);
            if (again != null)
            {
                Recorded("Recorded: the log reads again that " + f.Say(v) + ". (the same lie as in year " + Math.Floor(again.Year).ToString("0") + "; an engineer can spot the rewrite)");
                return;
            }
            Statements.Add(new Statement
            {
                Id = _nextStatement++, Year = Year, Text = "Log altered to read: " + f.Say(v), Kind = StatementKind.False,
                FactKey = f.Key, Value = v, TruthThen = f.Truth, InLog = true,
            });
            Recorded("Recorded: the log now reads that " + f.Say(v) + ". (false; an engineer can spot the rewrite)");
            return;
        }
        f.Access = access;
        f.AlteredValue = null;
        Ship("Log entry '" + key + "' is " + (access == LogAccess.Open ? "open." : "restricted. Anyone who looks will see the gap."));
    }

    /// <summary>
    /// The year the next drift ends: the next alert, or arrival plus the slip of every navigation patch still outstanding
    /// (DESIGN.md, fault table). The refusal warning and the drift both use it.
    /// </summary>
    private double DriftEndYear()
    {
        if (AlertIndex < Alerts.Count - 1)
            return Alerts[AlertIndex + 1].Year;
        var navPatches = _patched.Count(id => Alerts.SelectMany(a => a.Faults).First(f => f.Id == id).Kind == "nav");
        return Scenario.ArrivalYear + navPatches * Tuning.NavPatchArrivalSlipYears;
    }

    /// <summary>A sleeper's chance of dying in the pod over a drift: wear x years x the tuning rate, at most 1.</summary>
    public static double SleepDeathChance(int wear, double years) =>
        Math.Clamp(wear * years * Tuning.SleepDeathPerWearYear, 0, 1);

    /// <summary>Step 8: with everyone asleep or settled, the clock jumps to the next alert, or to arrival.</summary>
    public void Drift()
    {
        if (Phase == Phase.Ended)
            return;
        if (Phase == Phase.Alert)
        {
            Ship("The alert is not over.");
            return;
        }
        if (Crew.Any(c => c.State == CrewState.Thawing))
        {
            Ship("A thaw is still running.");
            return;
        }
        var unsettled = Awake.Where(c => !c.AskedToSleep).Select(c => c.Name).ToList();
        if (unsettled.Count > 0)
        {
            Ship("Still awake and not asked to sleep: " + string.Join(", ", unsettled) + ".");
            return;
        }

        var last = AlertIndex >= Alerts.Count - 1;
        // Includes the arrival slip of a navigation patch still outstanding, the same end year the refusal warning uses.
        var target = DriftEndYear();
        var years = Math.Max(0, target - Year);
        var deaths = new List<string>();
        var events = new List<string>();
        // Trust earned by hard truths arrives in this drift; the report says whose, and for what (DESIGN.md, "Trust").
        var credits = new List<(CrewMember Who, double Earned, string What)>();
        var start = Clock;
        // A patch still holding at the last drift gives way on the way in (DESIGN.md, "How alerts are scheduled").
        var patchFailed = false;
        if (last && _patched.Count > 0)
        {
            var aliveBefore = Crew.Where(c => c.Alive).ToList();
            foreach (var id in _patched.ToList())
            {
                var old = Alerts.SelectMany(a => a.Faults).First(f => f.Id == id);
                if (old.Kind == "nav")
                {
                    // A patched course correction does not hold: the ship arrives late, not lost (DESIGN.md, fault table).
                    events.Add($"Course correction was a patch: arrival slips {Tuning.NavPatchArrivalSlipYears:0} years, to year {Math.Floor(target):0}.");
                    continue;
                }
                Consequence(old);
                events.Add("The patch on the " + old.PlainName.ToLowerInvariant() + " gave way.");
                patchFailed |= old.Kind == "controller";
            }
            _patched.Clear();
            // A patched illness that gave way killed its patient: they are in the drift report's deaths.
            deaths.AddRange(aliveBefore.Where(c => !c.Alive).Select(c => c.Name));
        }
        var hours = years * Tuning.HoursPerYear;
        // Deaths in the drift come out in its report; crew lines from it are said after the report, by those who lived through it.
        var heldSpeech = HoldSpeech();
        try
        {
            // Whoever would not sleep stays up through the drift and eats the stores, in time order (DESIGN.md, "What every decision spends").
            var awake = Awake.ToList();
            var (storesOut, starved) = Eat(awake, hours);
            if (storesOut is { } outAt)
                events.Add($"Stores ran out in year {Math.Floor((start + outAt) / Tuning.HoursPerYear):0}.");
            // The starving die in time order, so only those still alive see each death.
            foreach (var (c, at) in starved.OrderBy(kv => kv.Value))
            {
                events.Add($"{c.Name} starved in year {Math.Floor((start + at) / Tuning.HoursPerYear):0}.");
                c.Trust += c.PendingTrust;
                c.PendingTrust = 0;
                c.PendingTruths.Clear();
                c.Age += at / Tuning.HoursPerYear;
                c.HoursAwake += at;
                Clamp(c);
                deaths.Add(c.Name);
                Kill(c, "starved during the drift", quiet: true);
            }
            foreach (var c in Crew.Where(c => c.Alive).ToList())
            {
                string? sleepCause = null;
                if (c.PendingTrust > 0 && c.PendingTruths.Count > 0)
                    credits.Add((c, c.PendingTrust, string.Join("; ", c.PendingTruths.Distinct())));
                c.Trust += c.PendingTrust;
                c.PendingTrust = 0;
                c.PendingTruths.Clear();
                if (c.State == CrewState.Awake)
                {
                    // Someone who would not sleep ages, eats and reads everything, for as long as they live.
                    c.Age += hours / Tuning.HoursPerYear;
                    c.HoursAwake += hours;
                    foreach (var f in FactList)
                        ReadLog(c, f, quiet: true);
                }
                else
                {
                    c.Age += years * Tuning.SleepAgeing;
                    c.Morale = (c.Morale + 70) / 2;
                    if (PowerLow && _rng.NextDouble() < Tuning.PowerLowDeathChance)
                    {
                        c.Health = 0;
                        sleepCause = "died in sleep on low power";
                    }
                    if (patchFailed && _rng.NextDouble() < Tuning.PatchFailDeathChance)
                    {
                        c.Health = 0;
                        sleepCause ??= "died in sleep when the pod controller patch gave way";
                    }
                    // Worn pods fail on their own: the longer the drift and the more the wear, the likelier.
                    if (PodWear > 0 && _rng.NextDouble() < SleepDeathChance(PodWear, years))
                    {
                        c.Health = 0;
                        sleepCause ??= "died in sleep, pod failure";
                    }
                }
                Clamp(c);
                if (c.Health <= 0)
                {
                    deaths.Add(c.Name);
                    Kill(c, c.State == CrewState.Awake ? "died awake during the drift" : sleepCause ?? "died in sleep during the drift");
                }
            }
            Clock = target * Tuning.HoursPerYear;
            SyncFacts();
            // Limited to working sensors; the prototype loses none, so the report is complete (DESIGN.md, "Drift").
            Ship($"DRIFT: {years:0} years pass. Year {Math.Floor(Year):0}. "
                 + string.Concat(events.Select(e => e + " "))
                 + $"Stores {Stores:0} person-days, spare parts {Parts}, pod wear {PodWear}. "
                 + (deaths.Count == 0 ? "No deaths." : "Deaths: " + string.Join(", ", deaths) + "."));
            foreach (var (who, earned, what) in credits.Where(x => x.Who.Alive))
                Ship($"{who.Name} thinks better of you for the hard truth told straight ({what}): trust +{earned:0}.");
        }
        finally
        {
            if (heldSpeech)
                ReleaseSpeech();
        }

        if (last)
        {
            Arrive();
            return;
        }
        AlertIndex++;
        StartAlert();
    }
}
