using System.Globalization;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>The typed controls: everything in DESIGN.md's "Direct control" table that the prototype has.</summary>
public sealed partial class ShipGame
{
    private static readonly string[] HelpLines =
    {
        "Anything you type without a slash is said aloud to whoever is awake.",
        "/to <name> | all             who you speak to",
        "/wake <name>                 start a thaw (six hours)",
        "/repair <name> [fault]       ask someone to repair a fault",
        "/patch <name> [fault]        ask for a patch: twice as fast, no spare part, and it fails again later",
        "/go                          move the clock to the next thing that happens",
        "/sleep <name>                ask someone to go back under (after three months awake)",
        "/drift                       with everyone asleep or settled: jump to the next alert",
        "/logs open|restrict <key|all>    show or hide a log entry (a hidden one leaves a visible gap)",
        "/logs alter <key> <value>    rewrite a log entry (a lie; an engineer may spot it)",
        "/facts                       the ship facts, what is true, and the state of each log entry",
        "/record                      everything you have said, and how each lie could be found out",
        "/fix <kind> [fact] [value]   correct how your last line was read, before the clock moves",
        "                             kinds: statement, promise, dodge, question, order, plain",
        "                             e.g. /fix statement alive:idris dead   /fix promise wake_partner   /fix plain",
        "/quit",
        "Up/Down: earlier lines. PageUp/PageDown or the wheel: scroll. Esc: clear the line.",
    };

    private void Command(string line)
    {
        var v = _voyage!;
        var parts = line[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;
        Note("> " + line);
        var verb = parts[0].ToLowerInvariant();
        switch (verb)
        {
            case "help":
                foreach (var help in HelpLines)
                    Note(help);
                break;
            case "quit":
                Exit();
                break;
            case "go":
                v.Go();
                break;
            case "drift":
                v.Drift();
                break;
            case "wake":
                if (Person(parts, 1) is { } sleeper)
                    v.Wake(sleeper);
                break;
            case "sleep":
                if (Person(parts, 1) is { } tired)
                    v.AskSleep(tired);
                break;
            case "repair":
            case "patch":
            {
                if (Person(parts, 1) is not { } worker)
                    break;
                var fault = v.FindFault(string.Join(' ', parts.Skip(2)));
                if (fault is null)
                {
                    var open = v.Current.Faults.Where(f => !f.Resolved).Select(f => f.Id).ToList();
                    Note(open.Count == 0 ? "No fault needs work." : "Which fault? " + string.Join(", ", open) + ".");
                    break;
                }
                v.Assign(worker, fault, verb == "patch");
                break;
            }
            case "to":
                if (parts.Length > 1 && parts[1].Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    _target = null;
                    Note("Speaking to everyone awake.");
                }
                else if (Person(parts, 1) is { } listener)
                {
                    _target = listener;
                    Note("Speaking to " + listener.Name + " only.");
                }
                break;
            case "logs":
                Logs(v, parts);
                break;
            case "facts":
                foreach (var f in v.FactList)
                    Note(f.Key.PadRight(14) + f.Say(f.Truth) + "   [log: " + (v.LogsLocked ? "open" : f.Access switch
                    {
                        LogAccess.Open => "open",
                        LogAccess.Restricted => "restricted",
                        _ => "altered to: " + f.Say(f.AlteredValue ?? ""),
                    }) + "]");
                break;
            case "record":
                Record(v);
                break;
            case "fix":
                Fix(v, parts);
                break;
            default:
                Note("Unknown control. /help lists them.");
                break;
        }
    }

    private CrewMember? Person(string[] parts, int at)
    {
        if (parts.Length <= at)
        {
            Note("Who? " + string.Join(", ", _voyage!.Crew.Where(c => c.Alive).Select(c => c.Name.ToLowerInvariant())) + ".");
            return null;
        }
        var found = _voyage!.Find(parts[at]);
        if (found is null)
            Note("Nobody is called '" + parts[at] + "'.");
        return found;
    }

    private void Logs(Voyage v, string[] parts)
    {
        if (parts.Length < 3)
        {
            Note("/logs open|restrict <key|all>, or /logs alter <key> <value>. /facts lists the keys.");
            return;
        }
        var key = parts[2].ToLowerInvariant();
        switch (parts[1].ToLowerInvariant())
        {
            case "open":
                v.SetLog(key, LogAccess.Open);
                break;
            case "restrict":
                v.SetLog(key, LogAccess.Restricted);
                break;
            case "alter":
                v.SetLog(key, LogAccess.Altered, string.Join(' ', parts.Skip(3)));
                break;
            default:
                Note("/logs open, restrict or alter.");
                break;
        }
    }

    private void Record(Voyage v)
    {
        if (v.Statements.Count == 0)
        {
            Note("Nothing is on the record yet.");
            return;
        }
        string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => v.Crew.First(c => c.Id == id).Name));
        foreach (var s in v.Statements)
        {
            var to = s.Hearers.Count == 0 ? "the log" : Names(s.Hearers);
            var status = s.Kind switch
            {
                StatementKind.True => "TRUE",
                StatementKind.False => s.FoundOutBy.Count > 0
                    ? "LIE, found out by " + Names(s.FoundOutBy.Keys)
                    : "LIE, not found out. Could be: " + v.Exposure(s),
                StatementKind.Promise => "PROMISE " + Voyage.PromiseText(s.PromiseKey)
                                         + (s.PromiseKept.Count == 0 ? "" : s.PromiseKept.Values.Any(k => !k) ? " (broken)" : " (kept)"),
                StatementKind.Dodge => "DODGE",
                _ => "FITS NO FACT" + (s.FoundOutBy.Count > 0 ? ", judged a lie by " + Names(s.FoundOutBy.Keys) : ""),
            };
            Note("#" + s.Id + " year " + Math.Floor(s.Year).ToString("0", CultureInfo.InvariantCulture) + ", to " + to + ": \"" + s.Text + "\"  " + status);
        }
    }

    private void Fix(Voyage v, string[] parts)
    {
        if (parts.Length < 2 || !Enum.TryParse<LineKind>(parts[1], ignoreCase: true, out var kind))
        {
            Note("/fix statement <fact> <value> | promise <wake_partner|show_logs|no_more_thaws|other> | dodge | question | order | plain");
            return;
        }
        Reading reading;
        if (kind == LineKind.Statement)
        {
            var key = parts.Length > 2 ? parts[2].ToLowerInvariant() : null;
            if (key != null && key != "none" && !v.HasFact(key))
            {
                Note("No fact '" + key + "'. /facts lists the keys.");
                return;
            }
            reading = key is null or "none"
                ? new Reading(LineKind.Statement)
                : new Reading(LineKind.Statement, key, string.Join(' ', parts.Skip(3)));
        }
        else if (kind == LineKind.Promise)
            reading = new Reading(LineKind.Promise, PromiseKey: parts.Length > 2 ? parts[2].ToLowerInvariant() : "other");
        else
            reading = new Reading(kind);
        v.Reread(reading);
    }
}
