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
