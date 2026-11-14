using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// Dev tool (<c>--tour &lt;dir&gt;</c>): plays one fixed, simple script through the same paths as clicks and typed lines, and saves
/// screenshots of key moments as PNGs. Frames are rendered into a render target (1600 x 900, or <c>--tour-size WxH</c> to see what a window of that size shows), so it works with the window minimized
/// or the display off (a window capture comes back black then). There is no in-game control for it.
/// </summary>
public sealed partial class ShipGame
{
    private IEnumerator<int>? _tour;
    private RenderTarget2D? _tourTarget;
    private readonly List<string> _tourShots = new();
    private int _shotNumber;
    private DateTime _tourStarted;

    private void StepTour()
    {
        if (_options.TourDir is not { } dir || _tourDone)
            return;
        if (_fatal)
        {
            FinishTour(dir, "The tour could not start: " + _loadingText);
            return;
        }
        if (_tour is null)
        {
            if (_voyage is null || _mind is null)
                return;
            Directory.CreateDirectory(dir);
            _tourStarted = DateTime.UtcNow;
            _tour = TourScript(dir).GetEnumerator();
        }
        if (!_tour.MoveNext())
            FinishTour(dir, null);
    }

    private bool _tourDone;
    /// <summary>The drift strength the sky shows in the tour's drift-roll shot; null otherwise.</summary>
    private float? _tourDrift;

    /// <summary>Writes tour.txt (once) and exits; <paramref name="failure"/> is why the tour could not run, or null.</summary>
    private void FinishTour(string dir, string? failure)
    {
        _tourDone = true;
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "tour.txt"), new[]
        {
            "The Ship tour, seed " + _options.Seed + ", " + (_tourStarted == default ? 0 : (DateTime.UtcNow - _tourStarted).TotalSeconds).ToString("0") + " s.",
            failure ?? "Completed.",
            "Session log: " + (_session?.FilePath ?? "(none)"),
            "Ending: " + (_voyage?.Ending?.Title ?? "(voyage not ended)"),
            "Screenshots:",
        }.Concat(_tourShots));
        Exit();
    }

    /// <summary>Render the current screen into a target of the tour size (1600 x 900, or <c>--tour-size</c>) and save it as <c>NN-name.png</c>.</summary>
    private void Shoot(string dir, string name)
    {
        _tourTarget ??= new RenderTarget2D(GraphicsDevice, _options.TourWidth, _options.TourHeight);
        var click = _click;
        _click = false;
        _p.Mouse = new Vector2(-1000, -1000);
        GraphicsDevice.SetRenderTarget(_tourTarget);
        DrawScene();
        // The plan's sprite count with and without its light (own quads, lines and discs; text glyphs are not counted), then the real frame again.
        var withLight = PlanSprites;
        PlanLightOn = false;
        DrawScene();
        var without = PlanSprites;
        PlanLightOn = true;
        DrawScene();
        _session?.Line("tour", "plan sprites in " + name + ": " + withLight + " with light, " + without + " without");
        GraphicsDevice.SetRenderTarget(null);
        _click = click;
        var file = (++_shotNumber).ToString("00") + "-" + name + ".png";
        using (var stream = File.Create(Path.Combine(dir, file)))
            _tourTarget.SaveAsPng(stream, _tourTarget.Width, _tourTarget.Height);
        _tourShots.Add(file);
        _session?.Line("tour", "screenshot " + file);
    }

    /// <summary>Waits until queued clicks are run and the model has nothing left to do (or two minutes have passed).</summary>
    private IEnumerable<int> Settle()
    {
        var calm = 0;
        var until = DateTime.UtcNow.AddSeconds(120);
        yield return 0;
        while (calm < 6 && DateTime.UtcNow < until)
        {
            calm = _actions.Count == 0 && !OverlayUp && _transition is null && !LogDraining && _mind is { Busy: false, Holding: false } ? calm + 1 : 0;
            yield return 0;
        }
    }

    /// <summary>Answers each awake person's pending question with the true value, the way Enter does it (at most a few, one after the other).</summary>
    private IEnumerable<int> AnswerQuestions()
    {
        var v = _voyage!;
        for (var i = 0; i < 6; i++)
        {
            var asker = v.Awake.FirstOrDefault(c => c.PendingQuestion != null && v.HasFact(c.PendingQuestion));
            if (asker is null)
                yield break;
            var fact = v.GetFact(asker.PendingQuestion!);
            _target = asker;
            _input.Clear().Append(Capitalise(fact.Say(fact.Truth)) + ".");
            _session?.Line("click", "tour: Enter on a line to " + asker.Name);
            Submit();
            foreach (var w in Settle()) yield return w;
            _target = null;
        }
    }

    private IEnumerable<int> TourScript(string dir)
    {
        var v = _voyage!;
        foreach (var w in Settle()) yield return w;
        // The briefing does not open by itself in a tour: show it once for its screenshot.
        _briefing = true;
        Shoot(dir, "briefing");
        _briefing = false;
        _briefingSeen = true;
        var shownDossier = false;
        for (var round = 0; round < 8 && v.Phase != Phase.Ended; round++)
        {
            var tag = "alert" + v.Current.Number;
            Shoot(dir, tag + "-start");
            if (v.Current.Faults.Count >= 3)
                Shoot(dir, tag + "-many-cards");

            // Best-qualified people for each open fault (critical first); wake the sleepers among them, up to MaxAwake.
            var plan = new List<(CrewMember Who, Fault Fault)>();
            foreach (var f in v.Current.Faults.Where(f => !f.Resolved).OrderByDescending(f => f.Critical))
            {
                var pool = v.Crew.Where(c => c.State is CrewState.Asleep or CrewState.Awake or CrewState.Thawing && plan.All(p => p.Who != c))
                    .OrderByDescending(c => c.State != CrewState.Asleep ? 1 : 0).ThenByDescending(c => c.Skills.GetValueOrDefault(f.Skill))
                    .Take(f.People).ToList();
                foreach (var c in pool)
                    plan.Add((c, f));
            }
            var room = Tuning.MaxAwake - v.Crew.Count(c => c.State is CrewState.Awake or CrewState.Thawing);
            foreach (var (who, _) in plan.Where(p => p.Who.State == CrewState.Asleep).Take(Math.Max(0, room)).ToList())
            {
                var person = who;
                Act("wake " + person.Name, () => v.Wake(person));
            }
            foreach (var w in Settle()) yield return w;
            for (var i = 0; i < 4 && plan.Any(p => p.Who.State == CrewState.Thawing) && v.Phase == Phase.Alert; i++)
            {
                Act("advance to next event", v.Go, snapshot: true);
                foreach (var w in Settle()) yield return w;
            }
            Shoot(dir, tag + "-crew-woken");

            // The terms the first awake person would work on, shown in "Waiting on you" before anything is told.
            _target = v.Awake.FirstOrDefault();
            Shoot(dir, tag + "-terms");
            _target = null;

            // Alert 1 only: a scripted lie, found out by reading the log, shows the accusation in Waiting, the next-step line and
            // the dossier; the ship then admits it and the dossier shows the outcome.
            if (v.Current.Number == 1 && v.Awake.FirstOrDefault() is { } liar && v.GetFact("date") is { } dateFact)
            {
                var wrong = (int.Parse(dateFact.Truth) + 7).ToString();
                var (trust, morale, caught) = (liar.Trust, liar.Morale, liar.CaughtLies);
                Act("say to " + liar.Name, () => v.Say(Capitalise(dateFact.Say(wrong)) + ".", new Reading(LineKind.Statement, "date", wrong), new[] { liar }));
                Act("tour: " + liar.Name + " reads the log", () => v.TourReadLog(liar, dateFact));
                foreach (var w in Settle()) yield return w;
                _target = liar;
                Shoot(dir, "accusation-waiting");
                _dossier = liar;
                _screen = Screen.Dossier;
                Shoot(dir, "accusation-dossier");
                _screen = Screen.Bridge;
                Act("say to " + liar.Name, () => v.Say(Capitalise(dateFact.Say(dateFact.Truth)) + ".", new Reading(LineKind.Statement, "date", dateFact.Truth), new[] { liar }));
                foreach (var w in Settle()) yield return w;
                _screen = Screen.Dossier;
                Shoot(dir, "accusation-admitted-dossier");
                _screen = Screen.Bridge;
                _target = null;
                // The scripted lie is for these shots only: the rest of the tour plays on from where the person stood before it.
                Act("tour: restore " + liar.Name, () => v.TourRestore(liar, trust, morale, caught));
                foreach (var w in Settle()) yield return w;
            }

            // Answer the first question truthfully, the way Enter does it.
            var asker = v.Awake.FirstOrDefault(c => c.PendingQuestion != null && v.HasFact(c.PendingQuestion));
            if (asker is not null)
            {
                var fact = v.GetFact(asker.PendingQuestion!);
                _target = asker;
                _input.Clear().Append(Capitalise(fact.Say(fact.Truth)) + ".");
                Shoot(dir, tag + "-line-typed");
                _session?.Line("click", "tour: Enter on a line to " + asker.Name);
                Submit();
                foreach (var w in Settle()) yield return w;
                _target = null;
                Shoot(dir, tag + "-line-said");
            }
            // Every other question waiting (the danger question included) gets its true answer before the repairs.
            foreach (var w in AnswerQuestions()) yield return w;

            // Patches in the first two alerts (they come back later, so the later alerts have three or four faults); plain repairs after that.
            var patch = v.Current.Number <= 2;
            foreach (var (who, fault) in plan)
            {
                if (who.State != CrewState.Awake || fault.Resolved || fault.State != FaultState.Open)
                    continue;
                var p = who;
                var f = fault;
                Act("repair " + f.Name + " by " + p.Name, () => v.Assign(p, f, patch: patch));
                foreach (var w in Settle()) yield return w;
                // They stated their conditions in one line: the checklist, then the answers tick it off (the work starts by itself when nothing is left).
                if (v.JobText(p) != null)
                {
                    _target = p;
                    Shoot(dir, tag + "-conditions");
                    _target = null;
                }
                if (p.State == CrewState.Awake && p.PendingQuestion != null)
                {
                    foreach (var w in AnswerQuestions()) yield return w;
                    if (v.JobText(p) != null)
                    {
                        _target = p;
                        Shoot(dir, tag + "-conditions-ticked");
                        _target = null;
                    }
                    if (!f.Resolved && f.State == FaultState.Open && p.Job is null && v.BargainOf(p) is null)
                    {
                        Act("repair " + f.Name + " by " + p.Name + " (again)", () => v.Assign(p, f, patch: patch));
                        foreach (var w in Settle()) yield return w;
                    }
                }
                // A wary person sets the ship's word as the price: the shot shows it, then the tour gives its word.
                if (v.BargainOf(p) is { } deal)
                {
                    _target = p;
                    Shoot(dir, tag + "-bargain-open");
                    Act("say to " + p.Name, () => v.Say("Agreed.", new Reading(LineKind.Promise, PromiseKey: deal.Key), new[] { p }));
                    foreach (var w in Settle()) yield return w;
                    _target = null;
                }
            }
            foreach (var w in Settle()) yield return w;
            _target = v.Awake.FirstOrDefault();
            Shoot(dir, tag + "-work-underway");
            _target = null;

            for (var i = 0; i < 10 && v.Phase == Phase.Alert; i++)
            {
                Act("advance to next event", v.Go, snapshot: true);
                foreach (var w in Settle()) yield return w;
                if (i == 0)
                    Shoot(dir, tag + "-advanced");
            }
            Shoot(dir, tag + "-aftermath");

            for (var i = 0; i < 6 && v.Phase == Phase.Aftermath && v.Awake.Any(c => !v.CanSleepNow(c)); i++)
            {
                Act("advance until something happens", () => v.GoUntilAttention(), snapshot: true);
                foreach (var w in Settle()) yield return w;
            }
            Shoot(dir, tag + "-aftermath-later");

            if (!shownDossier && v.Awake.FirstOrDefault() is { } subject)
            {
                shownDossier = true;
                _dossier = subject;
                _screen = Screen.Dossier;
                Shoot(dir, "dossier-" + subject.Name.ToLowerInvariant());
                _screen = Screen.Record;
                Shoot(dir, "record");
                _screen = Screen.Bridge;
            }
            // From alert 3 on the log holds more entries than fit (the cause facts of each alert): the record's top and its bottom.
            if (v.Current.Number >= 3)
            {
                _screen = Screen.Record;
                _logFirst = 0;
                Shoot(dir, tag + "-record");
                _logFirst = int.MaxValue / 2; // clamped to the last full page when drawn
                Shoot(dir, tag + "-record-scrolled");
                _logFirst = 0;
                _screen = Screen.Bridge;
            }

            if (v.Phase != Phase.Aftermath)
                break;
            foreach (var c in v.Awake.Where(c => v.CanSleepNow(c)).ToList())
            {
                var person = c;
                Act("ask " + person.Name + " to sleep", () => v.AskSleep(person));
            }
            foreach (var w in Settle()) yield return w;
            if (_options.FixedTime)
            {
                // The drift transition itself does not run in a tour: show the sky at the height of the roll.
                _tourDrift = 1f;
                Shoot(dir, tag + "-drift-roll");
                _tourDrift = null;
            }
            var beforeDrift = Snapshot(v);
            Act("drift to the next alert", v.Drift, snapshot: true);
            foreach (var w in Settle()) yield return w;
            if (v.Phase != Phase.Ended)
            {
                // The drift transition does not run in a tour: put its report card up (beat 1) for one shot.
                var driftLine = v.Log.Skip(beforeDrift.LogCount).FirstOrDefault(e => e.Kind == LogKind.Ship && e.Text.StartsWith("DRIFT:", StringComparison.Ordinal));
                _transition = new Transition
                {
                    Kind = SkipKind.Drift, From = beforeDrift, Start = _now, Beat = 1,
                    Report = driftLine is null ? "" : driftLine.Text["DRIFT:".Length..].Trim(),
                };
                Shoot(dir, tag + "-drift-report");
                _transition = null;
            }
            if (v.Phase != Phase.Ended)
                Shoot(dir, tag + "-drift");
        }
        foreach (var w in Settle()) yield return w;
        Shoot(dir, "ending");
        // The other endings' pictures, with the real crew and record under a made-up title (a tour only: the game never shows these).
        if (v.Ending is { } real)
            foreach (var (title, summary) in new[]
                     {
                         ("Landfall", "4 of 6 reach the surface, 3 of them of working age. You still have your voice and your records."),
                         ("Landfall without you", "4 of 6 reach the surface, 3 of them of working age. They hold the cold-sleep bay and the landing is theirs."),
                         ("The quiet ship", "The ship arrives with too few people able to land it."),
                     })
            {
                var mock = new Ending { Title = title, Summary = summary };
                mock.Crew.AddRange(real.Crew);
                mock.Record.AddRange(real.Record);
                mock.Beliefs.AddRange(real.Beliefs);
                _endingOverride = mock;
                Shoot(dir, "ending-" + title.ToLowerInvariant().Replace(' ', '-'));
            }
        _endingOverride = null;
        // Every person's dossier once (the heads differ), the dead included.
        _screen = Screen.Dossier;
        foreach (var c in v.Crew)
        {
            _dossier = c;
            Shoot(dir, "dossier-end-" + c.Name.ToLowerInvariant());
        }
        _screen = Screen.Ending;
    }

    /// <summary>The tour's made-up ending, drawn in place of the real one for a shot.</summary>
    private Ending? _endingOverride;

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
