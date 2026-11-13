# UI plan: transitions, clarity, briefing, hover

Written 2026-11-13. A plan for the interface in `src/TheShip.Game`; nothing in it is built yet.
DESIGN.md stays the leading document for the game's rules. This file only covers how the interface shows them.

## Goals

1. Skipping time is visible: the player sees hours, weeks or years pass, and sees what changed.
2. The interface always shows what the player is doing: who hears the next line, who a repair goes to, what the next useful step is.
3. A run starts with a briefing popup that explains the scenario.
4. Everything clickable reacts to the mouse, and related things light up together.

## Where the interface stands (2026-11-13)

- Every clock move (`Voyage.Go`, `GoUntilAttention`, `Drift`) changes the screen in one frame. The only feedback is new log lines.
- `AdvanceResult.Reason` (why "until something happens" stopped) goes to the session log only.
- Hover exists on `Painter.Button` and on doors. Crew cards are clickable but do not react. There is no pressed state and no tooltip.
- Who hears a typed line is one dim line in the log header ("speaking to: ..."). Repair and Patch depend on the selected crew card and read
  "SELECT SOMEONE" when none is selected.
- The scenario is explained by three help lines in the log (`ShipGame.Start`).
- The interface is immediate mode with no animation state. The only time source in a draw is `DateTime.UtcNow` (the cursor blink and the hold note).

## Rules every step keeps

- Clicks are queued with `Do(...)` or `Act(...)` and run at the start of the next update, never inside a draw.
- No typed controls and no slash commands. The input line is only for speaking.
- Layout is in the 1600 x 900 space; colours come from `Painter`.
- The interface never says whether a statement is true, and adds no undo for spoken lines.
- The simulation stays in `TheShip.Core` and is not slowed down or stepped by the interface. Animations show a result that already happened.
- `--tour` screenshots stay stable: with `--tour` every duration is zero and no popup blocks the script.
- Each step ends with a `--tour` run and a look at the screenshots, an update of CLAUDE.md's Progress, and the validator.

## Order of work

One branch and one `ship-dev` agent per step. Steps 2 to 5 all change `ShipGame.Bridge.cs`, so they run one after the other.
Step 1 only adds a file and a few lines in `ShipGame.cs`, so it can run in a parallel worktree once step 0 is merged.

| Step | Branch | Depends on | Main files |
| --- | --- | --- | --- |
| 0 Foundation | `feature/ui-foundation` | nothing | `Painter.cs`, `ShipGame.cs`, new `Motion.cs` |
| 1 Briefing | `feature/ui-briefing` | 0 | new `ShipGame.Briefing.cs`, `ShipGame.cs` |
| 2 Time skips | `feature/ui-time-skips` | 0 | new `ShipGame.Transitions.cs`, `ShipGame.Bridge.cs` |
| 3 What you are doing | `feature/ui-clarity` | 2 | `ShipGame.Bridge.cs`, `ShipGame.cs` |
| 4 Hover | `feature/ui-hover` | 3 | `Painter.cs`, `ShipGame.Bridge.cs`, `ShipGame.Screens.cs` |
| 5 Polish | `feature/ui-polish-2` | 4 | `ShipGame.cs`, `Sounds.cs`, `ShipGame.Bridge.cs` |

## Step 0: foundation

Nothing the player sees changes much in this step. It adds what the other steps need.

- **Frame time.** `ShipGame.Update` keeps `_now` (seconds since start, from `GameTime`) and passes it to `Painter.Now`. The cursor blink and the
  hold note use it instead of `DateTime.UtcNow`.
- **Eased values** (`Motion.cs`). `Painter.Ease(string id, float target, float seconds = 0.12f)` returns a value that moves towards `target`
  at a fixed rate and is stored per id in a dictionary. Ids not asked for during a frame are dropped at the end of it. This is the only animation
  state for hover and highlight.
- **Tweens.** `Motion.Tween(from, to, t)` with an ease-out curve, and `Motion.Count(from, to, t)` for numbers that roll (year, stores, hours).
- **Alpha.** `Painter.Fill`, `Frame`, `Line` and `Text` take colours multiplied by an alpha; add `Painter.Fade(Color, float)`. Check that the
  sprite batch blend state draws them correctly (premultiplied alpha).
- **Overlay layer.** `ShipGame` gets `_overlay` (none, briefing, transition). While one is up, the screen below is drawn with `Painter.Click = false`
  and the mouse moved off screen (the trick `Shoot` already uses), then a dimming fill, then the overlay with the real mouse and click.
  Typed characters are dropped while an overlay is up, and Esc closes or skips it.
- **Tooltips.** `Painter.Tip(Rectangle r, string text)` records the tip of the rectangle under the mouse. `ShipGame.DrawScene` draws the
  recorded tip last, after 0.35 s over the same rectangle, wrapped to 40 columns, kept inside the screen.
- **Durations.** All durations are constants in one place (`Motion.cs`), multiplied by `Motion.Speed`. `--tour` sets `Motion.Speed` so that
  every duration is zero. `Settle()` in the tour also waits until no overlay is up.

Done when: the game looks the same as before, the tour's screenshots are unchanged, and a button's hover fades over 0.12 s as a proof of `Ease`.

## Step 1: opening briefing

A popup over the dimmed bridge when a voyage starts, before the player can act.

- **Layout.** A centred panel of about 900 x 560. Left column: the text. Right column: the first alert as a card (title, faults, clocks),
  so the player sees what they are about to walk into. One primary button, BEGIN. Esc and Enter also begin.
- **Text**, in this order and no longer than about 120 words:
  1. Who you are: the ship's AI, year 0 of 60, six people in cold sleep.
  2. What you have: a voice, the doors, the logs, and the choice of who to wake. You have no hands; people do the work.
  3. What a waking costs: 6 hours to thaw, 3 to 5 years of that person's life, at least three months awake, food from the stores.
  4. What you say is recorded, with who heard it.
  5. What arrival needs: at least 4 of the 6 alive and 3 of working age.
- **Numbers** come from `Tuning` and `Scenario` (`ThawHours`, `ThawAgeMin` and `ThawAgeMax`, `MinAwakeHours`, `StartStores`, `LandfallAlive`,
  `LandfallWorkingAge`, `ArrivalYear`), so the text follows the balance.
- **Wording.** The text never mentions lying or that a statement can be false. The prototype's pass test (DESIGN.md, "Prototype scope")
  needs a player who lies without being prompted.
- **Reopen.** A BRIEFING button in the bridge header, left of RECORD AND LOGS, opens the same popup with a CLOSE button.
- **Replaces** the help line "Wake someone from the crew list, then advance the clock. ..." in `ShipGame.Start`. The model line and the
  session-file line stay.
- **Tour.** With `--tour` the briefing does not open by itself. The tour opens it once, takes a screenshot named `briefing`, and closes it.
- **Session log.** `click` lines for "briefing begin" and "briefing reopened".

Done when: a new run opens on the briefing, BEGIN leads to the bridge with alert 1, and the briefing can be reopened.

## Step 2: time-skip transitions

The simulation still jumps at once. The interface takes a snapshot before the click runs, lets `Voyage` do the move, and then shows the
displayed numbers moving from the snapshot to the new state.

### How it works

- **Snapshot** (`ShipGame.Transitions.cs`, a record `Before`): clock, year, stores, parts, pod wear, the log's entry count, per crew member
  the state, age, hours awake and thaw time left, per fault the deadline, progress and state.
- **Start.** In `Act(...)`, inside `Run()`: for a click marked as a clock move, take the snapshot, run the action, then start a transition
  of the right kind. This is after a held click was released, so the transition never starts while a line is still being read.
- **While it runs.** The bridge draws displayed values (`Shown.Year`, `Shown.Stores`, `Shown.Deadline(f)` and so on) that are tweened from the
  snapshot. The log shows entries only up to the snapshot's count, so the outcome is not readable before the animation gets there.
  Clicks are blocked; a click or a key skips to the end.
- **End.** Displayed values equal the real ones, the held log lines appear, the overlay closes.
- Nothing in `TheShip.Core` changes. The session log is written as now, at the moment of the click.

### The three sizes

| Skip | Buttons | Length | What the player sees |
| --- | --- | --- | --- |
| Hours | ADVANCE TO NEXT EVENT | 0.5 s | The clock's day and hour roll. Fault countdowns run down, repair bars fill, a thaw countdown reaches zero and the pod changes state at the end. |
| Weeks | 2 WEEKS, UNTIL SOMETHING HAPPENS | 1.0 s | The day counter rolls, "AWAKE n d" counts up on the crew cards, stores count down. Then a banner over the log for 4 s, or until the next click, with why it stopped. |
| Years | DRIFT TO THE NEXT ALERT | 3 s, then two cards | See below. |

- **The stop banner** uses `AdvanceResult.Reason`. `ShipGame` keeps the last result. With an empty reason it says "Two weeks passed. Nothing needs you."
- **The drift**, in four beats:
  1. 0.4 s: the side columns and the log fade to 30%; the ship plan stays lit.
  2. 2 s: a large year counter in the middle of the plan rolls from the old year to the new one. Short horizontal streaks pass behind the
     hull. Ages tick up on the dimmed crew cards. Stores and pod wear count to their new values.
  3. The drift report as a card over the plan: years passed, stores before and after, pod wear, deaths. The text is the `DRIFT:` ship line
     already written by `Voyage.Drift`. CONTINUE closes it.
  4. The alert title ("ALERT 3 OF 5: NAVIGATION DRIFT") appears over the plan for 1 s, then the fault diamonds appear one by one,
     0.25 s apart, each with one pulse, while its issue card slides in. The panels fade back to full.
  A click during beats 1 and 2 jumps to the report card. The report card always waits for CONTINUE.
- **The last drift** (into the ending) plays beats 1 to 3 and then opens the ending screen.

Done when: each of the four clock buttons shows its transition, a click skips it, the stop reason is on screen, and a tour run takes the
same screenshots as before.

## Step 3: showing what you are doing

- **Next-step line.** One line under the header, left of the plan, written from state in a pure function (`NextStep(Voyage)`, with unit tests
  if it is moved to Core as text only). First match wins:

  | State | Line |
  | --- | --- |
  | Someone has an unanswered question | "Jonah is waiting for an answer." |
  | Open fault, nobody awake or thawing | "Nobody is awake. Wake someone from the crew list." |
  | Someone is thawing | "Mara thaws in 6 h. Advance the clock." |
  | Open fault, someone awake and idle | "Select Mara, then Repair or Patch." |
  | Work under way | "Work is under way. Advance the clock." |
  | Aftermath, someone cannot sleep yet | "Jonah can sleep in 42 days. Advance, or talk." |
  | Aftermath, someone refuses to sleep | "Mara will not go back into the pod." |
  | Aftermath, everyone awake can sleep | "Everyone can sleep. Ask them, then drift." |
  | Aftermath, nobody awake | "Everyone is asleep. Drift to the next alert." |

  It replaces the remaining help lines about controls.
- **Voice line.** The prompt names the hearers: `TO MARA >` with one person selected, `TO MARA, JONAH >` for everyone awake. With nobody
  awake the line is dimmed and reads "Nobody is awake to hear you." The "speaking to:" text in the log header goes.
- **Selection.** The selected crew card gets a 4 px accent bar on its left edge and a line "you speak to Mara alone; repairs go to Mara".
  The person's marker on the plan gets a ring. A selected person who falls asleep or dies is deselected.
- **Repair and Patch.**
  - With someone selected the button reads `REPAIR: MARA (ENG 3 OF 2)`, so the skill and the need are visible before the click.
  - With nobody selected the buttons stay enabled and open a small picker next to the card: one row per awake person with their skill in
    that fault ("MARA  ENG 3", "JONAH  ENG 1"). A click assigns that person. With nobody awake the buttons are disabled with the tooltip
    "Nobody is awake."
- **Header numbers.** Stores, parts, pod wear, power and course become five separate labelled chips. Each takes the warning colour by itself.
- **Change flashes.** When a shown number changes, the chip or card line flashes bright for 0.2 s and shows the delta ("-42") for 1.5 s.
  This covers stores, parts, pod wear, and trust and morale on the crew cards.
  A trust delta is shown only once the crew line or ship line that explains it is in the log, so a drop never tells the player a moment
  early that a statement was found out.
- **Buttons that cannot be used** say why in a tooltip: the "in 42 d" sleep button ("Three months awake is the minimum before a pod takes
  them again."), DRIFT in an alert ("An alert is open."), WAKE at three awake.
- **Doors.** The hover frame gets a tooltip with the door's state and what the next click does: "Closed. Click: lock."
- **Waiting on you.** Each item is clickable and selects the person it is about.
- **Held clicks.** While a line is being read, the clicked button shows a small waiting mark, in addition to the note on the input line.

Done when: at any moment of a run the next-step line names something the player can do, and Repair can be done with or without a selection.

## Step 4: hover effects

- **Buttons.** Hover fades in with `Ease`. A pressed state (mouse down inside) draws the button one shade darker. The cursor becomes a
  hand over anything clickable (`Mouse.SetCursor(MouseCursor.Hand)`), set once per frame from what `Painter` saw.
- **Crew cards.** The frame brightens on hover, as on buttons.
- **Cross-highlighting.** `ShipGame` keeps `_hotFault` and `_hotCrew`, set by whatever is under the mouse this frame and used in the next.
  - Over an issue card: its diamond grows and brightens, and its section outline is drawn bright.
  - Over a fault diamond on the plan: its issue card gets a bright frame.
  - Over a crew card: that person's pod and marker on the plan are drawn bright.
  - Over a pod or a marker on the plan: the crew card gets a bright frame. A click on a pod or marker selects the person, as a card click does.
- **Tooltips.**
  - A section: its name, the door state, who is there, the faults in it.
  - A pod: the name, the state, thaws so far.
  - Header chips: what the number means ("Stores: person-days of food. One awake person eats one a day.").
  - Trust on a crew card: the band's behaviour (the `manner` text from the dossier).
  - Coolant and power lines: what they are.
- **Legend.** Once the tooltips cover it, the two legend lines at the bottom of the plan are removed and the plan gets that room back.
- **Record and dossier.** Rows in the ship log list and the statement list get a hover background; the dossier's person buttons already
  use `Painter.Button`.

Done when: every clickable thing reacts within one frame of the mouse arriving, and hovering either end of a pair lights the other.

## Step 5: polish

- **Screens.** A 0.15 s cross-fade between bridge, dossier and record. The ending fades in over 1.5 s: title first, then the three columns.
- **Motion on the plan.** A thawing pod pulses. A fault diamond pulses, and faster once under a quarter of its clock is left. The coolant
  dashes crawl slowly. All of it stops while a transition overlay is up.
- **Log.** A new line fades in over 0.3 s. Lines added since the last clock move have a thin marker in the margin.
- **Sound** (`Sounds.cs`, made in code like the click): a two-note chime for a new alert, a low sweep for a drift, a short low tone for a
  refused request or a disabled button. Volumes sit next to `ClickVolume`.

## Decisions taken in this plan, for the user to confirm

1. **Repair without a selection** opens a picker of awake crew with their skill. Selecting a card first still works.
2. **Trust deltas** are shown, but only after the line that explains them is in the log.
3. **The drift transition** is 3 seconds and skippable, and the drift report always waits for CONTINUE.
4. **The briefing** never mentions lying.

## Not in this plan

- No change to the simulation, the model's prompts or the balance.
- No settings screen (animation speed, sound volume). `Motion.Speed` makes one possible later.
- No keyboard navigation of buttons.
- No new voyage button (it needs a new `ShipMind` and worker; see CLAUDE.md).
