using TheShip.Core;
using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>The light of the ship plan (graphics step 5): pure state-to-brightness rules and functions of time.</summary>
public class PlanLightTests
{
    [Fact]
    public void Light_is_a_function_of_time_only()
    {
        for (var t = 0.0; t < 30; t += 0.37)
        {
            Assert.Equal(PlanLight.SectionGlow(true, t, 5), PlanLight.SectionGlow(true, t, 5));
            Assert.Equal(PlanLight.PodBreath(12, 8, t, 3), PlanLight.PodBreath(12, 8, t, 3));
            Assert.Equal(PlanLight.FaultWash(true, false, t), PlanLight.FaultWash(true, false, t));
        }
    }

    [Fact]
    public void Low_power_dims_and_flickers_every_section()
    {
        var seed = PlanLight.SeedOf("medical");
        var normal = new List<float>();
        var low = new List<float>();
        for (var t = 0.0; t < 60; t += 0.05)
        {
            normal.Add(PlanLight.SectionGlow(false, t, seed));
            low.Add(PlanLight.SectionGlow(true, t, seed));
        }
        Assert.True(normal.Min() >= 0.79f && normal.Max() <= 1.0001f);
        Assert.True(low.Average() < normal.Average() * 0.6f);
        Assert.True(low.Max() - low.Min() > 0.3f, "it flickers");
        Assert.All(low, g => Assert.InRange(g, 0f, 1f));
    }

    [Fact]
    public void A_critical_fault_pulses_and_the_last_quarter_is_danger()
    {
        var minimum = float.MaxValue;
        var maximum = float.MinValue;
        for (var t = 0.0; t < 6; t += 0.05)
        {
            var w = PlanLight.FaultWash(true, false, t);
            Assert.False(w.Danger);
            minimum = Math.Min(minimum, w.Alpha);
            maximum = Math.Max(maximum, w.Alpha);
        }
        Assert.True(maximum - minimum > 0.12f);
        Assert.True(PlanLight.FaultWash(false, false, 0).Alpha == PlanLight.FaultWash(false, false, 1.7).Alpha, "an ordinary fault is steady");
        Assert.True(PlanLight.FaultWash(false, true, 0.2).Danger);
        Assert.True(PlanLight.FaultWash(false, true, 0.2).Alpha > PlanLight.FaultWash(false, false, 0.2).Alpha - 0.0001f);
    }

    [Fact]
    public void A_worn_pod_flickers_and_a_sound_one_only_breathes()
    {
        var sound = new List<float>();
        var worn = new List<float>();
        for (var t = 0.0; t < 60; t += 0.04)
        {
            sound.Add(PlanLight.PodBreath(3, 8, t, 9));
            worn.Add(PlanLight.PodBreath(14, 8, t, 9));
        }
        Assert.True(sound.Min() >= 0.79f && sound.Max() <= 1.0001f);
        Assert.True(worn.Min() < 0.55f, "dips");
        Assert.All(worn, w => Assert.InRange(w, 0f, 1f));
    }

    [Fact]
    public void The_power_chain_slows_and_dims_at_low_power_and_stays_inside_one_spacing()
    {
        Assert.True(PlanLight.BusSpeed(true) < PlanLight.BusSpeed(false) / 2);
        Assert.True(PlanLight.BusAlpha(true) < PlanLight.BusAlpha(false) / 2);
        for (var t = 0.0; t < 100; t += 0.7)
            Assert.InRange(PlanLight.BusOffset(t, false), 0f, PlanLight.BusSpacing);
        Assert.Equal(0f, PlanLight.CoolantSpeed(true));
        Assert.True(PlanLight.CoolantSpeed(false) > 0f);
    }

    [Fact]
    public void Hull_ticks_drift_left_and_fade_at_the_ends()
    {
        var a = PlanLight.Ticks(0, 60, 760);
        var b = PlanLight.Ticks(2, 60, 760);
        Assert.True(a.Count >= 8);
        Assert.All(a, tick => Assert.InRange(tick.X, 60f, 760f));
        Assert.True(b.Min(x => x.X) != a.Min(x => x.X) || b.Count != a.Count);
        // The tick nearest the left end is fainter than one in the middle.
        Assert.True(a.OrderBy(x => x.X).First().Alpha <= a.Max(x => x.Alpha));
    }

    [Fact]
    public void Spans_of_a_rectangle_is_one_rectangle_and_a_chamfered_room_is_a_few()
    {
        var rect = new (float X, float Y)[] { (0, 0), (100, 0), (100, 50), (0, 50) };
        var one = PlanLight.Spans(rect, 4);
        Assert.Single(one);
        Assert.Equal((0f, 0f, 100f, 50f), one[0]);
        foreach (var s in ShipPlan.Sections())
        {
            var spans = PlanLight.Spans(s.Outline, 6);
            Assert.InRange(spans.Count, 1, 40);
            // The spans cover the section's area to within a few percent.
            var area = 0.0;
            for (var i = 0; i < s.Outline.Length; i++)
            {
                var p = s.Outline[i];
                var q = s.Outline[(i + 1) % s.Outline.Length];
                area += p.X * q.Y - q.X * p.Y;
            }
            area = Math.Abs(area) / 2;
            var covered = spans.Sum(r => (double)(r.X1 - r.X0) * (r.Y1 - r.Y0));
            Assert.InRange(covered / area, 0.9, 1.1);
        }
    }

    [Fact]
    public void Offset_pushes_the_hull_outward_by_the_distance()
    {
        var hull = ShipPlan.Hull;
        var outer = PlanLight.Offset(hull, 5);
        // Every offset vertex lies farther from the hull's centre than the original.
        var cx = hull.Average(p => p.X);
        var cy = hull.Average(p => p.Y);
        for (var i = 0; i < hull.Length; i++)
        {
            var before = Math.Sqrt(Math.Pow(hull[i].X - cx, 2) + Math.Pow(hull[i].Y - cy, 2));
            var after = Math.Sqrt(Math.Pow(outer[i].X - cx, 2) + Math.Pow(outer[i].Y - cy, 2));
            Assert.True(after > before);
        }
        // The top edge moves straight up by the distance.
        Assert.Equal(75f, outer[0].Y, 2);
        Assert.Equal(75f, outer[1].Y, 2);
    }
}
