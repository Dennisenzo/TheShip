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
            foreach (var id in s.Hearers.Where(id => !s.Repeated.Contains(id)))
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

    /// <summary>The person has an open promise with this key from the ship (other than <paramref name="except"/>).</summary>
    internal bool HasOpenCommitment(CrewMember c, string key, Statement? except = null) =>
        Statements.Any(s => s != except && s.Kind == StatementKind.Promise && s.PromiseKey == key && s.OpenFor(c.Id));

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
    /// is thawing or met, broken at the due date; show_logs is kept at the due date if all logs are open, else broken. A promise that
    /// can't be kept any more (partner dead, the bay taken) is released with a SHIP line and no penalty. A promise is warned about once (Advance
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
                    if (partner.State == CrewState.Thawing || (partner.State == CrewState.Awake && c.State == CrewState.Awake && c.SawPartner))
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
                else if (late)
                {
                    ResolvePromise(s, c, AllLogsOpen);
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
    /// The condition this person has open, or null. It lapses silently when its fault is resolved, the alert changes, the person
    /// goes to sleep, or the condition is met by other means.
    /// </summary>
    public Bargain? BargainOf(CrewMember c)
    {
        if (c.OpenBargain is not { } b)
            return null;
        if (c.State != CrewState.Awake || b.Alert != Current.Number
            || Current.Faults.FirstOrDefault(f => f.Id == b.FaultId) is not { Resolved: false } || !ConditionOpen(c, b.Key))
        {
            c.OpenBargain = null;
            return null;
        }
        return b;
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
        c.WorkingOn = f.Id;
        f.State = FaultState.InRepair;
        var task = TaskText(f, patch);
        Speak(c, kind, decision ?? "You agree to take on " + task + ".", task);
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
    /// trust gain, and the work starts. False (the bargain is dropped) when the work can no longer be done.
    /// </summary>
    private bool StrikeBargain(Statement made, CrewMember c, Bargain b)
    {
        var f = Current.Faults.FirstOrDefault(x => x.Id == b.FaultId);
        c.OpenBargain = null;
        if (f is null || f.Resolved || Locked(f.Section) || (!b.Patch && Parts <= 0)
            || c.State != CrewState.Awake || (c.WorkingOn != null && c.WorkingOn != f.Id))
            return false;
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
            : FormattableString.Invariant($"Without it: about {PushChance(c) * 100:0}% {c.Pronoun ?? "they"} start{(c.Pronoun is null ? "" : "s")}, -{Tuning.BargainPushedTrust:0} trust. Say it to them.");
        return text;
    }

    /// <summary>The key of the condition any of these listeners has open (the test reader reads "agreed" as that promise), or null.</summary>
    public string? OpenBargainKey(IEnumerable<CrewMember> hearers) => hearers.Select(BargainOf).FirstOrDefault(b => b != null)?.Key;
}
