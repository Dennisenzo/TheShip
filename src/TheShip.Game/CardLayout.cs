namespace TheShip.Game;

/// <summary>
/// The pure layout rules of the bridge's cards (no MonoGame, so they are unit-tested; GRAPHICS-PLAN.md step 6): where a long text is cut,
/// how the crew column spends its height on living cards, dead strips and "Waiting on you", and the segments of a thin meter.
/// </summary>
internal static class CardLayout
{
    public const string Ellipsis = "...";

    /// <summary>
    /// The text cut to at most <paramref name="maxChars"/> characters: whole if it fits, else cut after the last whole word that leaves room
    /// for the ellipsis, with no punctuation before it ("seal..." not "seal,..."). One word too long to fit alone is cut inside the word.
    /// </summary>
    public static string CutAtWord(string text, int maxChars)
    {
        maxChars = Math.Max(Ellipsis.Length + 1, maxChars);
        if (text.Length <= maxChars)
            return text;
        var room = maxChars - Ellipsis.Length;
        var cut = text.LastIndexOf(' ', Math.Min(room, text.Length - 1));
        var head = cut > 0 ? text[..cut] : text[..room];
        head = head.TrimEnd(' ', '.', ',', ';', ':');
        return (head.Length == 0 ? text[..room] : head) + Ellipsis;
    }

    /// <summary>The text cut at a word so that it wraps to at most <paramref name="maxLines"/> lines of <paramref name="columns"/> characters.</summary>
    public static string CutToLines(string text, int columns, int maxLines)
    {
        if (TextLayout.Wrap(text, columns).Count <= maxLines)
            return text;
        var words = text.Split(' ').ToList();
        while (words.Count > 1 && TextLayout.Wrap(string.Join(' ', words).TrimEnd('.', ',', ';', ':') + Ellipsis, columns).Count > maxLines)
            words.RemoveAt(words.Count - 1);
        return string.Join(' ', words).TrimEnd('.', ',', ';', ':') + Ellipsis;
    }

    /// <summary>True when <paramref name="text"/> wraps to more than <paramref name="maxLines"/> lines.</summary>
    public static bool NeedsCut(string text, int columns, int maxLines) => TextLayout.Wrap(text, columns).Count > maxLines;

    // ---- The crew column ----

    public const int StripHeight = 30;
    public const int CardMin = 82;
    public const int CardMaxWithItems = 90;
    public const int CardMaxEmpty = 104;
    public const int CrewGap = 6;
    /// <summary>"Waiting on you" with nothing in it: one strip.</summary>
    public const int WaitingStrip = 34;
    /// <summary>"Waiting on you" with items: the least that holds a heading and a few rows.</summary>
    public const int WaitingMin = 130;

    /// <summary>Heights in the crew column: living cards, dead strips, and what is left for "Waiting on you" (the strip when it is empty).</summary>
    public readonly record struct CrewColumn(int CardHeight, int WaitingHeight, int Used);

    /// <summary>
    /// <paramref name="available"/> is the height from the first card to the bottom of the column. Dead people take one strip each, which
    /// gives the living cards room (up to <see cref="CardMaxWithItems"/>, or <see cref="CardMaxEmpty"/> when nothing waits on the player).
    /// The living cards never shrink below <see cref="CardMin"/>; "Waiting on you" gets the rest, and never less than its minimum.
    /// </summary>
    public static CrewColumn Crew(int available, int living, int dead, bool waitingEmpty)
    {
        var waitingMin = waitingEmpty ? WaitingStrip : WaitingMin;
        var max = waitingEmpty ? CardMaxEmpty : CardMaxWithItems;
        var cardHeight = 0;
        if (living > 0)
        {
            var room = available - waitingMin - 4 - dead * (StripHeight + CrewGap);
            cardHeight = Math.Clamp(room / living - CrewGap, CardMin, max);
        }
        var used = living * (cardHeight + CrewGap) + dead * (StripHeight + CrewGap);
        var waiting = waitingEmpty ? WaitingStrip : Math.Max(waitingMin, available - used - 4);
        return new CrewColumn(cardHeight, waiting, used);
    }

    // ---- Thin segmented meters ----

    /// <summary>One segment of a thin meter: its left edge and width in pixels, and how many of them the value fills.</summary>
    public readonly record struct Segment(int X, int Width, int Fill);

    /// <summary>
    /// A meter of <paramref name="edges"/>.Length - 1 segments across <paramref name="width"/> pixels with <paramref name="gap"/> pixels
    /// between them; segment widths follow the spans between the edges (trust: the four bands; morale: ten equal parts), and each is
    /// filled in proportion to <paramref name="value"/>.
    /// </summary>
    public static List<Segment> Segments(int width, int gap, double[] edges, double value)
    {
        var count = edges.Length - 1;
        var usable = width - gap * (count - 1);
        var total = edges[^1] - edges[0];
        var result = new List<Segment>(count);
        var x = 0;
        for (var i = 0; i < count; i++)
        {
            var right = (int)Math.Round(usable * (edges[i + 1] - edges[0]) / total);
            var w = Math.Max(1, right - (x - gap * i));
            var share = Math.Clamp((value - edges[i]) / (edges[i + 1] - edges[i]), 0, 1);
            result.Add(new Segment(x, w, (int)Math.Round(w * share)));
            x += w + gap;
        }
        return result;
    }
}
