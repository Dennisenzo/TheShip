using System.Text.RegularExpressions;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>How a log line is coloured (<see cref="LogTones.Of"/>): the palette's meanings applied to the log.</summary>
internal enum LogTone
{
    /// <summary>An ordinary SHIP line: <see cref="Painter.Ink"/>.</summary>
    Neutral,
    /// <summary>An alert, a fault, the drift line, a lockout, or something waiting on the player: <see cref="Painter.Accent"/>.</summary>
    Needs,
    /// <summary>A death: <see cref="Painter.Danger"/>.</summary>
    Danger,
    /// <summary>What the player said: <see cref="Painter.Bright"/>.</summary>
    You,
    /// <summary>A crew member's line: <see cref="Painter.Blue"/>.</summary>
    Crew,
    /// <summary>A "Recorded:" note: <see cref="Painter.Dim"/>.</summary>
    Recorded,
}

/// <summary>
/// Which colour a log line gets (pure, so it is unit-tested). Amber means "needs you", so an ordinary SHIP line is neutral and only
/// the lines that ask for something or announce a change are amber; a death is red-orange. The ship's lines are plain text, so the
/// rule reads their wording (the prefixes are the ones the simulation writes: ALERT, Fault:, DRIFT:, LOCKOUT, ARRIVAL).
/// </summary>
internal static class LogTones
{
    private static readonly string[] NeedsStart = { "ALERT ", "Fault:", "DRIFT:", "LOCKOUT", "ARRIVAL", "Promise to " };

    private static readonly string[] NeedsContains =
    {
        "Stopped:", "the clock ran out", "ran out in year", "No spare parts", "is locked", "can no longer", "before the work can start",
        "no repair is staffed", "won't last", "can take them again", "go back to sleep", "The thaw went badly", "is hurt",
        "The accounts differ",
    };

    private static readonly Regex Death = new(@"\b(is not breathing|did not survive|died|starved|is dead)\b", RegexOptions.Compiled);

    public static LogTone Of(LogKind kind, string text)
    {
        switch (kind)
        {
            case LogKind.Crew:
                return LogTone.Crew;
            case LogKind.You:
                return LogTone.You;
            case LogKind.Recorded:
                return LogTone.Recorded;
        }
        foreach (var start in NeedsStart)
            if (text.StartsWith(start, StringComparison.Ordinal))
                return LogTone.Needs;
        if (Death.IsMatch(text))
            return LogTone.Danger;
        foreach (var part in NeedsContains)
            if (text.Contains(part, StringComparison.Ordinal))
                return LogTone.Needs;
        return LogTone.Neutral;
    }

    /// <summary>A fault is in its final stretch when under <see cref="Tuning.FaultFinalShare"/> of its clock is left (red-orange, a fast pulse).</summary>
    public static bool IsFinal(double hoursLeft, double clockHours) => hoursLeft < Tuning.FaultFinalShare * clockHours;
}
