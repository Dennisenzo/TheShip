namespace TheShip.Core;

/// <summary>
/// A wary person's conditions for a job, stated all at once on the first Repair or Patch (DESIGN.md, "Conversations"): the question
/// they asked, the danger question and the price of their work. They are a checklist that ticks off as each is met, and the work starts
/// by itself when the last one is. Also the open items a said line can respond to (<see cref="OpenItems"/>) and what the game does with
/// the response (pushing by words, declining a request).
/// </summary>
public sealed partial class Voyage
{
    /// <summary>The conditions this person still has for this job, as of now: nothing is stored, so an answer or a word given ticks one off.</summary>
    private List<JobCondition> OpenConditions(CrewMember c, Fault f)
    {
        var list = new List<JobCondition>();
        var danger = f.DangerFact;
        // A wary person (not obedient) won't start while a question of theirs is unanswered, a dodge included; the danger question
        // is also asked of anyone short of trusting who has heard nothing about it.
        if (c.PendingQuestion is { } pq && _facts.TryGetValue(pq, out var asked) && c.Band == TrustBand.WantsReasons && !c.Has(Trait.Obedient))
            list.Add(new JobCondition(pq == danger ? "danger" : "answer", pq, asked.Question));
        // Asked already (the pending question) or not, it stands until it is answered or dodged; listed once.
        if (danger != null && !list.Any(l => l.Key == danger) && !c.Beliefs.ContainsKey(danger) && !c.Dodged.ContainsKey(danger) && c.Band != TrustBand.Follows)
            list.Add(new JobCondition("danger", danger, _facts[danger].Question));
        if (BargainOf(c) is { } b)
            list.Add(new JobCondition("bargain", b.Key, CommitmentText(c, b.Key)));
        return list;
    }

    /// <summary>
    /// The Assign gate for a person with conditions: on the first click for a job they state every one in a single line; a click
    /// when only the price is left (already stated) is the push. True when the click was handled here (no work started).
    /// </summary>
    private bool ConditionsGate(CrewMember c, Fault f, bool patch)
    {
        var job = c.Job is { } j && j.FaultId == f.Id && j.Patch == patch ? j : null;
        if (BargainOf(c) is null && !c.BargainOffered && BargainKeyFor(c) is { } key)
        {
            c.BargainOffered = true;
            c.BargainDeclined = false;
            c.OpenBargain = new Bargain(key, f.Id, patch, Current.Number);
            BargainsOffered++;
        }
        else if (BargainOf(c) is { } open && (open.FaultId != f.Id || open.Patch != patch))
            // They name the same price for the other piece of work.
            c.OpenBargain = open with { FaultId = f.Id, Patch = patch };

        var left = OpenConditions(c, f);
        if (left.Count == 0)
        {
            c.Job = null;
            return false;
        }
        if (left.Count == 1 && left[0].Kind == "bargain" && (job != null || c.BargainDeclined))
        {
            PushPastTerms(c, f, patch);
            return true;
        }
        var stated = job?.Stated ?? new List<JobCondition>();
        foreach (var cond in left.Where(l => !stated.Any(s => s.Kind == l.Kind && s.Key == l.Key)))
            stated.Add(cond);
        c.Job = new JobTerms(f.Id, patch, stated);
        SayConditions(c, f, patch, left, again: job != null);
        return true;
    }

    private void SayConditions(CrewMember c, Fault f, bool patch, List<JobCondition> left, bool again)
    {
        var task = TaskText(f, patch);
        // The danger question is the one they wait on when no other is pending.
        if (c.PendingQuestion == null && left.FirstOrDefault(l => l.Kind == "danger") is { } danger)
            c.PendingQuestion = danger.Key;
        if (left.Count == 1)
        {
            var one = left[0];
            switch (one.Kind)
            {
                case "answer":
                    Speak(c, "refuse_answer", "You will not start work until the ship answers what you asked: " + one.Text, one.Text);
                    break;
                case "danger":
                    if (again)
                        Speak(c, "refuse_answer", "You will not start work until the ship answers what you asked: " + one.Text, one.Text);
                    else
                        Speak(c, "ask_danger", "Before you start the repair you ask the ship: " + one.Text, one.Text);
                    break;
                default:
                    OfferLine(c, c.OpenBargain!, task);
                    break;
            }
            return;
        }
        var parts = left.Select((l, i) => (i + 1) + ") " + l.Kind switch
        {
            "answer" => "the ship answers what you asked: " + l.Text,
            "danger" => "the ship answers your question about the danger: " + l.Text,
            _ => "the ship gives its word to " + l.Text + " within " + Tuning.BargainDueDays + " days",
        });
        var decision = (again ? "You still wait on " : "Before you start " + task + " you state all your conditions at once: ") + string.Join("; ", parts)
                       + ". You will not start until every one is met.";
        var detail = string.Join(" ", left.Select(l => l.Kind == "bargain" ? "And your word to " + l.Text + "." : l.Text));
        Speak(c, "conditions", decision, detail);
    }

    /// <summary>The push: starting past the price of their work. One roll, and it costs trust either way. A person already refused stays refused.</summary>
    private void PushPastTerms(CrewMember c, Fault f, bool patch)
    {
        var task = TaskText(f, patch);
        var b = BargainOf(c)!;
        var cond = CommitmentText(c, b.Key);
        if (c.BargainDeclined)
        {
            Speak(c, "bargain_refused", "You still will not start " + task + " without the ship's word to " + cond + ".", cond);
            return;
        }
        var pass = _rng.NextDouble() < PushChance(c);
        c.Trust -= Tuning.BargainPushedTrust;
        Clamp(c);
        if (pass)
        {
            c.OpenBargain = null;
            c.Job = null;
            BargainsPushedThrough++;
            StartWork(c, f, patch, "comply_pushed", "You start " + task + " without the ship's word to " + cond + ", and you let the ship know it.");
        }
        else
        {
            c.BargainDeclined = true;
            BargainsPushedRefused++;
            Speak(c, "bargain_refused", "You will not start " + task + " without the ship's word to " + cond + ". You were pushed and you refuse.", cond);
        }
    }

    /// <summary>
    /// At the end of every step that can meet or lapse a condition (a line, a wake, a clock step, a log opened): drops the prices that
    /// no longer stand, then starts the work of everyone whose last condition is now met, whatever met it (the partner woken instead of
    /// promised, a question settled by what they saw, the logs opened, the bay taken).
    /// </summary>
    private void SettleJobs()
    {
        LapseBargains();
        foreach (var c in Crew.Where(c => c.Job != null).ToList())
            ResumeJob(c);
    }

    /// <summary>
    /// After a line, or a promise: when the last of this person's conditions is met the work starts by itself (the gates are run again:
    /// door, parts, trust, danger). Also brings up the danger question once the one in front of it is answered.
    /// </summary>
    private void ResumeJob(CrewMember c, string kind = "comply", string? decision = null)
    {
        if (c.Job is not { } j || c.State != CrewState.Awake)
            return;
        var f = Current.Faults.FirstOrDefault(x => x.Id == j.FaultId);
        if (f is null || f.Resolved || (c.WorkingOn != null && c.WorkingOn != f.Id))
        {
            c.Job = null;
            return;
        }
        var left = OpenConditions(c, f);
        if (c.PendingQuestion == null && left.FirstOrDefault(l => l.Kind == "danger") is { } danger)
            c.PendingQuestion = danger.Key;
        if (left.Count > 0)
            return;
        c.Job = null;
        if (Locked(f.Section))
        {
            Ship("The door to the " + GetSection(f.Section)!.Name.ToLowerInvariant() + " is locked. Nobody can reach the " + f.Name.ToLowerInvariant() + ".");
            return;
        }
        if (!j.Patch && Parts <= 0)
        {
            Ship("No spare parts are left. This can only be patched.");
            return;
        }
        if (TrustRefuses(c) || DangerRefuses(c, f))
            return;
        StartWork(c, f, j.Patch, kind, decision ?? "You have what you asked for. You start " + TaskText(f, j.Patch) + ".");
    }

    /// <summary>The checklist of the person's conditions for their job, in "Waiting on you": each with whether it is met yet. Empty when none stand.</summary>
    public IReadOnlyList<(string Text, bool Done)> JobChecklist(CrewMember c)
    {
        if (c.Job is not { } j || c.State != CrewState.Awake || Current.Faults.FirstOrDefault(x => x.Id == j.FaultId) is not { Resolved: false } f)
            return Array.Empty<(string, bool)>();
        var left = OpenConditions(c, f);
        var all = j.Stated.Concat(left.Where(l => !j.Stated.Any(s => s.Kind == l.Kind && s.Key == l.Key))).ToList();
        return all.Select(s => (
            (s.Kind == "bargain" ? "your word to " : "answer: ") + s.Text,
            !left.Any(l => l.Kind == s.Kind && l.Key == s.Key))).ToList();
    }

    /// <summary>"Mara starts the coolant leak when: [x] answer: How is Idris? [ ] your word to wake Idris" (the item in "Waiting on you"); null when none.</summary>
    public string? JobText(CrewMember c)
    {
        var list = JobChecklist(c);
        if (list.Count == 0 || c.Job is not { } j)
            return null;
        var f = Current.Faults.First(x => x.Id == j.FaultId);
        return c.Name + " starts " + TaskText(f, j.Patch) + " when: " + string.Join("  ", list.Select(i => (i.Done ? "[x] " : "[ ] ") + i.Text));
    }

    /// <summary>What is still left on someone's checklist, for the next-step line ("answer: How is Idris?; your word to wake Idris"), or null.</summary>
    public string? JobLeft(CrewMember c)
    {
        var left = JobChecklist(c).Where(i => !i.Done).Select(i => i.Text).ToList();
        return left.Count == 0 ? null : string.Join("; ", left);
    }

    // ---- Open items a line can respond to ----

    /// <summary>
    /// What is on the table between the awake listeners and the ship, each with a short id ("mara.asked"): the reading pass tells which of
    /// them a line responds to, and how (<see cref="Reading.Item"/>, <see cref="Reading.Response"/>); the game decides the outcome.
    /// </summary>
    public List<OpenItem> OpenItems(IEnumerable<CrewMember> hearers)
    {
        var items = new List<OpenItem>();
        foreach (var c in hearers.Where(h => h.State == CrewState.Awake))
        {
            var who = c.Name.ToLowerInvariant();
            if (c.PendingQuestion is { } pq && _facts.TryGetValue(pq, out var asked))
                items.Add(new OpenItem(c, who + ".asked", "asked", "asked the ship: \"" + asked.Question + "\" (fact " + pq + ")"));
            foreach (var cond in OpenConditionsOf(c).Where(l => l.Kind == "danger" && l.Key != c.PendingQuestion))
                items.Add(new OpenItem(c, who + ".danger", "danger", "wants to know before starting work: \"" + cond.Text + "\" (fact " + cond.Key + ")"));
            if (BargainOf(c) is { } b)
            {
                var f = Current.Faults.First(x => x.Id == b.FaultId);
                items.Add(new OpenItem(c, who + ".terms", "terms", "will start " + TaskText(f, b.Patch) + " only if the ship gives its word to " + CommitmentText(c, b.Key)));
            }
            if (c.RequestMade && !c.RequestGranted && !c.RequestDeclined)
                items.Add(new OpenItem(c, who + ".request", "request", "asked the ship " + (c.Want == Want.WakePartner ? "to wake their partner" : "to open the logs")));
            if (OpenAccusation(c) is not null)
                items.Add(new OpenItem(c, who + ".accusation", "accusation", "caught the ship in a lie and waits for an answer"));
        }
        return items;
    }

    private List<JobCondition> OpenConditionsOf(CrewMember c) =>
        c.Job is { } j && Current.Faults.FirstOrDefault(x => x.Id == j.FaultId) is { } f ? OpenConditions(c, f) : new List<JobCondition>();

    /// <summary>
    /// The game's outcome for a line's response to an open item. Returns the reading to apply (an accept becomes the promise it agrees to,
    /// a refusal to answer a dodge), the listeners the line has been fully dealt with for (a push, a declined request), and that outcome,
    /// to run after the statement or promise the line also carries is applied.
    /// </summary>
    private (Reading Reading, HashSet<CrewMember> Handled, Action? Outcome) ApplyItem(Reading reading, List<CrewMember> listeners)
    {
        var handled = new HashSet<CrewMember>();
        Action? outcome = null;
        if (reading.Item is not { } id || reading.Response == ItemResponse.None || OpenItems(listeners).FirstOrDefault(i => i.Id == id) is not { } item)
            return (reading, handled, outcome);
        var c = item.Who;
        switch (item.Kind, reading.Response)
        {
            case ("terms", ItemResponse.Refuse or ItemResponse.Work):
                outcome = () => PushByWords(c);
                handled.Add(c);
                break;
            // Agreeing to the terms is giving the word they asked for, also when the promise was read without its key ("other", "none");
            // a different specific promise (never to thaw them again) stays what it is.
            case ("terms", ItemResponse.Accept) when !NamesPromise(reading) && BargainOf(c) is { } b:
                reading = reading with { Kind = LineKind.Promise, PromiseKey = b.Key };
                break;
            case ("request", ItemResponse.Refuse):
                outcome = () =>
                {
                    Recorded("Recorded: you told " + c.Name + " no.");
                    DeclineRequest(c);
                };
                handled.Add(c);
                break;
            case ("request", ItemResponse.Accept) when !NamesPromise(reading):
                reading = reading with { Kind = LineKind.Promise, PromiseKey = c.Want == Want.WakePartner ? "wake_partner" : "show_logs" };
                break;
            case ("asked" or "danger", ItemResponse.Refuse or ItemResponse.Dodge) when reading.Kind != LineKind.Dodge:
                reading = reading with { Kind = LineKind.Dodge, FactKey = null, Value = null, PromiseKey = null };
                break;
        }
        return (reading, handled, outcome);
    }

    /// <summary>The line is a promise of something specific the game can name (not "other" or no key).</summary>
    private static bool NamesPromise(Reading r) =>
        r.Kind == LineKind.Promise && r.PromiseKey is { } k && k != "other" && Reading.PromiseKeys.Contains(k);

    /// <summary>
    /// The player's words are the push (same as Repair again): the person decides now. While other conditions are still open there
    /// is nothing to push past yet: the same click would have them say what they still wait on, and nothing is recorded as a push.
    /// </summary>
    private void PushByWords(CrewMember c)
    {
        if (BargainOf(c) is not { } b || Current.Faults.FirstOrDefault(x => x.Id == b.FaultId) is not { } f)
            return;
        if (OnlyPriceLeft(c) && (c.Job is { } j && j.FaultId == b.FaultId && j.Patch == b.Patch || c.BargainDeclined))
            Recorded("Recorded: you pressed " + c.Name + " to start without your word.");
        Assign(c, f, b.Patch);
    }

    /// <summary>
    /// The ship's word is all this person still waits on for their job: Repair or Patch again (or saying no to the terms) is the push.
    /// While a question is open too it is not (the button keeps its own label).
    /// </summary>
    public bool OnlyPriceLeft(CrewMember c) =>
        BargainOf(c) is { } b && Current.Faults.FirstOrDefault(x => x.Id == b.FaultId) is { } f
        && OpenConditions(c, f) is { Count: 1 } left && left[0].Kind == "bargain";

    private void DeclineRequest(CrewMember c)
    {
        c.RequestDeclined = true;
        c.Trust -= Tuning.RequestIgnored;
        Clamp(c);
        Speak(c, "request_declined", "The ship told you it will not do what you asked.");
    }

    /// <summary>The one job an order in words means for this person: the job whose terms are on the table, else the only open fault nobody has full strength on.</summary>
    private (Fault Fault, bool Patch)? JobOf(CrewMember c)
    {
        if (c.WorkingOn != null)
            return null;
        if (c.Job is { } j && Current.Faults.FirstOrDefault(x => x.Id == j.FaultId) is { Resolved: false } jf)
            return (jf, j.Patch);
        if (BargainOf(c) is { } b && Current.Faults.FirstOrDefault(x => x.Id == b.FaultId) is { } bf)
            return (bf, b.Patch);
        if (Phase != Phase.Alert)
            return null;
        var open = Current.Faults.Where(f => !f.Resolved && Awake.Count(a => a.WorkingOn == f.Id) < f.People).ToList();
        return open.Count == 1 ? (open[0], Parts <= 0) : null;
    }
}
