namespace TheShip.Game;

/// <summary>What the reveal needs to know about one log entry.</summary>
/// <param name="Spoken">A crew line: it is typed out letter by letter. Every other line appears whole.</param>
/// <param name="Pending">The model is still wording it: the line shows as "..." and holds back everything after it.</param>
/// <param name="Length">The characters to type (the speaker's name and dash included).</param>
public readonly record struct RevealItem(bool Spoken, bool Pending, int Length);

/// <summary>
/// Display-only order of the bridge log (no dependencies, so it is unit-tested). A line is not shown until every line before it is
/// fully shown: a crew line is "..." while the model words it, then types out at a readable speed; the lines after it wait.
/// The simulation never waits on this: entries are in the voyage's log at once, only the display holds them back.
/// A backlog types faster and, when long, shows at once, so it never grows without bound.
/// </summary>
public sealed class LogReveal
{
    public const double CharsPerSecond = 90;
    /// <summary>Each waiting line behind the current one adds this share to the typing speed.</summary>
    public const double BacklogSpeedUp = 1.0;
    /// <summary>With this many lines waiting behind the current one, everything is shown at once.</summary>
    public const int BacklogInstant = 6;

    private double _typed;

    /// <summary>Entries shown in full.</summary>
    public int Shown { get; private set; }

    /// <summary>The entry after the shown ones is on screen too, as "..." or part typed (see <see cref="Chars"/>).</summary>
    public bool HeadVisible { get; private set; }

    /// <summary>Entries on screen: those in full and the head being typed.</summary>
    public int Visible => Shown + (HeadVisible ? 1 : 0);

    /// <summary>How many characters of the head entry are on screen.</summary>
    public int Chars => (int)_typed;

    /// <summary>True while anything inside the first <c>limit</c> entries is still held back.</summary>
    public bool Draining { get; private set; }

    /// <summary>
    /// Move on by <paramref name="dt"/> seconds. <paramref name="limit"/> is how many entries may be shown at all (a clock move
    /// holds its lines back); <paramref name="skip"/> finishes everything up to the first line still being worded;
    /// <paramref name="instant"/> shows it all (the tour).
    /// </summary>
    public void Update(IReadOnlyList<RevealItem> items, int limit, double dt, bool skip = false, bool instant = false)
    {
        limit = Math.Min(limit, items.Count);
        if (Shown > limit)
        {
            Shown = limit;
            _typed = 0;
        }
        var budget = dt;
        while (Shown < limit)
        {
            var item = items[Shown];
            if (item.Pending)
            {
                HeadVisible = true;
                _typed = 0;
                Draining = true;
                return;
            }
            var waiting = limit - Shown - 1;
            if (!item.Spoken || instant || skip || waiting >= BacklogInstant)
            {
                Finish();
                continue;
            }
            HeadVisible = true;
            var speed = CharsPerSecond * (1 + BacklogSpeedUp * waiting);
            _typed += speed * budget;
            budget = 0;
            if (_typed >= item.Length)
            {
                // The time left over after this line goes to the next one.
                budget = (_typed - item.Length) / speed;
                Finish();
                continue;
            }
            Draining = true;
            return;
        }
        HeadVisible = false;
        _typed = 0;
        Draining = false;

        void Finish()
        {
            Shown++;
            HeadVisible = false;
            _typed = 0;
        }
    }
}
