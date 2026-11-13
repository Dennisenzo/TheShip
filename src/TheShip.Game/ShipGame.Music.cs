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

    /// <summary>Each update, on the game thread: follow the game's mood and hand the rendered sound to the audio device.</summary>
    private void UpdateMusic()
    {
        _music?.Follow(_voyage, Drifting);
        _music?.Update();
    }

    /// <summary>A small MUSIC on/off button; "NO AUDIO" (disabled) when the sound could not start or failed.</summary>
    private void MusicButton(Rectangle r)
    {
        if (_music is null)
        {
            // The tour runs silent.
            _p.Button(r, "MUSIC OFF", enabled: false);
            return;
        }
        if (_music is not { Available: true } music)
        {
            _p.Button(r, "NO AUDIO", enabled: false, tip: "No sound: the audio device could not be opened, or failed.");
            return;
        }
        var on = music.MusicOn;
        if (_p.Button(r, on ? "MUSIC ON" : "MUSIC OFF", selected: on))
            Do(() => music.MusicOn = !music.MusicOn);
    }
}
