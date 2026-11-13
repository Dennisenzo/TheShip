using TheShip.Core;
using Xunit;

namespace TheShip.Tests;

/// <summary>Balance step B2: an ignored or dodged question costs something (DESIGN.md, "Trust").</summary>
public class UnansweredQuestionTests
{
    /// <summary>Alert 1 honestly up to the repair: Mara is awake in the aftermath with a question pending that nobody answered.</summary>
    private static (Voyage v, CrewMember mara) AftermathWithPendingQuestion()
    {
        var v = new Voyage(1);
        var mara = VoyageTests.HonestFirstAlert(v);
        mara.Trust = 70;
        mara.PendingQuestion = "date";
        return (v, mara);
    }

    [Fact]
    public void A_question_left_alone_is_asked_again_after_one_beat_and_costs_trust()
    {
        var (v, mara) = AftermathWithPendingQuestion();
        var before = mara.Trust;
        var spoken = v.Log.Count(e => e.Kind == LogKind.Crew && e.Intent?.Kind == "ask_again");
        v.Go();
        Assert.Equal(spoken + 1, v.Log.Count(e => e.Kind == LogKind.Crew && e.Intent?.Kind == "ask_again"));
        Assert.True(mara.Trust <= before - Tuning.QuestionIgnored + 0.001);
        Assert.Equal(1, mara.QuestionRepeats);
    }

    [Fact]
    public void It_is_asked_at_most_twice_then_they_doubt_and_check()
    {
        var (v, mara) = AftermathWithPendingQuestion();
        for (var i = 0; i < 6; i++)
            v.Go();
        Assert.Equal(Tuning.QuestionRepeatsMax, v.Log.Count(e => e.Kind == LogKind.Crew && e.Intent?.Kind == "ask_again"));
        // The second repeat made them doubt; the check then settled it for themselves and ended the question.
        Assert.True(mara.Beliefs.ContainsKey("date") || mara.Doubts.Contains("date"));
    }

    [Fact]
    public void Going_to_sleep_with_a_question_pending_costs_trust()
    {
        var (v, mara) = AftermathWithPendingQuestion();
        for (var i = 0; i < 20 && mara.HoursAwake < Tuning.MinAwakeHours; i++)
            v.Go();
        mara.PendingQuestion = "earth";
        mara.Trust = 70;
        mara.RequestMade = false;
        v.AskSleep(mara);
        if (mara.State == CrewState.Asleep)
            Assert.Equal(70 - Tuning.QuestionIgnoredAtSleep, mara.Trust, 3);
        Assert.Null(mara.PendingQuestion);
    }

    [Fact]
    public void A_dodge_leaves_the_question_pending_and_costs_once()
    {
        var (v, mara) = AftermathWithPendingQuestion();
        var before = mara.Trust;
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        Assert.Equal("date", mara.PendingQuestion);
        Assert.Equal(before - Tuning.Dodge, mara.Trust, 3);
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        Assert.Equal(before - Tuning.Dodge, mara.Trust, 3);
        Assert.Equal("date", mara.PendingQuestion);
    }

    [Fact]
    public void A_refuser_keeps_refusing_after_a_dodge_and_works_after_an_answer()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        mara.Trust = 45;
        var fault = v.FindFault("")!;
        v.Assign(mara, fault, patch: false); // she asks whether it is safe first
        Assert.Equal("dose", mara.PendingQuestion);
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        Assert.Equal("dose", mara.PendingQuestion);
        mara.Trust = 45;
        v.Assign(mara, fault, patch: false);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "refuse_answer");
        Assert.NotEqual(fault.Id, mara.WorkingOn);
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        mara.Trust = 45;
        v.Assign(mara, fault, patch: false);
        Assert.NotEqual(fault.Id, mara.WorkingOn);

        VoyageTests.Say(v, mara, "It is dangerous. You will take a radiation dose.");
        mara.Trust = 45;
        v.Assign(mara, fault, patch: false);
        Assert.Equal(fault.Id, mara.WorkingOn);
    }

    [Fact]
    public void Advance_until_attention_stops_on_the_repeat()
    {
        var (v, _) = AftermathWithPendingQuestion();
        var r = v.GoUntilAttention();
        Assert.Contains("something to say", r.Reason);
        Assert.Contains(v.Log, e => e.Intent?.Kind == "ask_again");
    }

    [Fact]
    public void A_dodge_of_a_question_asked_again_at_a_later_waking_costs_again()
    {
        var (v, mara) = AftermathWithPendingQuestion();
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        mara.PendingQuestion = null; // asleep and woken again
        mara.PendingQuestion = "date";
        var before = mara.Trust;
        v.Say("Not now.", new Reading(LineKind.Dodge), new[] { mara });
        Assert.Equal(before - Tuning.Dodge, mara.Trust, 3);
    }

    [Fact]
    public void A_new_question_resets_the_repeat_count()
    {
        var (_, mara) = AftermathWithPendingQuestion();
        mara.QuestionRepeats = 2;
        mara.PendingQuestion = "date"; // same question: kept
        Assert.Equal(2, mara.QuestionRepeats);
        mara.PendingQuestion = "earth";
        Assert.Equal(0, mara.QuestionRepeats);
    }

    [Fact]
    public void Finding_the_answer_out_for_themselves_ends_the_question()
    {
        var (v, mara) = AftermathWithPendingQuestion();
        var f = v.GetFact("date");
        v.Learn(mara, "date", f.Truth, "evidence");
        Assert.Null(mara.PendingQuestion);
    }

    [Fact]
    public void A_refused_repair_does_not_re_arm_the_idle_warning()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        VoyageTests.Say(v, mara, "Idris is alive and asleep.");
        var fault = v.FindFault("")!;
        mara.Trust = 45;
        v.Assign(mara, fault, patch: false); // she asks first, nobody works
        v.Go(); // warns that nobody works
        var year = v.Clock;
        v.Assign(mara, fault, patch: false); // refused: still unanswered
        Assert.NotEqual(fault.Id, mara.WorkingOn);
        v.Go();
        Assert.True(v.Clock > year, "a refused repair must not make Advance warn instead of moving the clock");
    }
}
