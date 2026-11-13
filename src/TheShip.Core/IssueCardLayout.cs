namespace TheShip.Core;

/// <summary>
/// Heights and text budget of the issue cards in the bridge's left column (1600 x 900 space), so that four cards fit in every state. One
/// fault gets a full card (variable height, always fits); two or more get compact cards whose height depends on how many are shown (more
/// lines with fewer cards), and a fault that is done is a one-line row.
/// </summary>
public static class IssueCardLayout
{
    /// <summary>Pixels between the first card and the clock box.</summary>
    public const int Available = 634;
    public const int Gap = 4;
    public const int ResolvedRow = 30;
    /// <summary>Above the text: padding, the state line and the name.</summary>
    public const int CardTop = 50;
    /// <summary>Below the text: the buttons and the padding under them.</summary>
    public const int CardBottom = 42;
    /// <summary>One line of the small font (<c>Painter.Paragraph</c>).</summary>
    public const int LineHeight = 19;

    /// <summary>Width of the text inside a card (the column less 14 px padding each side).</summary>
    public const int TextWidth = 302;
    /// <summary>
    /// A conservative estimate of one character of the small font (the width of "M", which <c>Painter.Paragraph</c> also wraps by),
    /// and of the body font the compact card's name is drawn in.
    /// </summary>
    public const double SmallCharWidth = 7.19;
    public const double BodyCharWidth = 8.2;

    /// <summary>Characters of the small font that fit one line of a card.</summary>
    public static int Columns => (int)(TextWidth / SmallCharWidth);

    /// <summary>True when <paramref name="text"/> fits one line of card text in the small font.</summary>
    public static bool FitsOneLine(string text) => text.Length <= Columns;

    /// <summary>True when <paramref name="text"/> fits a compact card's name line in the body font.</summary>
    public static bool FitsNameLine(string text) => text.Length * BodyCharWidth <= TextWidth;

    /// <summary>Lines of text an open compact card holds: six with up to three cards shown, three with four.</summary>
    public static int TextLines(int shown) => shown <= 3 ? 6 : 3;

    public static int OpenCardHeight(int shown) => CardTop + TextLines(shown) * LineHeight + CardBottom;

    /// <summary>Total height of <paramref name="shown"/> compact cards of which <paramref name="resolved"/> are done.</summary>
    public static int Total(int shown, int resolved) =>
        (shown - resolved) * OpenCardHeight(shown) + resolved * ResolvedRow + shown * Gap;

    /// <summary>How a compact card spends its lines: the needs text, the danger or exposure line, and whether the workers line shows.</summary>
    public readonly record struct CardLines(int Needs, int Extra, bool Workers);

    /// <summary>
    /// Splits a compact card's lines. The verdict always gets its own line and the needs text at least one; then the workers line, then
    /// the danger or exposure line (up to two), then a second needs line. <paramref name="needsLines"/> and <paramref name="extraLines"/>
    /// are how many lines those texts take unclipped.
    /// </summary>
    public static CardLines Split(int shown, int needsLines, int extraLines, bool working)
    {
        var budget = TextLines(shown) - 2;
        var workers = working && budget > 0;
        if (workers)
            budget--;
        var extra = Math.Min(Math.Min(extraLines, 2), budget);
        budget -= extra;
        var needs = 1 + Math.Min(Math.Max(0, Math.Min(needsLines, 2) - 1), budget);
        return new CardLines(needs, extra, workers);
    }

    /// <summary>The name on a card: a returning patch is "Returned: coolant leak" (the log keeps "Old patch gives way: coolant leak").</summary>
    public static string CardName(Fault f) => f.Name == f.PlainName ? f.Name : "Returned: " + f.PlainName;
}
