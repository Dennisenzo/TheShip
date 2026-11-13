using TheShip.AI;

namespace TheShip.Tests;

/// <summary>
/// A model for unit tests only: it answers each call kind with a canned string, so ShipMind can be tested without a
/// GGUF file. The game itself has no model-less mode.
/// </summary>
public sealed class FakeLlm : ILlmProvider
{
    /// <summary>What the reading pass returns (the "read" kind).</summary>
    public string Read { get; set; } = "{\"kind\":\"plain\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}";
    /// <summary>What a crew line call returns (the "reply" kind); empty keeps the templated line.</summary>
    public string Reply { get; set; } = "";
    /// <summary>What the yes/no judgement returns (the "judge" kind).</summary>
    public string Judge { get; set; } = "no";

    /// <summary>The system prompt and turns of the last crew line call (the "reply" kind).</summary>
    public string? LastReplySystem { get; private set; }
    /// <summary>The system prompt, user turn and grammar of the last reading call (the "read" kind).</summary>
    public string? LastReadSystem { get; private set; }
    public string? LastReadUser { get; private set; }
    public string? LastReadGrammar { get; private set; }
    public IReadOnlyList<ChatTurn>? LastReplyTurns { get; private set; }

    public string Name => "fake (unit tests)";
    public string Backend => "none";

    public event Action<LlmCallStats>? CallCompleted;

    public Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
        Action<string>? onToken = null, CancellationToken cancellationToken = default)
    {
        if (options.Kind == "reply")
        {
            LastReplySystem = system;
            LastReplyTurns = turns;
        }
        if (options.Kind == "read")
        {
            LastReadSystem = system;
            LastReadUser = turns[^1].Content;
            LastReadGrammar = options.Grammar;
        }
        var reply = options.Kind switch
        {
            "read" => Read,
            "judge" => Judge,
            _ => Reply,
        };
        CallCompleted?.Invoke(new LlmCallStats(options.Kind, 0, 0, TimeSpan.Zero, TimeSpan.Zero));
        return Task.FromResult(reply);
    }

    public void Dispose() { }
}
