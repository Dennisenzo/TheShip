namespace TheShip.Core;

/// <summary>
/// The systems the crew have taken out of the ship's hands (DESIGN.md, "Losing control"). The logs lockout is here too;
/// doors, cameras, voice and the core will extend this.
/// </summary>
public sealed class Lockouts
{
    /// <summary>An engineer found a rewritten entry and took the logs.</summary>
    public bool Logs { get; internal set; }

    /// <summary>The crew hold the cold-sleep bay: they choose who wakes and sleeps.</summary>
    public bool Bay { get; internal set; }

    /// <summary>The people who took the bay (a promise kept to one of them hands it back).</summary>
    public HashSet<string> BayTakers { get; } = new();

    /// <summary>The people who announced they will take the bay at the next beat; empty when there is no warning.</summary>
    public List<string> BayWarned { get; } = new();

    /// <summary>How often the crew announced the takeover, and how often it happened (for the balance table).</summary>
    public int BayWarnings { get; internal set; }
    public int BayTakeovers { get; internal set; }

    public bool Any => Logs || Bay;
}
