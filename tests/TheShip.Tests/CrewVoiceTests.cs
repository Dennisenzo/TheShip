using Xunit;
using TheShip.AI;
using TheShip.Core;

namespace TheShip.Tests;

public class CrewVoiceTests
{
    [Xunit.FactAttribute]
    public void Every_crew_member_has_a_distinct_sheet_and_the_partners_know_each_other()
    {
        var crew = Scenario.Crew();
        Assert.All(crew, c => Assert.NotNull(c.Sheet));
        Assert.Equal(crew.Count, crew.Select(c => c.Sheet!.Voice).Distinct().Count());
        Assert.Contains("partner", crew.First(c => c.Id == "mara").Sheet!.Bonds["idris"]);
        Assert.Contains("partner", crew.First(c => c.Id == "idris").Sheet!.Bonds["mara"]);
    }

    [Xunit.FactAttribute]
    public void The_crew_prompt_has_the_sheet_the_bonds_and_the_recent_lines()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var system = Prompts.CrewSystem(mara, v);
        Assert.Contains(mara.Sheet!.Voice, system);
        Assert.Contains(mara.Sheet.Background, system);
        Assert.Contains("Idris is your partner", system);
        Assert.Contains("speaking aloud to the ship", system);

        v.Log.Add(new LogEntry(LogKind.You, "YOU", "Stay calm, I am here.") { Hearers = ["mara"] });
        v.Log.Add(new LogEntry(LogKind.Crew, "MARA", "Understood.") { CrewId = "mara" });
        v.Log.Add(new LogEntry(LogKind.Crew, "LIN", "Not mine to hear.") { CrewId = "lin" });
        var user = Prompts.CrewUser(new CrewIntent("comply", "You agree to take on the coolant leak."), mara, v);
        Assert.Contains("The ship: Stay calm, I am here.", user);
        Assert.Contains("You: Understood.", user);
        Assert.DoesNotContain("Not mine to hear", user);
        Assert.Contains("You agree to take on the coolant leak.", user);
        Assert.Contains("You feel:", user);
    }

    [Xunit.FactAttribute]
    public void Only_the_last_few_recent_lines_are_carried_and_each_is_cut_short()
    {
        var v = new Voyage(1);
        for (var i = 0; i < 12; i++)
            v.Log.Add(new LogEntry(LogKind.You, "YOU", "line" + i + " " + new string('x', 300)) { Hearers = ["mara"] });
        var user = Prompts.CrewUser(new CrewIntent("small", "x"), v.Find("mara")!, v);
        Assert.Equal(Prompts.RecentLines, user.Split("The ship:").Length - 1);
        Assert.Contains("line11", user);
        Assert.DoesNotContain("line6 ", user);
        Assert.True(user.Length < 1200);
    }

    [Xunit.FactAttribute]
    public void A_longer_line_inside_the_new_limit_is_accepted_and_an_unknown_fact_is_not()
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        var none = new Dictionary<string, Belief>();
        var longLine = "Right. Thirty years of fixing other people's leaks and now it is mine, and I will not pretend I like it. "
                       + "Idris would laugh at me for talking to a wall of pipes, but somebody has to. Give me a minute with the valve and I will tell you exactly what I find, good or bad.";
        Assert.InRange(longLine.Length, 250, Prompts.CrewLineMaxChars);
        Assert.True(mind.Acceptable(longLine, none, "You agree to take on the coolant leak."));
        Assert.False(mind.Acceptable(longLine + " Anyway, Idris is dead.", none, "You agree."));
        Assert.False(mind.Acceptable(new string('a', Prompts.CrewLineMaxChars + 1), none, "x"));
    }

    [Xunit.FactAttribute]
    public async Task A_voiced_crew_line_reaches_the_log_with_the_prompt_the_game_built()
    {
        var v = new Voyage(1);
        var llm = new FakeLlm { Reply = "Fine. I will see to it, though I like none of it." };
        using var mind = new ShipMind(v, llm, new MainThreadQueue { Immediate = true });
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Assert.Contains(v.Log, e => e.Kind == LogKind.Crew);
        for (var i = 0; i < 100 && llm.LastReplySystem is null; i++)
            await Task.Delay(20);
        Assert.NotNull(llm.LastReplySystem);
        Assert.Contains(mara.Sheet!.Voice, llm.LastReplySystem);
        Assert.Contains("What happens now:", llm.LastReplyTurns![0].Content);
        for (var i = 0; i < 100 && !v.Log.Any(e => e.Text == llm.Reply); i++)
            await Task.Delay(20);
        Assert.Contains(v.Log, e => e.Text == llm.Reply);
    }

    [Xunit.FactAttribute]
    public void A_player_line_reaches_only_the_prompts_of_those_who_heard_it()
    {
        var v = new Voyage(1);
        var lin = v.Find("lin")!;
        var mara = v.Find("mara")!;
        // Said to Lin and Mara while Mara is still in her pod: only Lin hears it.
        v.Wake(lin);
        v.Go();
        v.Say("The pumps are holding.", new Reading(LineKind.Plain), [lin, mara]);
        v.Wake(mara);
        v.Go();
        Assert.Equal(CrewState.Awake, mara.State);
        // Said to Lin alone with both awake.
        v.Say("Idris is dead.", new Reading(LineKind.Plain), [lin]);
        var intent = new CrewIntent("comply", "You agree to take on the coolant leak.");
        var maraUser = Prompts.CrewUser(intent, mara, v);
        Assert.DoesNotContain("Idris is dead", maraUser);
        Assert.DoesNotContain("The pumps are holding", maraUser);
        var linUser = Prompts.CrewUser(intent, lin, v);
        Assert.Contains("The ship: Idris is dead.", linUser);
        Assert.Contains("The ship: The pumps are holding.", linUser);
        Assert.Equal(["lin"], v.Log.Last(e => e.Kind == LogKind.You).Hearers);
    }

    [Xunit.TheoryAttribute]
    [Xunit.InlineDataAttribute(75, true)]
    [Xunit.InlineDataAttribute(45, true)]
    [Xunit.InlineDataAttribute(20, false)]
    [Xunit.InlineDataAttribute(5, false)]
    public void The_tone_toward_the_ship_follows_the_trust_band(double trust, bool civil)
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        mara.Trust = trust;
        var system = Prompts.CrewSystem(mara, v);
        var user = Prompts.CrewUser(new CrewIntent("work_failed", "The repair failed."), mara, v);
        Assert.Contains("Your tone toward the ship:", user);
        if (civil)
        {
            Assert.Contains(Prompts.CivilTone, system);
            Assert.Contains("insults", user);
        }
        else
        {
            Assert.DoesNotContain(Prompts.CivilTone, system);
            Assert.DoesNotContain("insults", user);
        }
        Assert.DoesNotContain("swears mildly,", mara.Sheet!.Voice);
        Assert.Contains("not at whoever she is talking to", mara.Sheet.Voice);
    }

    [Xunit.FactAttribute]
    public void A_dodge_puzzles_a_trusting_person_and_angers_a_hostile_one_and_no_parts_is_tense()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var dodge = new CrewIntent("dodge", "The ship did not answer your question.");
        mara.Trust = 75;
        Assert.Contains("puzzled", Prompts.CrewUser(dodge, mara, v));
        mara.Trust = 45;
        Assert.DoesNotContain("angry", Prompts.CrewUser(dodge, mara, v));
        mara.Trust = 5;
        Assert.Contains("angry", Prompts.CrewUser(dodge, mara, v));
        Assert.Contains("You feel: tense and wary", Prompts.CrewUser(new CrewIntent("work_no_parts", "No parts."), mara, v));
    }

    [Xunit.FactAttribute]
    public void A_bond_turns_past_tense_only_once_the_speaker_believes_the_person_is_dead()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var lin = v.Find("lin")!;
        Assert.Contains("Idris is your partner", Prompts.CrewSystem(mara, v));
        mara.Beliefs["alive:idris"] = new Belief("dead", "ship", 5);
        var system = Prompts.CrewSystem(mara, v);
        Assert.DoesNotContain("Idris is your partner", system);
        Assert.Contains("Idris was your partner. You know Idris is dead.", system);
        Assert.Contains("Jonah is a green kid", system);
        lin.Beliefs["alive:idris"] = new Belief("dead", "witness", 5);
        Assert.Contains("Idris was the one person who was simply kind to you.", Prompts.CrewSystem(lin, v));
        // Believing he is alive (whatever the truth) keeps the bond as it is.
        mara.Beliefs["alive:idris"] = new Belief("alive", "ship", 6);
        Assert.Contains("Idris is your partner", Prompts.CrewSystem(mara, v));
    }
}
