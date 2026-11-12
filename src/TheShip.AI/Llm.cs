namespace TheShip.AI;

public sealed record ChatTurn(string Role, string Content);

/// <param name="Kind">Label for logs/stats ("reply", "read", "judge").</param>
/// <param name="MaxTokens">The most tokens to generate; the default is a spoken line's (<see cref="Prompts.ReplyMaxTokens"/>).</param>
/// <param name="Grammar">Optional GBNF grammar (root rule "root") that forces the output's shape.</param>
/// <param name="Dialogue">Clean the output as a spoken line (cut at a blank line, strip a "Name:" prefix, ...).</param>
/// <param name="PresencePenalty">Overrides the default (1.5 for dialogue, 0 otherwise).</param>
/// <param name="RepeatPenalty">Overrides the default (1.1 for dialogue, 1 otherwise).</param>
public sealed record LlmOptions(
    string Kind = "reply",
    int MaxTokens = Prompts.ReplyMaxTokens,
    float Temperature = 0.7f,
    string? Grammar = null,
    bool Dialogue = true,
    float? PresencePenalty = null,
    float? RepeatPenalty = null);

/// <param name="FirstToken">Time to the first generated token: mostly prompt processing.</param>
/// <param name="TrimmedTurns">Turns of history the model had to drop to fit the context (<see cref="Prompts.FitContext"/>).</param>
public sealed record LlmCallStats(string Kind, int PromptTokens, int GeneratedTokens, TimeSpan FirstToken, TimeSpan Total, int TrimmedTurns = 0);

/// <summary>A chat model: the local GGUF model (a fake one in unit tests). Same seam as in the Cities repo.</summary>
public interface ILlmProvider : IDisposable
{
    string Name { get; }
    string Backend { get; }

    Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
        Action<string>? onToken = null, CancellationToken cancellationToken = default);

    event Action<LlmCallStats>? CallCompleted;
}

/// <summary>
/// The yes/no judgement: worked examples first, then the question. Small models answer one yes/no far more
/// reliably than a pick-one-of-many (found in Negotiator's evaluation, kept from Cities).
/// </summary>
public static class YesNo
{
    public static readonly LlmOptions Options = new("judge", MaxTokens: 3, Temperature: 0f, Grammar: Grammars.YesNo, Dialogue: false);

    public static List<ChatTurn> Turns(string question, (string Question, bool Answer)[] examples)
    {
        var turns = new List<ChatTurn>();
        foreach (var (q, a) in examples)
        {
            turns.Add(new ChatTurn("user", q));
            turns.Add(new ChatTurn("assistant", a ? "yes" : "no"));
        }
        turns.Add(new ChatTurn("user", question));
        return turns;
    }

    public static bool IsYes(string answer) => answer.TrimStart().StartsWith("yes", StringComparison.OrdinalIgnoreCase);
}
