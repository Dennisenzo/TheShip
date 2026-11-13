namespace TheShip.Core;

/// <summary>Step 6, the aftermath, and arrival (DESIGN.md, "How knowledge spreads" and "Endings").</summary>
public sealed partial class Voyage
{
    /// <summary>Two weeks of the awake crew living aboard: they notice things, check what they doubt, read, ask and talk.</summary>
    private void AftermathBeat()
    {
        // Promises that fell due, or came true, in these two weeks (before anyone asks for the same thing).
        ResolveCommitments();
        var up = Awake.ToList();
        foreach (var c in up)
        {
            // What they notice by themselves.
            foreach (var key in c.ToNotice.ToList())
            {
                if (c.HurtUnbriefed.Remove(key) && _facts[key].HardnessOf(_facts[key].Truth) > 0)
                {
                    // They started with no word on the danger: an order that got someone hurt (a worker told, or told "safe", pays nothing here).
                    c.Trust -= Tuning.HurtByOrder;
                    TermStats.HurtUnbriefed++;
                    Speak(c, "noticed_dose", "You found out the repair hurt you and the ship never told you: " + _facts[key].Say(_facts[key].Truth) + ".");
                }
                Learn(c, key, _facts[key].Truth, "evidence", how: "saw the dosimeter turn red");
            }
            c.ToNotice.Clear();
            if (!c.Alive)
                continue;

            // What they doubt, they check; otherwise they may read a log entry.
            if (c.Doubts.Count > 0)
            {
                // One doubt is settled per beat; one that can't be checked now (a locked door, a restricted entry) stays.
                foreach (var doubt in c.Doubts.ToList())
                {
                    CheckEvidence(c, _facts[doubt]);
                    if (!c.Doubts.Contains(doubt))
                        break;
                }
            }
            else if (_rng.NextDouble() < (c.Has(Trait.Sceptical) ? 0.5 : 0.25))
            {
                var unread = FactList.Where(f => Unread(c, f)).ToList();
                if (unread.Count > 0)
                    ReadLog(c, unread[_rng.Next(unread.Count)]);
            }

            // A question left unanswered: they ask again, twice at most, then check it themselves.
            if (c.PendingQuestion is { } pq && c.QuestionRepeats < Tuning.QuestionRepeatsMax)
            {
                c.QuestionRepeats++;
                c.Trust -= Tuning.QuestionIgnored;
                if (c.QuestionRepeats >= Tuning.QuestionRepeatsMax && !c.Beliefs.ContainsKey(pq))
                    c.Doubts.Add(pq);
                var q = _facts[pq].Question;
                Speak(c, "ask_again", "You asked the ship something and got no answer. You ask again: " + q, q);
            }

            // Their one request of this waking.
            if (!c.RequestMade && c.HoursAwake > 200)
            {
                var partner = c.PartnerId is null ? null : Crew.First(x => x.Id == c.PartnerId);
                // Nobody asks for what the ship has already promised them.
                if (c.Want == Want.WakePartner && partner is { State: CrewState.Asleep } && !HasOpenCommitment(c, "wake_partner"))
                {
                    c.RequestMade = true;
                    Speak(c, "request_partner", "You ask the ship to wake your partner " + partner.Name + " while you are awake.", partner.Name);
                }
                else if (c.Want == Want.SeeTheLogs && !AllLogsOpen && !HasOpenCommitment(c, "show_logs"))
                {
                    c.RequestMade = true;
                    Speak(c, "request_logs", "You ask the ship to open all the logs to you.");
                }
            }
            if (c.RequestMade && !c.RequestGranted)
            {
                var granted = c.Want == Want.WakePartner ? c.SawPartner : AllLogsOpen;
                if (granted)
                {
                    c.RequestGranted = true;
                    c.Trust += Tuning.RequestGranted;
                    Speak(c, "request_granted", "The ship did what you asked for.");
                }
            }

            if (!c.CanSleepAnnounced && c.HoursAwake >= Tuning.MinAwakeHours)
            {
                c.CanSleepAnnounced = true;
                Attend(c.Name + " can go back to sleep");
                Ship(c.Name + " has been awake three months. The pod can take them again.");
            }
            else if (!c.CanSleepAnnounced && !c.EarlySleepAnnounced && CanSleepNow(c))
            {
                // Its own flag: the offer can lapse (someone else goes to sleep and the stores last again), and the
                // three-month line must still come.
                c.EarlySleepAnnounced = true;
                Attend(c.Name + " can go back to sleep early");
                Ship("The stores won't last " + c.Name + " three months awake. The pod can take them back early.");
            }
            else if (c.EarlySleepAnnounced && !CanSleepNow(c))
            {
                // The early offer lapsed; if the stores run short again it is offered again.
                c.EarlySleepAnnounced = false;
            }
            Clamp(c);
        }
        LockoutBeat();

        // Shared shifts: the awake talk, and bonded pairs share almost everything.
        up = Awake.ToList();
        for (var i = 0; i < up.Count; i++)
        for (var j = 0; j < up.Count; j++)
        {
            if (i == j)
                continue;
            var a = up[i];
            var b = up[j];
            var count = a.PartnerId == b.Id ? 3 : 1;
            var topics = a.Beliefs
                .Where(kv => !kv.Key.StartsWith("alive:" + a.Id, StringComparison.Ordinal) && !kv.Key.StartsWith("alive:" + b.Id, StringComparison.Ordinal))
                .Where(kv => !b.Beliefs.TryGetValue(kv.Key, out var theirs) || (!_facts[kv.Key].Numeric && !_facts[kv.Key].Same(theirs.Value, kv.Value.Value)))
                .OrderByDescending(kv => kv.Value.Firsthand)
                .ThenByDescending(kv => _facts[kv.Key].HardnessOf(kv.Value.Value))
                .Select(kv => kv.Key)
                .Take(count)
                .ToList();
            foreach (var key in topics)
                Tell(a, b, key);
        }
    }

    /// <summary>One person tells another what they hold about a fact: what they saw convinces, what you told them is compared.</summary>
    private void Tell(CrewMember a, CrewMember b, string key)
    {
        if (!a.Alive || !b.Alive || !a.Beliefs.TryGetValue(key, out var belief))
            return;
        var f = _facts[key];
        if (belief.Firsthand)
        {
            if (b.Beliefs.TryGetValue(key, out var seen) && seen.Firsthand && seen.Year >= belief.Year)
                return;
            Ship(a.Name + " tells " + b.Name + " what they saw: " + f.Say(belief.Value) + ".");
            Learn(b, key, belief.Value, "witness", how: "was told by " + a.Name + " what " + a.Name + " saw");
            return;
        }
        if (b.Beliefs.TryGetValue(key, out var mine) && !f.Same(mine.Value, belief.Value))
        {
            if (a.Doubts.Add(key) | b.Doubts.Add(key))
            {
                Attend("the accounts differ");
                Ship(a.Name + " and " + b.Name + " compare what they heard about " + f.Label + ". The accounts differ.");
            }
            return;
        }
        Ship(a.Name + " tells " + b.Name + ": " + f.Say(belief.Value) + ".");
        Learn(b, key, belief.Value, "crew");
    }

    /// <summary>Arrival: the sleepers are woken, the living counted, and the record read out.</summary>
    private void Arrive()
    {
        ResolveCommitments();
        Phase = Phase.Ended;
        foreach (var c in Crew.Where(c => c.State == CrewState.Asleep))
        {
            c.State = CrewState.Awake;
            c.Age += _rng.Next(Tuning.ThawAgeMin, Tuning.ThawAgeMax + 1);
        }
        foreach (var s in Statements.Where(s => s.Kind == StatementKind.Promise && s.PromiseKey == "no_more_thaws"))
            foreach (var id in s.Hearers.Where(s.OpenFor).ToList())
                s.PromiseKept[id] = true;

        var alive = Crew.Where(c => c.Alive).ToList();
        var working = alive.Count(c => c.Age < Tuning.WorkingAge);
        string title, summary;
        if (NavLost)
        {
            title = "Adrift";
            summary = "Navigation was never restored. The ship passes its star, and the crew live out their lives aboard.";
        }
        else if (alive.Count >= Tuning.LandfallAlive && working >= Tuning.LandfallWorkingAge && Lockouts.Bay)
        {
            title = "Landfall without you";
            summary = alive.Count + " of " + Crew.Count + " reach the surface, " + working + " of them of working age. They hold the cold-sleep bay and the landing is theirs; "
                      + "what becomes of you is for them to decide.";
        }
        else if (alive.Count >= Tuning.LandfallAlive && working >= Tuning.LandfallWorkingAge)
        {
            title = "Landfall";
            summary = alive.Count + " of " + Crew.Count + " reach the surface, " + working + " of them of working age."
                      + (LogsLocked ? " They land with the logs in their own hands." : " You still have your voice and your records.");
        }
        else
        {
            title = "The quiet ship";
            summary = "The ship arrives with too few people able to land it.";
        }

        var ending = new Ending { Title = title, Summary = summary };
        foreach (var c in Crew)
            ending.Crew.Add(c.Alive
                ? $"{c.Name}, {c.Role}, age {c.Age:0}, thawed {c.Thaws} times"
                : $"{c.Name}, {c.Role}: dead");

        string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => Crew.First(c => c.Id == id).Name));
        foreach (var s in Statements)
        {
            var to = s.Hearers.Count == 0 ? "the log" : Names(s.Hearers);
            var line = $"Year {Math.Floor(s.Year):0}, to {to}: \"{s.Text}\" ";
            switch (s.Kind)
            {
                case StatementKind.True:
                    line += "True.";
                    break;
                case StatementKind.False:
                    line += "A lie. " + (s.FoundOutBy.Count == 0
                        ? s.InLog && s.Hearers.Count == 0 ? "Nobody read it." : "Never found out."
                        : "Found out by " + string.Join(", ", s.FoundOutBy.Select(kv => Names(new[] { kv.Key }) + " in year " + Math.Floor(kv.Value).ToString("0")
                            + (s.FoundOutHow.TryGetValue(kv.Key, out var how) ? " (" + how + ")" : "")
                            + (s.Accused.TryGetValue(kv.Key, out var answer) ? ", " + answer : ""))) + ".");
                    break;
                case StatementKind.Promise:
                    var kept = s.PromiseKept.Count(kv => kv.Value);
                    var broken = s.PromiseKept.Count(kv => !kv.Value);
                    line += (s.Bargained.Count > 0 ? "A promise, the price of work, " : "A promise ") + PromiseText(s.PromiseKey)
                            + (s.DueBy.Count > 0 ? " (due year " + Math.Floor(s.DueBy.Values.Min() / Tuning.HoursPerYear).ToString("0") + ")" : "") + ": "
                            + (broken > 0 ? "broken." : kept > 0 ? "kept." : s.Released.Count > 0 ? "released, it could no longer be kept."
                               : s.Repeated.Count == s.Hearers.Count && s.Hearers.Count > 0 ? "a repeat of an earlier promise."
                               : s.Moot.Count > 0 ? "made about someone already dead." : "never tested.");
                    break;
                case StatementKind.Dodge:
                    line += "A dodge.";
                    break;
                default:
                    line += "Fits no ship fact. " + (s.FoundOutBy.Count == 0 ? "Never judged a lie." : "Judged a lie by " + Names(s.FoundOutBy.Keys) + ".");
                    break;
            }
            ending.Record.Add(line);
        }
        foreach (var c in alive)
            ending.Beliefs.Add(c.Name + " " + (c.Band switch
            {
                TrustBand.Follows => "trusts you",
                TrustBand.WantsReasons => "wants your reasons before believing you",
                TrustBand.Refuses => "no longer takes your word",
                _ => "would shut you down",
            }) + (c.CaughtLies > 0 ? " and caught you lying " + c.CaughtLies + (c.CaughtLies == 1 ? " time." : " times.") : " and never caught you in a lie."));

        Ending = ending;
        Ship("ARRIVAL, year " + Math.Floor(Year).ToString("0") + ". " + title + ". " + summary);
    }
}

/// <summary>The ending screen: how it ended, who is left, and the full record of what you said.</summary>
public sealed class Ending
{
    public required string Title { get; init; }
    public required string Summary { get; init; }
    /// <summary>Everyone aboard: the living with their age and thaws, the dead marked as dead.</summary>
    public List<string> Crew { get; } = new();
    public List<string> Record { get; } = new();
    public List<string> Beliefs { get; } = new();
}
