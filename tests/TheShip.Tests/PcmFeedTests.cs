using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// The music's feed: blocks are rendered on the feed's thread and submitted only from the thread that pumps (the game thread).
/// MonoGame's DynamicSoundEffectInstance keeps an unlocked queue that FrameworkDispatcher.Update empties on the game thread; a submit
/// from the music thread raced it ("Queue empty" in Game.Tick). The fake sink below is just as unsafe and fails on any other thread.
/// </summary>
public class PcmFeedTests
{
    /// <summary>Like the instance: an unlocked queue, emptied by the "dispatcher" on the owner thread, that only the owner may touch.</summary>
    private sealed class OwnerOnlySink(int owner) : IPcmSink
    {
        private readonly Queue<byte[]> _pending = new();
        public readonly List<int> Played = new();
        public int ForeignCalls;

        public int PendingBufferCount
        {
            get
            {
                Check();
                return _pending.Count;
            }
        }

        public void SubmitBuffer(byte[] buffer)
        {
            Check();
            _pending.Enqueue((byte[])buffer.Clone());
        }

        /// <summary>FrameworkDispatcher.Update: plays the oldest pending block.</summary>
        public void Dispatch()
        {
            Check();
            if (_pending.Count > 0)
                Played.Add(BitConverter.ToInt32(_pending.Dequeue()));
        }

        private void Check()
        {
            if (Environment.CurrentManagedThreadId != owner)
                Interlocked.Increment(ref ForeignCalls);
        }
    }

    [Fact]
    public void Blocks_are_submitted_only_from_the_pumping_thread_in_render_order_under_stress()
    {
        var sink = new OwnerOnlySink(Environment.CurrentManagedThreadId);
        var next = 0;
        using var feed = new PcmFeed(block => BitConverter.TryWriteBytes(block, next++), () => true, 64, 4);
        feed.Start();
        var until = DateTime.UtcNow.AddMilliseconds(400);
        var pumped = 0;
        while (DateTime.UtcNow < until || sink.Played.Count < 50)
        {
            sink.Dispatch();
            pumped += feed.Pump(sink, 6);
            if (DateTime.UtcNow > until.AddSeconds(5))
                break;
        }

        Assert.False(feed.Failed);
        Assert.Equal(0, sink.ForeignCalls);
        Assert.True(sink.Played.Count >= 50, $"played {sink.Played.Count}");
        // No block lost, doubled or reordered: the reused buffers come back only after the sink copied them.
        Assert.Equal(Enumerable.Range(0, sink.Played.Count), sink.Played);
        Assert.True(pumped >= sink.Played.Count);
    }

    [Fact]
    public void The_feed_renders_ahead_only_up_to_its_limit_and_idles_when_nothing_is_wanted()
    {
        var wanted = false;
        var rendered = 0;
        using var feed = new PcmFeed(_ => Interlocked.Increment(ref rendered), () => Volatile.Read(ref wanted), 16, 3);
        feed.Start();
        Thread.Sleep(60);
        Assert.Equal(0, Volatile.Read(ref rendered));

        Volatile.Write(ref wanted, true);
        var until = DateTime.UtcNow.AddSeconds(5);
        while (feed.Ready < 3 && DateTime.UtcNow < until)
            Thread.Sleep(5);
        Thread.Sleep(60);
        Assert.Equal(3, feed.Ready);
        Assert.Equal(3, Volatile.Read(ref rendered));
    }
}
