using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// The overlay layer: at most one popup or transition on top of the current screen. While one is up the screen below
/// gets no clicks, no hover and no typing (<see cref="DrawScene"/>).
/// </summary>
public sealed partial class ShipGame
{
    private bool OverlayUp => _briefing;

    /// <summary>Enter or Esc while an overlay is up.</summary>
    private void OverlayKey(bool enter)
    {
        if (_briefing)
            CloseBriefing();
    }

    /// <summary>Draws whatever overlay is up, with the real mouse and click.</summary>
    private void DrawOverlay(Voyage v)
    {
        if (_briefing)
            DrawBriefing(v);
    }
}
