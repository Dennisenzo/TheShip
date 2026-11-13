using System.Collections;
using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>
/// A line said at once counts at the moment it was said, not when the model's reading lands: clicks that change the
/// game wait for it (<see cref="ShipMind.AfterReading"/>, which the game's Act clicks go through), and the read prompt
/// is built on the game thread from the state at Enter.
/// </summary>
public class SayAtOnceTimingTests
{
    private const string Promise = "{\"kind\":\"promise\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"wake_partner\"}";
    private const string Dose = "{\"kind\":\"statement\",\"fact\":\"dose\",\"value\":\"dosed\",\"promise\":\"none\"}";

    /// <summary>Drains the game-thread queue until the condition holds (or about 3 s pass).</summary>
    private static async Task Until(MainThreadQueue main, Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++)
        {
            main.Drain();
            await Task.Delay(10);
        }
        main.Drain();
    }

    [Fact]
    public async Task A_sleep_clicked_before_the_reading_lands_waits_for_the_line()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.FinishAlert(v);
        for (var i = 0; i < 20 && mara.HoursAwake < Tuning.MinAwakeHours; i++)
            v.Go();
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, new FakeLlm { Read = Promise }, main);
        var before = v.Statements.Count;
        mind.SayAtOnce("I promise I will wake Idris next time.", new List<CrewMember> { mara });
        var entry = v.Log.Last(e => e.Kind == LogKind.You);
        mind.AfterReading(() => v.AskSleep(mara));
        Assert.True(mind.Holding);
        Assert.Equal(CrewState.Awake, mara.State); // the click is held, not run yet
        await Until(main, () => !mind.Holding);
        Assert.False(entry.Unread);
        Assert.Equal(before + 1, v.Statements.Count);
        Assert.DoesNotContain(v.Log, e => e.Text == "Nobody is awake to hear you.");
    }

    [Fact]
    public async Task An_advance_clicked_before_the_reading_lands_records_the_line_at_the_year_it_was_said()
    {
        var v = new Voyage(1);
        VoyageTests.HonestFirstAlert(v);
        VoyageTests.FinishAlert(v);
        var mara = v.Awake.First();
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, new FakeLlm { Read = Promise }, main);
        mind.SayAtOnce("I promise I will wake Idris next time.", new List<CrewMember> { mara });
        var year = v.Year;
        mind.AfterReading(v.Go);
        await Until(main, () => !mind.Holding);
        Assert.Equal(year, v.Statements[^1].Year);
        Assert.True(v.Year > year); // and the advance did run
    }

    [Fact]
    public async Task A_repair_clicked_right_after_answering_the_question_starts()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var f = v.Current.Faults[0];
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        v.AssignDeal(mara, f, false); // "Is it safe in there?"
        Assert.NotNull(mara.PendingQuestion);
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, new FakeLlm { Read = Dose }, main);
        mind.SayAtOnce("It is dangerous. You will take a radiation dose.", new List<CrewMember> { mara });
        mind.AfterReading(() => v.AssignDeal(mara, f, false));
        await Until(main, () => !mind.Holding);
        Assert.Equal(f.Id, mara.WorkingOn);
    }

    [Fact]
    public async Task A_line_said_before_the_last_drift_reaches_the_ending_record()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        VoyageTests.FinishAlert(v);
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, new FakeLlm { Read = Promise }, main);
        mind.SayAtOnce("I promise I will wake Idris next time.", new List<CrewMember> { mara });
        var said = v.Statements.Count;
        mind.AfterReading(() =>
        {
            for (var i = 0; i < 50 && v.Phase != Phase.Ended; i++)
            {
                foreach (var c in v.Awake.ToList())
                    v.AskSleep(c);
                v.Drift();
            }
        });
        await Until(main, () => !mind.Holding);
        Assert.Equal(said + 1, v.Statements.Count);
        Assert.Equal("I promise I will wake Idris next time.", v.Statements[^1].Text);
    }

    [Fact]
    public void A_click_with_no_line_being_read_runs_at_once()
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue());
        var ran = false;
        mind.AfterReading(() => ran = true);
        Assert.True(ran);
        Assert.False(mind.Holding);
    }

    [Fact]
    public async Task A_hanging_model_holds_clicks_only_until_the_timeout_and_the_line_is_plain_talk()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var llm = new HangingLlm();
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, llm, main) { ReadTimeout = TimeSpan.FromMilliseconds(100) };
        var seen = new List<(Reading Reading, bool Failed)>();
        mind.LineRead += (_, r, failed) => seen.Add((r, failed));
        mind.SayAtOnce("Idris is alive.", new List<CrewMember> { mara });
        var ran = false;
        mind.AfterReading(() => ran = true);
        await Until(main, () => ran);
        Assert.True(ran);
        Assert.Equal((Reading.Plain, true), Assert.Single(seen));
        Assert.Empty(v.Statements);
        // The late answer changes nothing.
        llm.Release.SetResult("{\"kind\":\"statement\",\"fact\":\"alive:idris\",\"value\":\"alive\",\"promise\":\"none\"}");
        await Until(main, () => false);
        Assert.Single(seen);
        Assert.Empty(v.Statements);
    }

    [Fact]
    public async Task The_read_prompt_is_built_from_the_state_when_the_line_was_typed()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var f = v.Current.Faults[0];
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        v.AssignDeal(mara, f, false);
        var asked = mara.PendingQuestion;
        Assert.NotNull(asked);
        var llm = new HangingLlm();
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, llm, main);
        mind.SayAtOnce("It is dangerous.", new List<CrewMember> { mara });
        mara.PendingQuestion = null; // anything that changes the state after Enter
        llm.Release.SetResult(Dose);
        await Until(main, () => !mind.Holding);
        Assert.Contains("fact " + asked, llm.LastUser);
    }

    private sealed class Spy(List<CrewMember> inner, List<int> threads) : IReadOnlyList<CrewMember>
    {
        public CrewMember this[int index] => inner[index];
        public int Count => inner.Count;
        public IEnumerator<CrewMember> GetEnumerator()
        {
            lock (threads)
                threads.Add(Environment.CurrentManagedThreadId);
            return inner.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public async Task The_second_queued_line_is_read_from_the_game_thread()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, new FakeLlm(), main);
        var t1 = new List<int>();
        var t2 = new List<int>();
        var me = Environment.CurrentManagedThreadId;
        mind.SayAtOnce("First.", new Spy([mara], t1));
        mind.SayAtOnce("Second.", new Spy([mara], t2));
        var entry = v.Log.Last(e => e.Kind == LogKind.You);
        // Drained on this thread only, so everything the game does runs here.
        for (var i = 0; i < 300 && entry.Unread; i++)
        {
            main.Drain();
            Thread.Sleep(10);
        }
        Assert.False(entry.Unread);
        await Task.Yield();
        Assert.NotEmpty(t2);
        Assert.All(t1, t => Assert.Equal(me, t));
        Assert.All(t2, t => Assert.Equal(me, t));
    }

    /// <summary>
    /// Validator finding: the timeout counted from Enter, so lines typed together behind a slow one ran out before their
    /// own model call started and became plain talk. It now counts from when each line's call starts.
    /// </summary>
    [Fact]
    public async Task Lines_typed_together_each_get_their_own_read_time()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var main = new MainThreadQueue();
        var llm = new SlowLlm(TimeSpan.FromMilliseconds(150), Dose);
        using var mind = new ShipMind(v, llm, main) { ReadTimeout = TimeSpan.FromMilliseconds(200) };
        var seen = new List<(string Text, bool Failed)>();
        mind.LineRead += (text, _, failed) => seen.Add((text, failed));
        mind.SayAtOnce("a", new List<CrewMember> { mara });
        mind.SayAtOnce("b", new List<CrewMember> { mara });
        mind.SayAtOnce("c", new List<CrewMember> { mara });
        await Until(main, () => seen.Count == 3);
        Assert.Equal(new[] { ("a", false), ("b", false), ("c", false) }, seen);
    }

    /// <summary>Validator finding: a timed-out call kept the worker busy, so the lines behind it timed out too. It is now cancelled.</summary>
    [Fact]
    public async Task A_timed_out_read_is_cancelled_and_frees_the_model_for_the_next_line()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var main = new MainThreadQueue();
        var llm = new FirstHangsLlm(Dose);
        using var mind = new ShipMind(v, llm, main) { ReadTimeout = TimeSpan.FromMilliseconds(150) };
        var seen = new List<(string Text, bool Failed)>();
        mind.LineRead += (text, _, failed) => seen.Add((text, failed));
        mind.SayAtOnce("a", new List<CrewMember> { mara });
        mind.SayAtOnce("b", new List<CrewMember> { mara });
        await Until(main, () => seen.Count == 2);
        Assert.Equal(new[] { ("a", true), ("b", false) }, seen);
        Assert.True(llm.FirstCancelled);
    }

    /// <summary>Validator finding: a held click that threw left the clicks behind it waiting for good.</summary>
    [Fact]
    public async Task A_held_click_that_throws_does_not_stop_the_ones_behind_it()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, new FakeLlm(), main);
        mind.SayAtOnce("Hello.", new List<CrewMember> { mara });
        mind.AfterReading(() => throw new InvalidOperationException("boom"));
        var ran = false;
        mind.AfterReading(() => ran = true);
        Exception? thrown = null;
        for (var i = 0; i < 300 && mind.Holding; i++)
        {
            try { main.Drain(); }
            catch (InvalidOperationException e) { thrown = e; }
            await Task.Delay(10);
        }
        Assert.False(mind.Holding);
        Assert.True(ran);
        Assert.Equal("boom", thrown?.Message); // still reported
    }

    /// <summary>A model that takes a fixed time per reading and stops when cancelled.</summary>
    private sealed class SlowLlm(TimeSpan delay, string answer) : ILlmProvider
    {
        public string Name => "slow (unit tests)";
        public string Backend => "none";
        public event Action<LlmCallStats>? CallCompleted;

        public async Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
            Action<string>? onToken = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delay, cancellationToken);
            CallCompleted?.Invoke(new LlmCallStats(options.Kind, 0, 0, delay, delay));
            return options.Kind == "read" ? answer : "";
        }

        public void Dispose() { }
    }

    /// <summary>A model whose first reading hangs until it is cancelled; the others answer at once.</summary>
    private sealed class FirstHangsLlm(string answer) : ILlmProvider
    {
        private int _calls;
        public bool FirstCancelled { get; private set; }
        public string Name => "first hangs (unit tests)";
        public string Backend => "none";
        public event Action<LlmCallStats>? CallCompleted;

        public async Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
            Action<string>? onToken = null, CancellationToken cancellationToken = default)
        {
            if (options.Kind != "read")
                return "";
            if (Interlocked.Increment(ref _calls) == 1)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    FirstCancelled = true;
                    throw;
                }
            }
            CallCompleted?.Invoke(new LlmCallStats(options.Kind, 0, 0, TimeSpan.Zero, TimeSpan.Zero));
            return answer;
        }

        public void Dispose() { }
    }

    /// <summary>A model whose reading only comes when the test releases it; it remembers the last read prompt.</summary>
    private sealed class HangingLlm : ILlmProvider
    {
        public TaskCompletionSource<string> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string LastUser { get; private set; } = "";
        public string Name => "hanging (unit tests)";
        public string Backend => "none";
        public event Action<LlmCallStats>? CallCompleted;

        public async Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
            Action<string>? onToken = null, CancellationToken cancellationToken = default)
        {
            if (options.Kind != "read")
                return "";
            LastUser = turns[^1].Content;
            var raw = await Release.Task.WaitAsync(cancellationToken);
            CallCompleted?.Invoke(new LlmCallStats(options.Kind, 0, 0, TimeSpan.Zero, TimeSpan.Zero));
            return raw;
        }

        public void Dispose() { }
    }
}
