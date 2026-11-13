using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// Dev tool (<c>--tour &lt;dir&gt;</c>): plays one fixed, simple script through the same paths as clicks and typed lines, and saves
/// screenshots of key moments as PNGs. Frames are rendered into a 1600 x 900 render target, so it works with the window minimized
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

    /// <summary>Render the current screen into a 1600 x 900 target and save it as <c>NN-name.png</c>.</summary>
    private void Shoot(string dir, string name)
    {
        _tourTarget ??= new RenderTarget2D(GraphicsDevice, Painter.ScreenWidth, Painter.ScreenHeight);
        var click = _click;
        _click = false;
        _p.Mouse = new Vector2(-1000, -1000);
        GraphicsDevice.SetRenderTarget(_tourTarget);
        DrawScene();
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
            calm = _actions.Count == 0 && !OverlayUp && _mind is { Busy: false, Holding: false } ? calm + 1 : 0;
            yield return 0;
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

            // Patches in the first two alerts (they come back later, so the later alerts have three or four faults); plain repairs after that.
            var patch = v.Current.Number <= 2;
            foreach (var (who, fault) in plan)
            {
                if (who.State != CrewState.Awake || fault.Resolved || fault.State != FaultState.Open)
                    continue;
                var p = who;
                var f = fault;
                Act("repair " + f.Name + " by " + p.Name, () => v.Assign(p, f, patch: patch));
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

            if (v.Phase != Phase.Aftermath)
                break;
            foreach (var c in v.Awake.Where(c => v.CanSleepNow(c)).ToList())
            {
                var person = c;
                Act("ask " + person.Name + " to sleep", () => v.AskSleep(person));
            }
            foreach (var w in Settle()) yield return w;
            Act("drift to the next alert", v.Drift, snapshot: true);
            foreach (var w in Settle()) yield return w;
            if (v.Phase != Phase.Ended)
                Shoot(dir, tag + "-drift");
        }
        foreach (var w in Settle()) yield return w;
        Shoot(dir, "ending");
    }

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
