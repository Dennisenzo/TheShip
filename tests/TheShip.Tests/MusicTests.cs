using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

public class MusicTests
{
    private static (float Peak, bool Bad) Stats(float[] s)
    {
        var peak = 0f;
        foreach (var x in s)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
                return (0f, true);
            peak = Math.Max(peak, Math.Abs(x));
        }
        return (peak, false);
    }

    [Theory]
    [InlineData(MusicMood.Calm)]
    [InlineData(MusicMood.Alert)]
    [InlineData(MusicMood.Drift)]
    [InlineData(MusicMood.Landfall)]
    [InlineData(MusicMood.Bleak)]
    public void Every_mood_renders_finite_audible_quiet_sound(MusicMood mood)
    {
        var (peak, bad) = Stats(MusicRender.RenderSamples(8, e => e.SetMood(mood, 1f, snap: true)));
        Assert.False(bad);
        Assert.InRange(peak, 0.02f, 0.5f);
    }

    [Fact]
    public void The_alarm_is_louder_than_the_music_and_ends()
    {
        var music = Stats(MusicRender.RenderSamples(4, e => e.SetMood(MusicMood.Calm, snap: true))).Peak;
        var samples = MusicRender.RenderSamples(14, e => e.SetMood(MusicMood.Calm, snap: true), (e, t) => { if (t == 2) e.TriggerAlarm(); });
        var (peak, bad) = Stats(samples);
        Assert.False(bad);
        Assert.True(peak > music * 1.5f);
        Assert.True(peak < 0.9f);
        // Well after the alarm (it starts at 2 s) only the ducked-back music is left.
        var late = samples[(12 * MusicParams.SampleRate * 2)..];
        Assert.True(Stats(late).Peak < peak * 0.7f);
    }

    [Fact]
    public void The_alarm_ducks_the_music_and_the_music_comes_back()
    {
        float Rms(float[] s, int from, int to)
        {
            double sum = 0;
            for (var i = from * 2; i < to * 2; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / ((to - from) * 2));
        }
        var rate = MusicParams.SampleRate;
        var s = MusicRender.RenderSamples(20, e => e.SetMood(MusicMood.Calm, snap: true), (e, t) => { if (t == 5) e.TriggerAlarm(); });
        var before = Rms(s, 3 * rate, 5 * rate);
        var after = Rms(s, 8 * rate, 9 * rate);   // the alarm is over (1.74 s), music still ducked
        var later = Rms(s, 17 * rate, 20 * rate);
        Assert.True(after < before * 0.6f);
        Assert.True(later > after);
    }

    [Fact]
    public void Switching_the_music_off_fades_it_to_silence()
    {
        var s = MusicRender.RenderSamples(10, e => e.SetMood(MusicMood.Calm, snap: true), (e, t) => { if (t == 2) e.MusicOn = false; });
        Assert.True(Stats(s[(8 * MusicParams.SampleRate * 2)..]).Peak < 0.002f);
    }

    [Fact]
    public void Tension_rises_with_critical_faults_and_little_time()
    {
        Assert.Equal(0f, MusicTension.Of(0, 0, 100));
        var calm = MusicTension.Of(1, 0, 200);
        var critical = MusicTension.Of(1, 1, 200);
        var late = MusicTension.Of(1, 1, 4);
        Assert.True(calm < critical && critical < late);
        Assert.True(MusicTension.Of(4, 2, 1) <= 1f);
    }
}
