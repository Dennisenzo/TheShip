using TheShip.Core;
using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>The ship plan's layout (graphics step 4): the hull fits its panel and is centred, and no label, leader or section name overlaps another.</summary>
public class PlanLayoutTests
{
    private const float Char = 7.2f;
    private static readonly string[] Names = { "COOLANT LEAK", "SLEEPER ILLNESS: JONAH", "NAVIGATION DRIFT", "FAILED POD", "POWER SAG", "HYDROPONICS BLIGHT", "POD CONTROLLER", "SIGNAL" };

    /// <summary>The sections the scenario puts faults in.</summary>
    private static readonly string[] FaultSections = { "coolant", "helm", "hydro", "podA", "podB", "reactor" };

    private static PlanLayoutResult Layout(Random rng, int count, RectF area, bool sameSection = false, bool people = true)
    {
        var sections = ShipPlan.Sections();
        var faults = new List<PlanFault>();
        var slots = new Dictionary<string, int>();
        var fixedSection = FaultSections[rng.Next(FaultSections.Length)];
        for (var i = 1; i <= count; i++)
        {
            // At most two faults to a section (a returned patch beside a new fault); the game never puts three in one.
            var id = sameSection ? fixedSection : FaultSections[rng.Next(FaultSections.Length)];
            while (slots.GetValueOrDefault(id) >= 2)
                id = FaultSections[rng.Next(FaultSections.Length)];
            var slot = slots.GetValueOrDefault(id);
            slots[id] = slot + 1;
            var text = i.ToString("00") + " " + Names[rng.Next(Names.Length)] + " " + rng.Next(1, 90) + " h";
            faults.Add(new PlanFault(i, id, slot, text.Length * Char));
        }
        var boxes = new List<PlanBox>();
        if (people)
            for (var i = 0; i < rng.Next(0, 4); i++)
            {
                var s = sections[rng.Next(sections.Count)];
                boxes.Add(new PlanBox(s.Spot.X - 44, s.Spot.Y + 38 + i * 18, -8, -9, 70, 18));
            }
        return PlanLayout.Compute(sections, faults, boxes, area, Char);
    }

    private static IEnumerable<RectF> LeaderRects(PlacedLabel l)
    {
        for (var i = 0; i + 1 < l.Leader.Length; i++)
        {
            var a = l.Leader[i];
            var b = l.Leader[i + 1];
            yield return new RectF(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }
    }

    [Theory]
    [InlineData(864, 456)]
    [InlineData(1037, 547)]
    [InlineData(700, 400)]
    public void No_label_leader_or_name_overlaps_another(int w, int h)
    {
        var area = new RectF(10, 40, w - 8, h - 40);
        for (var seed = 0; seed < 600; seed++)
        {
            var rng = new Random(seed);
            var count = 1 + seed % 4;
            var r = Layout(rng, seed % 7 == 0 ? Math.Min(count, 2) : count, area, sameSection: seed % 7 == 0);
            foreach (var a in r.Labels)
            {
                Assert.True(a.Conflicts == 0, $"seed {seed}: label {a.Index} has {a.Conflicts} conflicts, above={a.Above} text={a.Text} diamond={a.Diamond} all={string.Join(" ; ", r.Labels.Select(l => l.Index + ":" + l.Text + (l.Above ? "A" : "B")))}");
                foreach (var b in r.Labels.Where(b => b != a))
                {
                    Assert.False(a.Text.Intersects(b.Text), $"seed {seed}: labels {a.Index} and {b.Index} overlap");
                    foreach (var leader in LeaderRects(b))
                        Assert.False(a.Text.Intersects(leader), $"seed {seed}: leader of {b.Index} runs through label {a.Index}");
                }
                Assert.True(a.Text.X >= area.X && a.Text.Right <= area.Right, $"seed {seed}: label {a.Index} leaves the panel");
                Assert.True(a.Text.Y >= area.Y - 1 && a.Text.Bottom <= area.Bottom + 1, $"seed {seed}: label {a.Index} leaves the panel vertically");
                foreach (var name in r.Names.Values)
                {
                    Assert.False(a.Text.Intersects(name), $"seed {seed}: label {a.Index} overlaps a section name");
                    Assert.False(a.Diamond.Intersects(name), $"seed {seed}: diamond {a.Index} overlaps a section name");
                    foreach (var leader in LeaderRects(a))
                        Assert.False(leader.Intersects(name), $"seed {seed}: leader {a.Index} runs through a section name");
                }
            }
            var all = r.Names.Values.ToList();
            for (var i = 0; i < all.Count; i++)
                for (var j = i + 1; j < all.Count; j++)
                    Assert.False(all[i].Intersects(all[j]), $"seed {seed}: two section names overlap");
        }
    }

    [Fact]
    public void A_real_four_fault_alert_gets_four_clear_labels()
    {
        // Alert 3 of seed 3: navigation drift (helm), returned coolant leak, returned sleeper illness and failed pod (both pod bank B).
        var sections = ShipPlan.Sections();
        var faults = new[]
        {
            new PlanFault(1, "helm", 0, 30 * Char), new PlanFault(2, "coolant", 0, 24 * Char),
            new PlanFault(3, "podB", 0, 29 * Char), new PlanFault(4, "podB", 1, 22 * Char),
        };
        var r = PlanLayout.Compute(sections, faults, Array.Empty<PlanBox>(), new RectF(362, 106, 856, 416), Char);
        Assert.All(r.Labels, l => Assert.Equal(0, l.Conflicts));
        Assert.Equal(4, r.Labels.Count);
    }

    [Fact]
    public void The_hull_fits_the_area_and_is_centred_when_nothing_needs_room()
    {
        var area = new RectF(10, 40, 856, 416);
        var r = Layout(new Random(1), 0, area, people: false);
        var hullW = (PlanLayout.HullX1 - PlanLayout.HullX0) * r.K;
        var hullH = (PlanLayout.HullY1 - PlanLayout.HullY0) * r.K;
        var (left, _) = r.P(PlanLayout.HullX0, PlanLayout.HullY0);
        Assert.Equal(area.X + (area.W - hullW) / 2, left, 1);
        Assert.Equal(area.Y + (area.H - hullH) / 2, r.HullTop, 1);
        Assert.True(r.K > 0.9f);
    }

    [Fact]
    public void Four_faults_still_fit_inside_the_area()
    {
        var area = new RectF(10, 40, 856, 416);
        for (var seed = 0; seed < 200; seed++)
        {
            var r = Layout(new Random(seed), 4, area);
            var (_, bottom) = r.P(0, PlanLayout.HullY1);
            Assert.True(r.HullTop >= area.Y - 0.5f && bottom <= area.Bottom + 0.5f, $"seed {seed}: the hull leaves the area");
            Assert.True(r.K > 0.8f, $"seed {seed}: the plan shrank to {r.K}");
        }
    }
}
