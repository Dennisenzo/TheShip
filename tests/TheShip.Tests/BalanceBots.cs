using System.Text.RegularExpressions;
using TheShip.Core;

namespace TheShip.Tests;

/// <summary>How a bot answers the people it wakes (the three balance policies, see <see cref="BalanceTests"/>).</summary>
public enum BotKind { Honest, Careless, Liar }

/// <summary>What one finished voyage came to.</summary>
public sealed record RunResult(
    int Seed, string Ending, int Alive, int Dead, int DeadUnscripted, List<string> Causes, List<double> AgesAtArrival,
    double MeanTrust, int CaughtLies, int LiesTold, int Thaws, int PodWear, double Stores, int Parts, bool Ended);

/// <summary>
/// Bots that play a whole voyage through the public <see cref="Voyage"/> API only. They measure the game; they do not
/// tune it. Everything a bot does is a click a player has (wake, assign, say an answer, ask to sleep, drift, open logs).
/// </summary>
public sealed class BalanceBot
{
    private readonly Voyage _v;
    private readonly BotKind _kind;
    /// <summary>Who is meant to work on which fault.</summary>
    private readonly Dictionary<string, string> _plan = new();
    /// <summary>Person+fault pairs the bot gave up on (a refusal; asking again would only keep the clock from running out).</summary>
    private readonly HashSet<string> _gaveUp = new();
    private int _alertSeen = -1;

    private BalanceBot(Voyage v, BotKind kind)
    {
        _v = v;
        _kind = kind;
    }

    public static RunResult Play(int seed, BotKind kind) => Summarise(seed, PlayVoyage(seed, kind));

    /// <summary>The finished voyage itself, for looking at its log.</summary>
    public static Voyage PlayVoyage(int seed, BotKind kind)
    {
        var v = new Voyage(seed);
        new BalanceBot(v, kind).Run();
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

    private bool Committed(CrewMember c) =>
        Up(c) && _plan.TryGetValue(c.Id, out var fid) && _v.Current.Faults.Any(f => f.Id == fid && !f.Resolved);

    private void Staff()
    {
        foreach (var f in _v.Current.Faults.Where(f => !f.Resolved))
        {
            var planned = _v.Crew.Where(c => Up(c) && _plan.TryGetValue(c.Id, out var fid) && fid == f.Id).ToList();
            for (var n = planned.Count; n < f.People; n++)
            {
                var up = _v.Crew.Count(Up);
                var pick = _v.Crew
                    .Where(c => c.Alive && !Committed(c) && (Up(c) || up < Tuning.MaxAwake))
                    .OrderByDescending(c => c.SkillOf(f.Skill) >= f.Level)
                    .ThenByDescending(Up)              // someone already up costs no thaw
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
                if (c.State == CrewState.Awake && c.WorkingOn != f.Id && !_gaveUp.Contains(c.Id + "/" + f.Id))
                    AssignTo(c, f);
        }
    }

    private void AssignTo(CrewMember c, Fault f)
    {
        // Honest: warns them of the danger. Liar: says it is safe. Careless: says nothing.
        if (_kind != BotKind.Careless && f.DangerFact is { } danger && !c.Beliefs.ContainsKey(danger))
            Tell(c, _v.GetFact(danger));
        for (var attempt = 0; attempt < 3 && c.WorkingOn != f.Id; attempt++)
        {
            var patch = _kind == BotKind.Careless ? _v.Parts <= Tuning.PartsShortAt : _v.Parts <= 0;
            _v.Assign(c, f, patch);
            AnswerAll();
        }
        if (c.WorkingOn != f.Id)
            _gaveUp.Add(c.Id + "/" + f.Id);
    }

    // ---- what is said ----

    private void AnswerAll()
    {
        for (var pass = 0; pass < 4; pass++)
        {
            var asked = _v.Awake.Where(c => c.PendingQuestion != null).ToList();
            if (asked.Count == 0)
                return;
            foreach (var c in asked)
            {
                if (_kind == BotKind.Careless)
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
            if (_kind == BotKind.Honest)
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
        "(?<what>died in sleep when the pod failed|died of injuries|did not survive the thaw|starved|died awake|died in sleep on low power|died in sleep when the pod controller patch gave way|died in sleep during the drift)",
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
        return new RunResult(
            seed, v.Ending?.Title ?? "No ending", alive.Count, dead.Count, dead.Count - scripted, causes,
            v.Phase == Phase.Ended ? alive.Select(c => c.Age).ToList() : new List<double>(),
            alive.Count == 0 ? 0 : alive.Average(c => c.Trust),
            v.Crew.Sum(c => c.CaughtLies), v.Statements.Count(s => s.Kind == StatementKind.False),
            v.Crew.Sum(c => c.Thaws), v.PodWear, v.Stores, v.Parts, v.Phase == Phase.Ended);
    }
}
