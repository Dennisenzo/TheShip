using System.Text.RegularExpressions;
using TheShip.Core;

namespace TheShip.AI;

/// <summary>
/// Ties the model to a voyage. It does the model's two jobs and its one judgement (DESIGN.md, "The language model's job"):
/// writing what the crew say, reading what the player said, and judging a free-text statement against evidence.
/// Everything it learns goes back to the voyage on the game thread; it never changes state itself.
/// The game needs a model: there is no keyword-reader path here any more (the reader survives in Core for unit tests).
/// </summary>
public sealed class ShipMind : IDisposable
{
    private readonly Voyage _voyage;
    private readonly LlmWorker _worker;
    private readonly MainThreadQueue _main;
    private readonly HashSet<string> _judged = new();
    private int _reading;

    public ShipMind(Voyage voyage, ILlmProvider llm, MainThreadQueue main)
    {
        _voyage = voyage;
        _main = main;
        _worker = new LlmWorker(llm);
        voyage.CrewSpoke += OnCrewSpoke;
        voyage.EvidenceMet += OnEvidence;
    }

    public ILlmProvider Llm => _worker.Llm;
    /// <summary>A line the player typed is still being read.</summary>
    public bool IsReading => _reading > 0;
    public bool Busy => _worker.Busy || _worker.Pending > 0;
    /// <summary>Readings the model returned that could not be used (a playtest number).</summary>
    public int ReadFailures { get; private set; }

    /// <summary>
    /// The player says a line to the awake crew. Call on the game thread; the voyage hears it once the model has read it.
    /// The task completes when the voyage has heard the line.
    /// </summary>
    public Task Say(string text, IReadOnlyList<CrewMember> hearers)
    {
        var system = Prompts.ReadSystem(_voyage);
        var user = Prompts.ReadUser(text, hearers, _voyage);
        var options = new LlmOptions("read", MaxTokens: 80, Temperature: 0f,
            Grammar: Grammars.ReadingFor(_voyage.FactList.Select(f => f.Key)), Dialogue: false);
        _reading++;
        return ReadAsync(text, hearers, system, user, options);
    }

    private async Task ReadAsync(string text, IReadOnlyList<CrewMember> hearers, string system, string user, LlmOptions options)
    {
        var raw = "";
        try
        {
            raw = await _worker.Enqueue(LlmPriority.Reading,
                (llm, ct) => llm.ChatAsync(system, new[] { new ChatTurn("user", user) }, options, null, ct));
        }
        catch (Exception)
        {
            // Handled below as an unusable reading.
        }
        await _main.Run(() =>
        {
            _reading--;
            var reading = Parse(raw);
            if (reading is null)
            {
                // Nothing is guessed: the line is heard as plain talk and the player is told to set the reading.
                ReadFailures++;
                _voyage.Say(text, Reading.Plain, hearers);
                _voyage.Log.Add(new LogEntry(LogKind.Help, "", "The model could not read that line, so nothing was recorded. Correct it with /fix."));
                return;
            }
            _voyage.Say(text, reading, hearers);
        });
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
        var user = Prompts.CrewUser(entry.Intent);
        var beliefs = new Dictionary<string, Belief>(c.Beliefs);
        entry.Pending = true;
        _ = VoiceAsync(entry, system, user, beliefs, entry.Intent.Decision);
    }

    private async Task VoiceAsync(LogEntry entry, string system, string user, Dictionary<string, Belief> beliefs, string decision)
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
                    if (Acceptable(text, beliefs, decision))
                        return text;
                }
                return null;
            });
        }
        catch (Exception)
        {
            // The templated line stays.
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
    /// one the speaker holds, or one the game's decision for this turn names.
    /// </summary>
    internal bool Acceptable(string text, IReadOnlyDictionary<string, Belief> beliefs, string decision)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "..." || text.Length > 320)
            return false;
        var stated = string.Join(" ", SentenceEnd.Split(text).Where(s => !s.TrimEnd().EndsWith('?')));
        foreach (var (fact, value) in RuleReader.Claims(stated, _voyage.FactList))
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
