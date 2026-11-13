namespace TheShip.Core;

/// <summary>
/// The per-run choices a seed draws (DESIGN.md, "Run variation"). Drawn from their own random generator, one roll per slot in a fixed order,
/// so a seed always draws the same variation whatever the player does later. <see cref="Classic"/> is the fixed scenario of the early tests.
/// </summary>
public sealed record Variation
{
    public const string Blight = "blight";
    public const string PowerSag = "powersag";

    /// <summary>Who dies in sleep in alert 2.</summary>
    public required string Alert2Victim { get; init; }
    /// <summary>The victim's bank neighbour who falls ill in alert 2 (a later step uses it).</summary>
    public required string Alert2Patient { get; init; }
    /// <summary>Side fault of alert 3 and alert 5 (<see cref="Blight"/>, <see cref="PowerSag"/>, or null for none). Stored for later steps.</summary>
    public string? Alert3SideFault { get; init; }
    public string? Alert5SideFault { get; init; }
    /// <summary>Extra clock hours per alert (index 0 is alert 1, which keeps its loose clock). Stored for later steps.</summary>
    public required IReadOnlyList<int> SlackHours { get; init; }
    /// <summary>Years added to each alert's year (alert order is kept, arrival stays at year 60).</summary>
    public required IReadOnlyList<int> YearJitter { get; init; }

    public static Variation Classic { get; } = new()
    {
        Alert2Victim = "idris", Alert2Patient = "tomas", Alert3SideFault = null, Alert5SideFault = null,
        SlackHours = new[] { 0, 0, 0, 0, 0 }, YearJitter = new[] { 0, 0, 0, 0, 0 },
    };

    private static readonly (string Id, int Weight)[] Victims = { ("idris", 3), ("tomas", 1), ("asha", 1), ("jonah", 1) };

    public static Variation Draw(int seed)
    {
        var rng = new Random(unchecked(seed * 7919 + 13));

        // Slot 1: victim (weighted).
        var total = Victims.Sum(v => v.Weight);
        var roll = rng.Next(total);
        var victim = Victims[^1].Id;
        foreach (var (id, weight) in Victims)
        {
            if (roll < weight) { victim = id; break; }
            roll -= weight;
        }

        // Slot 2: patient, a neighbour in the victim's bank.
        var bank = ShipPlan.Pod(victim).Bank;
        var neighbours = Scenario.Crew().Select(c => c.Id).Where(id => id != victim && ShipPlan.Pod(id).Bank == bank).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var patient = neighbours[rng.Next(neighbours.Count)];

        // Slots 3 and 4: side faults. Alert 5's is always power sag, also after a sag in alert 3 (decided 2026-11-13: no no-repeat rule).
        // Its slot is still rolled, so the draws after it stay the same when the pool grows.
        var kinds3 = new[] { Blight, PowerSag };
        var side3 = kinds3[rng.Next(kinds3.Length)];
        var kinds5 = new[] { PowerSag };
        var side5 = kinds5[rng.Next(kinds5.Length)];

        // Slot 5: slack hours per alert; slot 6: year jitter per alert (always rolled, so the sequence is fixed).
        var slack = new int[5];
        for (var i = 0; i < slack.Length; i++)
        {
            var s = rng.Next(Tuning.AlertSlackMin, Tuning.AlertSlackMax + 1);
            slack[i] = i == 0 ? 0 : s;
        }
        var jitter = new int[5];
        for (var i = 0; i < jitter.Length; i++)
            jitter[i] = rng.Next(-Tuning.AlertYearJitter, Tuning.AlertYearJitter + 1);

        return new Variation
        {
            Alert2Victim = victim, Alert2Patient = patient, Alert3SideFault = side3, Alert5SideFault = side5,
            SlackHours = slack, YearJitter = jitter,
        };
    }

    public override string ToString() =>
        $"victim {Alert2Victim}, patient {Alert2Patient}, side faults {Alert3SideFault ?? "none"}/{Alert5SideFault ?? "none"}, "
        + $"slack [{string.Join(",", SlackHours)}], year jitter [{string.Join(",", YearJitter)}]";
}
