namespace TheShip.AI;

/// <summary>
/// Stand-in when there's no model file. It writes nothing of its own: <see cref="ShipMind"/> sees
/// <see cref="IsReal"/> is false and keeps the game's templated crew lines and the keyword reader
/// (DESIGN.md, "No model"). Asked anyway, it gives the empty or negative answer.
/// </summary>
public sealed class ScriptedLlm : ILlmProvider
{
    public string Name => "templates (no model)";
    public bool IsReal => false;
    public string Backend => "none";

    public event Action<LlmCallStats>? CallCompleted;

    public Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
        Action<string>? onToken = null, CancellationToken cancellationToken = default)
    {
        var reply = options.Kind switch
        {
            "judge" => "no",
            "read" => "{\"kind\":\"plain\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}",
            _ => "",
        };
        CallCompleted?.Invoke(new LlmCallStats(options.Kind, 0, 0, TimeSpan.Zero, TimeSpan.Zero));
        return Task.FromResult(reply);
    }

    public void Dispose() { }
}
