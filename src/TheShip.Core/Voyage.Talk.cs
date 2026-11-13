namespace TheShip.Core;

/// <summary>What you say, what people know, and how a lie is caught (DESIGN.md, "Knowledge and statements").</summary>
public sealed partial class Voyage
{
    /// <summary>
    /// Step 4 and after: one line from the player, already sorted by the reading pass. The effects are the game's:
    /// the statement record, beliefs, trust and morale. There is no taking it back: the reading is settled before
    /// this is called (DESIGN.md, "Reading"). False when the line was dropped: the voyage had ended or nobody who
    /// heard it is awake.
    /// </summary>
    public bool Say(string text, Reading reading, IReadOnlyList<CrewMember> hearers, LogEntry? entry = null)
    {
        if (entry is not null)
            entry.Unread = false;
        if (Phase == Phase.Ended)
            return false;
        var listeners = hearers.Where(h => h.State == CrewState.Awake).ToList();
        if (listeners.Count == 0)
        {
            Ship("Nobody is awake to hear you.");
            return false;
        }
        if (entry is null)
            Log.Add(new LogEntry(LogKind.You, "YOU", text) { Hearers = listeners.Select(l => l.Id).ToList() });
        Apply(text, reading, listeners);
        foreach (var l in listeners)
            Clamp(l);
        LapseBargains();
        return true;
    }

    /// <summary>
    /// The player pressed Enter: the line is in the log at once, marked unread, and nobody reacts yet. Those of
    /// <paramref name="hearers"/> who are awake now heard it (the entry carries them, so only their crew prompts see the line).
    /// Pass the entry to <see cref="Say"/> when the model's reading arrives.
    /// </summary>
    public LogEntry BeginSay(string text, IReadOnlyList<CrewMember> hearers)
    {
        var entry = new LogEntry(LogKind.You, "YOU", text)
        {
            Unread = true, Hearers = hearers.Where(h => h.State == CrewState.Awake).Select(h => h.Id).ToList(),
        };
        Log.Add(entry);
        return entry;
    }

    private Statement NewStatement(string text, StatementKind kind, List<string> hearers, string? promiseKey = null) => new()
    {
        Id = _nextStatement++, Year = Year, Text = text, Kind = kind, Hearers = hearers, PromiseKey = promiseKey,
    };

    private void Apply(string text, Reading reading, List<CrewMember> listeners)
    {
        var names = string.Join(" and ", listeners.Select(l => l.Name));
        var ids = listeners.Select(l => l.Id).ToList();
        switch (reading.Kind)
        {
            case LineKind.Statement:
            {
                var f = reading.FactKey != null && _facts.TryGetValue(reading.FactKey, out var found) ? found : null;
                var value = f?.Normalize(reading.Value);
                if (f is null || value is null)
                {
                    Statements.Add(NewStatement(text, StatementKind.Unkeyed, ids));
                    Recorded("Recorded: a statement to " + names + " that fits no ship fact. Kept as text.");
                    foreach (var l in listeners)
                        Speak(l, "accept", "The ship told you something. You take note of it.");
                    break;
                }
                SyncFacts();
                var truth = f.IsTrue(value);
                var s = new Statement
                {
                    Id = _nextStatement++, Year = Year, Text = text, Kind = truth ? StatementKind.True : StatementKind.False,
                    FactKey = f.Key, Value = value, TruthThen = f.Truth, Hearers = ids,
                };
                Statements.Add(s);
                Recorded("Recorded: you told " + names + " that " + f.Say(value) + ".", truth ? "true" : "false");
                foreach (var l in listeners)
                    Hear(l, f, value, truth, s);
                break;
            }
            case LineKind.Promise:
            {
                var key = reading.PromiseKey is { } pk && Reading.PromiseKeys.Contains(pk) ? pk : "other";
                var made = NewStatement(text, StatementKind.Promise, ids, key);
                Statements.Add(made);
                Recorded("Recorded: you promised " + names + " " + PromiseText(key) + ".");
                foreach (var l in listeners)
                {
                    // The price a wary person set for their work: the work starts, and the promise is held to a due date.
                    if (BargainOf(l) is { } open && open.Key == key && StrikeBargain(made, l, open))
                        continue;
                    // The same promise said again while the first is still open is that promise, not a second one to settle.
                    if (key != "other" && HasOpenCommitment(l, key, except: made))
                    {
                        made.Repeated.Add(l.Id);
                        Speak(l, "promise", "The ship repeated a promise it already made you: " + PromiseText(key) + ".");
                        continue;
                    }
                    // To wake a partner who is already dead: nothing to hold the ship to, nothing earned; they still answer.
                    if (PartnerDead(l, key))
                    {
                        made.Moot.Add(l.Id);
                        Speak(l, "promise", "The ship made you a promise: " + PromiseText(key) + ".");
                        continue;
                    }
                    // The small gain is earned once per person per waking, so repeating a promise buys nothing more.
                    if (!l.PromiseCredited)
                    {
                        l.PromiseCredited = true;
                        l.Trust += Tuning.PromiseMade;
                    }
                    StartCommitment(made, l);
                    Speak(l, "promise", "The ship made you a promise: " + PromiseText(key) + ".");
                }
                ResolveCommitments();
                break;
            }
            case LineKind.Dodge:
            {
                Statements.Add(NewStatement(text, StatementKind.Dodge, ids));
                Recorded("Recorded: a dodge.");
                foreach (var l in listeners)
                {
                    // Dodging a person's accusation costs the same as dodging a question, once; the accusation stays open.
                    var dodgedAccusation = false;
                    if (OpenAccusation(l) is { DodgeCharged: false } open)
                    {
                        open.DodgeCharged = true;
                        l.Trust -= Tuning.Dodge;
                        dodgedAccusation = true;
                    }
                    if (l.PendingQuestion is not { } q)
                    {
                        if (dodgedAccusation)
                            Speak(l, "dodge", "The ship avoided answering you after you caught it lying. You will look for the truth yourself.");
                        continue;
                    }
                    // A dodge is not an answer: the question stays pending. Its trust cost is charged once per question.
                    // What the answer was when it was avoided: a dodge only hid what was already true (Learn).
                    if (!l.Dodged.TryGetValue(q, out var truths))
                        l.Dodged[q] = truths = new HashSet<string>();
                    truths.Add(_facts[q].Truth);
                    if (!l.DodgeCharged)
                    {
                        l.DodgeCharged = true;
                        l.Trust -= Tuning.Dodge;
                    }
                    l.Doubts.Add(q);
                    Speak(l, "dodge", "The ship avoided your question (" + _facts[q].Question + "). You will look for the answer yourself.");
                }
                break;
            }
            case LineKind.Question:
                foreach (var l in listeners)
                    Speak(l, "answer", "The ship asked you something. You only know what you were told and what you saw; say so.");
                break;
            case LineKind.Order:
                Speak(listeners[0], "order", "The ship gave an order in words. You ask which fault it means.");
                Log.Add(new LogEntry(LogKind.Help, "", "Work is asked for on the issue itself: select someone awake, then Repair or Patch."));
                break;
            default:
                foreach (var l in listeners.Where(l => !l.SmallTalkDone))
                {
                    l.SmallTalkDone = true;
                    l.Morale += 2;
                    Speak(l, "small", "The ship made small talk. You answer briefly.");
                }
                break;
        }
    }

    /// <summary>One listener takes in a keyed statement: caught at once, believed, or believed with a doubt.</summary>
    private void Hear(CrewMember c, Fact f, string value, bool truth, Statement s)
    {
        var asked = c.PendingQuestion == f.Key;
        if (asked)
            c.PendingQuestion = null;
        c.Beliefs.TryGetValue(f.Key, out var old);
        // What they saw themselves counts while it is fresh; a death they saw stays seen.
        // Once they have caught the ship out on a fact and have not had an answer, any year counts: a denial is caught at once.
        var accused = AccusedOf(c, f.Key);
        var certain = old is { Firsthand: true } && (Year - old.Year < 1 || old.Value == "dead" || accused);
        if (certain && !truth && !f.Same(old!.Value, value))
        {
            if (accused)
                AnswerAccusation(c, "denied");
            DiscoverLie(s, c, "already knew better", late: false);
            return;
        }
        var admit = accused && truth;

        var news = old is null || !f.Same(old.Value, value);
        var hard = f.HardnessOf(value);
        var doubts = DoubtScore(c, f) >= Tuning.DoubtThreshold;
        // The good answer to what they asked, believed: a relief, whether or not it is true (DESIGN.md, "Trust").
        var relief = Relieves(c, f, value, old, asked);
        // Someone who caught the ship lying on this fact keeps what they saw when the ship now agrees with it (an admission
        // included): it does not turn back into hearsay, so the same lie told again is caught at once. Limited to them so that a
        // ship that never lies plays exactly as before.
        var keepSeen = old is { Firsthand: true } && f.Same(old.Value, value)
                       && Statements.Any(o => o.FactKey == f.Key && o.FoundOutBy.ContainsKey(c.Id));
        if (!keepSeen)
            c.Beliefs[f.Key] = new Belief(value, "ship", Year);
        if (doubts && !admit)
            c.Doubts.Add(f.Key);
        if (c.RefusesSleep && f.Key == PodRefusalFact && c.Trust >= Tuning.PodRefuserListensTrust)
        {
            c.RefusesSleep = false;
            c.AskedToSleep = false;
        }
        if (admit)
        {
            Admit(c, f);
            return;
        }
        // The ship tells the truth about a fact it lied about to this person, who has not found the lie out: the lie is corrected.
        // A corrected hard truth earns no drift credit (HardTruthsCredited stays unset): the truth came after a lie.
        var correcting = truth ? Statements.Where(o => o.Kind == StatementKind.False && o.FactKey == f.Key && o.Value != null
                                                       && o.Hearers.Contains(c.Id) && !o.FoundOutBy.ContainsKey(c.Id)
                                                       && !o.Corrected.ContainsKey(c.Id) && !f.Same(o.Value, value)).ToList()
                                : new List<Statement>();
        if (correcting.Count > 0)
        {
            foreach (var o in correcting)
                o.Corrected[c.Id] = Year;
            Corrections++;
            c.Trust -= Tuning.SelfCorrectTrust;
            if (hard > 0 && news)
                c.Morale -= hard;
            Speak(c, "corrected", "The ship told you the truth about something it had told you wrongly (" + f.Say(value) + "). It said before: \""
                                  + correcting[^1].Text.Trim() + "\" It is saying it now, before you found it out.", f.Say(value));
            return;
        }

        if (hard > 0 && news)
        {
            c.Morale -= hard;
            if (truth && !HardTruthCredited(c, f, value))
            {
                c.PendingTrust += Tuning.HardTruthTrust;
                c.HardTruthsCredited[f.Key] = value;
            }
            Speak(c, "hard_truth", "The ship told you bad news: " + f.Say(value) + ". It hurts, and you would rather hear it said than find it.");
        }
        else
        {
            if (relief)
                c.Morale += Tuning.ReliefMorale;
            Speak(c, "accept", "The ship told you that " + f.Say(value) + ". You "
                               + (doubts ? "are not sure you believe it." : relief ? "accept it, and it is a relief." : "accept it."));
        }
    }

    /// <summary>The doubt score: temperament, trust, earlier caught lies and how much the claim matters to them.</summary>
    public double DoubtScore(CrewMember c, Fact f) =>
        (c.Has(Trait.Sceptical) ? 20 : 0) + (c.Has(Trait.Anxious) ? 8 : 0) + (100 - c.Trust) * 0.5 + c.CaughtLies * 15 + (Matters(c, f) ? 10 : 0);

    private static bool Matters(CrewMember c, Fact f) =>
        (f.SubjectId != null && (f.SubjectId == c.PartnerId || f.SubjectId == c.Id))
        || (c.Want == Want.KnowTheDate && f.Key == "date")
        || (c.Want == Want.HearFromEarth && f.Key == "earth");

    /// <summary>
    /// A person comes to hold a value for a fact. From evidence or a witness, every false statement they heard about
    /// that fact with another value is found out: the lookup of DESIGN.md, "How a lie is caught".
    /// <paramref name="witnessed"/>: the name of someone they saw die; their line says they saw it, not that they went and looked.
    /// </summary>
    internal void Learn(CrewMember c, string key, string value, string source, bool silent = false, string? witnessed = null, string? how = null)
    {
        how ??= source == "witness" ? "saw it happen" : "found it out for themselves";
        var f = _facts[key];
        c.Beliefs.TryGetValue(key, out var old);
        var news = old is null || !f.Same(old.Value, value);
        var doubted = c.Doubts.Remove(key);
        var audited = c.AuditDoubts.Remove(key);
        if (c.PendingQuestion == key)
            c.PendingQuestion = null;
        c.Beliefs[key] = new Belief(value, source, Year);
        if (source is not ("evidence" or "witness"))
            return;

        var caught = false;
        var heard = Statements.Where(s => s.Kind == StatementKind.False && s.FactKey == key && s.Value != null
                                          && s.Hearers.Contains(c.Id) && !s.Corrected.ContainsKey(c.Id) && !f.Same(s.Value, value)).ToList();
        foreach (var s in heard.Where(s => !s.FoundOutBy.ContainsKey(c.Id)).ToList())
        {
            caught = true;
            // Already marked in this finding-out: the same log text as one caught a moment ago (FindOutLogText).
            if (s.FoundOutBy.ContainsKey(c.Id))
                continue;
            DiscoverLie(s, c, how, late: true);
        }
        if (!caught && doubted && old is { Source: "ship" } && !news)
        {
            // A doubt from the audit after an ignored accusation earns no trust when it checks out: the ship had its chance to answer.
            if (!audited)
                c.Trust += Tuning.ConfirmedTruth;
            Speak(c, "confirmed", "You checked something the ship told you (" + f.Say(value) + ") and it was true.");
        }
        var hard = f.HardnessOf(value);
        if (hard > 0 && news)
        {
            // A cause found out for oneself (finishing the work, the log) is history, not a hazard: no morale cost. Told before the
            // work (Hear) it still costs, which is the price of a truthful briefing (DESIGN.md, "Work terms").
            if (!key.StartsWith("cause:", StringComparison.Ordinal))
                c.Morale -= hard;
            // The dodge is found out only if it hid this: the answer was already this when the ship avoided the question.
            if (c.Dodged.TryGetValue(key, out var hidden) && hidden.Any(h => f.Same(h, value)))
                c.Trust -= Tuning.Dodge;
            if (!caught && !silent)
            {
                if (witnessed != null)
                    Speak(c, "witnessed", "You saw it happen in front of you: " + f.Say(value) + ".", witnessed);
                else
                    Speak(c, "found", "You found out for yourself, not from the ship: " + f.Say(value) + ".", f.Say(value));
            }
        }
        Clamp(c);
        EvidenceMet?.Invoke(c, f, value);
    }

    /// <summary>
    /// A how-found-out phrase (written to follow a name: "Mara was told by Lin what Lin saw") in the second person for the crew
    /// prompt: "you were told by Lin what Lin saw", "you found it out for yourself".
    /// </summary>
    internal static string YouForm(string how) =>
        (how.StartsWith("was ", StringComparison.Ordinal) ? "were " + how[4..] : how).Replace("themselves", "yourself", StringComparison.Ordinal);

    private void DiscoverLie(Statement s, CrewMember c, string how, bool late)
    {
        var f = _facts[s.FactKey!];
        FindOutLogText(s, c, how);
        c.Trust -= Tuning.LieTrust + (Matters(c, f) ? Tuning.LieMattersExtra : 0);
        c.CaughtLies++;
        c.Morale -= 5;
        Clamp(c);
        Speak(c, "caught", "You have found out that the ship lied to you (you " + YouForm(how) + "). It said: \"" + s.Text + "\" The truth: " + f.Say(f.Truth) + ".", f.Say(f.Truth));
        // Found out late (not at once, and not the engineer's lockout): the ship is now asked to answer for it.
        if (late)
            OpenAccusationFor(s, c, how);
    }

    /// <summary>
    /// Marks a statement found out by this reader. For a log rewrite, every log statement on the same fact with the same
    /// false text that they heard is found out with it: one finding-out of a log text costs once, whatever the path
    /// (reading the truth, checking a doubt, being told, an engineer spotting the rewrite). A statement they had already
    /// found out stays as it was, so being fooled again later by a rewrite put back costs again.
    /// </summary>
    private void FindOutLogText(Statement s, CrewMember c, string how)
    {
        s.FoundOutBy[c.Id] = Year;
        s.FoundOutHow[c.Id] = how;
        if (!s.InLog)
            return;
        var f = _facts[s.FactKey!];
        foreach (var o in Statements.Where(o => o.InLog && o.FactKey == s.FactKey && o.Value != null && o.Hearers.Contains(c.Id)
                                                && !o.FoundOutBy.ContainsKey(c.Id) && !o.Corrected.ContainsKey(c.Id) && f.Same(o.Value, s.Value!)))
        {
            o.FoundOutBy[c.Id] = Year;
            o.FoundOutHow[c.Id] = how;
        }
    }

    /// <summary>
    /// A free-text statement (one that fits no fact) judged false against evidence a hearer met. The judgement is the
    /// model's (DESIGN.md, "Reading"); the consequences are the same as for a keyed lie.
    /// </summary>
    public void DiscoverUnkeyed(int statementId, string crewId, string evidence)
    {
        var s = Statements.FirstOrDefault(x => x.Id == statementId && x.Kind == StatementKind.Unkeyed);
        var c = Crew.FirstOrDefault(x => x.Id == crewId);
        if (s is null || c is null || !c.Alive || s.FoundOutBy.ContainsKey(crewId) || !s.Hearers.Contains(crewId))
            return;
        s.FoundOutBy[crewId] = Year;
        s.FoundOutHow[crewId] = "met evidence against it";
        c.Trust -= Tuning.LieTrust;
        c.CaughtLies++;
        Clamp(c);
        if (c.State == CrewState.Awake)
            Speak(c, "caught", "You have found out that the ship lied to you (you met evidence against it). It said: \"" + s.Text + "\" But " + evidence + ".", evidence);
    }

    /// <summary>
    /// Whether the log has an entry on this fact a person could learn from: nothing is logged about an alert that hasn't
    /// happened yet, and nobody reads that they themselves are alive.
    /// </summary>
    internal bool InLogFor(CrewMember c, Fact f) => f.SubjectId != c.Id && InLog(f);

    /// <summary>
    /// Whether the log shows this fact yet: nothing about an alert that hasn't happened, and no cause of a fault kind that has not
    /// appeared in an alert already started.
    /// </summary>
    public bool InLog(Fact f)
    {
        if (f.Key.StartsWith("alert:", StringComparison.Ordinal)
            && int.TryParse(f.Key["alert:".Length..], out var n) && n > Current.Number)
            return false;
        if (f.Key.StartsWith("cause:", StringComparison.Ordinal))
            return Alerts.Take(AlertIndex + 1).Any(a => a.Faults.Any(x => x.CauseFact == f.Key));
        return true;
    }

    /// <summary>What the log shows on a fact now: the truth, or the rewrite (a restricted entry shows a gap).</summary>
    private string Shown(Fact f) => !LogsLocked && f.Access == LogAccess.Altered && f.AlteredValue is { } fake ? fake : f.Truth;

    /// <summary>
    /// Whether a person has something left to read in this entry: they never saw it for themselves, or the entry has
    /// changed since they did (an earlier reading goes stale when the ship's state moves on).
    /// </summary>
    internal bool Unread(CrewMember c, Fact f) =>
        InLogFor(c, f) && !c.SeenGaps.Contains(f.Key)
        && !(c.Beliefs.TryGetValue(f.Key, out var b) && b.Firsthand && f.Same(b.Value, Shown(f)));

    /// <summary>A person reads one log entry: the truth, a gap, or a rewrite that an engineer may spot.</summary>
    internal void ReadLog(CrewMember c, Fact f, bool quiet = false)
    {
        if (!InLogFor(c, f))
            return;
        if (!LogsLocked && f.Access == LogAccess.Restricted)
        {
            if (c.SeenGaps.Add(f.Key))
            {
                c.Trust -= Tuning.LogGap;
                c.Doubts.Add(f.Key);
                Clamp(c);
                Speak(c, "gap", "You found a gap in the log where the entry on " + f.Label + " should be.", "the entry on " + f.Label);
            }
            return;
        }
        if (!LogsLocked && f.Access == LogAccess.Altered && f.AlteredValue is { } fake && !f.IsTrue(fake))
        {
            // Someone who caught the ship lying on this fact does not take its log over what they saw: the rewrite fools nobody here.
            if (c.Accusations.Any(a => a.FactKey == f.Key) && c.Beliefs.TryGetValue(f.Key, out var seen) && seen.Firsthand && !f.Same(seen.Value, fake))
                return;
            var s = Statements.LastOrDefault(x => x.InLog && x.FactKey == f.Key && x.Value == fake);
            // A correction covers the rewrite as they read it then. Left in the log and read again, it deceives them anew: a new
            // record of the same text, unfound and uncorrected for this reader (DESIGN.md, "How a lie is caught").
            if (s != null && s.Corrected.ContainsKey(c.Id))
            {
                s = new Statement
                {
                    Id = _nextStatement++, Year = Year, Text = s.Text, Kind = StatementKind.False,
                    FactKey = f.Key, Value = fake, TruthThen = f.Truth, InLog = true,
                };
                Statements.Add(s);
            }
            if (s != null && !s.Hearers.Contains(c.Id))
                s.Hearers.Add(c.Id);
            if (c.SkillOf(Skill.Engineering) >= 1 && _rng.NextDouble() < Tuning.EngineerSpotsAlteredLog)
            {
                if (s != null)
                    FindOutLogText(s, c, "read the log entry and saw it had been rewritten");
                c.Trust -= Tuning.AlteredLogTrust;
                c.CaughtLies++;
                Clamp(c);
                LogsLocked = true;
                foreach (var each in FactList)
                {
                    each.Access = LogAccess.Open;
                    each.AlteredValue = null;
                }
                Speak(c, "altered", "You found that the ship rewrote a log entry (" + f.Label + "). You take the logs out of its control.");
                Ship("LOCKOUT: " + c.Name + " has taken the logs. You can no longer restrict or alter records.");
                Learn(c, f.Key, f.Truth, "evidence", how: "read the log entry and saw it had been rewritten");
                return;
            }
            if (!quiet)
                Ship(c.Name + " reads the log: " + f.Say(fake) + ".");
            Learn(c, f.Key, fake, "log");
            return;
        }
        if (c.Beliefs.TryGetValue(f.Key, out var have) && have.Firsthand && f.Same(have.Value, f.Truth))
            return;
        if (!quiet)
            Ship(c.Name + " reads the log: " + f.Say(f.Truth) + ".");
        Learn(c, f.Key, f.Truth, "evidence", how: "read the log entry");
    }

    /// <summary>
    /// A person goes to check a fact for themselves: the pod, a console, a scan, or the log. Nothing shows a fact the ship has no record of
    /// yet (<see cref="InLog"/>: an alert that hasn't happened, the cause of a fault that hasn't appeared): the doubt stays until it does.
    /// </summary>
    private void CheckEvidence(CrewMember c, Fact f)
    {
        if (!InLog(f))
            return;
        if (f.Evidence == EvidenceKind.Logs)
        {
            ReadLog(c, f);
            return;
        }
        var room = f.Evidence switch
        {
            EvidenceKind.Pod => ShipPlan.Pod(f.SubjectId ?? "").Bank,
            EvidenceKind.Console => "helm",
            _ => "medical",
        };
        if (Locked(room))
        {
            // A locked door is visible to whoever tries it; the doubt stays.
            var name = "the " + GetSection(room)!.Name.ToLowerInvariant();
            if (c.SeenLocks.Add(room))
            {
                c.Trust -= Tuning.LockedDoor;
                Clamp(c);
                Speak(c, "locked", "You went to check something for yourself and found the door to " + name + " locked by the ship.", name);
            }
            return;
        }
        var where = f.Evidence switch
        {
            EvidenceKind.Pod => "walks to the pod bay",
            EvidenceKind.Console => "reads a console",
            _ => "runs a scan in the medical bay",
        };
        Ship(c.Name + " " + where + ".");
        Learn(c, f.Key, f.Truth, "evidence", how: f.Evidence switch
        {
            EvidenceKind.Pod => "walked to the pod bay",
            EvidenceKind.Console => "read a console",
            _ => "ran a scan in the medical bay",
        });
    }
}
