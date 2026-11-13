namespace TheShip.Core;

public sealed partial class Voyage
{
    /// <summary>Dev tool (the screenshot tour): the person reads the log entry on a fact now, as an aftermath beat would make them.</summary>
    public void TourReadLog(CrewMember c, Fact f) => ReadLog(c, f);

    /// <summary>
    /// Dev tool (the screenshot tour): puts back the trust, morale and caught lies a person had before the tour's scripted lie, so
    /// the shots after it come from a normal run. Never called by the game.
    /// </summary>
    public void TourRestore(CrewMember c, double trust, double morale, int caughtLies)
    {
        c.Trust = trust;
        c.Morale = morale;
        c.CaughtLies = caughtLies;
        Clamp(c);
    }
}
