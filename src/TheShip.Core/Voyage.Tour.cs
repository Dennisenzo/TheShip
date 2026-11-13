namespace TheShip.Core;

public sealed partial class Voyage
{
    /// <summary>Dev tool (the screenshot tour): the person reads the log entry on a fact now, as an aftermath beat would make them.</summary>
    public void TourReadLog(CrewMember c, Fact f) => ReadLog(c, f);
}
