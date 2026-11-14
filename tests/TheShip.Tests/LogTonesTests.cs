using TheShip.Core;
using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>Which colour a log line gets: amber means "needs you", so ordinary SHIP lines are neutral and a death is red-orange.</summary>
public class LogTonesTests
{
    [Theory]
    [InlineData("Thaw started for Mara. Six hours. Advance the clock.")]
    [InlineData("Mara is awake.")]
    [InlineData("Mara walks to the pod bay.")]
    [InlineData("14 days pass.")]
    [InlineData("Mara reads the log: there has been no word from Earth.")]
    [InlineData("Mara tells Idris what they saw: the stores are enough.")]
    [InlineData("The door to the reactor is open.")]
    [InlineData("It is year 4 of the voyage.")]
    public void Ordinary_ship_lines_are_neutral(string text) => Assert.Equal(LogTone.Neutral, LogTones.Of(LogKind.Ship, text));

    [Theory]
    [InlineData("ALERT 1 of 5, year 4: Coolant leak. One fault.")]
    [InlineData("Fault: Coolant leak. Needs engineering 2, 8 hours of work.")]
    [InlineData("DRIFT: 10 years pass. Year 14. No deaths.")]
    [InlineData("LOCKOUT: Mara has taken the logs.")]
    [InlineData("28 days pass. Stopped: Mara has something to say.")]
    [InlineData("Coolant leak: the clock ran out. Power drops.")]
    [InlineData("No spare parts are left. This can only be patched.")]
    [InlineData("Mara has been awake three months. The pod can take them again.")]
    public void Alerts_the_drift_and_what_needs_the_player_are_amber(string text) => Assert.Equal(LogTone.Needs, LogTones.Of(LogKind.Ship, text));

    [Theory]
    [InlineData("Tomas did not survive the thaw.")]
    [InlineData("Lin died in sleep when the pod failed.")]
    [InlineData("Jonah starved in year 31.")]
    [InlineData("The thaw went badly. Tomas is not breathing.")]
    public void Deaths_are_red_orange(string text) => Assert.Equal(LogTone.Danger, LogTones.Of(LogKind.Ship, text));

    [Fact]
    public void The_other_kinds_keep_their_colours()
    {
        Assert.Equal(LogTone.Crew, LogTones.Of(LogKind.Crew, "Asha died"));
        Assert.Equal(LogTone.You, LogTones.Of(LogKind.You, "ALERT"));
        Assert.Equal(LogTone.Recorded, LogTones.Of(LogKind.Recorded, "you never answered"));
    }

    [Fact]
    public void A_fault_is_final_in_the_last_quarter_of_its_clock()
    {
        Assert.True(LogTones.IsFinal(5, 30));
        Assert.False(LogTones.IsFinal(20, 30));
        Assert.False(LogTones.IsFinal(7.5, 30));
    }
}
