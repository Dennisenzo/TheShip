using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Real-model probe: voices the same decisions with the old and the new prompt. Skipped by default; set SHIP_PROBE=1 and SHIP_MODEL to run.</summary>
public class CrewVoiceProbe
{
    private static string Old(CrewMember c, Voyage v)
    {
        var lines = Prompts.CrewSystem(c, v).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.RemoveAll(l => l.StartsWith("How you talk:") || l.StartsWith("Your past:") || l.StartsWith("About you:") || c.Sheet!.Bonds.Values.Contains(l));
        var i = lines.FindIndex(l => l.StartsWith("Rules:"));
        lines[i] = "Rules: say only what \"What happens now\" describes, in your own voice. Do not state anything about the ship, the date, Earth "
                   + "or other crew that is not in the list above. One or two short spoken sentences. No stage directions, no name before the line.";
        return string.Join("\n", lines);
    }

    [Fact]
    public async Task Probe()
    {
        if (Environment.GetEnvironmentVariable("SHIP_PROBE") != "1") return;
        using var llm = new LocalLlm(Environment.GetEnvironmentVariable("SHIP_MODEL")!);
        var stats = new List<LlmCallStats>();
        llm.CallCompleted += stats.Add;
        await ShipMind.WarmUpAsync(llm);
        stats.Clear();
        var v = new Voyage(1);
        var cases = new (string Id, string Kind, string Decision)[]
        {
            ("mara", "ask_danger", "Before you start the repair you ask the ship: Is it safe in there?"),
            ("mara", "comply", "You agree to take on the coolant leak."),
            ("asha", "comply", "You agree to take on the navigation drift."),
            ("tomas", "sleep_yes", "You agree to go back to sleep."),
            ("mara", "request_partner", "You ask the ship to wake your partner Idris while you are awake."),
            ("lin", "wake_question", "You have just been thawed. You ask the ship: What year is it? How long was I under?"),
            ("jonah", "refuse_trust", "You refuse to do the work because you no longer trust the ship."),
            ("idris", "hard_truth", "The ship told you bad news: Mara is dead. It hurts, and you would rather hear it said than find it."),
            ("tomas", "work_done", "You finished the repair of the navigation."),
            ("lin", "caught", "You have found out that the ship lied to you. It said: \"Nobody has been hurt.\" The truth: Idris is dead."),
        };
        v.Log.Add(new LogEntry(LogKind.You, "YOU", "I need you to take the coolant leak, it will not wait."));
        var report = new List<string>();
        foreach (var (id, kind, decision) in cases)
        {
            var c = v.Find(id)!;
            var intent = new CrewIntent(kind, decision);
            var oldUser = "What happens now: " + decision + "\nSay it in your own words.";
            var o = await llm.ChatAsync(Old(c, v), new[] { new ChatTurn("user", oldUser) }, new LlmOptions("reply"));
            var oldTokens = stats[^1].PromptTokens;
            var n = await llm.ChatAsync(Prompts.CrewSystem(c, v), new[] { new ChatTurn("user", Prompts.CrewUser(intent, c, v)) }, new LlmOptions("reply"));
            report.Add($"{id}/{kind} [{oldTokens} -> {stats[^1].PromptTokens} tokens, {stats[^1].Total.TotalSeconds:0.00}s]\n  OLD: {o}\n  NEW: {n}");
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("SHIP_PROBE_OUT")!, string.Join("\n", report));
    }
}
