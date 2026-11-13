namespace TheShip.Core;

/// <summary>What one "advance until something happens" did: two-week steps taken and why it stopped ("" if it just ran out of steps).</summary>
public sealed record AdvanceResult(int Steps, string Reason);

public sealed partial class Voyage
{
    /// <summary>Why the step in progress needs the player; set at the source, cleared at the start of each step.</summary>
    private string? _attention;

    private void Attend(string reason) => _attention ??= reason;

    /// <summary>
    /// Advance in the aftermath until something needs the player: a crew line, a person who can go back to sleep (after
    /// three months, or early because the stores are too low), a death,
    /// a lost fault, differing accounts, the stores running out. In the Alert phase it is <see cref="Go"/>, which is already event-driven.
    /// Stops at <see cref="Tuning.AdvanceMaxSteps"/> steps at the latest, and does not move the clock when nothing could happen.
    /// </summary>
    public AdvanceResult GoUntilAttention()
    {
        if (Phase == Phase.Ended)
            return new AdvanceResult(0, "");
        if (Phase == Phase.Alert)
        {
            Go();
            return new AdvanceResult(1, "");
        }

        var start = Clock;
        var steps = 0;
        string? reason = null;
        while (steps < Tuning.AdvanceMaxSteps)
        {
            var up = Crew.Where(c => c.State is CrewState.Awake or CrewState.Thawing).ToList();
            if (up.Count == 0)
            {
                if (steps == 0)
                    GoAftermath();
                reason = "everyone is asleep";
                break;
            }
            // Three months awake, or stores too low to last that long (CanSleepNow): waiting on would only starve them.
            if (up.All(CanSleepNow) && !Lockouts.Bay)
            {
                if (steps == 0)
                    Ship("Everyone awake can go back to sleep. Nothing more will happen until you ask them to sleep or drift.");
                reason = "everyone awake can go back to sleep";
                break;
            }

            _attention = null;
            GoAftermath();
            SyncFacts();
            steps++;
            if (_attention is not null)
            {
                reason = _attention;
                break;
            }
            if (Phase != Phase.Aftermath)
                break;
        }
        _attention = null;

        if (steps > 0)
        {
            var passed = Hours(Clock - start) + " pass.";
            Ship(reason is null ? passed : passed + " Stopped: " + reason + ".");
        }
        return new AdvanceResult(steps, reason ?? "");
    }
}
