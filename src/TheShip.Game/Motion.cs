namespace TheShip.Game;

/// <summary>
/// Durations and curves of everything that moves in the interface (UI-PLAN.md). The simulation never waits for any of it:
/// an animation shows a result that already happened.
/// </summary>
internal static class Motion
{
    /// <summary>Multiplies every duration. 0 makes everything instant: the <c>--tour</c> dev tool runs that way so its screenshots are stable.</summary>
    public static float Speed = 1f;

    public const float Hover = 0.12f;
    public const float TipDelay = 0.35f;
    public const float SkipHours = 0.5f;
    public const float SkipWeeks = 1.0f;
    public const float DriftRoll = 2.4f;
    public const float AlertTitle = 1.0f;
    public const float FaultReveal = 0.25f;
    public const float Banner = 4f;
    public const float Flash = 0.2f;
    public const float DeltaShown = 1.5f;
    public const float ScreenFade = 0.15f;
    public const float EndingFade = 1.5f;
    public const float LogFade = 0.3f;

    /// <summary>A crew line types out in the log at this many characters a second (<see cref="LogReveal"/>).</summary>
    public const double CharsPerSecond = 90;
    /// <summary>Each waiting line behind the one typing adds this share to the typing speed.</summary>
    public const double BacklogSpeedUp = 1.0;
    /// <summary>With this many lines waiting behind the one typing, everything is shown at once.</summary>
    public const int BacklogInstant = 6;

    /// <summary>Whether animation runs at a fixed time: only in a tour (<c>--tour</c>) given <c>--tour-time</c>, never in play.</summary>
    public static bool FixedTime(string? tourDir, double? tourTime) => tourDir is not null && tourTime is not null;

    /// <summary>True when nothing should move (the tour).</summary>
    public static bool Still => Speed <= 0f;

    /// <summary>A duration in seconds, scaled by <see cref="Speed"/>.</summary>
    public static float Seconds(float seconds) => seconds * Speed;

    /// <summary>How far an animation of <paramref name="seconds"/> that started at <paramref name="start"/> has come, 0 to 1.</summary>
    public static float Progress(double now, double start, float seconds)
    {
        var length = Seconds(seconds);
        if (length <= 0f)
            return 1f;
        return Math.Clamp((float)((now - start) / length), 0f, 1f);
    }

    /// <summary>Fast at first, slow at the end.</summary>
    public static float EaseOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var rest = 1f - t;
        return 1f - rest * rest * rest;
    }

    public static double Lerp(double from, double to, float t) => from + (to - from) * t;

    /// <summary>A value that swings between 0 and 1 once every <paramref name="period"/> seconds; 1 when nothing moves.</summary>
    public static float Pulse(double now, float period)
    {
        if (Still || period <= 0f)
            return 1f;
        return 0.5f + 0.5f * MathF.Sin((float)(now / period * Math.PI * 2));
    }
}
