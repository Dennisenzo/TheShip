using System;

namespace TheShip.Game;

public sealed partial class ShipGame
{
    /// <summary>Move the sky to now and draw it on the ground, before any panel. Reads the game, never changes it.</summary>
    private void DrawBackdrop()
    {
        if (!_backdrop.Enabled)
            return;
        var mood = _voyage is null ? MusicMood.Calm : MusicPlayer.MoodOf(_voyage, Drifting);
        // The sky rushes with the years rolling (the same curve as the streaks in the plan).
        var drift = _tourDrift ?? (_transition is { Kind: SkipKind.Drift, Beat: 0 } t
            ? MathF.Sin(Motion.Progress(_now, t.Start, Motion.DriftRoll) * MathF.PI)
            : 0f);
        _backdrop.Step(_now, mood, drift, _options.FixedTime);
        _backdrop.Draw(_p);
    }
}
