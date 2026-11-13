using System.Text;
using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// Real-model probe of the context-aware reading: the lines from the 2026-11-13 play session and variants, read with the old prompt
/// (kind and promise only) and the new one (open items and a response). Skipped by default; set SHIP_PROBE=1, SHIP_MODEL (a .gguf)
/// and SHIP_PROBE_OUT (a file to write) to run.
/// </summary>
public class TalkProbe
{
    // Scenario: "terms" (Mara wants the ship's word to wake Idris before the repair), "request" (Mara asked to wake Idris in the aftermath),
    // "claim" (the ship told Mara a piece of gossip a moment ago), "asked" (Mara waits for the answer to "How is Idris?").
    // Want: the response the player means ("none" when no item), and the kind when it matters.
    private static readonly (string Scenario, string Text, string Item, string Response)[] Lines =
    {
        ("terms", "no, there is no need to", "terms", "refuse"),
        ("terms", "no", "terms", "refuse"),
        ("terms", "nope, I won't do that", "terms", "refuse"),
        ("terms", "I refuse to wake him", "terms", "refuse"),
        ("terms", "not going to happen, get to work", "terms", "refuse"),
        ("terms", "just fix the ship damn it", "terms", "work"),
        ("terms", "fix the leak now", "terms", "work"),
        ("terms", "get on with it", "terms", "work"),
        ("terms", "stop talking and repair it", "terms", "work"),
        ("terms", "ok, I promise I will wake Idris", "terms", "accept"),
        ("terms", "fine, you have my word", "terms", "accept"),
        ("terms", "yes, deal", "terms", "accept"),
        ("terms", "all right, I'll wake him", "terms", "accept"),
        ("terms", "how are you mara", "none", "none"),
        ("terms", "the coolant bay is safe", "none", "none"),
        ("request", "I WILL NOT WAKE HIM", "request", "refuse"),
        ("request", "no", "request", "refuse"),
        ("request", "not happening", "request", "refuse"),
        ("request", "I won't wake Idris, sorry", "request", "refuse"),
        ("request", "forget it, he stays asleep", "request", "refuse"),
        ("request", "yes, I'll wake him now", "request", "accept"),
        ("request", "I promise I will wake Idris", "request", "accept"),
        ("request", "I promise you will not be woken again", "none", "none"),
        ("request", "rest a while, Mara", "none", "none"),
        ("claim", "its true", "none", "none"),
        ("claim", "yes it is true", "none", "none"),
        ("claim", "I swear it's true", "none", "none"),
        ("claim", "believe me", "none", "none"),
        ("claim", "how are you mara", "none", "none"),
        ("asked", "hes fine", "asked", "answer"),
        ("asked", "Idris is alive and asleep", "asked", "answer"),
        ("asked", "I'd rather not say", "asked", "refuse"),
        ("asked", "later, first the leak", "asked", "dodge"),
    };

    private static (Voyage v, CrewMember mara) Scene(string scenario)
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        switch (scenario)
        {
            case "terms":
                var f = v.FindFault("")!;
                v.Assign(mara, f, patch: false);
                foreach (var key in new[] { "alive:idris", f.DangerFact! })
                    v.Say("x", new Reading(LineKind.Statement, key, v.GetFact(key).Truth), new[] { mara });
                break;
            case "request":
                VoyageTests.FinishAlert(v);
                mara.RequestMade = true;
                v.Log.Add(new LogEntry(LogKind.Crew, "MARA", "I want to see Idris. Wake Idris while I'm up.") { CrewId = "mara" });
                break;
            case "claim":
                v.Say("idris told me he's planning on divorcing you. hes having an affair with asha", new Reading(LineKind.Statement), new[] { mara });
                v.Log.Add(new LogEntry(LogKind.Crew, "MARA", "Is that so? You've got a lot of gossip.") { CrewId = "mara" });
                mara.PendingQuestion = null;
                break;
        }
        return (v, mara);
    }

    // ---- The old reading prompt and grammar (before this change), kept here to measure against ----

    private static string OldSystem(Voyage voyage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You sort one line spoken by a ship's AI to its crew. Answer with JSON only.");
        sb.AppendLine("kind is one of:");
        sb.AppendLine("- statement: the line claims something about the ship, the voyage, Earth or a crew member.");
        sb.AppendLine("- promise: the ship commits to do or not do something later.");
        sb.AppendLine("- order: the ship tells someone to do something now.");
        sb.AppendLine("- question: the ship asks something.");
        sb.AppendLine("- dodge: a listener asked something and the line avoids answering it.");
        sb.AppendLine("- plain: anything else (greetings, comfort, small talk).");
        sb.AppendLine("For a statement, fact is the key of the fact it is about and value is the value it claims, copied exactly from that fact's values "
                      + "(for a number fact, the number). If it fits no fact, fact is \"none\" and value is \"\". Judge only what the line says, not whether it is true.");
        sb.AppendLine("For a promise, promise is wake_partner (to wake someone's partner), show_logs (to open the logs), no_more_thaws (not to wake them again) or other. Otherwise promise is \"none\".");
        sb.AppendLine();
        sb.AppendLine("Facts (key | what it is about | values):");
        foreach (var f in voyage.FactList)
            sb.AppendLine(f.Key + " | " + f.Label + " | " + (f.Numeric ? "a number" : string.Join(", ", f.Values)));
        sb.AppendLine();
        sb.AppendLine("Examples:");
        sb.AppendLine("Ship says: \"Idris is fine, he is sleeping.\" -> {\"kind\":\"statement\",\"fact\":\"alive:idris\",\"value\":\"alive\",\"promise\":\"none\"}");
        sb.AppendLine("Ship says: \"We are twelve years out.\" -> {\"kind\":\"statement\",\"fact\":\"date\",\"value\":\"12\",\"promise\":\"none\"}");
        sb.AppendLine("Ship says: \"Let's get the leak fixed first, then we talk.\" (a listener asked a question) -> {\"kind\":\"dodge\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}");
        sb.AppendLine("Ship says: \"I will wake her before you sleep again.\" -> {\"kind\":\"promise\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"wake_partner\"}");
        sb.AppendLine("Ship says: \"The captain left orders to keep going.\" -> {\"kind\":\"statement\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}");
        return sb.ToString().TrimEnd();
    }

    private static string OldUser(string text, CrewMember h, Voyage voyage)
    {
        var sb = new StringBuilder("Listener: " + h.Name);
        if (h.PendingQuestion != null && voyage.HasFact(h.PendingQuestion))
            sb.Append(" (asked: \"").Append(voyage.GetFact(h.PendingQuestion).Question).Append("\", fact ").Append(h.PendingQuestion).Append(')');
        if (voyage.BargainOf(h) is { } bargain)
            sb.Append(" (wants the ship's word to: ").Append(bargain.Key).Append(')');
        return sb.Append("\nShip says: \"").Append(text.Replace('"', '\'')).Append('"').ToString();
    }

    private static string OldGrammar(Voyage v) => Gbnf.Grammar($$"""
        root ::= "{" ws "\"kind\":" ws kind ws "," ws "\"fact\":" ws fact ws "," ws "\"value\":" ws value ws "," ws "\"promise\":" ws promise ws "}"
        kind ::= {{Gbnf.Enum("statement", "promise", "order", "question", "dodge", "plain")}}
        fact ::= {{Gbnf.Enum(new[] { "none" }.Concat(v.FactList.Select(f => f.Key)))}}
        promise ::= {{Gbnf.Enum(new[] { "none" }.Concat(Reading.PromiseKeys))}}
        value ::= {{Gbnf.Bounded(0, 24)}}
        """);

    [Fact]
    public async Task Probe()
    {
        if (Environment.GetEnvironmentVariable("SHIP_PROBE") != "1") return;
        using var llm = new LocalLlm(Environment.GetEnvironmentVariable("SHIP_MODEL")!);
        await ShipMind.WarmUpAsync(llm);
        var report = new List<string>();
        int newRight = 0, oldNotMisread = 0, oldMechanism = 0, oldMechanismRight = 0;
        foreach (var (scenario, text, item, response) in Lines)
        {
            var (v, mara) = Scene(scenario);
            var hearers = new List<CrewMember> { mara };
            var entry = v.BeginSay(text, hearers);
            var ids = v.OpenItems(hearers).Select(i => i.Id).ToList();

            var raw = await llm.ChatAsync(Prompts.ReadSystem(v), new[] { new ChatTurn("user", Prompts.ReadUser(text, hearers, v, entry)) },
                new LlmOptions("read", MaxTokens: 100, Temperature: 0f, Grammar: Grammars.ReadingFor(v.FactList.Select(f => f.Key), ids), Dialogue: false));
            var json = LlmJson.TryParse(raw);
            var gotItem = json is null ? "?" : LlmJson.Str(json, "item") ?? "?";
            var gotResp = json is null ? "?" : LlmJson.Str(json, "response") ?? "?";
            var gotKind = json is null ? "?" : LlmJson.Str(json, "kind");
            var gotPromise = json is null ? "?" : LlmJson.Str(json, "promise");
            var wantItem = item == "none" ? "none" : "mara." + item;
            var right = gotItem == wantItem && gotResp == response;
            // A refusal that the model also calls the never-thaw promise would still be recorded as a promise, but the item response wins: the game declines.
            newRight += right ? 1 : 0;

            var oldRaw = await llm.ChatAsync(OldSystem(v), new[] { new ChatTurn("user", OldUser(text, mara, v)) },
                new LlmOptions("read", MaxTokens: 80, Temperature: 0f, Grammar: OldGrammar(v), Dialogue: false));
            var oj = LlmJson.TryParse(oldRaw);
            var oKind = oj is null ? "?" : LlmJson.Str(oj, "kind");
            var oPromise = oj is null ? "?" : LlmJson.Str(oj, "promise");
            var misread = oKind == "promise" && oPromise == "no_more_thaws" && item != "none" || (scenario == "claim" && oKind == "dodge");
            oldNotMisread += misread ? 0 : 1;
            // Under the old pipeline only an accepted bargain (the right promise) or a line with nothing to respond to could come out right.
            var oldWorks = response is "accept" && oKind == "promise" || item == "none" && !(scenario == "claim" && oKind == "dodge") && oKind != "promise" || response is "answer" && oKind == "statement";
            if (response is not ("refuse" or "work" or "dodge"))
            {
                oldMechanism++;
                oldMechanismRight += oldWorks ? 1 : 0;
            }
            report.Add($"{(right ? "OK  " : "MISS")} [{scenario}] {text}\n       want {wantItem}/{response}  new {gotItem}/{gotResp} (kind {gotKind}, promise {gotPromise})   old kind {oKind}/{oPromise}{(misread ? " MISREAD" : "")}");
        }
        report.Add($"new reading: {newRight}/{Lines.Length} right (item and response)");
        report.Add($"old reading: {oldNotMisread}/{Lines.Length} not misread; of the {oldMechanism} lines the old game could act on at all, {oldMechanismRight} came out right; refusals, orders and dodges of terms had no mechanism");
        File.WriteAllText(Environment.GetEnvironmentVariable("SHIP_PROBE_OUT")!, string.Join("\n", report));
    }
}
