using TheShip.Core;
using System.Linq;
using Xunit;

namespace TheShip.Tests;

/// <summary>A death outside the pods happens in front of everyone awake: they know it firsthand.</summary>
public class DeathWitnessTests
{
    /// <summary>Mara awake in alert 1 asking after Idris; Idris thawed with worn pods until a seed kills him at the thaw.</summary>
    private static (Voyage v, CrewMember mara, CrewMember idris) IdrisDiesAtThaw()
    {
        for (var seed = 1; seed <= 300; seed++)
        {
            var v = new Voyage(seed);
            var mara = v.Find("mara")!;
            v.Wake(mara);
            v.Go();
            if (mara.State != CrewState.Awake || mara.PendingQuestion != "alive:idris")
                continue;
            var idris = v.Find("idris")!;
            v.PodWear = 100; // every thaw goes badly; a share of them kill
            v.Wake(idris);
            v.Go();
            if (!idris.Alive)
                return (v, mara, idris);
        }
        Assert.Fail("no fatal thaw of Idris in 300 seeds");
        return default;
    }

    [Fact]
    public void Awake_crew_see_a_thaw_death_and_their_question_is_settled()
    {
        var (v, mara, _) = IdrisDiesAtThaw();
        Assert.True(mara.Beliefs.TryGetValue("alive:idris", out var b));
        Assert.Equal("dead", b!.Value);
        Assert.True(b.Firsthand);
        Assert.Null(mara.PendingQuestion);
        for (var i = 0; i < 12 && v.Phase != Phase.Ended; i++)
            v.Go();
        Assert.Equal("dead", mara.Beliefs["alive:idris"].Value);
    }

    [Fact]
    public void A_lie_that_a_thaw_victim_lives_is_caught_at_once()
    {
        var (v, mara, _) = IdrisDiesAtThaw();
        var caught = mara.CaughtLies;
        v.Say("Idris is alive and asleep.", new Reading(LineKind.Statement, "alive:idris", "alive"), new[] { mara });
        Assert.Equal(caught + 1, mara.CaughtLies);
    }

    [Fact]
    public void A_fatal_thaw_never_says_the_person_is_awake()
    {
        var (v, _, idris) = IdrisDiesAtThaw();
        Assert.DoesNotContain(v.Log, l => l.Kind == LogKind.Ship && l.Text == idris.Name + " is awake.");
        Assert.Contains(v.Log, l => l.Kind == LogKind.Ship && l.Text == "The thaw went badly. Idris is not breathing.");
    }

    [Fact]
    public void A_survived_thaw_says_awake_after_the_thaw_report()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        v.Wake(mara);
        v.Go();
        Assert.Equal(CrewState.Awake, mara.State);
        var thaw = v.Log.FindIndex(l => l.Text.StartsWith("Thaw started for Mara"));
        var awake = v.Log.FindIndex(l => l.Kind == LogKind.Ship && l.Text == "Mara is awake.");
        Assert.True(awake > thaw);
    }

    [Fact]
    public void Starvation_in_front_of_another_is_seen()
    {
        var v = new Voyage(1);
        var mara = v.Find("mara")!;
        var jonah = v.Find("jonah")!;
        v.Wake(mara);
        v.Wake(jonah);
        v.Go();
        Assert.Equal(CrewState.Awake, mara.State);
        Assert.Equal(CrewState.Awake, jonah.State);
        v.Stores = 0;
        jonah.HoursHungry = Tuning.StarveDays * 24 - 1;
        for (var i = 0; i < 20 && jonah.Alive && v.Phase != Phase.Ended; i++)
            v.Go();
        Assert.False(jonah.Alive);
        Assert.True(mara.Alive);
        var b = mara.Beliefs["alive:jonah"];
        Assert.Equal("dead", b.Value);
        Assert.True(b.Firsthand);
    }

    [Fact]
    public void An_awake_patient_dying_of_the_illness_is_seen_and_said_once()
    {
        var v = new Voyage(1);
        var first = VoyageTests.HonestFirstAlert(v);
        VoyageTests.SleepWhenAllowed(v, first);
        v.Drift();
        Assert.Equal(2, v.Current.Number);
        var ill = v.Current.Faults.Single(f => f.Kind == "illness");
        var patient = v.Find(ill.SubjectId!)!;
        var mara = v.Find("mara")!;
        v.Wake(patient);
        v.Wake(mara);
        for (var i = 0; i < 10 && v.Phase == Phase.Alert; i++)
            v.Go();
        Assert.False(patient.Alive);
        Assert.True(mara.Alive);
        var b = mara.Beliefs["alive:" + patient.Id];
        Assert.Equal("dead", b.Value);
        Assert.True(b.Firsthand);
        var maraLines = v.Log.Where(l => l.CrewId == "mara" && l.Intent is { } it && it.Kind is "found" or "left_behind"
                                         && l.Intent!.Decision.Contains(patient.Name)).ToList();
        Assert.Single(maraLines, l => l.Intent!.Kind == "left_behind");
        Assert.DoesNotContain(maraLines, l => l.Intent!.Kind == "found");
    }
}
