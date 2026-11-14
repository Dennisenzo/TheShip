using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// Plays <see cref="MusicEngine"/> through a DynamicSoundEffectInstance. The music is rendered on its own thread (<see cref="PcmFeed"/>)
/// a few blocks ahead, so a slow frame or the model loading never starves the sound; the finished blocks are submitted from the game
/// thread in <see cref="Update"/>, because the instance is not thread-safe. No audio device (or any audio failure) means silence, never a crash.
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    /// <summary>50 ms blocks: the instance holds <see cref="BuffersAhead"/> of them (300 ms, a frame stall that long doesn't underrun) and
    /// the feed keeps <see cref="BuffersReady"/> more rendered (a mood change or the alarm is heard within about half a second).</summary>
    private const int FramesPerBuffer = MusicParams.SampleRate / 20;
    private const int BuffersAhead = 6;
    private const int BuffersReady = 4;

    public readonly MusicEngine Engine = new();
    private DynamicSoundEffectInstance? _instance;
    private PcmFeed? _feed;
    private Sink? _sink;
    private readonly float[] _floats = new float[FramesPerBuffer * 2];
    private readonly List<Fault> _open = new();
    private bool _failed;

    /// <summary>Start playing. Safe to call without an audio device.</summary>
    public void Start()
    {
        try
        {
            _instance = new DynamicSoundEffectInstance(MusicParams.SampleRate, AudioChannels.Stereo) { Volume = 1f };
            _instance.Play();
            _sink = new Sink(_instance);
            _feed = new PcmFeed(Render, () => Engine.Active, FramesPerBuffer * 4, BuffersReady);
            _feed.Start();
        }
        catch (Exception)
        {
            _failed = true;
        }
    }

    /// <summary>False when the audio could not start or failed since: the MUSIC button shows that.</summary>
    public bool Available => !_failed && _instance is not null && _feed is { Failed: false };

    public bool MusicOn
    {
        get => Engine.MusicOn;
        set => Engine.MusicOn = value;
    }

    public void Alarm() => Engine.TriggerAlarm();

    /// <summary>Hand the rendered blocks to the sound instance. Game thread only (from Update).</summary>
    public void Update()
    {
        if (!Available)
            return;
        try
        {
            _feed!.Pump(_sink!, BuffersAhead);
        }
        catch (Exception)
        {
            _failed = true;
        }
    }

    /// <summary>Follow the game: read the voyage and set the mood (the music never writes to it).</summary>
    public void Follow(Voyage? voyage, bool drifting)
    {
        if (voyage is null)
            return;
        var mood = MoodOf(voyage, drifting);
        if (mood != MusicMood.Alert)
        {
            Engine.SetMood(mood);
            return;
        }
        switch (voyage.Phase)
        {
            case Phase.Alert:
                // Reused every update: no allocation per frame.
                _open.Clear();
                foreach (var f in voyage.Current.Faults)
                    if (!f.Resolved)
                        _open.Add(f);
                var shortest = double.MaxValue;
                var critical = 0;
                foreach (var f in _open)
                {
                    shortest = Math.Min(shortest, f.Deadline - voyage.Clock);
                    if (f.Critical)
                        critical++;
                }
                Engine.SetMood(MusicMood.Alert, MusicTension.Of(_open.Count, critical, shortest));
                break;
        }
    }

    /// <summary>The mood the game is in: the music follows it, and the sky behind the interface reads it too (read-only).</summary>
    public static MusicMood MoodOf(Voyage voyage, bool drifting) =>
        drifting ? MusicMood.Drift
        : voyage.Phase == Phase.Ended ? (voyage.Ending?.Title == "Landfall" ? MusicMood.Landfall : MusicMood.Bleak)
        : voyage.Phase == Phase.Alert ? MusicMood.Alert
        : MusicMood.Calm;

    /// <summary>One block, on the feed's thread: the engine's floats as 16-bit little-endian stereo.</summary>
    private void Render(byte[] bytes)
    {
        Engine.Render(_floats, FramesPerBuffer);
        for (var i = 0; i < _floats.Length; i++)
        {
            var v = (short)Math.Clamp(_floats[i] * short.MaxValue, short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(v & 0xff);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
    }

    private sealed class Sink(DynamicSoundEffectInstance instance) : IPcmSink
    {
        public int PendingBufferCount => instance.PendingBufferCount;
        public void SubmitBuffer(byte[] buffer) => instance.SubmitBuffer(buffer);
    }

    public void Dispose()
    {
        try
        {
            _feed?.Dispose();
            _instance?.Dispose();
        }
        catch (Exception)
        {
            // Closing without sound is fine.
        }
    }
}
