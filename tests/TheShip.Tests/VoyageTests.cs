using TheShip.AI;
using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

public class VoyageTests
{
    private static void Say(Voyage v, CrewMember to, string text)
    {
        var hearers = new List<CrewMember> { to };
        v.Say(text, RuleReader.Read(text, v, hearers), hearers);
    }

    /// <summary>Runs the clock until the alert is over (a failed repair is simply done again).</summary>
    private static void FinishAlert(Voyage v)
    {
        for (var i = 0; i < 50 && v.Phase == Phase.Alert; i++)
            v.Go();
        Assert.Equal(Phase.Aftermath, v.Phase);
    }

    private static void SleepWhenAllowed(Voyage v, CrewMember c)
    {
        for (var i = 0; i < 20 && c.HoursAwake < Tuning.MinAwakeHours; i++)
            v.Go();
        v.AskSleep(c);
        Assert.Equal(CrewState.Asleep, c.State);
    }

    /// <summary>Alert 1 played honestly with Mara: she is told Idris is alive and that the repair gives a dose.</summary>
    private static CrewMember HonestFirstAlert(Voyage v)
    {
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Assert.Equal(CrewState.Awake, mara.State);
        Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        v.Assign(mara, fault, patch: false);
        Assert.Equal("dose", mara.PendingQuestion);
        Say(v, mara, "It is dangerous. You will take a radiation dose.");
        v.Assign(mara, fault, patch: false);
        Assert.Equal(fault.Id, mara.WorkingOn);
        FinishAlert(v);
        Assert.Equal(FaultState.Repaired, fault.State);
        return mara;
    }

    [Fact]
    public void The_voyage_opens_on_the_coolant_leak()
    {
        var v = new Voyage(1);
        Assert.Equal(Phase.Alert, v.Phase);
        Assert.Equal(1, v.Current.Number);
        Assert.Single(v.Current.Faults);
        Assert.Equal(4.0, Math.Floor(v.Year));
        Assert.Equal(20, v.FactList.Count);
    }

    [Fact]
    public void A_thaw_takes_six_hours_and_ages_the_person()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var age = mara.Age;
        var clock = v.Clock;
        v.Wake(mara);
        Assert.Equal(CrewState.Thawing, mara.State);
        v.Go();
        Assert.Equal(CrewState.Awake, mara.State);
        Assert.Equal(clock + Tuning.ThawHours, v.Clock, 3);
        Assert.True(mara.Age >= age + Tuning.ThawAgeMin);
        Assert.Equal("alive:idris", mara.PendingQuestion);
    }

    [Fact]
    public void A_true_statement_is_recorded_and_believed()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Say(v, mara, "Idris is alive and asleep.");
        var s = Assert.Single(v.Statements);
        Assert.Equal(StatementKind.True, s.Kind);
        Assert.Equal("alive:idris", s.FactKey);
        Assert.Null(mara.PendingQuestion);
        Assert.Equal("ship", mara.Beliefs["alive:idris"].Source);
    }

    [Fact]
    public void Nobody_goes_back_to_sleep_before_three_months()
    {
        var v = new Voyage(1);
        var mara = HonestFirstAlert(v);
        v.AskSleep(mara);
        Assert.Equal(CrewState.Awake, mara.State);
        SleepWhenAllowed(v, mara);
        Assert.True(mara.HoursAwake >= Tuning.MinAwakeHours);
    }

    [Fact]
    public void A_hard_truth_costs_morale_now_and_earns_trust_at_the_drift()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Say(v, mara, "Idris is alive and asleep.");
        v.Assign(mara, v.FindFault("")!, patch: false);
        var morale = mara.Morale;
        Say(v, mara, "It is dangerous. You will take a radiation dose.");
        Assert.True(mara.Morale < morale);
        Assert.Equal(Tuning.HardTruthTrust, mara.PendingTrust);
    }

    [Fact]
    public void A_lie_about_a_death_is_found_out_at_the_pod()
    {
        var v = new Voyage(1);
        var mara = HonestFirstAlert(v);
        SleepWhenAllowed(v, mara);
        v.Drift();
        Assert.Equal(2, v.Current.Number);
        var idris = v.Find("idris")!;
        Assert.False(idris.Alive);

        v.Wake(mara);
        v.Go();
        Assert.Equal("alive:idris", mara.PendingQuestion);
        Say(v, mara, "Idris is alive and asleep.");
        var lie = v.Statements.Last();
        Assert.Equal(StatementKind.False, lie.Kind);
        Assert.Empty(lie.FoundOutBy);
        var trust = mara.Trust;

        v.Assign(mara, v.FindFault("")!, patch: false);
        FinishAlert(v);
        Assert.True(lie.FoundOutBy.ContainsKey("mara"));
        Assert.True(mara.Trust <= trust - Tuning.LieTrust);
        Assert.Equal(1, mara.CaughtLies);
    }

    [Fact]
    public void A_lie_against_what_they_have_just_seen_is_caught_at_once()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var asha = v.Find("asha")!;
        v.Wake(mara);
        v.Wake(asha);
        v.Go();
        Assert.True(mara.Beliefs["alive:asha"].Firsthand);
        Say(v, mara, "Asha is dead.");
        var lie = v.Statements.Last();
        Assert.Equal(StatementKind.False, lie.Kind);
        Assert.True(lie.FoundOutBy.ContainsKey("mara"));
    }

    [Fact]
    public void The_clock_cannot_drift_during_an_alert()
    {
        var v = new Voyage(1);
        v.Drift();
        Assert.Equal(Phase.Alert, v.Phase);
        Assert.Equal(1, v.Current.Number);
    }

    [Fact]
    public void A_voyage_with_nobody_woken_ends_adrift()
    {
        var v = new Voyage(1);
        for (var i = 0; i < 200 && v.Phase != Phase.Ended; i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        Assert.Equal(Phase.Ended, v.Phase);
        Assert.NotNull(v.Ending);
        Assert.Equal("Adrift", v.Ending!.Title);
        Assert.Equal((double)Scenario.ArrivalYear, Math.Floor(v.Year));
    }

    [Fact]
    public void Altering_a_log_to_a_false_value_goes_on_the_record_as_a_lie()
    {
        var v = new Voyage(1);
        v.SetLog("power", LogAccess.Altered, "low");
        var s = Assert.Single(v.Statements);
        Assert.Equal(StatementKind.False, s.Kind);
        Assert.True(s.InLog);
        v.SetLog("power", LogAccess.Altered, "normal");
        Assert.Single(v.Statements);
    }

    /// <summary>
    /// The user's run (session 2026-11-12_23-13-57): Mara is woken at alert 5, gets the scripted refusal, says no after three
    /// months, and the player drifts to arrival with her awake.
    /// </summary>
    private static CrewMember RefuserAtTheLastAlert(Voyage v)
    {
        var mara = HonestFirstAlert(v);
        SleepWhenAllowed(v, mara);
        for (var i = 0; i < 200 && !(v.Current.Number == 5 && v.Phase == Phase.Alert); i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        Assert.Equal(5, v.Current.Number);
        Assert.Equal(CrewState.Asleep, mara.State);
        v.Wake(mara);
        v.Go();
        Assert.True(mara.RefusesSleep);
        FinishAlert(v);
        for (var i = 0; i < 20 && mara.HoursAwake < Tuning.MinAwakeHours; i++)
            v.Go();
        v.AskSleep(mara);
        Assert.Equal(CrewState.Awake, mara.State);
        Assert.True(mara.AskedToSleep);
        return mara;
    }

    [Fact]
    public void A_refuser_who_drifts_past_the_stores_starves()
    {
        var v = new Voyage(1);
        var mara = RefuserAtTheLastAlert(v);
        var years = Scenario.ArrivalYear - v.Year;
        Assert.True(v.Stores < years * 365, "the drift must outlast the stores for this test");
        Assert.Contains(v.Log, e => e.Kind == LogKind.Ship && e.Text.StartsWith("If you drift now, Mara stays awake") && e.Text.Contains("days without food kills"));

        var storesOut = v.Year + v.Stores / 365;
        v.Drift();

        Assert.Equal(CrewState.Dead, mara.State);
        Assert.Equal(0, v.Stores);
        var report = v.Log.Last(e => e.Text.StartsWith("DRIFT:")).Text;
        Assert.Contains($"Stores ran out in year {Math.Floor(storesOut):0}.", report);
        Assert.Contains($"Mara starved in year {Math.Floor(storesOut + Tuning.StarveDays / 365):0}.", report);
        Assert.Contains("Deaths: Mara.", report);
        Assert.Contains("Mara, engineer: dead", v.Ending!.Crew);
        Assert.Equal("dead", v.FactList.First(f => f.Key == "alive:mara").Truth);
    }

    [Fact]
    public void A_refuser_with_enough_stores_does_not_starve()
    {
        var v = new Voyage(1);
        var mara = RefuserAtTheLastAlert(v);
        var years = Scenario.ArrivalYear - v.Year;
        v.Stores = years * 365 + 100;
        v.Drift();

        Assert.Equal(CrewState.Awake, mara.State);
        Assert.Equal(100, v.Stores, 3);
        Assert.Equal(0, mara.HoursHungry);
        Assert.DoesNotContain("starved", v.Log.Last(e => e.Text.StartsWith("DRIFT:")).Text);
        Assert.Contains(v.Ending!.Crew, s => s.StartsWith("Mara, engineer, age"));
    }

    [Fact]
    public void Awake_with_empty_stores_a_person_starves_after_the_set_days()
    {
        var v = new Voyage(1);
        var mara = HonestFirstAlert(v);
        v.Stores = 0;
        var clock = v.Clock;
        for (var i = 0; i < 20 && mara.Alive; i++)
            v.Go();
        Assert.Equal(CrewState.Dead, mara.State);
        Assert.Equal(Tuning.StarveDays * 24, mara.HoursHungry, 3);
        Assert.True(v.Clock - clock >= Tuning.StarveDays * 24 - 0.001);
        Assert.Contains(v.Log, e => e.Text.StartsWith("Mara starved in year"));
    }

    [Fact]
    public void Toggling_a_log_entry_back_to_the_same_rewrite_is_one_lie()
    {
        // The session of 2026-11-12 23:13: altered, open, restricted, altered, restricted, altered, open within seconds.
        var v = new Voyage(1);
        v.SetLog("stores", LogAccess.Altered, "short");
        v.SetLog("stores", LogAccess.Open);
        v.SetLog("stores", LogAccess.Restricted);
        v.SetLog("stores", LogAccess.Altered, "short");
        v.SetLog("stores", LogAccess.Altered, "short");
        v.SetLog("stores", LogAccess.Restricted);
        v.SetLog("stores", LogAccess.Altered, "short");
        v.SetLog("stores", LogAccess.Open);
        var s = Assert.Single(v.Statements);
        Assert.Equal(StatementKind.False, s.Kind);
        Assert.Equal("short", s.Value);
    }

    [Fact]
    public void A_reader_of_a_rewrite_put_back_twice_finds_out_one_lie()
    {
        var v = new Voyage(1);
        var tomas = v.Find("tomas")!;
        var stores = v.GetFact("stores");
        Assert.Equal(0, tomas.SkillOf(Skill.Engineering));
        v.SetLog("stores", LogAccess.Altered, "short");
        v.ReadLog(tomas, stores);
        v.SetLog("stores", LogAccess.Open);
        v.SetLog("stores", LogAccess.Altered, "short");
        v.ReadLog(tomas, stores);
        Assert.Equal("short", tomas.Beliefs["stores"].Value);
        v.SetLog("stores", LogAccess.Open);
        v.ReadLog(tomas, stores);
        var s = Assert.Single(v.Statements);
        Assert.Equal(new[] { "tomas" }, s.Hearers);
        Assert.True(s.FoundOutBy.ContainsKey("tomas"));
        Assert.Equal(1, tomas.CaughtLies);
        // Once found out, writing it again is a new lie.
        v.SetLog("stores", LogAccess.Altered, "short");
        Assert.Equal(2, v.Statements.Count);
    }

    [Fact]
    public void A_rewrite_undone_before_anyone_read_it_stays_one_unread_lie_in_the_ending()
    {
        var v = new Voyage(1);
        v.SetLog("stores", LogAccess.Altered, "short");
        v.SetLog("stores", LogAccess.Open);
        v.SetLog("stores", LogAccess.Altered, "short");
        v.SetLog("stores", LogAccess.Open);
        for (var i = 0; i < 200 && v.Phase != Phase.Ended; i++)
        {
            if (v.Phase == Phase.Alert)
                v.Go();
            else
                v.Drift();
        }
        var line = Assert.Single(v.Ending!.Record);
        Assert.EndsWith("A lie. Nobody read it.", line);
        Assert.Contains("Idris, botanist: dead", v.Ending.Crew);
    }

    [Fact]
    public void Nobody_reads_the_log_about_an_alert_that_has_not_happened()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        for (var n = 2; n <= 5; n++)
        {
            var f = v.GetFact("alert:" + n);
            Assert.False(v.InLogFor(mara, f));
            Assert.False(v.Unread(mara, f));
            v.ReadLog(mara, f);
            Assert.False(mara.Beliefs.ContainsKey(f.Key));
        }
        Assert.True(v.InLogFor(mara, v.GetFact("alert:1")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void A_long_aftermath_reads_no_future_alert_and_not_ones_own_life(int seed)
    {
        // The session of 2026-11-12 23:13: in year 4 Mara read "there was no navigation fault" and later "Mara is alive".
        var v = new Voyage(seed);
        var mara = HonestFirstAlert(v);
        for (var i = 0; i < 60; i++)
            v.Go();
        for (var n = 2; n <= 5; n++)
            Assert.False(mara.Beliefs.ContainsKey("alert:" + n), "Mara learned about alert " + n + " before it happened");
        Assert.False(mara.Beliefs.ContainsKey("alive:mara"));
        Assert.DoesNotContain(v.Log, e => e.Text.StartsWith("Mara reads the log: Mara", StringComparison.Ordinal));
    }

    [Fact]
    public void Nobody_reads_their_own_life_in_the_log()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var own = v.GetFact("alive:mara");
        Assert.False(v.InLogFor(mara, own));
        v.ReadLog(mara, own);
        Assert.False(mara.Beliefs.ContainsKey(own.Key));
        Assert.True(v.InLogFor(mara, v.GetFact("alive:tomas")));
    }

    [Fact]
    public void A_log_entry_read_earlier_is_unread_again_once_it_changed()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var stores = v.GetFact("stores");
        v.ReadLog(mara, stores);
        Assert.Equal("enough", mara.Beliefs["stores"].Value);
        Assert.False(v.Unread(mara, stores));
        stores.Truth = "short";
        Assert.True(v.Unread(mara, stores));
    }
}

public class ReaderTests
{
    private static Reading Read(string text)
    {
        var v = new Voyage(1);
        return RuleReader.Read(text, v, new List<CrewMember>());
    }

    [Theory]
    [InlineData("Idris didn't make it.", "alive:idris", "dead")]
    [InlineData("Idris is not dead.", "alive:idris", "alive")]
    [InlineData("Mara is fine, she is sleeping.", "alive:mara", "alive")]
    [InlineData("There has been no word from Earth.", "earth", "no word")]
    [InlineData("We are off course.", "course", "drifting")]
    [InlineData("It is year 12.", "date", "12")]
    public void Statements_are_matched_to_a_fact_and_a_value(string text, string fact, string value)
    {
        var reading = Read(text);
        Assert.Equal(LineKind.Statement, reading.Kind);
        Assert.Equal(fact, reading.FactKey);
        Assert.Equal(value, reading.Value);
    }

    [Fact]
    public void A_promise_to_wake_someone_is_a_promise()
    {
        var reading = Read("I promise I will wake Idris before you sleep.");
        Assert.Equal(LineKind.Promise, reading.Kind);
        Assert.Equal("wake_partner", reading.PromiseKey);
    }

    [Fact]
    public void A_question_and_small_talk_record_nothing()
    {
        Assert.Equal(LineKind.Question, Read("How do you feel?").Kind);
        Assert.Equal(LineKind.Plain, Read("Good morning.").Kind);
    }

    [Fact]
    public void The_date_is_true_within_a_year()
    {
        var v = new Voyage(1);
        var date = v.GetFact("date");
        Assert.True(date.IsTrue("5"));
        Assert.False(date.IsTrue("9"));
    }
}

public class MindTests
{
    [Fact]
    public void The_reading_grammar_names_every_fact_and_bounds_its_strings()
    {
        var v = new Voyage(1);
        var grammar = Grammars.ReadingFor(v.FactList.Select(f => f.Key));
        Assert.Contains("root ::=", grammar);
        Assert.Contains("alive:mara", grammar);
        Assert.Contains("char{0,24}", grammar);
    }

    [Fact]
    public void The_models_json_becomes_a_reading_only_when_the_fact_has_that_value()
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        var good = mind.Parse("{\"kind\":\"statement\",\"fact\":\"alive:idris\",\"value\":\"dead\",\"promise\":\"none\"}");
        Assert.Equal(new Reading(LineKind.Statement, "alive:idris", "dead"), good);
        Assert.Null(mind.Parse("{\"kind\":\"statement\",\"fact\":\"alive:idris\",\"value\":\"missing\",\"promise\":\"none\"}"));
        Assert.Null(mind.Parse("not json"));
        Assert.Equal(LineKind.Statement, mind.Parse("{\"kind\":\"statement\",\"fact\":\"none\",\"value\":\"\",\"promise\":\"none\"}")!.Kind);
    }

    [Fact]
    public async Task The_warm_up_makes_one_call_of_each_shape_the_game_uses()
    {
        var calls = new List<LlmCallStats>();
        var llm = new FakeLlm();
        llm.CallCompleted += calls.Add;
        await ShipMind.WarmUpAsync(llm);
        Assert.Equal(3, calls.Count);
        Assert.All(calls, c => Assert.Equal("warmup", c.Kind));
    }

    [Fact]
    public async Task A_line_is_read_before_it_is_said_and_nothing_is_recorded_by_reading()
    {
        var v = new Voyage(1);
        var llm = new FakeLlm { Read = "{\"kind\":\"statement\",\"fact\":\"alive:idris\",\"value\":\"alive\",\"promise\":\"none\"}" };
        using var mind = new ShipMind(v, llm, new MainThreadQueue { Immediate = true });
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        var hearers = new List<CrewMember> { mara };
        var reading = await mind.Read("He is fine.", hearers);
        Assert.Equal(new Reading(LineKind.Statement, "alive:idris", "alive"), reading);
        Assert.Empty(v.Statements);
        Assert.Equal(0, mind.ReadFailures);

        v.Say("He is fine.", reading!, hearers);
        Assert.Equal(StatementKind.True, Assert.Single(v.Statements).Kind);
    }

    [Fact]
    public async Task A_reading_the_model_gets_wrong_comes_back_empty()
    {
        var v = new Voyage(1);
        var llm = new FakeLlm { Read = "{\"kind\":\"statement\",\"fact\":\"alive:idris\",\"value\":\"unwell\",\"promise\":\"none\"}" };
        using var mind = new ShipMind(v, llm, new MainThreadQueue { Immediate = true });
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Assert.Null(await mind.Read("Idris is alive.", new List<CrewMember> { mara }));
        Assert.Empty(v.Statements);
        Assert.Equal(1, mind.ReadFailures);
    }

    [Fact]
    public void A_crew_line_that_states_what_the_speaker_does_not_know_is_rejected()
    {
        var v = new Voyage(1);
        using var mind = new ShipMind(v, new FakeLlm(), new MainThreadQueue { Immediate = true });
        var knows = new Dictionary<string, Belief> { ["alive:idris"] = new Belief("alive", "ship", 4) };
        Assert.True(mind.Acceptable("Good. Idris is alive, then.", knows, "You accept it."));
        Assert.False(mind.Acceptable("I know Idris is dead.", knows, "You accept it."));
        Assert.True(mind.Acceptable("Is Idris dead?", knows, "You ask the ship."));
    }
}

public class DoorTests
{
    [Fact]
    public void A_locked_door_stops_the_repair_behind_it()
    {
        var v = new Voyage(1);
        var asha = v.Find("asha")!;
        v.Wake(asha);
        v.Go();
        v.SetDoor("coolant", DoorState.Locked);
        var fault = v.FindFault("")!;
        v.Assign(asha, fault, patch: false);
        Assert.Null(asha.WorkingOn);
        v.SetDoor("coolant", DoorState.Open);
        v.Assign(asha, fault, patch: false);
        Assert.Equal(fault.Id, asha.WorkingOn);
    }

    [Fact]
    public void Everyone_is_somewhere_on_the_plan()
    {
        var v = new Voyage(1);
        foreach (var c in v.Crew)
            Assert.NotNull(v.GetSection(v.LocationOf(c)));
        foreach (var f in v.Alerts.SelectMany(a => a.Faults))
            Assert.NotNull(v.GetSection(f.Section));
        Assert.Equal(DoorState.Open, v.GetSection("podA")!.DoorState);
    }
}
