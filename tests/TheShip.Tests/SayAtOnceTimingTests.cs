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
        v.Assign(mara, f, false); // "Is it safe in there?"
        Assert.NotNull(mara.PendingQuestion);
        var main = new MainThreadQueue();
        using var mind = new ShipMind(v, new FakeLlm { Read = Dose }, main);
        mind.SayAtOnce("It is dangerous. You will take a radiation dose.", new List<CrewMember> { mara });
        mind.AfterReading(() => v.Assign(mara, f, false));
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
        v.Assign(mara, f, false);
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
