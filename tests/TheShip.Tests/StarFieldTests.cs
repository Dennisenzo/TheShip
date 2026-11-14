using System.Linq;
using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>The sky behind the interface: stars from the seed, slow drift, mood brightness, the vignette.</summary>
public class StarFieldTests
{
    [Fact]
    public void A_seed_gives_the_same_sky_and_another_seed_another_one()
    {
        Assert.Equal(StarField.Generate(7), StarField.Generate(7));
        Assert.NotEqual(StarField.Generate(7), StarField.Generate(8));
        Assert.Equal(StarField.Count, StarField.Generate(7).Length);
    }

    [Fact]
    public void Stars_are_faint_inside_the_layout_and_in_three_layers()
    {
        var stars = StarField.Generate(3);
        Assert.All(stars, s =>
        {
            Assert.InRange(s.Alpha, 0.15f, 0.4f);
            Assert.InRange(s.X, 0f, 1f);
            Assert.InRange(s.Y, 0f, 1f);
        });
        Assert.Equal(3, stars.Select(s => s.Layer).Distinct().Count());
    }

    [Fact]
    public void Near_layers_drift_faster_left_and_wrap_round_the_width()
    {
        var far = new Star(0.5f, 0.5f, 0, 0.2f, false);
        var near = far with { Layer = 2 };
        Assert.Equal(800f - 10f, StarField.X(far, 10, 1600), 3);
        Assert.Equal(800f - 30f, StarField.X(near, 10, 1600), 3);
        Assert.Equal(1600f - 100f, StarField.X(new Star(0f, 0, 0, 0.2f, false), 100, 1600), 3);
        Assert.InRange(StarField.X(near, 1e6, 1600), 0f, 1600f);
    }

    [Fact]
    public void Slow_by_default_and_much_faster_in_the_drift_roll()
    {
        Assert.Equal(1f, StarField.SpeedFactor(0));
        Assert.True(StarField.SpeedFactor(1) > 10f);
        Assert.True(StarField.Speeds.All(s => s is >= 1f and <= 3f));
        var star = new Star(0.5f, 0.5f, 2, 0.3f, true);
        Assert.Equal(0f, StarField.Stretch(star, 0));
        Assert.True(StarField.Stretch(star, 1) > 0f);
    }

    [Fact]
    public void Alert_and_drift_brighten_bleak_dims_and_alpha_is_capped()
    {
        Assert.True(StarField.Brightness(MusicMood.Alert) > StarField.Brightness(MusicMood.Calm));
        Assert.True(StarField.Brightness(MusicMood.Drift) > StarField.Brightness(MusicMood.Calm));
        Assert.True(StarField.Brightness(MusicMood.Bleak) < StarField.Brightness(MusicMood.Calm));
        var bright = new Star(0, 0, 2, 0.4f, true);
        Assert.True(StarField.AlphaOf(bright, 10f) <= StarField.MaxAlpha);
    }

    [Fact]
    public void The_sky_follows_the_mood_over_the_music_fade()
    {
        var v = 1f;
        for (var i = 0; i < 70; i++) // 7 s in tenths
            v = StarField.Follow(v, 0.4f, 0.1f, MusicParams.MoodFadeSeconds);
        Assert.InRange(v, 0.4f, 0.4f + 0.6f * 0.06f);
        Assert.Equal(0.4f, StarField.Follow(1f, 0.4f, 1f, 0f));
    }

    [Fact]
    public void The_vignette_is_clear_in_the_middle_and_a_quarter_dark_in_the_corners()
    {
        Assert.Equal(0f, StarField.Vignette(0.5f, 0.5f));
        Assert.Equal(StarField.VignetteStrength, StarField.Vignette(0f, 0f), 3);
        Assert.True(StarField.Vignette(0.1f, 0.1f) < StarField.Vignette(0f, 0f));
    }
}
