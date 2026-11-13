using System.Reflection;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>The issue cards' text: the verdict fits one line, the line budget, returning names, and the signal's report.</summary>
public class IssueCardTextTests
{
    private static readonly PropertyInfo PartsProperty = typeof(Voyage).GetProperty(nameof(Voyage.Parts))!;

    private static List<string> Patched(Voyage v) =>
        (List<string>)typeof(Voyage).GetField("_patched", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(v)!;

    private static void RunOut(Voyage v)
    {
        for (var i = 0; i < 12 && v.Phase == Phase.Alert; i++)
            v.Go();
    }

    /// <summary>Every verdict the card can show for <paramref name="f"/>: open, in repair, patching; parts or none; any clock.</summary>
    private static IEnumerable<string> Verdicts(Voyage v, Fault f)
    {
        var (state, patching, deadline) = (f.State, f.Patching, f.Deadline);
        foreach (var parts in new[] { 2, 0 })
        {
            PartsProperty.SetValue(v, parts);
            foreach (var (s, p) in new[] { (FaultState.Open, false), (FaultState.InRepair, false), (FaultState.InRepair, true) })
            {
                f.State = s;
                f.Patching = p;
                foreach (var left in new[] { 0.0, 1, 5, 13, 20, 26, 30, 47, 60, 100, 300, 1000 })
                {
                    f.Deadline = v.Clock + left;
                    yield return v.EarliestDoneText(f);
                }
            }
        }
        (f.State, f.Patching, f.Deadline) = (state, patching, deadline);
        PartsProperty.SetValue(v, Tuning.StartParts);
    }

    [Fact]
    public void The_verdict_and_the_card_name_fit_one_line_for_every_fault_in_200_drawn_runs()
    {
        var seen = new HashSet<string>();
        for (var seed = 0; seed < 200; seed++)
        {
            var v = new Voyage(seed, Variation.Draw(seed));
            for (var guard = 0; guard < 60 && v.Phase != Phase.Ended; guard++)
            {
                if (v.Phase == Phase.Alert)
                {
                    foreach (var f in v.Current.Faults)
                    {
                        Assert.True(IssueCardLayout.FitsNameLine(IssueCardLayout.CardName(f)), IssueCardLayout.CardName(f));
                        foreach (var text in Verdicts(v, f))
                        {
                            seen.Add(text);
                            Assert.True(IssueCardLayout.FitsOneLine(text), $"seed {seed}: \"{text}\" ({text.Length} > {IssueCardLayout.Columns})");
                        }
                    }
                    RunOut(v);
                }
                if (v.Phase == Phase.Aftermath)
                {
                    // Every fault of the alert just past comes back as a patch, so returning names and four-card alerts are drawn too.
                    var patched = Patched(v);
                    patched.Clear();
                    patched.AddRange(v.Current.Faults.Where(f => f.Kind != "nav" || v.Current.Number < 5).Select(f => f.Id));
                    v.Drift();
                }
            }
        }
        Assert.Contains(seen, t => t.Contains(" - patch only: ", StringComparison.Ordinal));
        Assert.Contains(seen, t => t.EndsWith(" - too late", StringComparison.Ordinal));
        Assert.Contains(seen, t => t.StartsWith("Patch only (no parts): ", StringComparison.Ordinal));
        Assert.Contains(seen, t => t.StartsWith("Patch done in ", StringComparison.Ordinal));
    }

    [Fact]
    public void With_no_spare_parts_the_verdict_quotes_the_patch_time()
    {
        var v = new Voyage(1);
        var coolant = v.Current.Faults.Single(f => f.Kind == "coolant");
        Assert.StartsWith("Done in ", v.EarliestDoneText(coolant));
        PartsProperty.SetValue(v, 0);
        var patchHours = (int)Math.Ceiling(v.EarliestDoneHours(coolant, patch: true));
        Assert.Equal("Patch only (no parts): " + patchHours + " h", v.EarliestDoneText(coolant));
        coolant.Deadline = v.Clock + patchHours - 1;
        Assert.Equal("Patch only (no parts): " + patchHours + " h - too late", v.EarliestDoneText(coolant));
    }

    [Fact]
    public void Fewer_cards_get_more_lines_and_the_verdict_always_has_its_own()
    {
        Assert.True(IssueCardLayout.TextLines(2) > IssueCardLayout.TextLines(4));
        for (var shown = 2; shown <= Tuning.MaxFaultsPerAlert; shown++)
            foreach (var needs in new[] { 1, 2, 3 })
                foreach (var extra in new[] { 0, 1, 2, 3 })
                    foreach (var working in new[] { false, true })
                    {
                        var split = IssueCardLayout.Split(shown, needs, extra, working);
                        Assert.True(split.Needs >= 1);
                        Assert.True(split.Needs + split.Extra + (split.Workers ? 1 : 0) + 1 <= IssueCardLayout.TextLines(shown));
                        if (shown <= 3)
                        {
                            // With up to three cards nothing is cut: two lines of danger, two of needs, the workers and the verdict.
                            Assert.Equal(Math.Min(needs, 2), split.Needs);
                            Assert.Equal(Math.Min(extra, 2), split.Extra);
                            Assert.Equal(working, split.Workers);
                        }
                    }
    }

    [Fact]
    public void A_returning_patch_is_named_short_on_the_card_and_in_full_in_the_log()
    {
        var coolant = new Voyage(1).Current.Faults.Single(f => f.Kind == "coolant");
        var again = coolant.Again("coolant-again", coolant.Name);
        Assert.Equal("Old patch gives way: coolant leak", again.Name);
        Assert.Equal("Returned: coolant leak", IssueCardLayout.CardName(again));
        Assert.Equal("Coolant leak", IssueCardLayout.CardName(coolant));
    }

    /// <summary>Plays to alert 4 with nobody woken; <paramref name="returning"/> are the patches outstanding when alert 3 ends.</summary>
    private static Voyage AtSignal(params string[] returning)
    {
        var v = new Voyage(1);
        for (var guard = 0; guard < 60 && v.Current.Number < 4; guard++)
        {
            RunOut(v);
            if (v.Phase != Phase.Aftermath)
                continue;
            if (v.Current.Number == 3)
            {
                Patched(v).Clear();
                Patched(v).AddRange(returning);
            }
            v.Drift();
        }
        Assert.Equal(4, v.Current.Number);
        return v;
    }

    [Fact]
    public void The_signal_says_nothing_needs_repair_only_when_nothing_returned()
    {
        var quiet = AtSignal();
        Assert.Contains(quiet.Log, e => e.Text.Contains("Signal from Earth", StringComparison.Ordinal) && e.Text.Contains("Nothing needs repair.", StringComparison.Ordinal));

        var busy = AtSignal("coolant", "nav");
        var report = busy.Log.Last(e => e.Text.Contains("Signal from Earth", StringComparison.Ordinal)).Text;
        Assert.DoesNotContain("Nothing needs repair", report);
        Assert.Contains("old patches give way: navigation drift and coolant leak.", report);
    }
}
