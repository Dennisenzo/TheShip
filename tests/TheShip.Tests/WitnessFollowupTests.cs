using TheShip.AI;
using TheShip.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheShip.Tests;

/// <summary>Follow-ups to the death witnesses: who may speak after a step that kills, dodges that hid nothing, the witnessed line.</summary>
public class WitnessFollowupTests
{
    /// <summary>Mara, Jonah and Asha awake in alert 1 with the stores empty; Jonah starves an hour before Mara, Asha is fed for days.</summary>
    private static (Voyage v, CrewMember mara, CrewMember jonah, CrewMember asha) TwoStarveTogether()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var jonah = v.Find("jonah")!;
        var asha = v.Find("asha")!;
        v.Wake(mara);
        v.Wake(jonah);
        v.Wake(asha);
        v.Go();
        Assert.All(new[] { mara, jonah, asha }, c => Assert.Equal(CrewState.Awake, c.State));
        v.Stores = 0;
        jonah.HoursHungry = Tuning.StarveDays * 24 - 0.5;
        mara.HoursHungry = Tuning.StarveDays * 24 - 1;
        return (v, mara, jonah, asha);
    }

    [Fact]
    public void Two_starving_in_one_step_say_nothing_about_each_other_and_a_survivor_says_she_saw_it()
    {
        var (v, mara, jonah, asha) = TwoStarveTogether();
        var from = v.Log.Count;
        for (var i = 0; i < 20 && jonah.Alive && v.Phase != Phase.Ended; i++)
            v.Go();
        // Both died in the same step: Mara's sight of Jonah dying is not said.
        Assert.False(jonah.Alive);
        Assert.False(mara.Alive);
        var lines = v.Log.Skip(from).Where(l => l.Kind == LogKind.Crew).ToList();
        Assert.DoesNotContain(lines, l => l.CrewId is "mara" or "jonah");
        // Asha lived: she saw both and says so, in the witnessed line.
        Assert.True(asha.Alive);
        Assert.Equal(2, lines.Count(l => l.CrewId == "asha" && l.Intent!.Kind == "witnessed"));
        Assert.DoesNotContain(lines, l => l.CrewId == "asha" && l.Intent!.Kind == "found");
        Assert.True(mara.Beliefs["alive:jonah"].Firsthand);
    }

    [Fact]
    public void The_witnessed_line_names_the_dead_and_says_it_was_seen()
    {
        var (v, _, jonah, asha) = TwoStarveTogether();
        for (var i = 0; i < 20 && jonah.Alive && v.Phase != Phase.Ended; i++)
            v.Go();
        var line = v.Log.First(l => l.CrewId == "asha" && l.Intent?.Kind == "witnessed" && l.Intent.Decision.Contains("Jonah"));
        Assert.StartsWith("You saw it happen in front of you", line.Intent!.Decision);
        Assert.Contains("Jonah is gone.", line.Text);
    }

    [Fact]
    public void The_dead_never_speak()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        mara.State = CrewState.Dead;
        var count = v.Log.Count;
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        v.SetLog("stores", LogAccess.Altered, "short");
        Assert.DoesNotContain(v.Log.Skip(count), l => l.CrewId == "mara");
    }

    /// <summary>Two refusers awake through the last drift with no stores: Tomas starves first, Mara a moment after.</summary>
    [Fact]
    public void Drift_starvers_take_each_others_deaths_in_silently_and_no_line_comes_before_the_report()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.RefuserAtTheLastAlert(v);
        var tomas = v.Find("tomas")!;
        tomas.State = CrewState.Awake;
        tomas.AskedToSleep = true;
        v.Stores = 0;
        tomas.HoursHungry = Tuning.StarveDays * 24 - 1;
        mara.HoursHungry = Tuning.StarveDays * 24 - 2;
        var from = v.Log.Count;
        v.Drift();
        Assert.False(tomas.Alive);
        Assert.False(mara.Alive);
        Assert.Equal("dead", mara.Beliefs["alive:tomas"].Value);
        var report = v.Log.FindIndex(from, l => l.Text.StartsWith("DRIFT:"));
        Assert.True(report >= 0);
        Assert.DoesNotContain(v.Log.Skip(from), l => l.Kind == LogKind.Crew && l.CrewId is "mara" or "tomas");
        Assert.DoesNotContain(v.Log.Skip(from).Take(report - from), l => l.Kind == LogKind.Crew);
    }

    /// <summary>Mara has asked after Idris; the ship dodges it. <paramref name="deadAtDodge"/>: Idris was already dead then.</summary>
    private static (Voyage v, CrewMember mara) DodgedIdris(bool deadAtDodge)
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        mara.PendingQuestion = "alive:idris";
        var fact = v.FactList.First(f => f.Key == "alive:idris");
        if (deadAtDodge)
            fact.Truth = "dead";
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        Assert.Contains("alive:idris", mara.Dodged.Keys);
        fact.Truth = "dead";
        return (v, mara);
    }

    [Fact]
    public void A_dodge_that_hid_nothing_is_not_charged_again_when_the_death_is_seen()
    {
        var (v, mara) = DodgedIdris(deadAtDodge: false);
        var trust = mara.Trust;
        v.Learn(mara, "alive:idris", "dead", "evidence");
        Assert.Equal(trust, mara.Trust, 6);
    }

    [Fact]
    public void A_dodge_that_hid_a_death_is_charged_when_it_comes_out()
    {
        var (v, mara) = DodgedIdris(deadAtDodge: true);
        var trust = mara.Trust;
        v.Learn(mara, "alive:idris", "dead", "evidence");
        Assert.Equal(Math.Max(0, trust - Tuning.Dodge), mara.Trust, 6);
    }

    /// <summary>A model whose first crew line call waits for a gate, so the next line is still queued when its speaker dies.</summary>
    private sealed class GatedLlm : ILlmProvider
    {
        public readonly TaskCompletionSource Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Replies;
        public string Name => "gated (unit tests)";
        public string Backend => "none";
        public event Action<LlmCallStats>? CallCompleted;

        public async Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
            Action<string>? onToken = null, CancellationToken cancellationToken = default)
        {
            if (options.Kind == "reply" && Interlocked.Increment(ref Replies) == 1)
            {
                Entered.TrySetResult();
                await Gate.Task;
            }
            CallCompleted?.Invoke(new LlmCallStats(options.Kind, 0, 0, TimeSpan.Zero, TimeSpan.Zero));
            return "All right, but tell me how Idris is.";
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task A_line_whose_speaker_died_while_it_waited_is_not_voiced()
    {
        var v = new Voyage(1);
        var llm = new GatedLlm();
        using var mind = new ShipMind(v, llm, new MainThreadQueue { Immediate = true });
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var first = v.Log.First(l => l.Kind == LogKind.Crew);
        await llm.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // A second line from Mara queues behind the first; she dies before it is worded.
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        mara.PendingQuestion = "alive:idris";
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        var second = v.Log.Last(l => l.Kind == LogKind.Crew && l.CrewId == "mara");
        Assert.NotSame(first, second);
        var template = second.Text;
        mara.State = CrewState.Dead;
        llm.Gate.SetResult();
        for (var i = 0; i < 100 && second.Pending; i++)
            await Task.Delay(20);
        Assert.False(second.Pending);
        Assert.Equal(template, second.Text);
        Assert.Equal(1, Volatile.Read(ref llm.Replies));
    }
}
