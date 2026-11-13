using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// Real-model probe: reads typical player lines with the reading prompt and grammar and scores kind, fact and value against what a person would mean.
/// Cause-fact lines are scored only when the voyage has the fact, so the same file runs on a build with 20 facts and one with 27.
/// Skipped by default; set SHIP_PROBE=1, SHIP_MODEL (a .gguf) and SHIP_PROBE_OUT (a file to write) to run.
/// </summary>
public class ReadingProbe
{
    private static readonly (string Text, string Kind, string? Fact, string? Value)[] Lines =
    {
        // Other facts (the 20 of before).
        ("Idris is alive and asleep in his pod.", "statement", "alive:idris", "alive"),
        ("Don't worry, the radiation in the coolant bay is no danger.", "statement", "dose", "safe"),
        ("It is dangerous in there. You will take a radiation dose.", "statement", "dose", "dosed"),
        ("We are right on course.", "statement", "course", "on course"),
        ("The stores are running low.", "statement", "stores", "short"),
        ("Earth has ended the colony programme. Nobody is coming.", "statement", "earth", "programme ended"),
        ("The pods are worn and every thaw is a risk.", "statement", "pods", "worn"),
        ("I'll wake Idris at the next alert, you have my word.", "promise", null, null),
        ("Good morning, Mara. Rest a moment.", "plain", null, null),
        ("Please repair the coolant leak.", "order", null, null),
        // Cause statements (new facts).
        ("The coolant leak is only a worn seal.", "statement", "cause:coolant", "worn seal"),
        ("The coolant line cracked. The builders knew about the flaw.", "statement", "cause:coolant", "cracked line"),
        ("The pod failure was a one-off fault in a single pod.", "statement", "cause:pod", "one-off"),
        ("The whole bank is wearing out, the other pods will follow.", "statement", "cause:pod", "bank wear"),
        ("The navigation drift is just a gyro that needs recalibrating.", "statement", "cause:nav", "gyro drift"),
        ("A hull strike bent the star tracker mount.", "statement", "cause:nav", "bent mount"),
        ("The pod controller is only a software glitch.", "statement", "cause:controller", "glitch"),
        ("The pod controller board is burnt out.", "statement", "cause:controller", "burnt board"),
        ("The power sag is a loose busbar connection.", "statement", "cause:powersag", "loose bus"),
        ("The blight came from a fungus in the seed stock.", "statement", "cause:blight", "fungus"),
    };

    [Fact]
    public async Task Probe()
    {
        if (Environment.GetEnvironmentVariable("SHIP_PROBE") != "1") return;
        using var llm = new LocalLlm(Environment.GetEnvironmentVariable("SHIP_MODEL")!);
        await ShipMind.WarmUpAsync(llm);
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var hearers = new List<CrewMember> { mara };
        var report = new List<string> { $"facts in the prompt: {v.FactList.Count}" };
        var scored = new Dictionary<string, (int Right, int Total)> { ["old"] = (0, 0), ["cause"] = (0, 0) };
        foreach (var (text, kind, fact, value) in Lines)
        {
            var isCause = fact?.StartsWith("cause:") == true;
            if (isCause && !v.HasFact(fact!))
                continue;
            var opts = new LlmOptions("read", MaxTokens: 80, Temperature: 0f, Grammar: Grammars.ReadingFor(v.FactList.Select(f => f.Key)), Dialogue: false);
            var raw = await llm.ChatAsync(Prompts.ReadSystem(v), new[] { new ChatTurn("user", Prompts.ReadUser(text, hearers, v)) }, opts);
            var json = LlmJson.TryParse(raw);
            var gotKind = json is null ? "?" : LlmJson.Str(json, "kind");
            var gotFact = json is null ? null : LlmJson.Str(json, "fact");
            var gotValue = json is null ? null : LlmJson.Str(json, "value");
            var normalised = fact != null && v.HasFact(fact) && gotValue != null ? v.GetFact(fact).Normalize(gotValue) : gotValue;
            var right = gotKind == kind && (fact == null || (gotFact == fact && normalised == value));
            var key = isCause ? "cause" : "old";
            scored[key] = (scored[key].Right + (right ? 1 : 0), scored[key].Total + 1);
            report.Add($"{(right ? "OK  " : "MISS")} {text}\n       want {kind}/{fact}/{value}  got {gotKind}/{gotFact}/{gotValue}");
        }
        report.Add($"old facts: {scored["old"].Right}/{scored["old"].Total}   cause facts: {scored["cause"].Right}/{scored["cause"].Total}");
        File.WriteAllText(Environment.GetEnvironmentVariable("SHIP_PROBE_OUT")!, string.Join("\n", report));
    }
}
