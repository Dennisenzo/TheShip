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

    /// <summary>
    /// A small speaker button in the plan header, drawn in code: waves when the music is on, a cross when it is off.
    /// Disabled (and crossed) when the sound could not start or failed, and under the silent tour.
    /// </summary>
    private void MusicButton(Rectangle r)
    {
        bool on;
        if (_music is null)
        {
            // The tour runs silent.
            _p.Button(r, "", enabled: false, tip: "Music is off (silent run).");
            on = false;
        }
        else if (_music is not { Available: true } music)
        {
            _p.Button(r, "", enabled: false, tip: "No audio: the sound device could not be opened, or failed.");
            on = false;
        }
        else
        {
            on = music.MusicOn;
            if (_p.Button(r, "", selected: on, tip: "Music on/off"))
                Do(() => music.MusicOn = !music.MusicOn);
        }
        var ink = _music is { Available: true } ? (on ? Painter.Bright : Painter.Ink) : Painter.Dim;
        DrawSpeaker(r, ink, on);
    }

    /// <summary>The speaker icon: a box and a cone, then two arcs (on) or a cross (off).</summary>
    private void DrawSpeaker(Rectangle r, Color ink, bool waves)
    {
        var cx = r.X + r.Width / 2f - 2f;
        var cy = r.Y + r.Height / 2f;
        _p.Poly(new[]
        {
            new Vector2(cx - 8, cy - 2.5f), new Vector2(cx - 4, cy - 2.5f), new Vector2(cx + 1, cy - 6.5f),
            new Vector2(cx + 1, cy + 6.5f), new Vector2(cx - 4, cy + 2.5f), new Vector2(cx - 8, cy + 2.5f),
        }, ink, 1f);
        if (waves)
        {
            foreach (var radius in new[] { 4.5f, 8f })
            {
                var arc = new Vector2[5];
                for (var i = 0; i < arc.Length; i++)
                {
                    var a = (-0.7f + 1.4f * i / (arc.Length - 1));
                    arc[i] = new Vector2(cx + 1.5f + MathF.Cos(a) * radius, cy + MathF.Sin(a) * radius);
                }
                _p.Poly(arc, ink, 1f, closed: false);
            }
        }
        else
        {
            _p.Line(new Vector2(cx + 4, cy - 3.5f), new Vector2(cx + 10, cy + 3.5f), ink, 1f);
            _p.Line(new Vector2(cx + 10, cy - 3.5f), new Vector2(cx + 4, cy + 3.5f), ink, 1f);
        }
    }
}
