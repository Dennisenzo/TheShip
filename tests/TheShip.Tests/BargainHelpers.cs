using TheShip.Core;

namespace TheShip.Tests;

/// <summary>Test helpers for the bargain rule: the real Assign, and the ship's word when the person asks for it.</summary>
public static class BargainHelpers
{
    /// <summary>
    /// Asks the person to work (the real rule). When they set the ship's word as the price, the ship gives it (the matching
    /// promise, said to them), so the work starts as the bargain says.
    /// </summary>
    public static void AssignDeal(this Voyage v, CrewMember c, Fault f, bool patch)
    {
        v.Assign(c, f, patch);
        if (v.BargainOf(c) is { } b && !c.BargainDeclined)
            v.Say("Agreed.", new Reading(LineKind.Promise, PromiseKey: b.Key), new[] { c });
    }

    /// <summary>Releases every open promise to a person (no penalty), for tests of something else that start from a played alert.</summary>
    public static void ReleaseOwed(this Voyage v, CrewMember c)
    {
        foreach (var s in v.Statements.Where(s => s.OpenFor(c.Id)))
            s.Released.Add(c.Id);
    }
}
