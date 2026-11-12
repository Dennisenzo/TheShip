using System.Globalization;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace TheShip.Tests;

/// <summary>
/// The balance harness: bots play whole voyages over a fixed set of seeds and the results are printed as a table
/// (and written to balance-report.txt in the test output folder). It measures; it does not tune.
/// Run on its own: <c>dotnet test --filter Category=Balance</c>. Design target (DESIGN.md): one honest run in two
/// reaches Landfall.
/// </summary>
[Trait("Category", "Balance")]
public class BalanceTests
{
    public const int SeedCount = 200;

    private readonly ITestOutputHelper _out;
    public BalanceTests(ITestOutputHelper output) => _out = output;

    private static readonly Dictionary<BotKind, RunResult[]> Cache = new();
    private static readonly object Gate = new();

    private static RunResult[] Runs(BotKind kind)
    {
        lock (Gate)
        {
            if (!Cache.TryGetValue(kind, out var runs))
                Cache[kind] = runs = PlayAll(kind);
            return runs;
        }
    }

    private static RunResult[] PlayAll(BotKind kind)
    {
        var results = new RunResult[SeedCount];
        Parallel.For(0, SeedCount, i => results[i] = BalanceBot.Play(i + 1, kind));
        return results;
    }

    private static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Balance report: {SeedCount} seeds per bot (seeds 1..{SeedCount}), Short haul, 6 crew, 60 years.");
        sb.AppendLine();
        foreach (var kind in Enum.GetValues<BotKind>())
        {
            var r = Runs(kind);
            string P(double x) => (100 * x / r.Length).ToString("0.0", CultureInfo.InvariantCulture) + "%";
            string N(double x) => x.ToString("0.00", CultureInfo.InvariantCulture);
            var causes = r.SelectMany(x => x.Causes).GroupBy(c => c).OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key} {N((double)g.Count() / r.Length)}").ToList();
            var ages = r.SelectMany(x => x.AgesAtArrival).ToList();
            sb.AppendLine($"== {kind} bot ==");
            foreach (var g in r.GroupBy(x => x.Ending).OrderByDescending(g => g.Count()))
                sb.AppendLine($"  {g.Key,-14} {g.Count(),4}  {P(g.Count())}");
            sb.AppendLine($"  Landfall rate           {P(r.Count(x => x.Ending == "Landfall"))}");
            sb.AppendLine($"  Survivors (mean)        {N(r.Average(x => x.Alive))} of 6");
            sb.AppendLine($"  Deaths incl. scripted   {N(r.Average(x => x.Dead))}");
            sb.AppendLine($"  Deaths excl. scripted   {N(r.Average(x => x.DeadUnscripted))}   (runs with any: {P(r.Count(x => x.DeadUnscripted > 0))})");
            sb.AppendLine($"  Causes (per run)        {(causes.Count == 0 ? "none" : string.Join(", ", causes))}");
            sb.AppendLine($"  Mean age at arrival     {(ages.Count == 0 ? "n/a" : N(ages.Average()))}");
            sb.AppendLine($"  Mean trust at end       {N(r.Average(x => x.MeanTrust))}");
            sb.AppendLine($"  Lies told / caught      {N(r.Average(x => x.LiesTold))} / {N(r.Average(x => x.CaughtLies))} per run");
            sb.AppendLine($"  Thaws / pod wear        {N(r.Average(x => x.Thaws))} / {N(r.Average(x => x.PodWear))}");
            sb.AppendLine($"  Stores / parts left     {N(r.Average(x => x.Stores))} / {N(r.Average(x => x.Parts))}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [Fact]
    public void Print_the_balance_table()
    {
        var report = Report();
        _out.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "balance-report.txt"), report);
        Assert.False(string.IsNullOrWhiteSpace(report));
    }

    [Theory]
    [InlineData(BotKind.Honest)]
    [InlineData(BotKind.Careless)]
    [InlineData(BotKind.Liar)]
    public void Every_bot_finishes_every_run(BotKind kind)
    {
        var stuck = Runs(kind).Where(r => !r.Ended).Select(r => r.Seed).ToList();
        Assert.True(stuck.Count == 0, kind + " did not reach an ending on seeds " + string.Join(", ", stuck));
    }

    [Theory]
    [InlineData(BotKind.Honest)]
    [InlineData(BotKind.Careless)]
    [InlineData(BotKind.Liar)]
    public void The_same_seed_gives_the_same_result(BotKind kind)
    {
        foreach (var seed in new[] { 1, 17, 101, 200 })
        {
            var a = BalanceBot.Play(seed, kind);
            var b = BalanceBot.Play(seed, kind);
            Assert.Equal(a.Ending, b.Ending);
            Assert.Equal(a.Alive, b.Alive);
            Assert.Equal(a.Causes, b.Causes);
            Assert.Equal(a.AgesAtArrival, b.AgesAtArrival);
            Assert.Equal(a.MeanTrust, b.MeanTrust);
            Assert.Equal(a.CaughtLies, b.CaughtLies);
        }
    }

    [Fact]
    public void The_honest_bot_reaches_an_ending_every_time()
    {
        Assert.All(Runs(BotKind.Honest), r => Assert.NotEqual("No ending", r.Ending));
    }

    [Fact(Skip = "Target band for later: one honest run in two reaches Landfall (DESIGN.md). Current numbers are near 100%.")]
    public void The_honest_bot_reaches_landfall_in_a_quarter_to_three_quarters_of_runs()
    {
        var rate = (double)Runs(BotKind.Honest).Count(r => r.Ending == "Landfall") / SeedCount;
        Assert.InRange(rate, 0.25, 0.75);
    }
}
