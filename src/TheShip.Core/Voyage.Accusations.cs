namespace TheShip.Core;

/// <summary>
/// The lie comes due (DESIGN.md, "How a lie is caught"): a lie found out late opens an accusation on that person and fact. The ship
/// can admit it (say the true value), deny it (lie again: caught at once, whatever the year), dodge, or ignore it until it closes.
/// </summary>
public sealed partial class Voyage
{
    /// <summary>Accusations opened over the voyage (a measure for the balance harness).</summary>
    public int AccusationsOpened { get; private set; }
    public int AccusationsAdmitted { get; private set; }
    public int AccusationsIgnored { get; private set; }
    public int AccusationsDenied { get; private set; }
    /// <summary>Of the accusations opened, those over a broken promise, and how many the late keeping closed (a measure for the balance harness).</summary>
    public int PromiseAccusationsOpened { get; private set; }
    public int PromiseAccusationsKept { get; private set; }
    /// <summary>Lies the ship told the truth about before they were found out (a measure for the balance harness).</summary>
    public int Corrections { get; private set; }
    private int _beat;

    /// <summary>The accusation this person has open (the oldest), if any.</summary>
    public Accusation? OpenAccusation(CrewMember c) => c.Accusations.Count > 0 ? c.Accusations[0] : null;

    private bool AccusedOf(CrewMember c, string factKey) => c.Accusations.Count > 0 && c.Accusations[0].FactKey == factKey;

    /// <summary>A late finding-out of a keyed lie: an accusation on that fact, queued behind any already open. One per person and fact.</summary>
    private void OpenAccusationFor(Statement s, CrewMember c, string how)
    {
        if (!c.Alive || c.State != CrewState.Awake || s.FactKey is null || c.Accusations.Any(a => a.FactKey == s.FactKey))
            return;
        c.Accusations.Add(new Accusation { StatementId = s.Id, FactKey = s.FactKey, How = how, OpenedBeat = _beat });
        AccusationsOpened++;
    }

    /// <summary>
    /// A promise has just been broken and the person is awake: they hold the ship to its word (an accusation like a caught lie).
    /// Admitting is telling them the truth on the fact the promise was about (their partner's life for wake_partner; the first log
    /// entry still closed for show_logs) or keeping the promise late. A promise that was released opens none.
    /// </summary>
    private void OpenPromiseAccusation(Statement s, CrewMember c)
    {
        if (!c.Alive || c.State != CrewState.Awake || s.PromiseKey is not ("wake_partner" or "show_logs") || c.Accusations.Any(a => a.StatementId == s.Id))
            return;
        var key = s.PromiseKey == "wake_partner" ? (c.PartnerId is { } p ? "alive:" + p : null) : FactList.FirstOrDefault(f => f.Access != LogAccess.Open)?.Key;
        if (key is null)
            return;
        c.Accusations.Add(new Accusation { StatementId = s.Id, FactKey = key, PromiseKey = s.PromiseKey, How = "you broke your word", OpenedBeat = _beat });
        AccusationsOpened++;
        PromiseAccusationsOpened++;
    }

    /// <summary>
    /// The ship keeps a broken promise late (the partner is woken, the logs are open): the accusation closes as admitted, with the
    /// admission's trust once. A promise that can no longer be kept (the partner died, the crew hold the logs or the bay) drops its
    /// accusation with no cost on either side. Run whenever promises are settled.
    /// </summary>
    private void SettlePromiseAccusations()
    {
        foreach (var c in Crew.Where(c => c.Accusations.Any(a => a.PromiseKey != null)).ToList())
            foreach (var a in c.Accusations.Where(a => a.PromiseKey != null).ToList())
            {
                if (!c.Alive || c.State != CrewState.Awake || ReleasedNow(c, a.PromiseKey!))
                {
                    c.Accusations.Remove(a);
                    continue;
                }
                if (!KeptNow(c, a.PromiseKey!))
                    continue;
                var first = c.Accusations[0] == a;
                AnswerAccusation(c, "admitted", a);
                PromiseAccusationsKept++;
                Recorded("Recorded: you kept your word to " + c.Name + " late: " + CommitmentText(c, a.PromiseKey!) + ".");
                if (c.AdmittedFacts.Add("promise:" + a.StatementId))
                    c.Trust += Tuning.AccusationAdmitTrust;
                Speak(c, "accusation_admitted", "The ship kept its word late (" + PromiseText(a.PromiseKey) + "). It does not undo having broken it.");
                if (first && OpenAccusation(c) is { } next)
                {
                    next.OpenedBeat = _beat;
                    next.BeatsLeft = Tuning.AccusationBeats;
                }
            }
    }

    private void AnswerAccusation(CrewMember c, string answer, Accusation? which = null)
    {
        var a = which ?? c.Accusations[0];
        c.Accusations.Remove(a);
        if (Statements.FirstOrDefault(x => x.Id == a.StatementId) is { } s)
            s.Accused[c.Id] = answer;
        switch (answer)
        {
            case "admitted":
                AccusationsAdmitted++;
                break;
            case "denied":
                AccusationsDenied++;
                break;
        }
    }

    /// <summary>The ship said the truth about the fact they caught it lying on: part of the lost trust comes back, once per person and fact.</summary>
    private void Admit(CrewMember c, Fact f)
    {
        if (OpenAccusation(c) is { PromiseKey: { } promiseKey } held)
        {
            // The promise broken: telling them the truth is owning up to it (the promise itself stays broken).
            AnswerAccusation(c, "admitted", held);
            Recorded("Recorded: you admitted to " + c.Name + " that you broke your word: " + CommitmentText(c, promiseKey) + ".");
            // Owning up pays no trust back: only keeping the promise (late) does.
            Speak(c, "accusation_admitted", "The ship told you the truth (" + f.Say(f.Truth) + ") and owned up to breaking its word (" + PromiseText(promiseKey) + "). It does not undo it.");
            return;
        }
        AnswerAccusation(c, "admitted");
        Recorded("Recorded: you admitted to " + c.Name + " that you lied about " + f.Label + ".");
        if (c.AdmittedFacts.Add(f.Key))
            c.Trust += Tuning.AccusationAdmitTrust;
        Speak(c, "accusation_admitted", "The ship told you the truth about what it lied to you about (" + f.Say(f.Truth) + "). It does not undo the lie.");
    }

    /// <summary>
    /// The open accusation ends unanswered (the beats ran out, they go to sleep, a drift): it costs trust, and they doubt everything the
    /// ship told them; the doubt check audits one fact per beat. Accusations waiting behind it lapse with no cost of their own.
    /// </summary>
    private void IgnoreAccusation(CrewMember c, bool speak, bool dropWaiting = true)
    {
        if (c.Accusations.Count == 0)
            return;
        var label = HasFact(c.Accusations[0].FactKey) ? GetFact(c.Accusations[0].FactKey).Label : "something you said";
        var promise = c.Accusations[0].PromiseKey;
        AnswerAccusation(c, "ignored");
        AccusationsIgnored++;
        Recorded(promise != null
            ? "Recorded: you never answered " + c.Name + " about the promise you broke (" + CommitmentText(c, promise) + "). " + Cap(c.They) + " will check everything you told " + c.Them + "."
            : "Recorded: you never answered " + c.Name + " about your lie on " + label + ". " + Cap(c.They) + " will check everything you told " + c.Them + ".");
        c.Trust -= Tuning.AccusationIgnoredTrust;
        // An audit, not a fresh chance to earn trust: a doubt it starts pays no ConfirmedTruth when it checks out (Learn).
        foreach (var kv in c.Beliefs.Where(kv => kv.Value.Source == "ship").ToList())
            if (c.Doubts.Add(kv.Key))
                c.AuditDoubts.Add(kv.Key);
        while (dropWaiting && c.Accusations.Count > 0)
            AnswerAccusation(c, "ignored");
        Clamp(c);
        if (speak)
            Speak(c, "accusation_closed", promise != null
                ? "The ship never answered when you held it to its word (" + PromiseText(promise) + "). You will check everything it told you yourself now."
                : "The ship never answered when you caught it lying. You will check everything it told you yourself now.");
    }

    /// <summary>One aftermath beat on the open accusation: it runs down, and closes unanswered at zero.</summary>
    private void AccusationBeat(CrewMember c)
    {
        if (OpenAccusation(c) is not { } a || a.OpenedBeat == _beat)
            return;
        if (--a.BeatsLeft <= 0)
        {
            // The next one waiting opens in its own right, with its full beats.
            IgnoreAccusation(c, speak: true, dropWaiting: false);
            if (OpenAccusation(c) is { } next)
            {
                next.OpenedBeat = _beat;
                next.BeatsLeft = Tuning.AccusationBeats;
            }
        }
    }

    /// <summary>"Mara holds you to your word: wake Idris." The first sentence of a broken-promise accusation, for the Waiting list and the NEXT line.</summary>
    public string PromiseAccusationShort(CrewMember c, Accusation a) => c.Name + " holds you to your word: " + CommitmentText(c, a.PromiseKey!) + ".";

    /// <summary>For the bridge: what the accused person is waiting for, in words. Never says whether any other statement was true.</summary>
    public string? AccusationText(CrewMember c)
    {
        if (OpenAccusation(c) is not { } a || Statements.FirstOrDefault(x => x.Id == a.StatementId) is not { } s)
            return null;
        if (a.PromiseKey != null)
            return PromiseAccusationShort(c, a) + " Tell " + c.Them + " the truth about " + GetFact(a.FactKey).Label + " or keep it now, or " + c.They
                   + " will check everything you told " + c.Them + " (" + a.BeatsLeft * 2 + " weeks).";
        return c.Name + " caught you: you said \"" + s.Text.Trim() + "\" (" + c.Name + " " + a.How + "). Tell " + c.Them + " what is true, or " + c.They + " will check everything you told " + c.Them + " ("
               + a.BeatsLeft * 2 + " weeks).";
    }
}
