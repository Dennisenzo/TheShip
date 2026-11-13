using Microsoft.Xna.Framework;

namespace TheShip.Game;

/// <summary>The ambient music and the alarm: it reads the game's state and never changes it. Silent under --tour.</summary>
public sealed partial class ShipGame
{
    private MusicPlayer? _music;

    private void StartMusic()
    {
        if (_options.TourDir is not null || _music is not null)
            return;
        _music = new MusicPlayer();
        _music.Start();
    }

    /// <summary>The alarm of a new alert, once, when its title shows.</summary>
    private void SoundAlarm() => _music?.Alarm();

    private void UpdateMusic() => _music?.Follow(_voyage, Drifting);

    /// <summary>A small MUSIC on/off button; returns where it ends so the caller can place the rest.</summary>
    private void MusicButton(Rectangle r)
    {
        var on = _music?.MusicOn ?? false;
        if (_p.Button(r, on ? "MUSIC ON" : "MUSIC OFF", enabled: _music is not null, selected: on))
            Do(() => { if (_music is not null) _music.MusicOn = !_music.MusicOn; });
    }
}
