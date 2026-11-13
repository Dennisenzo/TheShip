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
            foreach (var id in s.Hearers)
            {
                var c = Crew.First(x => x.Id == id);
                var state = s.PromiseKept.TryGetValue(id, out var kept) ? (kept ? "kept" : "broken")
                    : s.Released.Contains(id) ? "released" : "open";
                list.Add(new LedgerEntry(c, s.Id, s.PromiseKey!, "promise", CommitmentText(c, s.PromiseKey!), s.DueBy.TryGetValue(id, out var due) ? due : null, state));
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

    /// <summary>The person has an open, checkable promise with this key from the ship.</summary>
    internal bool HasOpenCommitment(CrewMember c, string key) =>
        Statements.Any(s => s.Kind == StatementKind.Promise && s.PromiseKey == key && s.OpenFor(c.Id));

    /// <summary>A promise has just been made to a listener: its due date, and whether there is anything left to keep.</summary>
    private void StartCommitment(Statement s, CrewMember c)
    {
        if (!Checkable(s.PromiseKey))
            return;
        s.DueBy[c.Id] = Clock + Tuning.PromiseDueDays * 24;
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
    /// can't be kept any more (partner dead, the bay taken) is released with a SHIP line and no penalty. A promise with 14 days
    /// left or less is warned about once (Advance stops). <paramref name="only"/> and <paramref name="force"/> settle one person's
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
                if (c.State == CrewState.Awake && due - Clock <= Tuning.PromiseDueWarnDays * 24 && s.Warned.Add(id))
                {
                    Attend("a promise to " + c.Name + " falls due");
                    Ship("Promise to " + c.Name + ": " + CommitmentText(c, s.PromiseKey!) + ". " + DaysLeft(due) + " days left.");
                }
            }
    }
}
