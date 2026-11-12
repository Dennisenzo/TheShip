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

    public string Name => "fake (unit tests)";
    public string Backend => "none";

    public event Action<LlmCallStats>? CallCompleted;

    public Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
        Action<string>? onToken = null, CancellationToken cancellationToken = default)
    {
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
