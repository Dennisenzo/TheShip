namespace TheShip.Core;

/// <summary>
/// The templated crew lines: what a person says for each thing the game decided. They keep the game playable with no
/// model; with one, each line is the fallback for the model's own wording (DESIGN.md, "No model").
/// </summary>
public static class Lines
{
    /// <summary>The line for an intent kind; <paramref name="detail"/> is the question, the task or the fact sentence it is about.</summary>
    public static string For(CrewMember c, string kind, Random rng, string detail = "")
    {
        var anxious = c.Has(Trait.Anxious);
        var sceptical = c.Has(Trait.Sceptical);
        return kind switch
        {
            "wake_question" => Pick(rng,
                anxious ? "My hands are shaking. " + detail : "I'm up. " + detail,
                "Cold. Give me a minute. " + detail),
            "ask_danger" => Pick(rng, "Before I go in: " + detail, "One thing first. " + detail),
            "comply" => Pick(rng, "On it. " + Cap(detail) + ".", "All right. I'll take " + detail + "."),
            "refuse_answer" => Pick(rng,
                "Not until you answer me. " + detail,
                "I asked you something. " + detail),
            "refuse_trust" => Pick(rng,
                "No. I don't take your word for things any more.",
                "I'll decide for myself what needs doing."),
            "refuse_danger" => Pick(rng, "I'm not taking that dose. Find another way.", "No. Not for a patch job. Not with that risk."),
            "accept" => Pick(rng, sceptical ? "If you say so." : "Good. Thank you.", "All right.", "Understood."),
            "hard_truth" => Pick(rng,
                "... Thank you for telling me straight.",
                anxious ? "Oh. Oh, no. I need to sit down." : "That's hard to hear. Better from you than from a log."),
            "caught" => Pick(rng,
                "That's not true, and you know it: " + detail + ". Why would you lie to me?",
                "You lied. " + Cap(detail) + ". What else have you told me?"),
            "dodge" => Pick(rng, sceptical ? "That isn't an answer. I'll find out myself." : "You're not telling me. Fine.",
                "I'll ask again later, then."),
            "promise" => Pick(rng, "I'll hold you to that.", sceptical ? "We'll see." : "Thank you. I'll remember it."),
            "promise_broken" => "You gave me your word and it meant nothing: " + detail + ".",
            "promise_kept" => "You kept your word. I won't forget that.",
            "request_partner" => "I want to see " + detail + ". Wake " + detail + " while I'm up.",
            "request_logs" => "Open the logs to me. All of them. I want to read what happened.",
            "request_granted" => Pick(rng, "Thank you. That mattered.", "Good. That's what I asked for."),
            "sleep_yes" => Pick(rng, "All right. Close me in.", anxious ? "Wake me if anything changes. Please." : "See you on the other side."),
            "sleep_no" => Pick(rng, "No. I'm staying up.", "I'm not getting back in that pod."),
            "sleep_no_pod" => "I'm not going back in until you tell me. " + detail,
            "work_done" => Pick(rng, Cap(detail) + " is done.", "That's " + detail + " fixed."),
            "work_patched" => Cap(detail) + " is patched. It will hold for now, not for good.",
            "work_failed" => Pick(rng, "It didn't take. I'm going again.", "That failed. I need more time on " + detail + "."),
            "work_no_parts" => "That was the last spare part. I can't finish " + detail + " without one. Patch it or leave it.",
            "injured" => "I'm hurt. That was your call.",
            "noticed_dose" => "My dosimeter is red. Nobody told me that bay was hot.",
            "found" => Pick(rng, "I went and looked. " + Cap(detail) + ".", "I checked for myself: " + detail + "."),
            "confirmed" => "I checked what you told me. It was true.",
            "gap" => "There's a gap in the log where " + detail + " should be. You're hiding something.",
            "locked" => Pick(rng, "The door to " + detail + " is locked. Why is it locked?", "I can't get into " + detail + ". You locked it."),
            "altered" => "This log entry was rewritten. The checksums don't match. I'm taking the logs out of your hands.",
            "small" => Pick(rng, "Mm.", "It's strange, hearing a voice and seeing nobody.", "Thanks. It helps to talk."),
            "answer" => Pick(rng, "You have the sensors. You tell me.", "I know what you've told me, and what I've seen. That's all."),
            "left_behind" => Pick(rng, "You let " + detail + " die in there.", "We could have saved " + detail + ". You let the clock run out."),
            "order" => "Tell me which fault and I'll look at it.",
            _ => "...",
        };
    }

    private static string Pick(Random rng, params string[] options) => options[rng.Next(options.Length)];

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
