using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TheShip.Game;

/// <summary>Where finished PCM blocks go: a DynamicSoundEffectInstance in the game, a fake in the tests.</summary>
public interface IPcmSink
{
    int PendingBufferCount { get; }

    /// <summary>Queue a block. The sink copies it (MonoGame's OpenAL backend copies into an OpenAL buffer at once).</summary>
    void SubmitBuffer(byte[] buffer);
}

/// <summary>
/// Renders PCM blocks on its own thread into a small queue of ready blocks; <see cref="Pump"/> hands them to the sink on the
/// thread that calls it. MonoGame's DynamicSoundEffectInstance is not thread-safe: SubmitBuffer and PendingBufferCount touch a
/// queue that FrameworkDispatcher.Update empties on the game thread, so they must only be called from there (calling them from
/// the music thread threw "Queue empty" in Game.Tick). Plain C#, no MonoGame, so the tests compile it in.
/// </summary>
public sealed class PcmFeed : IDisposable
{
    private readonly Action<byte[]> _render;
    private readonly Func<bool> _wanted;
    private readonly int _ready;
    private readonly ConcurrentQueue<byte[]> _full = new();
    private readonly ConcurrentQueue<byte[]> _free = new();
    private readonly AutoResetEvent _room = new(false);
    private Thread? _thread;
    private volatile bool _stop;
    private volatile bool _failed;

    /// <param name="render">Fills one block (called on the feed's thread only).</param>
    /// <param name="wanted">Whether there is anything to render now; while false the thread idles.</param>
    /// <param name="blockBytes">Bytes per block.</param>
    /// <param name="ready">How many rendered blocks wait ahead of the sink at most.</param>
    public PcmFeed(Action<byte[]> render, Func<bool> wanted, int blockBytes, int ready)
    {
        _render = render;
        _wanted = wanted;
        _ready = ready;
        for (var i = 0; i < ready + 1; i++)
            _free.Enqueue(new byte[blockBytes]);
    }

    /// <summary>The render callback threw: the feed stopped.</summary>
    public bool Failed => _failed;

    /// <summary>Rendered blocks not yet handed to the sink.</summary>
    public int Ready => _full.Count;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "TheShip music" };
        _thread.Start();
    }

    /// <summary>
    /// Top the sink up to <paramref name="ahead"/> pending blocks from the ready ones. Call only from the thread that owns the
    /// sink (the game thread). Returns how many blocks were submitted.
    /// </summary>
    public int Pump(IPcmSink sink, int ahead)
    {
        var sent = 0;
        while (sink.PendingBufferCount < ahead && _full.TryDequeue(out var block))
        {
            sink.SubmitBuffer(block);
            _free.Enqueue(block);
            sent++;
        }
        if (sent > 0)
            _room.Set();
        return sent;
    }

    private void Run()
    {
        try
        {
            while (!_stop)
            {
                if (_full.Count >= _ready || !_wanted() || !_free.TryDequeue(out var block))
                {
                    _room.WaitOne(10);
                    continue;
                }
                _render(block);
                _full.Enqueue(block);
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
        _room.Set();
        _thread?.Join(500);
        _room.Dispose();
    }
}
