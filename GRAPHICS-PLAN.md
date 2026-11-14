# Graphics plan: how The Ship looks

Written 2026-11-14 (ship-planner), decisions by the user the same day. Same shape as `UI-PLAN.md`. Looks only: no rules, prompts or balance. DESIGN.md stays the leading document.

## Goals

1. Everything drawn is crisp at the window's real size, not only at 1600 x 900.
2. Lines and shapes have even weight and soft edges: no crooked diagonals, no uneven 1-px lines.
3. The ship plan is the centrepiece: it fills its panel, reads as a ship, and shows state by light.
4. One accent colour with one meaning, so the eye goes to what needs the player.
5. A little atmosphere that never costs legibility, in step with the generated music (`Music.cs`: slow 43 s filter sweep, long reverb, calm/alert/drift moods).
6. Every screen gets the same care: dossier, record, ending, briefing, drift report.

## Findings (from the tour shots of 2026-11-14 and the code)

1. **Scaled picture.** `ShipGame.DrawScene` draws with `transformMatrix: scale * translation` and the default (linear) sampler. Fonts are baked at 15/17/22/32 px (`Painter` constructor) and stretched; on a maximized window (1920x1080 = 1.2, 2560x1440 = 1.6) text is resampled and soft. `Shoot` (`ShipGame.Tour.cs`) renders into a 1600x900 target, so every tour shot is sharper than the real game. No MSAA.
2. **Hard-edged lines.** `Painter.Line` is a stretched, rotated 1x1 pixel: diagonals stair-step (hull, section mesh, diamonds). At fractional scale `Frame`/`Fill` snap independently, so "1 px" lines come out 1 or 2 px. `Dashed` has hard, uneven ends.
3. **Only the person glyph is smooth.** `Painter.MakePersonGlyph` (4x supersampled premultiplied mask) is the pattern; diamonds (4 raw lines, notched corners), pod outlines, doors and the diamond numbers still use the 1x1 pixel.
4. **Text on layout units.** `MathF.Round` in `Painter.Text` is right at scale 1.0 only. `Wrap`, `SmallWidth`, `BodyWidth` assume an exact monospace advance in layout units; any font change must keep them.
5. **Type scale.** Nearly everything is `Small` (15). The header status numbers (STORES, PARTS, POD WEAR, POWER, COURSE) are 15 px `Dim`: the most important status, the least visible. Card text is cut with ".." ("Cause: the navigation drift is a drifting..").
6. **Amber overload.** Amber (242,176,76) is the primary button, critical card borders, the NEXT line, every SHIP line in the log, fault diamonds and labels, the phase chip, locked doors, trust deltas, LIE/PROMISE tags, the selected card bar. The log is always amber, so a real alert doesn't stand out.
7. **Button weight.** WAKE and FILE look the same; the record's OPEN buttons are solid light blocks and dominate the screen (RESTRICT/ALTER are outlines).
8. **Crew card is text soup.** "trust 43 wants reasons  morale 64" in one dim line; no meters (the dossier has them). Dead cards are full size.
9. **Empty states.** Empty "WAITING ON YOU" is a large empty panel; "Nothing needs repair." then 600 px of nothing; the ending shows 60 years as a small amber word in three thin columns; the dossier middle column is half empty.
10. **Plan fills only part of its panel** (`DrawPlan` scales by width only: ~60 px dead above, ~70 below). Faint mesh lines barely visible; empty engine room and reactor.
11. **Labels collide.** Diamonds sit on section labels (COOLANT BAY, HYDROPONICS); leaders run through label text; fault labels repeat the issue cards.
12. **No sense of state or light.** Sections look the same with or without power/fault/people; the power bus is static; nothing reacts to POWER LOW; no front/back, no engine glow.
13. **Clutter.** The legend line "rest the mouse on anything... door: outlined open, grey closed, amber locked" is still there; the MUSIC button is an unstyled box in the plan header.
14. **Flat background** (`Painter.Ground`), panels (`Panel`) barely separate from it.
15. **The tour can't show motion** (`Motion.Speed = 0`): animated effects need a frozen-phase option to be checked.
Other screens: briefing works (amber frame repeats the issue card style; empty space); the drift report looks like a form; the dossier's wireframe head is the same for everyone; record readable but OPEN column too loud.

## Steps (one ship-dev agent each; each ends with a `--tour` run and a look at the shots, a CLAUDE.md Progress line, then validation on main)

1. **Real-size rendering (`feature/gfx-resolution`).** `--tour-size <w>x<h>` (default 1600x900): `Shoot` and the back buffer use that size so tour shots show what a maximized window shows; first deliverable: "before" shots at 1920x1080 and 2560x1440 of the current code. Fonts drawn at `round(size * scale)` and drawn with 1/scale (a font cache keyed by pixel size; layout widths stay from the layout-size fonts; debounce re-baking during a window drag); `Painter.Scale` set each frame. `Fill`/`Frame` snap to device pixels (thickness `max(1, round(t*scale))`), text positions round to device pixels. Point sampling for flat fills, linear for glyph/feathered textures. Header status numbers in `Body` (17 px), value `Bright`, label `Dim` (user decision 4). Verify at 1280x720, 1600x900 (near-identical to before), 1920x1080, 2560x1440; long log lines at 0.8/1.2/1.6. Also fold in the last validation's Low findings: `MetText` grieves from the truth not the person's belief (`Voyage.Conditions.cs:~200-201`: grieve only when she believes the death; otherwise give the dropped job a line); add a test for the partner-death grief branch; `TextLayout.cs:~69` restore the `' '` escape; `Painter.cs:~247` comment on `PersonGlyphCoverage` is wrong (add a glyph test or fix the comment); entries held between a pending crew line and an early YOU line appear without fade-in (`ShipGame.Bridge.cs:~615-621`).
2. **Soft lines and shapes (`feature/gfx-lines`).** A feathered line texture (1 x 8 or 1 x 16, premultiplied, 1-px falloff) for `Painter.Line`; round caps/joins via a small disc texture so `Poly`/`Diamond` corners join cleanly; round dash ends, dash phase continuous across corners; doors with rounded ends; pods pick up the soft line; larger digits in the diamonds. No MSAA (user decision: texture only; add MSAA only if shots still stair-step). Retune `Faint`/`Hull` alpha after feathering. Verify with zoomed crops at 1600 and 1920 and a numeric test of the line profile (like the glyph coverage).
3. **Palette and weights (`feature/gfx-palette`).** Amber means "needs you" (user decision): ordinary SHIP lines neutral (`Ink`), amber only for alerts, deaths, the drift line and what needs the player; phase chip amber only in an open alert; trust deltas and LIE/PROMISE stay. A sparing red-orange `Danger` for deaths, a fault's final hours and a failed pod (user decision). A cool colour for ship-side info if needed. Buttons: WAKE primary-weight on a card, FILE an outline; the record's OPEN an outline (selected only when active). Panels slightly lighter (`Panel` ~18,28,33) with a 1-px inner top highlight. Contrast: raise `Dead`/`Faint` where used for text. Document the colour meanings in `Painter.cs`; search every colour use. Verify shots before/after.
4. **The plan fills its panel and loses its clutter (`feature/gfx-plan-layout`).** Fit by width and height, centre the hull; short fault labels "02 COOLANT LEAK 12 h" (user decision), no "OLD PATCH GIVES WAY:" prefix, CRITICAL as the diamond's thick border; a label collision pass (rows, no leader through text, bending leaders); section names in a corner, move labels not diamonds; remove the legend line (tooltips cover it); MUSIC as a small styled speaker button. A layout test that label rectangles don't intersect (worst case: a four-fault alert).
5. **The plan lights up (`feature/gfx-plan-light`).** Section glow by state (power, PowerLow flicker/dim, a soft amber wash on an open fault, critical pulse; locked doorway tint); a moving dot chain on the power bus that slows/dims at PowerLow, coolant crawl stops/greys when lost; pods breathe faintly, worn pods flicker; a hull glow outline, engine exhaust lines, hull ticks (a vessel moving right). `--tour-time <seconds>` freezes `Painter.Now` at a chosen phase for shots; all motion a function of `Painter.Now`; motion stops under a transition overlay; keep the plan under ~400 sprites a frame.
6. **Crew and issue cards (`feature/gfx-cards`).** Trust and morale as two thin segmented meters (dossier style) with numbers; band name in the tooltip; a dead card becomes a one-line strip (name, role, "dead", FILE) (user decision). Issue cards: no ".." truncation — cut at a word boundary with an ellipsis and move "Cause:"/"Known:" to the tooltip when space is short; full text in the tooltip. Empty states: "Nothing needs repair." gets a calm panel with the next suggestion; empty "WAITING ON YOU" collapses to a strip and gives its room to the crew list. A unit test for the truncation rule.
7. **Atmosphere (`feature/gfx-atmosphere`).** New `Backdrop.cs`: ~150 faint stars seeded by the run seed (each run its own sky), 2-3 depth layers drifting slowly left (faster in the drift transition, tied to the drift roll), soft 1-2 px dots at alpha 0.15-0.4, under the panels only, never behind text; brighten a little in Alert/Drift, dim in Bleak (read `MusicMood`, same 7 s fade); one radial vignette texture (~25% darker corners); a faint 20 px top gradient in panels. No scanlines (user decision). `Backdrop.Enabled` constant; off in the tour unless `--tour-time` is used.
8. **Ending, drift report, dossier, record (`feature/gfx-screens`).** Ending: the ending's name in `Big` centred over a small dim hull drawing (Landfall: lit hull and a planet disc; Adrift: the hull alone drifting; Quiet ship: a line of stars) (user decision), summary in `Body`, the crew as a row of small cards (glyph, name, alive/dead, age). Drift report: a small table with before/after bars (stores, parts, wear, alive), changes in accent, deaths in `Danger`; any click or key still continues. Dossier: portrait in a framed panel, a per-crew variation of the wireframe head if the code allows; fill or calm the empty middle column; selected person as an outline with an amber bar, not a solid block. Record: hover background on rows; quiet outline buttons.

Order: 1, 2, 3, then 4, 5, 6 (all in `ShipGame.Bridge.cs`, in order), 7 can run beside 4-6 (new file), 8 last.

## Risks
- Text legibility: font advances at another size differ from `SmallWidth`, breaking `Wrap`; keep layout widths from the layout-size fonts; test long lines at 0.8/1.2/1.6.
- Performance: sprite batch only; atlas bakes on resize can stall a frame — bake on demand, cache, debounce.
- Premultiplied alpha must match `Painter.Fade`; a wrong texture gives dark halos.
- The tour hides motion: steps 5 and 7 need `--tour-time`.
- Colour changes ripple: step 3 ends with a search of every colour use and a meaning table.

## Decisions (the user, 2026-11-14)
1. Scope: all eight steps, validating after each.
2. Render at real window size.
3. Smoothing: a feathered line texture; MSAA only if shots still stair-step.
4. Text: keep 15 px; header status numbers to 17 px and bright; revisit after seeing 1920x1080.
5. Amber means "needs you"; ordinary SHIP lines neutral.
6. A sparing red-orange Danger colour.
7. Dead crew cards become one-line strips.
8. Short fault labels on the plan.
9. Starfield and vignette, no scanlines.
10. The ending: a title with a ship drawing.

## Not in this plan
No change to simulation, prompts or balance. No new fonts or image assets (Share Tech Mono stays; everything drawn in code). No shaders or content pipeline. No settings screen.
