namespace TheShip.Core;

/// <summary>One thing the ship owes a person: a promise it made, with its due date and where it stands.</summary>
/// <param name="Origin">How it came to be: "promise" (said on its own); later "bargain" (the price of a person's work).</param>
/// <param name="State">"open", "kept", "broken" or "released" (it could no longer be kept).</param>
/// <param name="DueBy">The clock hour it falls due, or null for a standing promise (never to thaw them again).</param>
public sealed record LedgerEntry(CrewMember Person, int StatementId, string Key, string Origin, string Text, double? DueBy, string State);

/// <summary>The promise ledger: what the ship owes whom, by when, and how a promise ends (DESIGN.md, "Conversations").</summary>
public sealed partial class Voyage
{
    private static bool Checkable(string? key) => key is "wake_partner" or "show_logs";

    /// <summary>Every promise the ship has made that the game can check or hold it to, per hearer, oldest first.</summary>
    public IReadOnlyList<LedgerEntry> Ledger()
    {
        var list = new List<LedgerEntry>();
        foreach (var s in Statements.Where(s => s.Kind == StatementKind.Promise && s.PromiseKey is { } k && k != "other"))
            foreach (var id in s.Hearers.Where(id => !s.Repeated.Contains(id) && !s.Moot.Contains(id)))
            {
                var c = Crew.First(x => x.Id == id);
                var state = s.PromiseKept.TryGetValue(id, out var kept) ? (kept ? "kept" : "broken")
                    : s.Released.Contains(id) ? "released" : "open";
                list.Add(new LedgerEntry(c, s.Id, s.PromiseKey!, s.Bargained.Contains(id) ? "bargain" : "promise", CommitmentText(c, s.PromiseKey!), s.DueBy.TryGetValue(id, out var due) ? due : null, state));
            }
        return list;
    }

    /// <summary>What the ship promised, in words for the ledger ("wake Idris").</summary>
    public string CommitmentText(CrewMember c, string key) => key switch
    {
        "wake_partner" => c.PartnerId is { } p ? "wake " + Crew.First(x => x.Id == p).Name : "wake their partner",
        "show_logs" => "open the logs",
        "no_more_thaws" => "never thaw them again",
        _ => "something the game can't check",
    };

    /// <summary>Days left to a clock hour, rounded up; at most 0 when due.</summary>
    public int DaysLeft(double dueBy) => (int)Math.Max(0, Math.Ceiling((dueBy - Clock) / 24 - 1e-9));

    /// <summary>
    /// When an open promise to this person falls due, in words: "12 days left", "due now", "until arrival" (no date). While they are
    /// awake a promise the game checks also ends when they go to sleep (sleeping settles it), so it says so: "12 days left or when she sleeps".
    /// </summary>
    public string DueText(CrewMember c, string key, double? dueBy)
    {
        if (dueBy is not { } due)
            return "until arrival";
        var left = DaysLeft(due);
        var text = left <= 0 ? "due now" : left + (left == 1 ? " day left" : " days left");
        if (left > 0 && Checkable(key) && c.State is CrewState.Awake or CrewState.Thawing)
            text += " or when " + (c.Pronoun ?? "they") + (c.Pronoun is null ? " sleep" : " sleeps");
        return text;
    }

    /// <summary>"Promise to Mara: wake Idris, 12 days left or when she sleeps." (a standing promise has no days.)</summary>
    public string LedgerLine(LedgerEntry e) => "Promise to " + e.Person.Name + ": " + e.Text + ", " + DueText(e.Person, e.Key, e.DueBy) + ".";

    /// <summary>Whether an open promise to this person would count as kept if it were settled now (they went to sleep).</summary>
    private bool KeptNow(CrewMember c, string key)
    {
        if (key == "show_logs")
            return ShipOpenedLogs;
        var partner = c.PartnerId is null ? null : Crew.First(x => x.Id == c.PartnerId);
        return partner is not null && (partner.State == CrewState.Thawing || (partner.State == CrewState.Awake && c.State == CrewState.Awake && c.SawPartner));
    }

    /// <summary>Whether an open promise to this person can no longer be kept, so settling it now releases it (no penalty).</summary>
    private bool ReleasedNow(CrewMember c, string key)
    {
        if (key == "show_logs")
            return LogsLocked;
        var partner = c.PartnerId is null ? null : Crew.First(x => x.Id == c.PartnerId);
        return partner is null || partner.State == CrewState.Dead || (Lockouts.Bay && partner.State == CrewState.Asleep);
    }

    /// <summary>
    /// What going to sleep now would do to the open promises to this person (sleeping settles them at once), for the SLEEP button;
    /// null when nothing would break. "Mara still waits for Idris: sleeping now breaks that promise."
    /// </summary>
    public string? SleepBreaksText(CrewMember c)
    {
        var lines = new List<string>();
        foreach (var e in Ledger().Where(e => e.Person == c && e.State == "open" && Checkable(e.Key) && !KeptNow(c, e.Key) && !ReleasedNow(c, e.Key)))
        {
            var waits = e.Key == "wake_partner" && c.PartnerId is { } p ? Crew.First(x => x.Id == p).Name : "the logs to open";
            var line = c.Name + " still waits for " + waits + ": sleeping now breaks that promise.";
            if (!lines.Contains(line))
                lines.Add(line);
        }
        return lines.Count == 0 ? null : string.Join(" ", lines);
    }

    /// <summary>The person has an open promise with this key from the ship (other than <paramref name="except"/>).</summary>
    internal bool HasOpenCommitment(CrewMember c, string key, Statement? except = null) =>
        Statements.Any(s => s != except && s.Kind == StatementKind.Promise && s.PromiseKey == key && s.OpenFor(c.Id));

    /// <summary>A promise to wake this person's partner when the partner is already dead: it asks nothing the ship can do (no trust, no ledger entry).</summary>
    internal bool PartnerDead(CrewMember c, string key) =>
        key == "wake_partner" && c.PartnerId is { } p && Crew.First(x => x.Id == p).State == CrewState.Dead;

    /// <summary>A promise has just been made to a listener: its due date, and whether there is anything left to keep.</summary>
    private void StartCommitment(Statement s, CrewMember c, double days = Tuning.PromiseDueDays)
    {
        if (!Checkable(s.PromiseKey))
            return;
        s.DueBy[c.Id] = Clock + days * 24;
        if (s.PromiseKey != "wake_partner")
            return;
        var partner = c.PartnerId is null ? null : Crew.First(x => x.Id == c.PartnerId);
        // No partner, or the partner is already up: nothing was promised that is not already so (no free kept promise).
        if (partner is null || partner.State is CrewState.Awake or CrewState.Thawing)
            s.Released.Add(c.Id);
    }

    /// <summary>
    /// Settles every open promise that has come true, can no longer be kept, or fell due: wake_partner is kept when the partner
    /// is thawing or met, broken at the due date; show_logs is kept at the due date if the ship has opened every log, else broken. A promise that
    /// can't be kept any more (partner dead, the bay taken, the crew hold the logs) is released with a SHIP line and no penalty. A promise is warned about once (Advance
    /// stops) at the last beat that leaves time to act: when it has the warning days plus one beat left or less. <paramref name="only"/> and <paramref name="force"/> settle one person's
    /// promises now, as when they go under.
    /// </summary>
    internal void ResolveCommitments(CrewMember? only = null, bool force = false)
    {
        foreach (var s in Statements.Where(s => s.Kind == StatementKind.Promise && Checkable(s.PromiseKey)).ToList())
            foreach (var id in s.Hearers.Where(id => s.OpenFor(id) && (only is null || only.Id == id)).ToList())
            {
                var c = Crew.First(x => x.Id == id);
                if (!c.Alive)
                {
                    s.Released.Add(id);
                    continue;
                }
                var due = s.DueBy.TryGetValue(id, out var d) ? d : Clock;
                var late = force || Clock >= due - 0.001;
                if (s.PromiseKey == "wake_partner")
                {
                    var partner = c.PartnerId is null ? null : Crew.First(x => x.Id == c.PartnerId);
                    if (partner is null)
                    {
                        s.Released.Add(id);
                        continue;
                    }
                    if (KeptNow(c, "wake_partner"))
                    {
                        ResolvePromise(s, c, kept: true);
                        continue;
                    }
                    var why = partner.State == CrewState.Dead ? partner.Name + " died"
                        : Lockouts.Bay && partner.State == CrewState.Asleep ? "the crew hold the cold-sleep bay" : null;
                    if (why != null)
                    {
                        s.Released.Add(id);
                        Ship(c.Name + "'s promise can no longer be kept: " + why + ".");
                        continue;
                    }
                }
                else if (LogsLocked)
                {
                    // The crew took the logs: the ship can no longer open them, and their opening is not the ship's doing.
                    s.Released.Add(id);
                    Ship(c.Name + "'s promise can no longer be kept: the crew hold the logs.");
                    continue;
                }
                else if (late)
                {
                    ResolvePromise(s, c, ShipOpenedLogs);
                    continue;
                }
                if (late)
                {
                    ResolvePromise(s, c, kept: false);
                    continue;
                }
                if (c.State == CrewState.Awake && due - Clock <= Tuning.PromiseDueWarnDays * 24 + Tuning.AftermathStepHours && s.Warned.Add(id))
                {
                    Attend("a promise to " + c.Name + " falls due");
                    Ship("Promise to " + c.Name + ": " + CommitmentText(c, s.PromiseKey!) + ". " + DaysLeft(due) + " days left.");
                }
            }
    }

    // ---- Bargains: a person sets the ship's word as the price of starting work ----

    /// <summary>Bargains offered, struck, pushed past (they started anyway) and pushed into a refusal, over the voyage (for the balance table).</summary>
    public int BargainsOffered { get; private set; }
    public int BargainsStruck { get; private set; }
    public int BargainsPushedThrough { get; private set; }
    public int BargainsPushedRefused { get; private set; }

    /// <summary>
    /// The promise key this person would set as the price of starting work now, or null for none: a wary (not obedient) person
    /// whose want the ship can grant (wake their sleeping partner; open the logs) and has not already promised.
    /// </summary>
    private string? BargainKeyFor(CrewMember c)
    {
        if (c.Band != TrustBand.WantsReasons || c.Has(Trait.Obedient))
            return null;
        switch (c.Want)
        {
            case Want.WakePartner:
                var partner = c.PartnerId is null ? null : Crew.First(x => x.Id == c.PartnerId);
                if (partner is { State: CrewState.Asleep } && !Lockouts.Bay
                    && Crew.Count(x => x.State is CrewState.Awake or CrewState.Thawing) < Tuning.MaxAwake
                    && !(c.Beliefs.TryGetValue("alive:" + partner.Id, out var known) && known.Value == "dead")
                    && !HasOpenCommitment(c, "wake_partner"))
                    return "wake_partner";
                return null;
            case Want.SeeTheLogs:
                return !AllLogsOpen && !HasOpenCommitment(c, "show_logs") ? "show_logs" : null;
            default:
                return null;
        }
    }

    /// <summary>The condition still stands: the ship has not yet done it and still can.</summary>
    private bool ConditionOpen(CrewMember c, string key)
    {
        if (key == "wake_partner")
            return c.PartnerId is { } p && Crew.First(x => x.Id == p).State == CrewState.Asleep && !Lockouts.Bay;
        return !AllLogsOpen;
    }

    /// <summary>
    /// The condition this person has open, or null when it no longer stands: its fault is resolved, the alert changed, the person
    /// is not awake, or the condition was met by other means. Only reads (the draw calls it); <see cref="LapseBargains"/> clears
    /// what no longer stands after each step.
    /// </summary>
    public Bargain? BargainOf(CrewMember c) => c.OpenBargain is { } b && Stands(c, b) ? b : null;

    /// <summary>
    /// This person's word is what the work waits on: they have an open condition and the fault still needs them (others have not
    /// taken it on at full strength). For the next-step line.
    /// </summary>
    public bool AwaitsWord(CrewMember c) =>
        BargainOf(c) is { } b && Current.Faults.First(f => f.Id == b.FaultId) is var f
        && Crew.Count(o => o != c && o.State == CrewState.Awake && o.WorkingOn == f.Id) < f.People;

    private bool Stands(CrewMember c, Bargain b) =>
        c.State == CrewState.Awake && b.Alert == Current.Number
        && Current.Faults.FirstOrDefault(f => f.Id == b.FaultId) is { Resolved: false } && ConditionOpen(c, b.Key);

    /// <summary>
    /// Drops every condition that no longer stands, or whose person has left the wary band (trust fell or rose past it), so one
    /// can't come back to life later (a partner woken and put back to sleep in the same alert). Run at the end of each step.
    /// </summary>
    private void LapseBargains()
    {
        foreach (var c in Crew.Where(c => c.OpenBargain is { } b && (!Stands(c, b) || c.Band != TrustBand.WantsReasons)))
            c.OpenBargain = null;
    }

    private static string TaskText(Fault f, bool patch) => (patch ? "patching the " : "the ") + f.CrewName;

    /// <summary>Takes the work on: the person is on the fault and says so. Callers have checked it can be done.</summary>
    private void StartWork(CrewMember c, Fault f, bool patch, string kind = "comply", string? decision = null)
    {
        if (f.Patching != patch)
        {
            f.Progress = 0;
            f.Patching = patch;
        }
        _idleWarned = false;
        var terms = c.WorkingOn == f.Id && c.Terms is { } kept && kept.FaultId == f.Id ? kept : TermsFor(c, f);
        if (c.WorkingOn != f.Id)
        {
            TermStats.Starts++;
            if (terms.CauseBriefed)
                TermStats.CauseBonusStarts++;
            if (terms.Risk != RiskKnowledge.NoRisk)
            {
                TermStats.RiskStarts++;
                if (terms.RiskBriefed)
                    TermStats.BriefedStarts++;
            }
        }
        c.Terms = terms;
        c.WorkingOn = f.Id;
        f.State = FaultState.InRepair;
        var task = TaskText(f, patch);
        var knowing = terms.RiskBriefed ? " You know the danger, so you take precautions and work slower." : "";
        Speak(c, kind, (decision ?? "You agree to take on " + task + ".") + knowing, task);
        if (terms.RiskBriefed)
            Recorded(c.Name + " starts " + TaskText(f, patch) + " knowing " + _facts[f.DangerFact!].Say(c.Beliefs[f.DangerFact!].Value)
                     + ": precautions, work " + (int)Math.Round((1 - terms.RateFactor) * 100) + "% slower, dose and injury chance halved.");
        if (!Staffed(f))
            Ship(f.Name + " needs " + f.People + " people before the work can start.");
    }

    /// <summary>
    /// The Assign gate for a wary person: offer the condition once per waking, refuse while it stands, or let a push roll.
    /// True when the click was handled here (no work started, unless the push passed).
    /// </summary>
    private bool BargainGate(CrewMember c, Fault f, bool patch)
    {
        var task = TaskText(f, patch);
        if (BargainOf(c) is not { } b)
        {
            if (c.BargainOffered || BargainKeyFor(c) is not { } key)
                return false;
            c.BargainOffered = true;
            c.BargainDeclined = false;
            c.OpenBargain = new Bargain(key, f.Id, patch, Current.Number);
            BargainsOffered++;
            OfferLine(c, c.OpenBargain, task);
            return true;
        }
        if (b.FaultId != f.Id || b.Patch != patch)
        {
            // They name the same price for the other piece of work.
            c.OpenBargain = b = b with { FaultId = f.Id, Patch = patch };
            if (!c.BargainDeclined)
            {
                OfferLine(c, b, task);
                return true;
            }
        }
        var cond = CommitmentText(c, b.Key);
        if (c.BargainDeclined)
        {
            Speak(c, "bargain_refused", "You still will not start " + task + " without the ship's word to " + cond + ".", cond);
            return true;
        }
        // Pushing past the condition: one roll, and it costs trust either way.
        var pass = _rng.NextDouble() < PushChance(c);
        c.Trust -= Tuning.BargainPushedTrust;
        Clamp(c);
        if (pass)
        {
            c.OpenBargain = null;
            BargainsPushedThrough++;
            StartWork(c, f, patch, "comply_pushed", "You start " + task + " without the ship's word to " + cond + ", and you let the ship know it.");
        }
        else
        {
            c.BargainDeclined = true;
            BargainsPushedRefused++;
            Speak(c, "bargain_refused", "You will not start " + task + " without the ship's word to " + cond + ". You were pushed and you refuse.", cond);
        }
        return true;
    }

    private void OfferLine(CrewMember c, Bargain b, string task)
    {
        var cond = CommitmentText(c, b.Key);
        Speak(c, "bargain", "Before you start " + task + " you set a condition: the ship must give its word to " + cond
            + " within " + Tuning.BargainDueDays + " days. You will not start without it.", cond);
    }

    /// <summary>The chance a wary person starts anyway when pushed.</summary>
    public static double PushChance(CrewMember c) => Math.Clamp(c.Trust / 100 + Tuning.BargainPushBase, 0, 1);

    /// <summary>
    /// A promise with the open bargain's key has been given to this person: the commitment is recorded with its due date, no
    /// trust gain, and the work starts. False (the bargain is dropped, the promise is an ordinary one) when the work can no longer
    /// be done. When one of <see cref="Assign"/>'s gates now stops them (trust fell, a question is waiting, the danger), they say
    /// that refusal instead: the bargain is closed, the ship's word stands as an ordinary promise (no trust gain, no line of its
    /// own), and a later Repair or Patch starts the work once the gate is clear, without a new price.
    /// </summary>
    private bool StrikeBargain(Statement made, CrewMember c, Bargain b)
    {
        var f = Current.Faults.FirstOrDefault(x => x.Id == b.FaultId);
        c.OpenBargain = null;
        if (f is null || f.Resolved || Locked(f.Section) || (!b.Patch && Parts <= 0)
            || c.State != CrewState.Awake || (c.WorkingOn != null && c.WorkingOn != f.Id))
            return false;
        if (Refuses(c, f))
        {
            StartCommitment(made, c);
            return true;
        }
        StartCommitment(made, c, Tuning.BargainDueDays);
        made.Bargained.Add(c.Id);
        BargainsStruck++;
        StartWork(c, f, b.Patch, "bargain_struck", "You have the ship's word to " + CommitmentText(c, b.Key) + ". You start " + TaskText(f, b.Patch) + ".");
        return true;
    }

    /// <summary>
    /// What the open condition costs and risks, for the player (never says whether a statement is true): the price, what keeping
    /// and breaking it does, and what pushing past it does. Null when this person has no open condition.
    /// </summary>
    public string? BargainText(CrewMember c)
    {
        if (BargainOf(c) is not { } b)
            return null;
        var f = Current.Faults.First(x => x.Id == b.FaultId);
        var cond = CommitmentText(c, b.Key);
        var text = FormattableString.Invariant($"{c.Name} will start {TaskText(f, b.Patch)} if you promise to {cond} within {Tuning.BargainDueDays:0} days. Kept +{Tuning.PromiseKept:0} trust, broken -{Tuning.PromiseBroken:0}. ");
        text += c.BargainDeclined
            ? "They refuse to start without your word. Say it to them."
            : FormattableString.Invariant($"Without it: about {PushChance(c) * 100:0}% {StartsText(c)}, -{Tuning.BargainPushedTrust:0} trust. Say it to them.");
        return text;
    }

    /// <summary>"she starts", "he starts", or "they start" when the sheet has no pronoun.</summary>
    public static string StartsText(CrewMember c) => c.Pronoun is { } p ? p + " starts" : "they start";

    /// <summary>The key of the condition any of these listeners has open (the test reader reads "agreed" as that promise), or null.</summary>
    public string? OpenBargainKey(IEnumerable<CrewMember> hearers) => hearers.Select(BargainOf).FirstOrDefault(b => b != null)?.Key;
}
