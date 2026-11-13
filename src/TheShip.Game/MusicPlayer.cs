using System;
using System.Threading;
using Microsoft.Xna.Framework.Audio;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// Plays <see cref="MusicEngine"/> through a DynamicSoundEffectInstance, fed from its own background thread so a slow frame or the
/// model loading never starves the sound. No audio device (or any audio failure) means silence, never a crash.
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    private const int FramesPerBuffer = MusicParams.SampleRate / 10;
    private const int BuffersAhead = 4;

    public readonly MusicEngine Engine = new();
    private DynamicSoundEffectInstance? _instance;
    private Thread? _thread;
    private volatile bool _stop;
    private volatile bool _failed;

    /// <summary>Start playing. Safe to call without an audio device.</summary>
    public void Start()
    {
        try
        {
            _instance = new DynamicSoundEffectInstance(MusicParams.SampleRate, AudioChannels.Stereo) { Volume = 1f };
            _instance.Play();
            _thread = new Thread(Feed) { IsBackground = true, Name = "TheShip music" };
            _thread.Start();
        }
        catch (Exception)
        {
            _failed = true;
        }
    }

    public bool MusicOn
    {
        get => Engine.MusicOn;
        set => Engine.MusicOn = value;
    }

    public void Alarm() => Engine.TriggerAlarm();

    /// <summary>Follow the game: read the voyage and set the mood (the music never writes to it).</summary>
    public void Follow(Voyage? voyage, bool drifting)
    {
        if (voyage is null)
            return;
        if (drifting)
        {
            Engine.SetMood(MusicMood.Drift);
            return;
        }
        switch (voyage.Phase)
        {
            case Phase.Ended:
                Engine.SetMood(voyage.Ending?.Title == "Landfall" ? MusicMood.Landfall : MusicMood.Bleak);
                break;
            case Phase.Alert:
                var open = voyage.Current.Faults.Where(f => !f.Resolved).ToList();
                var shortest = open.Count == 0 ? double.MaxValue : open.Min(f => f.Deadline - voyage.Clock);
                Engine.SetMood(MusicMood.Alert, MusicTension.Of(open.Count, open.Count(f => f.Critical), shortest));
                break;
            default:
                Engine.SetMood(MusicMood.Calm);
                break;
        }
    }

    private void Feed()
    {
        var floats = new float[FramesPerBuffer * 2];
        var bytes = new byte[FramesPerBuffer * 4];
        try
        {
            while (!_stop && !_failed)
            {
                if (!Engine.Active || _instance is null || _instance.PendingBufferCount >= BuffersAhead)
                {
                    Thread.Sleep(20);
                    continue;
                }
                Engine.Render(floats, FramesPerBuffer);
                for (var i = 0; i < floats.Length; i++)
                {
                    var v = (short)Math.Clamp(floats[i] * short.MaxValue, short.MinValue, short.MaxValue);
                    bytes[i * 2] = (byte)(v & 0xff);
                    bytes[i * 2 + 1] = (byte)((v >> 8) & 0xff);
                }
                _instance.SubmitBuffer(bytes);
            }
        }
        catch (Exception)
        {
            _failed = true;
        }
    }

    public void Dispose()
    {
        _stop = true;
        try
        {
            _thread?.Join(500);
            _instance?.Dispose();
        }
        catch (Exception)
        {
            // Closing without sound is fine.
        }
    }
}
