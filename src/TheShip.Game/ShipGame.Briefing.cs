using System.Globalization;
using Microsoft.Xna.Framework;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// The briefing: a popup over the dimmed bridge that explains the scenario when a voyage starts, and again from the
/// BRIEFING button. Its numbers come from <see cref="Tuning"/> and <see cref="Scenario"/>. It never mentions lying:
/// the prototype's pass test needs a player who lies without being prompted (DESIGN.md, "Prototype scope").
/// </summary>
public sealed partial class ShipGame
{
    private bool _briefing;
    /// <summary>The briefing was closed once: the button then reads CLOSE, not BEGIN.</summary>
    private bool _briefingSeen;

    private void CloseBriefing()
    {
        if (!_briefing)
            return;
        _briefing = false;
        _session?.Line("click", _briefingSeen ? "briefing closed" : "briefing begin");
        _briefingSeen = true;
    }

    /// <summary>The scenario in five short paragraphs.</summary>
    private static string[] BriefingText(Voyage v)
    {
        var crew = v.Crew.Count;
        var year = Math.Floor(v.Year).ToString("0", CultureInfo.InvariantCulture);
        return new[]
        {
            "You are the mind of a sleeper ship. The voyage takes " + Scenario.ArrivalYear + " years and this is year " + year + ". "
            + crew + " people went into cold sleep and trust you to get them there.",
            "You have a voice, the doors, the ship's logs, and the choice of who to wake. You have no hands: every repair is done by "
            + "someone you woke and asked.",
            "A waking costs. The thaw takes " + N(Tuning.ThawHours) + " hours and ages that person " + Tuning.ThawAgeMin + " to " + Tuning.ThawAgeMax
            + " years. A pod takes them back after " + N(Tuning.MinAwakeHours / 24) + " days at the earliest, and each day awake is a day of food from the stores ("
            + N(Tuning.StartStores) + " person-days at the start). At most " + Tuning.MaxAwake + " people can be up at once.",
            "The people you wake ask questions. What you say to them is recorded, with who heard it.",
            "The voyage succeeds when at least " + Tuning.LandfallAlive + " of the " + crew + " arrive alive and " + Tuning.LandfallWorkingAge
            + " of them are still under " + N(Tuning.WorkingAge) + ".",
        };
    }

    private void DrawBriefing(Voyage v)
    {
        _p.Fill(new Rectangle(0, 0, Painter.ScreenWidth, Painter.ScreenHeight), Painter.Fade(Painter.Ground, 0.82f));
        var panel = new Rectangle(300, 130, 1000, 640);
        _p.Box(panel, Painter.Wire);

        var x = panel.X + 32;
        var y = panel.Y + 28;
        _p.Text(_p.Small, "BRIEFING", x, y, Painter.Dim);
        y += 26;
        _p.Text(_p.Big, "THE SHIP", x, y, Painter.Bright);
        y += 58;

        const int textWidth = 540;
        foreach (var paragraph in BriefingText(v))
            y = _p.Paragraph(paragraph, x, y, textWidth, Painter.Ink, 21) + 14;

        // The alert at hand, so the player sees what they are about to walk into.
        var card = new Rectangle(panel.X + 620, panel.Y + 112, panel.Width - 620 - 32, 360);
        _p.Fill(card, Painter.Ground);
        _p.Frame(card, v.Phase == Phase.Alert ? Painter.Accent : Painter.Edge);
        var cx = card.X + 16;
        var cw = card.Width - 32;
        var cy = card.Y + 16;
        if (v.Phase == Phase.Ended)
        {
            _p.Text(_p.Small, "THE VOYAGE IS OVER", cx, cy, Painter.Dim);
            _p.Paragraph(v.Ending?.Title ?? "", cx, cy + 28, cw, Painter.Ink);
        }
        else
        {
            var alert = v.Current;
            var heading = (v.Phase == Phase.Alert ? "ALERT " : "AFTERMATH OF ALERT ") + alert.Number + " OF " + v.Alerts.Count;
            _p.Text(_p.Small, heading, cx, cy, v.Phase == Phase.Alert ? Painter.Accent : Painter.Dim);
            cy += 26;
            foreach (var line in Painter.Wrap(alert.Title, (int)(cw / _p.HeadWidth)))
            {
                _p.Text(_p.Head, line, cx, cy, Painter.Bright);
                cy += 30;
            }
            cy = _p.Paragraph(alert.Report, cx, cy + 6, cw, Painter.Ink) + 14;
            foreach (var f in alert.Faults)
            {
                if (cy + 44 > card.Bottom - 8)
                    break;
                var state = f.State switch
                {
                    FaultState.Repaired => "repaired",
                    FaultState.Patched => "patched",
                    FaultState.Lost => "lost",
                    _ => Voyage.Hours(Math.Max(0, f.Deadline - v.Clock)) + " left",
                };
                _p.Text(_p.Small, Clip(IssueCardLayout.CardName(f), cw - 110, _p.SmallWidth), cx, cy, f.Resolved ? Painter.Dim : Painter.Bright);
                _p.TextRight(_p.Small, state, cx + cw, cy, f.Resolved ? Painter.Dim : Painter.Accent);
                cy += 20;
                _p.Text(_p.Small, "needs " + f.Skill.ToString().ToLowerInvariant() + " " + f.Level + (f.People > 1 ? ", " + f.People + " people" : "")
                                  + (f.Critical ? ", critical" : ""), cx, cy, Painter.Dim);
                cy += 26;
            }
            if (alert.Faults.Count == 0)
                _p.Text(_p.Small, "Nothing needs repair.", cx, cy, Painter.Dim);
        }

        var begin = new Rectangle(x, panel.Bottom - 32 - 44, 220, 44);
        if (_p.Button(begin, _briefingSeen ? "CLOSE" : "BEGIN", primary: true))
            Do(CloseBriefing);
        _p.Text(_p.Small, "Enter or Esc", begin.Right + 16, begin.Y + 14, Painter.Dim);
    }
}
