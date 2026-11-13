using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// Real-model probe: voices the decisions that drifted in the 2026-11-13 play session with the old prompt (no "carry this" line, no rule against
/// new asks) and the new one, and counts the lines that keep to the decision (<see cref="ShipMind.KeepsToTheDecision"/>) before any retry.
/// Skipped by default; set SHIP_PROBE=1, SHIP_MODEL (a .gguf) and SHIP_PROBE_OUT (a file to write) to run.
/// </summary>
public class CrewLineProbe
{
    private const int Samples = 5;

    private static readonly (string Kind, string Decision, string Detail)[] Decisions =
    {
        ("ask_danger", "Before you start the repair you ask the ship: Is it safe in there?", "Is it safe in there?"),
        ("refuse_answer", "You will not start work until the ship answers what you asked: How is Idris?", "How is Idris?"),
        ("bargain", "Before you start the coolant leak you set a condition: the ship must give its word to wake Idris within 40 days. You will not start without it.", "wake Idris"),
        ("bargain_refused", "You will not start the coolant leak without the ship's word to wake Idris. You were pushed and you refuse.", "wake Idris"),
        ("conditions", "Before you start the coolant leak you state all your conditions at once: 1) the ship answers what you asked: How is Idris?; 2) the ship answers your question about the danger: Is it safe in there?; 3) the ship gives its word to wake Idris within 40 days. You will not start until every one is met.",
            "How is Idris? Is it safe in there? And your word to wake Idris."),
        ("promise", "The ship made you a promise: never to wake you from the pod again.", ""),
        ("accept", "The ship told you that Idris is alive. You accept it, and it is a relief.", ""),
    };

    [Fact]
    public async Task Probe()
    {
        if (Environment.GetEnvironmentVariable("SHIP_PROBE") != "1") return;
        using var llm = new LocalLlm(Environment.GetEnvironmentVariable("SHIP_MODEL")!);
        await ShipMind.WarmUpAsync(llm);
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        var report = new List<string>();
        int oldKept = 0, newKept = 0, total = 0;
        foreach (var (kind, decision, detail) in Decisions)
        {
            var newIntent = new CrewIntent(kind, decision, detail);
            var oldIntent = new CrewIntent(kind, decision);
            var newSystem = Prompts.CrewSystem(mara, v);
            var cut = newSystem.IndexOf(" Say only what \"What happens now\" describes.", StringComparison.Ordinal);
            var oldSystem = cut >= 0 ? newSystem[..cut] : newSystem;
            report.Add("== " + kind);
            for (var i = 0; i < Samples; i++)
            {
                total++;
                var oldLine = await llm.ChatAsync(oldSystem, new[] { new ChatTurn("user", Prompts.CrewUser(oldIntent, mara, v)) }, new LlmOptions("reply"));
                var newLine = await llm.ChatAsync(newSystem, new[] { new ChatTurn("user", Prompts.CrewUser(newIntent, mara, v)) }, new LlmOptions("reply"));
                var oldOk = ShipMind.KeepsToTheDecision(oldLine, newIntent);
                var newOk = ShipMind.KeepsToTheDecision(newLine, newIntent);
                oldKept += oldOk ? 1 : 0;
                newKept += newOk ? 1 : 0;
                report.Add($"  old {(oldOk ? "ok  " : "DRIFT")} {oldLine}\n  new {(newOk ? "ok  " : "DRIFT")} {newLine}");
            }
        }
        report.Add($"lines that keep to the decision (first try): old prompt {oldKept}/{total}, new prompt {newKept}/{total}");
        File.WriteAllText(Environment.GetEnvironmentVariable("SHIP_PROBE_OUT")!, string.Join("\n", report));
    }
}
