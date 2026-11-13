using TheShip.Game;
using Xunit;

namespace TheShip.Tests;

public class LogRevealTests
{
    private static readonly RevealItem Plain = new(false, false, 10);
    private static RevealItem Crew(int n = 90, bool pending = false) => new(true, pending, n);

    [Fact]
    public void Plain_lines_appear_at_once_up_to_a_crew_line()
    {
        var r = new LogReveal();
        r.Update(new[] { Plain, Plain, Crew(), Plain }, 4, 0.01);
        Assert.Equal(2, r.Shown);
        Assert.True(r.HeadVisible);
        Assert.Equal(3, r.Visible);
    }

    [Fact]
    public void A_pending_crew_line_holds_every_later_line_back()
    {
        var r = new LogReveal();
        var items = new[] { Crew(pending: true), Plain, Plain };
        r.Update(items, 3, 5);
        Assert.Equal(0, r.Shown);
        Assert.Equal(1, r.Visible);
        Assert.Equal(0, r.Chars);
        Assert.True(r.Draining);
        items[0] = Crew(90);
        r.Update(items, 3, 0.1);
        Assert.Equal(1, r.Visible);
        Assert.InRange(r.Chars, 1, 89);
        r.Update(items, 3, 1.0);
        Assert.Equal(3, r.Shown);
        Assert.False(r.Draining);
    }

    [Fact]
    public void Typing_takes_time_and_a_skip_finishes_up_to_the_pending_line()
    {
        var r = new LogReveal();
        var items = new[] { Crew(900), Plain, Crew(pending: true), Plain };
        r.Update(items, 4, 0.1);
        Assert.Equal(0, r.Shown);
        r.Update(items, 4, 0.0, skip: true);
        Assert.Equal(2, r.Shown);
        Assert.Equal(3, r.Visible);
        Assert.True(r.Draining);
    }

    [Fact]
    public void A_long_backlog_is_shown_at_once()
    {
        var r = new LogReveal();
        var items = Enumerable.Range(0, 10).Select(_ => Crew(500)).ToArray();
        r.Update(items, 10, 0.0);
        Assert.Equal(4, r.Shown);
        Assert.False(r.Draining && r.Shown < 4);
    }

    [Fact]
    public void The_limit_holds_lines_back_and_instant_shows_all()
    {
        var r = new LogReveal();
        var items = new[] { Plain, Crew(), Plain };
        r.Update(items, 1, 10);
        Assert.Equal(1, r.Visible);
        r.Update(items, 3, 0, instant: true);
        Assert.Equal(3, r.Shown);
        Assert.False(r.Draining);
    }
}
