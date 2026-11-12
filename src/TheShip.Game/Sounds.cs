using System;
using Microsoft.Xna.Framework.Audio;

namespace TheShip.Game;

/// <summary>The interface sounds, made in code. No audio device (or any audio failure) means silence, never a crash.</summary>
public sealed class Sounds
{
    /// <summary>Loudness of the click, 0 to 1. Audio numbers live here, not in Tuning.cs (which is for the simulation).</summary>
    public const float ClickVolume = 0.25f;

    private const int SampleRate = 44100;
    private SoundEffect? _click;
    private bool _failed;

    /// <summary>Build the click once. Safe to call without an audio device.</summary>
    public void Load()
    {
        try
        {
            _click = new SoundEffect(MakeClick(), SampleRate, AudioChannels.Mono);
        }
        catch (Exception)
        {
            _failed = true;
        }
    }

    /// <summary>Play the button click; does nothing when audio is not available.</summary>
    public void Click()
    {
        if (_failed || _click is null)
            return;
        try
        {
            _click.Play(ClickVolume, 0f, 0f);
        }
        catch (Exception)
        {
            _failed = true;
        }
    }

    /// <summary>About 35 ms: a soft sine tick at 1.2 kHz that falls a little in pitch, with a fast decay.</summary>
    private static byte[] MakeClick()
    {
        var count = SampleRate * 35 / 1000;
        var bytes = new byte[count * 2];
        var phase = 0.0;
        for (var i = 0; i < count; i++)
        {
            var t = i / (double)count;
            var freq = 1200.0 - 400.0 * t;
            phase += 2 * Math.PI * freq / SampleRate;
            var attack = Math.Min(1.0, i / 40.0);
            var envelope = attack * Math.Exp(-7.0 * t);
            var value = (short)(Math.Sin(phase) * envelope * short.MaxValue * 0.8);
            bytes[i * 2] = (byte)(value & 0xff);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xff);
        }
        return bytes;
    }
}
