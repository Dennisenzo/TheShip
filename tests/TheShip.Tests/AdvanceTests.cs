using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

public class AdvanceTests
{
    private static void Say(Voyage v, CrewMember to, string text)
    {
        var hearers = new List<CrewMember> { to };
        v.Say(text, RuleReader.Read(text, v, hearers), hearers);
    }

    /// <summary>Alert 1 played honestly with Mara (as in VoyageTests), ending in the aftermath with her awake.</summary>
    private static CrewMember AftermathWithMara(Voyage v)
    {
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        v.Assign(mara, fault, patch: false);
        Say(v, mara, "It is dangerous. You will take a radiation dose.");
        v.Assign(mara, fault, patch: false);
        for (var i = 0; i < 50 && v.Phase == Phase.Alert; i++)
            v.Go();
        Assert.Equal(Phase.Aftermath, v.Phase);
        return mara;
    }

    [Fact]
    public void It_stops_at_the_three_month_mark_and_never_passes_it_by_more_than_a_step()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        var steps = 0;
        string reason = "";
        for (var i = 0; i < 6 && !reason.Contains("go back to sleep"); i++)
        {
            var r = v.GoUntilAttention();
            Assert.InRange(r.Steps, 1, Tuning.AdvanceMaxSteps);
            steps += r.Steps;
            reason = r.Reason;
        }
        Assert.Contains("go back to sleep", reason);
        Assert.True(mara.HoursAwake >= Tuning.MinAwakeHours);
        Assert.True(mara.HoursAwake < Tuning.MinAwakeHours + Tuning.AftermathStepHours);
        Assert.Contains(v.Log, e => e.Text.Contains("Stopped: Mara can go back to sleep"));
    }

    [Fact]
    public void A_crew_line_stops_it_after_the_step_it_came_in()
    {
        var v = new Voyage(1);
        AftermathWithMara(v);
        var spoken = v.Log.Count(e => e.Kind == LogKind.Crew);
        var r = v.GoUntilAttention();
        if (r.Reason.Contains("something to say"))
            Assert.True(v.Log.Count(e => e.Kind == LogKind.Crew) > spoken);
        else
            Assert.Contains("can go back to sleep", r.Reason);
    }

    [Fact]
    public void A_finished_thaw_stops_it()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        var idris = v.Find("idris")!;
        v.Wake(idris);
        var r = v.GoUntilAttention();
        Assert.Equal(CrewState.Awake, idris.State);
        Assert.Equal(1, r.Steps);
        Assert.Contains("something to say", r.Reason);
        Assert.NotNull(mara);
    }

    [Fact]
    public void Nobody_awake_leaves_the_clock_alone()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        for (var i = 0; i < 20 && mara.HoursAwake < Tuning.MinAwakeHours; i++)
            v.Go();
        v.AskSleep(mara);
        Assert.Equal(CrewState.Asleep, mara.State);
        var clock = v.Clock;
        var r = v.GoUntilAttention();
        Assert.Equal(0, r.Steps);
        Assert.Equal(clock, v.Clock);
    }

    [Fact]
    public void Everyone_awake_being_free_to_sleep_leaves_the_clock_alone()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        for (var i = 0; i < 20 && mara.HoursAwake < Tuning.MinAwakeHours; i++)
            v.Go();
        var clock = v.Clock;
        var r = v.GoUntilAttention();
        Assert.Equal(0, r.Steps);
        Assert.Equal(clock, v.Clock);
        Assert.Contains(v.Log, e => e.Text.StartsWith("Everyone awake can go back to sleep"));
    }

    [Fact]
    public void Stores_too_low_for_everyone_awake_leave_the_clock_alone()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        Assert.True(mara.HoursAwake < Tuning.MinAwakeHours);
        v.Stores = 0;
        Assert.True(v.CanSleepNow(mara));
        var clock = v.Clock;
        var r = v.GoUntilAttention();
        Assert.Equal(0, r.Steps);
        Assert.Equal(clock, v.Clock);
        Assert.Equal(0, mara.HoursHungry);
        Assert.Contains(v.Log, e => e.Text.StartsWith("Everyone awake can go back to sleep"));
    }

    [Fact]
    public void The_early_sleep_announcement_stops_it()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        for (var i = 0; i < 20 && mara.HoursAwake < 70 * 24; i++)
            v.Go();
        Assert.True(mara.HoursAwake < Tuning.MinAwakeHours);
        // 60 person-days feed Mara through her last weeks, but not Tomas through three months once he eats too.
        v.Stores = 60;
        var tomas = v.Find("tomas")!;
        v.Wake(tomas);
        v.Go();
        Assert.Equal(CrewState.Awake, tomas.State);
        tomas.PendingQuestion = null; // an unanswered wake question would be asked again and stop the advance first
        Assert.False(v.CanSleepNow(mara));
        Assert.True(v.CanSleepNow(tomas));

        var r = v.GoUntilAttention();
        Assert.Equal(1, r.Steps);
        Assert.Contains(v.Log, e => e.Text.StartsWith("The stores won't last Tomas three months awake"));
        Assert.Equal("Tomas can go back to sleep early", r.Reason);
    }

    [Fact]
    public void A_lapsed_early_sleep_offer_still_lets_the_three_month_line_come()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        Assert.True(mara.HoursAwake < Tuning.MinAwakeHours);
        // 120 person-days last Mara alone through her three months, but not Mara and Tomas together.
        v.Stores = 120;
        var tomas = v.Find("tomas")!;
        v.Wake(tomas);
        v.Go(); // the thaw
        v.Go(); // a two-week beat: the early offer
        Assert.Equal(CrewState.Awake, tomas.State);
        tomas.PendingQuestion = null; // an unanswered wake question would be asked again and stop the advance first
        Assert.True(v.CanSleepNow(mara));
        Assert.Contains(v.Log, e => e.Text.StartsWith("The stores won't last Mara three months awake"));

        // Tomas goes back: the stores last Mara again, so the early offer lapses.
        v.AskSleep(tomas);
        Assert.Equal(CrewState.Asleep, tomas.State);
        Assert.False(v.CanSleepNow(mara));

        var reason = "";
        for (var i = 0; i < 6 && !reason.Contains("go back to sleep"); i++)
            reason = v.GoUntilAttention().Reason;
        Assert.Equal("Mara can go back to sleep", reason);
        Assert.Contains(v.Log, e => e.Text.StartsWith("Mara has been awake three months"));
        Assert.True(v.CanSleepNow(mara));
    }

    [Fact]
    public void In_the_alert_it_is_the_same_as_go()
    {
        var a = new Voyage(1);
        var b = new Voyage(1);
        a.Wake(a.Find("mara")!);
        b.Wake(b.Find("mara")!);
        a.Go();
        var r = b.GoUntilAttention();
        Assert.Equal(1, r.Steps);
        Assert.Equal(a.Clock, b.Clock);
    }

    [Fact]
    public void A_death_stops_it()
    {
        var v = new Voyage(1);
        var mara = AftermathWithMara(v);
        v.Stores = 0;
        mara.HoursHungry = Tuning.StarveDays * 24 - 1;
        // With the stores empty Mara could sleep at once and the clock would stay put; a running thaw keeps it moving.
        v.Wake(v.Find("idris")!);
        var r = v.GoUntilAttention();
        Assert.False(mara.Alive);
        Assert.Contains("died", r.Reason);
    }
}
