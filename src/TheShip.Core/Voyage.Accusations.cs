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

    private void AnswerAccusation(CrewMember c, string answer)
    {
        var a = c.Accusations[0];
        c.Accusations.RemoveAt(0);
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
        AnswerAccusation(c, "admitted");
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
        AnswerAccusation(c, "ignored");
        AccusationsIgnored++;
        c.Trust -= Tuning.AccusationIgnoredTrust;
        foreach (var kv in c.Beliefs.Where(kv => kv.Value.Source == "ship").ToList())
            c.Doubts.Add(kv.Key);
        while (dropWaiting && c.Accusations.Count > 0)
            AnswerAccusation(c, "ignored");
        Clamp(c);
        if (speak)
            Speak(c, "accusation_closed", "The ship never answered when you caught it lying. You will check everything it told you yourself now.");
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

    /// <summary>For the bridge: what the accused person is waiting for, in words. Never says whether any other statement was true.</summary>
    public string? AccusationText(CrewMember c)
    {
        if (OpenAccusation(c) is not { } a || Statements.FirstOrDefault(x => x.Id == a.StatementId) is not { } s)
            return null;
        return c.Name + " caught you: you said \"" + s.Text.Trim() + "\" (" + c.Name + " " + a.How + "). Tell them what is true, or they will check everything you told them ("
               + a.BeatsLeft * 2 + " weeks).";
    }
}
