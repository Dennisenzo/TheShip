using System;

namespace TheShip.Game;

/// <summary>One faint star: a place on the 1600 x 900 layout (0 to 1 each way), the depth layer (0 far, 2 near), its alpha and whether it is the larger dot.</summary>
internal readonly record struct Star(float X, float Y, int Layer, float Alpha, bool Big);

/// <summary>
/// The sky behind the interface (GRAPHICS-PLAN.md, step 7). Pure rules, no MonoGame, so they are tested: the stars come from the run's seed
/// (each run has its own sky), drift slowly left in three depth layers, brighten a little in an alert and the drift and dim for the bleak ending.
/// </summary>
internal static class StarField
{
    public const int Count = 150;
    /// <summary>Layout pixels a second each layer drifts left (far to near).</summary>
    public static readonly float[] Speeds = { 1f, 2f, 3f };
    /// <summary>Alpha range of each layer, before the mood.</summary>
    private static readonly (float Low, float High)[] Alphas = { (0.15f, 0.24f), (0.2f, 0.32f), (0.28f, 0.4f) };
    /// <summary>Brightness never goes past this alpha, however bright the mood.</summary>
    public const float MaxAlpha = 0.5f;
    /// <summary>At full drift strength the sky moves this many times faster, and a star is stretched by <c>DriftStretch x (1 + layer)</c> px.</summary>
    public const float DriftSpeedUp = 60f;
    public const float DriftStretch = 3f;
    /// <summary>Seconds the sky takes to follow the mood (as long as the music fade) and the drift strength to rise and fall.</summary>
    public const float MoodFollowSeconds = MusicParams.MoodFadeSeconds;
    public const float DriftFollowSeconds = 0.4f;

    public static Star[] Generate(int seed, int count = Count)
    {
        var random = new Random(unchecked(seed * 7919 + 104729));
        var stars = new Star[count];
        for (var i = 0; i < count; i++)
        {
            var pick = random.NextDouble();
            var layer = pick < 0.5 ? 0 : pick < 0.8 ? 1 : 2;
            var (low, high) = Alphas[layer];
            var alpha = low + (float)random.NextDouble() * (high - low);
            var big = layer == 2 ? random.NextDouble() < 0.6 : layer == 1 && random.NextDouble() < 0.2;
            stars[i] = new Star((float)random.NextDouble(), (float)random.NextDouble(), layer, alpha, big);
        }
        return stars;
    }

    /// <summary>Layout x of a star after the sky has drifted for <paramref name="phase"/> seconds (wraps round the width).</summary>
    public static float X(Star star, double phase, float width)
    {
        var x = (star.X * width - Speeds[star.Layer] * phase) % width;
        return (float)(x < 0 ? x + width : x);
    }

    /// <summary>How much brighter or dimmer the sky is in a music mood.</summary>
    public static float Brightness(MusicMood mood) => mood switch
    {
        MusicMood.Alert => 1.3f,
        MusicMood.Drift => 1.5f,
        MusicMood.Landfall => 1.15f,
        MusicMood.Bleak => 0.4f,
        _ => 1f,
    };

    public static float AlphaOf(Star star, float brightness) => Math.Min(star.Alpha * brightness, MaxAlpha);

    /// <summary>How many times faster the sky moves at a drift strength of 0 to 1.</summary>
    public static float SpeedFactor(float drift) => 1f + DriftSpeedUp * Math.Clamp(drift, 0f, 1f);

    /// <summary>Extra length in layout pixels of a star at a drift strength of 0 to 1.</summary>
    public static float Stretch(Star star, float drift) => DriftStretch * (1 + star.Layer) * Math.Clamp(drift, 0f, 1f);

    /// <summary>One step of an exponential follow: <paramref name="value"/> moves towards <paramref name="target"/> as a fade of <paramref name="seconds"/> would (three time constants).</summary>
    public static float Follow(float value, float target, float dt, float seconds) =>
        seconds <= 0f ? target : value + (target - value) * (1f - MathF.Exp(-Math.Max(0f, dt) / (seconds / 3f)));

    /// <summary>The darkening of the vignette at a point of the image (0 to 1 each way): none in the middle, <see cref="VignetteStrength"/> in the corners.</summary>
    public const float VignetteStrength = 0.25f;

    public static float Vignette(float u, float v)
    {
        var dx = (u - 0.5f) * 2f;
        var dy = (v - 0.5f) * 2f;
        var d = MathF.Sqrt(dx * dx + dy * dy) / MathF.Sqrt(2f);
        var t = Math.Clamp((d - 0.45f) / 0.55f, 0f, 1f);
        return VignetteStrength * t * t;
    }
}
