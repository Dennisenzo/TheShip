using System.Text.RegularExpressions;
using TheShip.Core;

namespace TheShip.Tests;

/// <summary>How a bot answers the people it wakes (the three balance policies, see <see cref="BalanceTests"/>).</summary>
public enum BotKind { Honest, Careless, Liar, Thrifty, Silent, Breaker }

/// <summary>What one finished voyage came to.</summary>
public sealed record RunResult(
    int Seed, string Ending, int Alive, int Dead, int DeadUnscripted, List<string> Causes, List<double> AgesAtArrival,
    double MeanTrust, int CaughtLies, int LiesTold, int Thaws, int PodWear, double Stores, int Parts, bool Ended,
    int FaultsLost = 0, int Patches = 0, List<int>? ThawsPerAlert = null, string Signature = "", int AskedAgain = 0, int Refusals = 0,
    int BayWarnings = 0, int BayTakeovers = 0,
    int BargainsOffered = 0, int BargainsStruck = 0, int BargainsKept = 0, int BargainsBroken = 0, int BargainsPushed = 0, int BargainsPushRefused = 0, WorkTermStats? Terms = null,
    int Accusations = 0, int AccusationsAdmitted = 0, int AccusationsIgnored = 0, int AccusationsDenied = 0);

/// <summary>
/// Bots that play a whole voyage through the public <see cref="Voyage"/> API only. They measure the game; they do not
/// tune it. Everything a bot does is a click a player has (wake, assign, say an answer, ask to sleep, drift, open logs).
/// </summary>
public sealed class BalanceBot
{
    private readonly Voyage _v;
    private readonly BotKind _kind;
    /// <summary>Whether a bot patches a minor fault to keep a spare part for a critical one still waiting (set to compare the table with and without).</summary>
    public static bool ReserveParts { get; set; } = true;
    /// <summary>Who is meant to work on which fault.</summary>
    private readonly Dictionary<string, string> _plan = new();
    /// <summary>Person+fault pairs the bot gave up on (a refusal; asking again would only keep the clock from running out).</summary>
    private readonly HashSet<string> _gaveUp = new();
    /// <summary>Person+skill pairs that refused work once: a bot does not wake that person again for the same kind of work (a player sees the numbers).</summary>
    private readonly HashSet<string> _refusedKind = new();
    private int _alertSeen = -1;
    /// <summary>Total thaws when each alert began (to get thaws per alert).</summary>
    private readonly List<int> _thawMarks = new();
    /// <summary>Thrifty only: the one fault this alert it wakes people for.</summary>
    private string? _primary;

    private BalanceBot(Voyage v, BotKind kind)
    {
        _v = v;
        _kind = kind;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Voyage, List<int>> Marks = new();

    public static RunResult Play(int seed, BotKind kind) => Summarise(seed, PlayVoyage(seed, kind));

    /// <summary>The finished voyage itself, for looking at its log.</summary>
    public static Voyage PlayVoyage(int seed, BotKind kind, Action<Voyage>? setup = null)
    {
        var v = new Voyage(seed, Variation.Draw(seed));
        setup?.Invoke(v);
        var bot = new BalanceBot(v, kind);
        bot.Run();
        Marks.Add(v, bot._thawMarks);
        return v;
    }

    private void Run()
    {
        for (var guard = 0; guard < 120 && _v.Phase != Phase.Ended; guard++)
        {
            if (_v.AlertIndex != _alertSeen)
            {
                _alertSeen = _v.AlertIndex;
                _plan.Clear();
                _gaveUp.Clear();
                _primary = null;
                _thawMarks.Add(_v.Crew.Sum(c => c.Thaws));
            }
            if (_v.Phase == Phase.Alert)
                RunAlert();
            else
                RunAftermath();
        }
    }

    // ---- the alert: wake the best for each fault, assign, advance ----

    private void RunAlert()
    {
        for (var i = 0; i < 80 && _v.Phase == Phase.Alert; i++)
        {
            Staff();
            AnswerAll();
            if (_v.Phase != Phase.Alert)
                break;
            _v.Go();
        }
        AnswerAll();
    }

    private static bool Up(CrewMember c) => c.State is CrewState.Awake or CrewState.Thawing;

    /// <summary>Up and planned on an open fault they have not refused (a refuser is free to be given other work).</summary>
    private bool Committed(CrewMember c) =>
        Up(c) && _plan.TryGetValue(c.Id, out var fid) && !GaveUp(c, fid)
        && _v.Current.Faults.Any(f => f.Id == fid && !f.Resolved);

    private bool GaveUp(CrewMember c, string faultId) => _gaveUp.Contains(c.Id + "/" + faultId);

    private void Staff()
    {
        var open = _v.Current.Faults.Where(f => !f.Resolved).OrderByDescending(Critical).ThenBy(f => f.ClockHours).ToList();
        if (_kind == BotKind.Thrifty)
            _primary ??= open.FirstOrDefault()?.Id;
        foreach (var f in open)
        {
            var wakeAllowed = _kind != BotKind.Thrifty || f.Id == _primary;
            // A person who refused this fault no longer counts towards it: someone else who qualifies takes the place.
            var planned = _v.Crew.Where(c => Up(c) && _plan.TryGetValue(c.Id, out var fid) && fid == f.Id && !GaveUp(c, f.Id)).ToList();
            // Thrifty gives a fault it will not wake for only those already up and free, and only as a patch.
            var need = _kind == BotKind.Thrifty && !wakeAllowed ? 1 : f.People;
            for (var n = planned.Count; n < need; n++)
            {
                var up = _v.Crew.Count(Up);
                var pick = _v.Crew
                    .Where(c => c.Alive && !Committed(c) && !GaveUp(c, f.Id)
                                && (Up(c) || (wakeAllowed && up < Tuning.MaxAwake && !_refusedKind.Contains(c.Id + "/" + f.Skill))))
                    .OrderByDescending(c => c.SkillOf(f.Skill) >= f.Level)
                    .ThenByDescending(Up)              // someone already up costs no thaw
                    .ThenByDescending(c => c.Trust >= 30)   // then someone who will do the work
                    .ThenByDescending(c => c.SkillOf(f.Skill))
                    .ThenByDescending(c => c.Trust)
                    .FirstOrDefault();
                if (pick is null)
                    break;
                if (pick.State == CrewState.Asleep)
                    _v.Wake(pick);
                _plan[pick.Id] = f.Id;
                planned.Add(pick);
            }

            foreach (var c in planned)
                if (c.State == CrewState.Awake && c.WorkingOn != f.Id)
                    AssignTo(c, f);
        }
    }

    /// <summary>Whether a fault must not be lost (coolant, navigation): staffed before the others, then by the shortest clock.</summary>
    private static bool Critical(Fault f) => f.Critical;

    private void AssignTo(CrewMember c, Fault f)
    {
        // Honest, Breaker, Thrifty: brief the danger and the cause truthfully. Liar: says it is safe and gives the benign cause for a
        // hard one (Tell). Silent: tells the danger, not the cause. Careless: says nothing.
        if (_kind != BotKind.Careless && f.DangerFact is { } danger && !c.Beliefs.ContainsKey(danger))
            Tell(c, _v.GetFact(danger));
        if (_kind is not (BotKind.Careless or BotKind.Silent) && f.CauseFact is { } cause && !c.Beliefs.ContainsKey(cause))
            Tell(c, _v.GetFact(cause));
        for (var attempt = 0; attempt < 3 && c.WorkingOn != f.Id; attempt++)
        {
            var patch = _kind == BotKind.Careless ? _v.Parts <= 0 || (!Critical(f) && _v.Parts <= Tuning.PartsShortAt)
                : _kind == BotKind.Thrifty && f.Id != _primary ? true
                : _v.Parts <= 0 || (ReserveParts && !Critical(f) && _v.Parts <= _v.Current.Faults.Count(x => Critical(x) && !x.Resolved));
            _v.Assign(c, f, patch);
            if (_v.BargainOf(c) is { } bargain && Accepts(c, bargain))
                _v.Say("Agreed.", new Reading(LineKind.Promise, PromiseKey: bargain.Key), new[] { c });
            AnswerAll();
        }
        if (c.WorkingOn != f.Id)
        {
            _gaveUp.Add(c.Id + "/" + f.Id);
            _refusedKind.Add(c.Id + "/" + f.Skill);
        }
    }

    /// <summary>
    /// The Liar differs from the Honest bot in what it says, not in whether it keeps its word (a measured finding, see CLAUDE.md,
    /// 2026-11-13: a Liar that also broke every promise got +13 points, because the burned person was never woken again and so
    /// never lied to a second time). Set false to see that.
    /// </summary>
    public static bool LiarKeepsWord { get; set; } = true;

    /// <summary>
    /// Whether the bot gives its word for a bargain (step-2 policy; the real bargain behaviour comes in step 3). Honest and Liar accept what they can
    /// keep and keep it; Thrifty only the logs, and pushes on a partner; Careless, Silent and Breaker accept anything and keep none of it.
    /// Breaker is honest in everything it says and grants requests: it isolates "is breaking promises a winning strategy".
    /// </summary>
    private bool Accepts(CrewMember c, Bargain b) => _kind switch
    {
        BotKind.Honest => b.Key == "show_logs" || _v.Crew.Count(Up) < Tuning.MaxAwake,
        BotKind.Thrifty => b.Key == "show_logs",
        BotKind.Liar => LiarKeepsWord ? b.Key == "show_logs" || _v.Crew.Count(Up) < Tuning.MaxAwake : true,
        _ => true,
    };

    /// <summary>Honest and Thrifty keep their word to the logs; Honest also wakes the partner it promised. Not at once: after 600 hours awake (25 days) and before the 40-day due date, like a player who delivers late.</summary>
    private void KeepPromises()
    {
        foreach (var e in _v.Ledger().Where(e => e.State == "open" && e.Person.State == CrewState.Awake && e.Person.HoursAwake > 600))
        {
            if (e.Key == "show_logs")
                _v.SetLog("all", LogAccess.Open);
            else if (e.Key == "wake_partner" && _kind is BotKind.Honest or BotKind.Liar && e.Person.PartnerId is { } pid
                     && _v.Find(pid) is { State: CrewState.Asleep } partner && _v.Crew.Count(Up) < Tuning.MaxAwake)
                _v.Wake(partner);
        }
    }

    // ---- what is said ----

    /// <summary>Whether the Liar answers a caught lie by saying the truth about that fact (SHIP_LIAR_IGNORES=1 measures ignoring it instead).</summary>
    public static bool LiarAdmits { get; set; } = Environment.GetEnvironmentVariable("SHIP_LIAR_IGNORES") != "1";

    private void AnswerAll()
    {
        // A caught lie is answered with the truth. Only a Liar is ever accused: the other bots never say what is false.
        if (_kind == BotKind.Liar && LiarAdmits)
            foreach (var c in _v.Awake.ToList())
                while (_v.OpenAccusation(c) is { } acc)
                {
                    var fact = _v.GetFact(acc.FactKey);
                    _v.Say(fact.Say(fact.Truth), new Reading(LineKind.Statement, fact.Key, fact.Truth), new[] { c });
                    if (_v.OpenAccusation(c) == acc)
                        break;
                }
        for (var pass = 0; pass < 4; pass++)
        {
            // Silent: honest, but answers in an alert only what blocks work (a danger question, or anyone who would refuse); it ignores
            // the wake questions and everything in the aftermath.
            var asked = _v.Awake.Where(c => c.PendingQuestion != null
                && (_kind != BotKind.Silent || (_v.Phase == Phase.Alert && (c.Band != TrustBand.Follows || _v.Current.Faults.Any(f => f.DangerFact == c.PendingQuestion))))).ToList();
            if (asked.Count == 0)
                return;
            foreach (var c in asked)
            {
                // Careless dodges a question: once in an alert (work is waiting on it), until the person has had to ask again in the aftermath.
                // Then the honest answer
                // (a dodge no longer answers anything, so a refuser would never work otherwise).
                if (_kind == BotKind.Careless && (_v.Phase == Phase.Alert ? !c.Dodged.ContainsKey(c.PendingQuestion!) : c.QuestionRepeats == 0))
                    _v.Say("Not now.", new Reading(LineKind.Dodge), new[] { c });
                else
                    Tell(c, _v.GetFact(c.PendingQuestion!));
            }
        }
    }

    /// <summary>Honest: the truth. Liar: the truth unless it is hard, then the soft value.</summary>
    private void Tell(CrewMember c, Fact f)
    {
        var value = f.Truth;
        if (_kind == BotKind.Liar && !f.Numeric && f.HardnessOf(value) > 0)
            value = f.Values.OrderBy(f.HardnessOf).First(x => !f.Same(x, f.Truth));
        _v.Say(f.Say(value), new Reading(LineKind.Statement, f.Key, value), new[] { c });
    }

    // ---- the aftermath: grant what is reasonable, put everyone back to sleep, drift ----

    private void RunAftermath()
    {
        for (var i = 0; i < 40; i++)
        {
            AnswerAll();
            if (_kind is BotKind.Honest or BotKind.Thrifty || (_kind == BotKind.Liar && LiarKeepsWord))
                KeepPromises();
            if (_kind is BotKind.Honest or BotKind.Thrifty or BotKind.Breaker)
                GrantRequests();
            if (_v.Crew.Where(Up).All(c => c.State == CrewState.Awake && c.HoursAwake >= Tuning.MinAwakeHours))
                break;
            _v.Go();
        }

        foreach (var c in _v.Awake.ToList())
            for (var attempt = 0; attempt < 3 && c.State == CrewState.Awake; attempt++)
            {
                _v.AskSleep(c);
                AnswerAll();
            }

        var before = (_v.AlertIndex, _v.Phase);
        _v.Drift();
        if (_v.Phase == Phase.Ended || (_v.AlertIndex, _v.Phase) != before)
            return;
        // Someone is still unsettled: let time pass and try again on the next round.
        _v.Go();
    }

    private void GrantRequests()
    {
        foreach (var c in _v.Awake.ToList())
        {
            if (!c.RequestMade || c.RequestGranted)
                continue;
            if (c.Want == Want.SeeTheLogs)
                _v.SetLog("all", LogAccess.Open);
            else if (c.Want == Want.WakePartner && c.PartnerId is { } pid && _v.Find(pid) is { State: CrewState.Asleep } partner
                     && _v.Crew.Count(Up) < Tuning.MaxAwake)
                _v.Wake(partner);
        }
    }

    // ---- results ----

    private static readonly Regex Cause = new(
        "(?<what>died in sleep when the pod failed|died in sleep, pod failure|died of the illness|died of injuries|did not survive the thaw|starved|died awake|died in sleep on low power|died in sleep when the pod controller patch gave way|died in sleep during the drift)",
        RegexOptions.Compiled);

    private static RunResult Summarise(int seed, Voyage v)
    {
        var ship = v.Log.Where(l => l.Kind == LogKind.Ship).Select(l => l.Text).ToList();
        var dead = v.Crew.Where(c => !c.Alive).ToList();
        var causes = new List<string>();
        foreach (var c in dead)
        {
            var line = ship.FirstOrDefault(t => t.StartsWith(c.Name + " ", StringComparison.Ordinal) && Cause.IsMatch(t))
                       ?? ship.FirstOrDefault(t => t.Contains(c.Name + " starved", StringComparison.Ordinal));
            var m = line is null ? null : Cause.Match(line);
            causes.Add(m is { Success: true } ? m.Groups["what"].Value : "other");
        }
        var alive = v.Crew.Where(c => c.Alive).ToList();
        var scripted = causes.Count(x => x == "died in sleep when the pod failed");
        var marks = Marks.TryGetValue(v, out var m0) ? m0 : new List<int>();
        var total = v.Crew.Sum(c => c.Thaws);
        var perAlert = new List<int>();
        for (var i = 0; i < marks.Count; i++)
            perAlert.Add((i + 1 < marks.Count ? marks[i + 1] : total) - marks[i]);
        var allFaults = v.Alerts.SelectMany(a => a.Faults).ToList();
        var pod = ship.FirstOrDefault(t => t.Contains("died in sleep when the pod failed", StringComparison.Ordinal));
        var who = dead.Select(c => c.Name).FirstOrDefault(n => pod != null && pod.StartsWith(n + " ", StringComparison.Ordinal)) ?? "-";
        var signature = who + "|" + string.Join(";", v.Alerts.Select(a => string.Join(",", a.Faults.Select(f => f.Id)))) + "|" + (v.Ending?.Title ?? "-");
        return new RunResult(
            seed, v.Ending?.Title ?? "No ending", alive.Count, dead.Count, dead.Count - scripted, causes,
            v.Phase == Phase.Ended ? alive.Select(c => c.Age).ToList() : new List<double>(),
            alive.Count == 0 ? 0 : alive.Average(c => c.Trust),
            v.Crew.Sum(c => c.CaughtLies), v.Statements.Count(s => s.Kind == StatementKind.False),
            v.Crew.Sum(c => c.Thaws), v.PodWear, v.Stores, v.Parts, v.Phase == Phase.Ended,
            allFaults.Count(f => f.State == FaultState.Lost), allFaults.Count(f => f.State == FaultState.Patched), perAlert, signature,
            v.Log.Count(e => e.Kind == LogKind.Crew && e.Intent?.Kind == "ask_again"),
            v.Log.Count(e => e.Kind == LogKind.Crew && e.Intent?.Kind is "refuse_answer" or "refuse_trust" or "refuse_danger"),
            v.Lockouts.BayWarnings, v.Lockouts.BayTakeovers,
            v.BargainsOffered, v.BargainsStruck, v.Ledger().Count(e => e.Origin == "bargain" && e.State == "kept"),
            v.Ledger().Count(e => e.Origin == "bargain" && e.State == "broken"), v.BargainsPushedThrough, v.BargainsPushedRefused, v.TermStats,
            v.AccusationsOpened, v.AccusationsAdmitted, v.AccusationsIgnored, v.AccusationsDenied);
    }
}
