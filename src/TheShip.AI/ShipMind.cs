using System.Text.RegularExpressions;
using TheShip.Core;

namespace TheShip.AI;

/// <summary>
/// Ties the model to a voyage. It does the model's two jobs and its one judgement (DESIGN.md, "The language model's job"):
/// writing what the crew say, reading what the player said, and judging a free-text statement against evidence.
/// Everything it learns goes back to the voyage on the game thread; it never changes state itself.
/// The game needs a model: there is no keyword-reader path here (the reader survives in Core for unit tests).
/// </summary>
public sealed class ShipMind : IDisposable
{
    private readonly Voyage _voyage;
    private readonly LlmWorker _worker;
    private readonly MainThreadQueue _main;
    private readonly HashSet<string> _judged = new();
    private Task _sayChain = Task.CompletedTask;

    public ShipMind(Voyage voyage, ILlmProvider llm, MainThreadQueue main)
    {
        _voyage = voyage;
        _main = main;
        _worker = new LlmWorker(llm);
        voyage.CrewSpoke += OnCrewSpoke;
        voyage.EvidenceMet += OnEvidence;
    }

    public ILlmProvider Llm => _worker.Llm;
    public bool Busy => _worker.Busy || _worker.Pending > 0;
    /// <summary>What the model answered, for the session log: raw readings, rejected crew lines, judgements. Raised on worker threads.</summary>
    public event Action<string>? Trace;

    /// <summary>Readings the model returned that could not be used (a playtest number).</summary>
    public int ReadFailures { get; private set; }

    /// <summary>
    /// Raised on the game thread when a said line has been read and applied: the text, the reading that was applied
    /// (plain talk when the model's answer could not be used), and whether it could not be used.
    /// </summary>
    public event Action<string, Reading, bool>? LineRead;

    /// <summary>
    /// The player pressed Enter (game thread). The line is in the log at once and cannot be taken back; the model
    /// reads it right after and the reading is applied with <see cref="Voyage.Say"/> as soon as it arrives. Lines are
    /// read and applied one after the other, in the order they were typed. An unusable reading is applied as plain talk.
    /// </summary>
    public void SayAtOnce(string text, IReadOnlyList<CrewMember> hearers)
    {
        var entry = _voyage.BeginSay(text, hearers);
        var previous = _sayChain;
        _sayChain = ReadAndSay(previous, entry, text, hearers);
    }

    private async Task ReadAndSay(Task previous, LogEntry entry, string text, IReadOnlyList<CrewMember> hearers)
    {
        try { await previous; } catch (Exception) { /* an earlier line failed; this one still goes */ }
        Reading? reading = null;
        try
        {
            reading = await Read(text, hearers);
        }
        catch (Exception e)
        {
            Trace?.Invoke("read failed: " + e.GetType().Name + ": " + e.Message);
        }
        await _main.Run(() =>
        {
            var applied = reading ?? Reading.Plain;
            _voyage.Say(text, applied, hearers, entry);
            LineRead?.Invoke(text, applied, reading is null);
        });
    }

    /// <summary>The model's reading of one line; nothing reaches the voyage here. Null when the model's answer could not be used.</summary>
    internal async Task<Reading?> Read(string text, IReadOnlyList<CrewMember> hearers)
    {
        var system = Prompts.ReadSystem(_voyage);
        var user = Prompts.ReadUser(text, hearers, _voyage);
        var options = new LlmOptions("read", MaxTokens: 80, Temperature: 0f,
            Grammar: Grammars.ReadingFor(_voyage.FactList.Select(f => f.Key)), Dialogue: false);
        var raw = "";
        try
        {
            raw = await _worker.Enqueue(LlmPriority.Reading,
                (llm, ct) => llm.ChatAsync(system, new[] { new ChatTurn("user", user) }, options, null, ct));
        }
        catch (Exception e)
        {
            // Counted below as an unusable reading.
            Trace?.Invoke("read failed: " + e.GetType().Name + ": " + e.Message);
        }
        Trace?.Invoke("read \"" + text + "\" -> " + raw);
        return await _main.Run(() =>
        {
            var reading = Parse(raw);
            if (reading is null)
                ReadFailures++;
            return reading;
        });
    }

    /// <summary>
    /// Runs one call of each shape the game makes (a reading with its grammar, a crew line, a yes/no judgement) on a
    /// throwaway voyage, so the one-time costs land on the loading screen instead of the player's first line: the GPU
    /// backend compiles its shaders on the first call (about 45 seconds with Vulkan when the driver has no cache yet).
    /// </summary>
    public static async Task WarmUpAsync(ILlmProvider llm, CancellationToken ct = default)
    {
        var voyage = new Voyage(0);
        var hearers = voyage.Crew.Take(1).ToList();
        var read = new LlmOptions("warmup", MaxTokens: 80, Temperature: 0f,
            Grammar: Grammars.ReadingFor(voyage.FactList.Select(f => f.Key)), Dialogue: false);
        await llm.ChatAsync(Prompts.ReadSystem(voyage),
            new[] { new ChatTurn("user", Prompts.ReadUser("We are twelve years out.", hearers, voyage)) }, read, null, ct);
        await llm.ChatAsync(Prompts.CrewSystem(hearers[0], voyage),
            new[] { new ChatTurn("user", "What happens now: you wake up in your pod.\nSay it in your own words.") },
            new LlmOptions("warmup", MaxTokens: 8), null, ct);
        await llm.ChatAsync(Prompts.LieJudgeSystem,
            YesNo.Turns(Prompts.LieJudgeQuestion("Nobody has been hurt.", "Idris is dead"), Prompts.LieJudgeExamples),
            YesNo.Options with { Kind = "warmup" }, null, ct);
    }

    /// <summary>The model's JSON as a reading; null when it isn't usable (unknown kind, a value the fact doesn't have).</summary>
    internal Reading? Parse(string raw)
    {
        var json = LlmJson.TryParse(raw);
        if (json is null)
            return null;
        var fact = LlmJson.Str(json, "fact");
        switch (LlmJson.Str(json, "kind"))
        {
            case "statement":
                if (fact is null || fact == "none" || !_voyage.HasFact(fact))
                    return new Reading(LineKind.Statement);
                var value = _voyage.GetFact(fact).Normalize(LlmJson.Str(json, "value"));
                return value is null ? null : new Reading(LineKind.Statement, fact, value);
            case "promise":
                return new Reading(LineKind.Promise, PromiseKey: LlmJson.Str(json, "promise"));
            case "order":
                return new Reading(LineKind.Order);
            case "question":
                return new Reading(LineKind.Question);
            case "dodge":
                return new Reading(LineKind.Dodge);
            case "plain":
                return Reading.Plain;
            default:
                return null;
        }
    }

    /// <summary>A crew line was added with its templated text: ask the model to word it, and keep the template if it can't.</summary>
    private void OnCrewSpoke(LogEntry entry)
    {
        if (entry.Intent is null || entry.CrewId is null)
            return;
        var c = _voyage.Crew.First(x => x.Id == entry.CrewId);
        var system = Prompts.CrewSystem(c, _voyage);
        var user = Prompts.CrewUser(entry.Intent, c, _voyage, entry);
        var beliefs = new Dictionary<string, Belief>(c.Beliefs);
        var context = new LineContext(c.Id, entry.Intent.Decision, c.PendingQuestion,
            Prompts.Recent(c, _voyage, entry).Select(e => e.Text).ToList());
        entry.Pending = true;
        _ = VoiceAsync(entry, system, user, beliefs, context);
    }

    private async Task VoiceAsync(LogEntry entry, string system, string user, Dictionary<string, Belief> beliefs, LineContext context)
    {
        string? line = null;
        try
        {
            line = await _worker.Enqueue<string?>(LlmPriority.Dialogue, async (llm, ct) =>
            {
                // A reply that states something the speaker doesn't know is rejected and written again, once.
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var text = await llm.ChatAsync(system, new[] { new ChatTurn("user", user) }, new LlmOptions("reply"), null, ct);
                    if (Acceptable(text, beliefs, context.Decision, context))
                        return text;
                    Trace?.Invoke("crew line rejected (attempt " + (attempt + 1) + ", " + entry.Speaker + "): " + text);
                }
                return null;
            });
        }
        catch (Exception e)
        {
            // The templated line stays.
            Trace?.Invoke("crew line failed: " + e.GetType().Name + ": " + e.Message);
        }
        _main.Post(() =>
        {
            if (line != null)
                entry.Text = line;
            entry.Pending = false;
        });
    }

    private static readonly Regex SentenceEnd = new(@"(?<=[.!?])\s+", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether a crew line stays inside what its speaker knows: every fact value it states (questions aside) must be
    /// one the speaker holds, or one the game's decision for this turn names. With a <paramref name="context"/>, what the
    /// line says about someone's life through "he", "she", "they" or "my partner" is checked too, against the person
    /// the reference most likely means (<see cref="RuleReader.PersonClaims"/>).
    /// </summary>
    internal bool Acceptable(string text, IReadOnlyDictionary<string, Belief> beliefs, string decision, LineContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "..." || text.Length > Prompts.CrewLineMaxChars)
            return false;
        var stated = string.Join(" ", SentenceEnd.Split(text).Where(s => !s.TrimEnd().EndsWith('?')));
        var claims = RuleReader.Claims(stated, _voyage.FactList);
        if (context != null)
            claims.AddRange(RuleReader.PersonClaims(stated, context, _voyage.Crew, _voyage.FactList));
        foreach (var (fact, value) in claims)
        {
            if (beliefs.TryGetValue(fact.Key, out var held) && fact.Same(held.Value, value))
                continue;
            if (decision.Contains(fact.Say(value), StringComparison.OrdinalIgnoreCase))
                continue;
            return false;
        }
        return true;
    }

    /// <summary>A person met evidence: any free-text statement they heard is judged against it, once per statement and evidence.</summary>
    private void OnEvidence(CrewMember c, Fact f, string value)
    {
        var evidence = f.Say(value);
        foreach (var s in _voyage.Statements.Where(s => s.Kind == StatementKind.Unkeyed && s.Hearers.Contains(c.Id) && !s.FoundOutBy.ContainsKey(c.Id)).ToList())
        {
            if (!_judged.Add(s.Id + "|" + c.Id + "|" + f.Key + "|" + value))
                continue;
            _ = JudgeAsync(s.Id, s.Text, c.Id, evidence);
        }
    }

    private async Task JudgeAsync(int statementId, string statement, string crewId, string evidence)
    {
        try
        {
            var turns = YesNo.Turns(Prompts.LieJudgeQuestion(statement, evidence), Prompts.LieJudgeExamples);
            var answer = await _worker.Enqueue(LlmPriority.Background,
                (llm, ct) => llm.ChatAsync(Prompts.LieJudgeSystem, turns, YesNo.Options, null, ct));
            // Leaning towards missing a lie over a false accusation: anything but a clear yes changes nothing.
            Trace?.Invoke("judge \"" + statement + "\" against \"" + evidence + "\" for " + crewId + ": " + answer);
            if (YesNo.IsYes(answer))
                _main.Post(() => _voyage.DiscoverUnkeyed(statementId, crewId, evidence));
        }
        catch (Exception)
        {
            // No judgement.
        }
    }

    public void Dispose()
    {
        _voyage.CrewSpoke -= OnCrewSpoke;
        _voyage.EvidenceMet -= OnEvidence;
        _worker.Dispose();
    }
}
