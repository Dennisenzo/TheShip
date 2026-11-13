namespace TheShip.Core;

/// <summary>
/// Heights of the issue cards in the bridge's left column (1600 x 900 space), so that four cards fit in every state. One fault gets a full
/// card (variable height, always fits); two or more get fixed-height compact cards, and a fault that is done is a one-line row.
/// </summary>
public static class IssueCardLayout
{
    /// <summary>Pixels between the first card and the clock box.</summary>
    public const int Available = 634;
    public const int Gap = 4;
    public const int ResolvedRow = 30;
    /// <summary>An open compact card with room for the exposure and danger line (up to three faults shown).</summary>
    public const int RoomyCard = 168;
    /// <summary>An open compact card with four faults shown.</summary>
    public const int TightCard = 149;

    public static int OpenCardHeight(int shown) => shown <= 3 ? RoomyCard : TightCard;

    /// <summary>Total height of <paramref name="shown"/> compact cards of which <paramref name="resolved"/> are done.</summary>
    public static int Total(int shown, int resolved) =>
        (shown - resolved) * OpenCardHeight(shown) + resolved * ResolvedRow + shown * Gap;
}
