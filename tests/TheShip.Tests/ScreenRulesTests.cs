using TheShip.Core;
using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>The pure rules of the ending, drift report and dossier screens (GRAPHICS-PLAN.md step 8).</summary>
public class ScreenRulesTests
{
    [Theory]
    [InlineData("Landfall", EndingLook.Landfall)]
    [InlineData("Landfall without you", EndingLook.LandfallWithout)]
    [InlineData("Adrift", EndingLook.Adrift)]
    [InlineData("The quiet ship", EndingLook.QuietShip)]
    public void An_ending_title_picks_its_picture(string title, EndingLook look) => Assert.Equal(look, ScreenRules.LookOf(title));

    [Fact]
    public void Drift_rows_mark_changes_and_harm()
    {
        var rows = ScreenRules.DriftRows(900, 700, 2, 2, 10, 14, 6, 5, 6);
        Assert.Equal(new[] { "STORES", "SPARE PARTS", "POD WEAR", "ALIVE" }, rows.Select(r => r.Label));
        Assert.True(rows[0].Changed && rows[0].Worse);
        Assert.False(rows[1].Changed || rows[1].Worse);
        Assert.True(rows[2].Changed && rows[2].Worse);
        Assert.True(rows[3].Worse && rows[3].Harm);
        // Pod wear going down would be an improvement, not a worsening.
        Assert.False(ScreenRules.DriftRows(1, 1, 1, 1, 14, 10, 6, 6, 6)[2].Worse);
    }

    [Fact]
    public void The_drift_report_shows_the_drift_not_the_next_alerts_scripted_death()
    {
        // Into alert 2: its scripted pod death happens as the alert starts, after the drift and its "No deaths." report.
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.SleepWhenAllowed(v, mara);
        var stores = v.Stores;
        var parts = v.Parts;
        var wear = v.PodWear;
        var alive = v.Crew.Count(c => c.Alive);
        v.Drift();
        Assert.Equal(2, v.Current.Number);
        Assert.True(v.Crew.Count(c => c.Alive) < alive); // the scripted death is in the voyage's state...
        var line = v.Log.Last(e => e.Text.StartsWith("DRIFT:"));
        Assert.EndsWith("No deaths.", line.Text);

        var (rows, lost) = ScreenRules.DriftReport(stores, parts, wear, alive, v.Crew.Count, v.LastDrift);
        Assert.Empty(lost); // ...but not in the drift report
        var aliveRow = rows.Single(r => r.Label == "ALIVE");
        Assert.Equal(alive, aliveRow.Now);
        Assert.False(aliveRow.Changed);
        Assert.Equal(v.LastDrift!.Stores, rows.Single(r => r.Label == "STORES").Now);
    }

    [Fact]
    public void Drift_report_lists_who_died_in_the_drift()
    {
        var end = new DriftOutcome(100, 1, 12, 4, new[] { "Asha", "Jonah" });
        var (rows, lost) = ScreenRules.DriftReport(400, 2, 10, 6, 6, end);
        Assert.Equal(new[] { "Asha", "Jonah" }, lost);
        Assert.True(rows.Single(r => r.Label == "ALIVE").Worse);
        // No outcome recorded: nothing changed and nobody is lost.
        var (same, none) = ScreenRules.DriftReport(400, 2, 10, 6, 6, null);
        Assert.Empty(none);
        Assert.All(same, r => Assert.False(r.Changed));
    }

    [Fact]
    public void Bars_of_one_row_share_a_scale_and_stay_in_range()
    {
        foreach (var row in ScreenRules.DriftRows(1500, 400, 2, 0, 3, 40, 6, 3, 6))
        {
            Assert.InRange(row.WasFill, 0f, 1f);
            Assert.InRange(row.NowFill, 0f, 1f);
        }
        Assert.Equal(0f, ScreenRules.Fill(5, 0));
        Assert.Equal(1f, ScreenRules.Fill(9, 3));
    }

    [Fact]
    public void A_head_is_the_same_every_time_and_differs_between_the_crew()
    {
        var ids = new[] { "mara", "idris", "tomas", "lin", "asha", "jonah" };
        Assert.Equal(ScreenRules.HeadOf("mara"), ScreenRules.HeadOf("mara"));
        Assert.Equal(ids.Length, ids.Select(ScreenRules.HeadOf).Distinct().Count());
        Assert.True(ids.Select(i => ScreenRules.HeadOf(i).Hair).Distinct().Count() >= 3);
    }

    [Fact]
    public void A_head_stays_symmetric_and_inside_the_bust()
    {
        foreach (var id in new[] { "mara", "idris", "tomas", "lin", "asha", "jonah", "x", "yy", "" })
        {
            var look = ScreenRules.HeadOf(id);
            var outline = ScreenRules.HeadOutline(look);
            Assert.Equal(9, outline.Length);
            for (var i = 1; i <= 4; i++)
                Assert.Equal(240 - outline[i].X, outline[9 - i].X);
            Assert.All(outline, p => { Assert.InRange(p.X, 40, 200); Assert.InRange(p.Y, 10, 170); });
            Assert.InRange(look.Hair, 0, HeadLook.HairStyles - 1);
            Assert.All(ScreenRules.Hair(look).SelectMany(l => l), p => { Assert.InRange(p.X, 40, 200); Assert.InRange(p.Y, 10, 170); });
        }
    }
}
