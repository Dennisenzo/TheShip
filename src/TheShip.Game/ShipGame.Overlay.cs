using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// The overlay layer: at most one popup or transition on top of the current screen. While one is up the screen below
/// gets no clicks, no hover and no typing (<see cref="DrawScene"/>).
/// </summary>
public sealed partial class ShipGame
{
    /// <summary>
    /// A popup that takes every click and key: the briefing and the picker. A transition (the drift report included) is
    /// not one: a click or key ends it and goes through (<see cref="TransitionBlock"/>).
    /// </summary>
    private bool OverlayUp => _briefing || _picker is not null;

    /// <summary>Enter or Esc while an overlay is up.</summary>
    private void OverlayKey(bool enter)
    {
        if (_briefing)
            CloseBriefing();
        else if (!enter)
            _picker = null;
    }

    /// <summary>Draws whatever overlay is up, with the real mouse and click.</summary>
    private void DrawOverlay(Voyage v)
    {
        if (_briefing)
            DrawBriefing(v);
        else
            DrawPicker(v);
    }
}
