# CLAUDE.md

The Ship: you are the AI of a sleeper ship and keep the crew alive by choosing who to wake and what to tell them.
C# / .NET 10, MonoGame DesktopGL, a local LLM through LLamaSharp (same stack and versions as the Cities repo).

**Read DESIGN.md first.** It is the leading design document: make design changes there. Keep this file's Progress section up to date:
add a line for every non-trivial change, and move items between "Built" and "Not built yet".

## Commands

```sh
dotnet build TheShip.slnx
dotnet test
dotnet test --filter Category=Balance   # balance harness: bots play 200 seeds, prints a table, writes balance-report.txt
dotnet run --project src/TheShip.Game -- [--seed <n>] [--llm <file.gguf>] [--gpu-layers <n>]
```

- The game needs a GGUF model in `src/TheShip.Game/Content/Models/llm/` (or `--llm`). Without one it shows what is missing and starts no voyage.
- Models: see the README.txt in that folder.

## Layout

| Project | What it holds |
| --- | --- |
| `src/TheShip.Core` | The simulation. No dependencies. Owns every state and outcome: crew, facts, statements, faults, time. |
| `src/TheShip.AI` | The language model: `ILlmProvider`, `LocalLlm` (LLamaSharp), `LlmWorker`, GBNF grammars, prompts, `ShipMind`. |
| `src/TheShip.Game` | MonoGame window: the bridge (ship plan, issues, crew, log), crew dossiers, the record and ship logs. `Painter.cs` is the drawing kit. |
| `tests/TheShip.Tests` | xunit tests of the simulation and the reader. |

## Analysing the user's play sessions

Every run writes `src/TheShip.Game/bin/Debug/net10.0/win-x64/Sessions/<date>_<time>.log` (`SessionLog.cs`): a plain-text record with a
real-time stamp and a tag per line. The game's opening note gives the file's path.

| Tag | What it records |
| --- | --- |
| `start`, `quit` | seed, model and backend; whether the voyage had ended |
| `state` | after each clock move: year, phase, stores, parts, pod wear, the alert's faults, every person's state, age, trust, morale, days awake, thaws, beliefs, caught lies, doubts |
| `Y<year> ship/crew/you/recorded/help` | every ship log line; a crew line shows its template and the game's decision |
| `voiced` | the model's wording of that crew line, or that it was rejected and the template stayed |
| `click` | every click that changes the game: wake, sleep, repair, patch, advance, drift, doors, logs |
| `advance` | the result of "advance until something happens": steps taken and why it stopped |
| `said`, `read` | a line said when Enter was pressed (with its hearers); the model's reading applied to it a moment later (`unusable ... plain talk` when the answer could not be used). Whether a statement was true appears only in the `recorded` line of the log |
| `model` | the model's raw reading, rejected crew lines, yes/no judgements, failures |
| `llm` | each model call: kind, prompt and generated tokens, first-token and total seconds |
| `ending` | the ending, the crew (living and dead), beliefs and the record |

## Working with Claude (rules taken from the Cities repo, 2026-11-12)

- **Branches.** Work happens on branches (`feature/<topic>`, `fix/<topic>`) in git worktrees; `main` only receives merges. The main
  session merges, builds and runs the tests on `main` after each merge. Push only when the user asks.
- **Commits.** Commit after every change that builds and passes its tests, one commit per change, descriptive message. Update this file's
  Progress section, DESIGN.md (design changes) and README.md in the same commit. Stage files by name.
- **Models.** The main session runs on Opus. Sub-agents run on Sonnet unless their type says otherwise: `ship-bugfix` is Opus,
  `ship-validator` is Fable.
- **Sub-agents.** Use the agent types in `~/.claude/agents/`: `ship-planner` (read-only plan), `ship-dev` (feature, own worktree and branch),
  `ship-bugfix` (reproduce, root cause, regression test), `ship-validator` (checks a finished branch, runs the full suite, fixes warnings).
  Name each agent after the thing it works on (the Agent call's description) and report by that name. A multi-step plan is done one
  sub-agent per step, in order; tasks that touch mostly different files may run in parallel worktrees. Sub-agents may start their own
  sub-agents if they stay on the same branch.
- **Tests.** Dev and bugfix agents run targeted tests (`dotnet test --filter`); the validator runs the full suite.
- **Validation.** Dev/bugfix branch, then `ship-validator`, then the merge. With several tasks running, validate once all are done; fix what
  it finds with `ship-bugfix` agents without asking, then validate again.
- **The real model.** Before loading it (probes, runs), check no `TheShip` process is running (the game's exe is `TheShip.exe`): the user may be playing.

## Rules for the code

- The game owns the truth. The model writes crew lines and reads player lines; it never changes state.
  The one exception is the yes/no judgement of a free-text statement against evidence (DESIGN.md, "Reading").
- The game does not run without a model (decided 2026-11-12). What is left of the model-less path is for unit tests and safety nets only:
  `RuleReader.Read` drives the voyage in Core tests, `RuleReader.Claims` checks a model-written crew line against what the speaker knows,
  `Lines.cs` is shown when the model's line was rejected twice, and `tests/TheShip.Tests/FakeLlm.cs` stands in for the model in `ShipMind` tests.
- Model calls go through `LlmWorker` (one at a time, most urgent first); results reach the simulation through `MainThreadQueue`.
- Structured model output is forced with a GBNF grammar, and every string in a grammar is bounded.
- Numbers that tune the game live in `Tuning.cs`.
- No typed controls. Every control is a button or a click; the input line is only for speaking. When a control gets a button or
  another UI option, its text command is removed in the same change (the user's standing rule, 2026-11-12). Don't add slash commands.
- A line is said when it is typed (`ShipMind.SayAtOnce`: it is in the log at once, marked unread) and read by the model right after;
  the reading is applied with `Voyage.Say` when it arrives, in typing order, and an unusable reading is plain talk. There is no
  confirmation step and no picker (decided 2026-11-13). What the player sees never says whether a statement is true. Don't add an
  undo for spoken lines: it lets a failed lie be taken back.
- The interface is laid out in a 1600 x 900 space and scaled to the window (`ShipGame.View`). Colours come from `Painter`. Clicks are
  queued with `Do(...)` (or `Act(...)` for a click that changes the game, so the session log records it) and run at the start of the
  next update, never inside a draw.

## Progress

Scope of the first build is the prototype in DESIGN.md ("Prototype and open questions"): Short haul, text only,
6 crew, 60 years, 5 scripted alerts.

### Status

- 2026-11-12: repository scaffolded and the prototype written (Core, AI, Game, tests) in a cloud session with no .NET SDK:
  **nothing has been compiled or run yet.** The first job in a session that has `dotnet` is `dotnet build TheShip.slnx`,
  then `dotnet test`, and fixing whatever they report.
- 2026-11-12: DESIGN.md gained a "Run variation" section under "Gameplay variations" (what the seed varies per run).
  Design only; outside the prototype scope.
- 2026-11-12: DESIGN.md is now the leading design document. The earlier claude.ai document is no longer the source.
- 2026-11-12: the first version builds and runs on the user's machine (their report).
- 2026-11-12: the no-model mode is removed (`--no-llm`, `ScriptedLlm`, the `IsReal` switch). Written without a compiler again: build and run the tests.
- 2026-11-12: an interface design exists as a claude.ai Design canvas (https://claude.ai/artifact/4MRSyehpU9o9mcJ6G9QZND): a wireframe ship plan with doors,
  lines, faults and crew; crew dossiers; the record and ship logs. Not implemented in the game yet.

- 2026-11-12: the interface from the design canvas is built in the game: `ShipPlan.cs` (sections, doors, coolant and power lines, pods) in Core,
  doors that lock (a locked door stops a repair and blocks a crew member checking evidence behind it, at a trust cost), and the three screens.
  All typed controls are removed. A line is now read before it is said, with a picker for the reading; the take-back (`Voyage.Reread`) is gone.
  Written without a compiler: build, run the tests, and look at every screen.
- 2026-11-12: "stuck on the model is reading it" was the first model call paying a one-time Vulkan shader compile (about 45 s with no
  driver cache; 0.4 s per reading afterwards, measured with Gemma 4 E4B). The loading screen now runs `ShipMind.WarmUpAsync`
  (one reading, one crew line, one yes/no) after the model loads, so that cost is paid before the voyage starts. Builds; 27 tests pass.
- 2026-11-12: every run writes a session log (`SessionLog.cs`, Sessions/ next to the exe; see "Analysing the user's play sessions").
  Clicks that change the game go through `Act(...)`, which logs them; `ShipMind.Trace` reports the model's raw answers.
- 2026-11-12: the working rules (branches, commits, models, sub-agent types `ship-*`) are taken over from the Cities repo.
- 2026-11-13: witness follow-ups: crew lines in a step that can kill (time passing, a drift) are held and said only by those alive after it (after the drift report); the dead never speak and `ShipMind` skips voicing a line whose speaker died while it waited; a quiet (drift) death is witnessed silently. A dodge stores the fact's truth at dodge time and is charged again only if it hid what is learned. A seen death gives a `witnessed` line (Lines, Prompts.Feeling), not `found`. Tests in `WitnessFollowupTests`.
- 2026-11-13: claim reader edges (`RuleReader`): fact and value cues match whole words only (a plain ending allowed on cues of four letters or more), a naming with no value of its own is claimed with every value in its sentence (both readings of a denied one; conservative), names joined through an article share a value, and a negation counts past small words ("not at all", "no longer") and through "neither ... nor" to the end of its clause. Tests in `ClaimReaderEdgeTests`.
- 2026-11-13: balance harness: a Thrifty bot (honest, wakes only for the most urgent fault, patches the rest with whoever is up), all bots staff several faults per alert, and new metrics (faults lost, patches, thaws per alert, std dev of survivors/ages/thaws, distinct scenario signatures). Tests only; `Fault.Critical` hook is `BalanceBot.Critical`.
- 2026-11-13: side faults (feature/side-faults, on top of a2-illness): alert 3 = navigation drift (Nav 2, 2 people, clock 6+12+slack) plus a side fault drawn per seed from hydroponics blight (Botany 2, 40 h, stores x0.6 when lost, section hydro) and power sag (Engineering 2, 14 h, reactor, `PowerLow` when lost; Critical wherever it appears, so alert 3 can hold two Critical faults), swapped or dropped when nobody alive has the skill; alert 5 = pod controller (clock 6+4+slack) plus power sag always. Max 4 faults per alert (returning patches first, the side fault is dropped) and at most one drawn Critical except alert 3 with a sag; a returning patch keeps its Critical flag even beside it. When more patches are due than fit, Critical first then shortest clock; the rest stay outstanding (`_patched`) for the next alert or the last drift. `Fault.Again(id, name, slack)` clock = max(half, thaw + hours/PatchRate + slack) (a returned patched illness can be patched again at once). A lost illness says `left_behind` only when the clock killed the patient; a patient who dies otherwise drops their illness fault and its returning patch; the alert report and the fault line name the patient ("Fault: Sleeper illness: Tomas"; the card says "Patient: Tomas"); a patched illness that kills at the last drift is in the drift report's Deaths. Issue cards: Critical tag, "earliest done if started now" (`Voyage.EarliestDoneText`), compact cards from 2 open faults, fault diamonds in one section sit side by side. The Careless bot no longer patches a Critical fault while a part is left. 200 runs per bot, before (a2-illness head) -> after (Landfall / survivors / faults lost per run): Honest 95.0% / 4.20 / 1.07 -> 88.5% / 4.09 / 2.55; Careless 51.5% / 2.35 / 2.03 -> 85.0% / 3.25 / 2.74; Liar 31.5% / 2.82 / 1.99 -> 19.0% / 3.15 / 3.26 (Adrift 47% -> 71.5%); Thrifty 91.5% / 4.16 / 1.12 -> 69.0% / 3.35 / 3.21 (loses side faults: low-power deaths 0.95 per run). Why: the Honest drop came from the side fault (when the sag was non-critical it lost to the pod patch for the last part, and low power then killed sleepers); an earlier Liar collapse (1.5%) was largely the bot re-waking people who had refused, so bots now remember refusers per kind of work and prefer people already up or with trust 30+ over higher skill. Also: compact fixed-height issue cards (`IssueCardLayout`, four always fit), plan labels clamped inside the panel, the card line says when only a patch can make it, the illness is chosen before the victim dies, a repaired illness stays in history when its patient dies. Tests: `SideFaultTests`, `AlertFeasibilityTests`.
- 2026-11-13: alert 2 gets a sleeper illness (feature/a2-illness): fault `illness` (Medicine 2, 12 h, Critical, the drawn patient, in the pod bank, exposure `alive:<patient>`; the medic who treats also learns the victim is dead). Untreated, the patient dies ("died of the illness in their pod") and each awake crew member says `left_behind` (`Tuning.LeftBehindTrust` 4, plus the morale of learning a death). Alert 2 clocks are thaw 6 h + work + the run's slack (pod 10+, illness 18+ hours). `Variation.Classic` patient is now Tomas (Mara as patient killed every older test that sleeps her through alert 2). Balance bots patch a minor fault to keep a part for a waiting critical one (`BalanceBot.ReserveParts`). 200 runs per bot, before -> after (Landfall / survivors / faults lost per run): Honest 97.0% / 4.24 / 0.00 -> 95.5% / 4.23 / 1.07; Careless 90.0% / 3.43 / 0.00 -> 69.0% / 2.86 / 2.03; Liar 53.0% / 2.74 / 0.33 -> 33.5% / 2.94 / 2.01; Thrifty 98.5% / 4.28 / 0.00 -> 92.0% / 4.17 / 1.12. Without the part reserve the Honest bot lost the patient in 68% of runs (the pod repair took the last spare part). Waking the patient does not cure the illness (they die awake at the deadline, "died of the illness"). Table after that rule: Honest 95.0% / 4.20, Careless 51.5% / 2.35, Liar 31.5% / 2.82, Thrifty 91.5% / 4.16 (the tables above are before it). 9 new tests (`IllnessTests`).
- 2026-11-12: "Advance until something happens" in the aftermath (`Voyage.GoUntilAttention`, `Voyage.Advance.cs`): runs two-week steps until a crew line, a person who can go back to sleep, a death, a lost fault, differing accounts or the stores running out; at most `Tuning.AdvanceMaxSteps`; the clock does not move when nobody is awake or everyone awake can already sleep. The clock box has it as the main button, with a small "2 WEEKS" for the old step. Session log tag `advance`. 7 tests in `AdvanceTests.cs`.
  Merged with the stores follow-ups (validate/followups-advance-balance): "can go back to sleep" is `Voyage.CanSleepNow`, so the clock also
  stays put when the stores are too low for everyone awake, and the aftermath's early-sleep line stops the advance. 9 tests.
- 2026-11-12: running out of stores kills (fix/stores-run-out). Before, a drift with a refuser awake took the stores below zero and only
  cost 40 health, so Mara lived through 8 years on no food (session 2026-11-12_23-13-57). Now the awake eat one person-day each per day
  in time order (`Voyage.Eat`, used by the two-week clock and the drift); once the stores are empty, `Tuning.StarveDays` (60) hungry days
  kill, through the usual death path. The drift report says "Stores ran out in year X. Mara starved in year Y."; a refusal to sleep
  is followed by a SHIP line saying the refuser stays awake through the drift and how long the stores last. 3 regression tests.
- 2026-11-12: log and record fixes from a play session: altering a log entry back to the same false value is one lie, not one
  per click (a reader is caught once); nobody reads log entries about alerts that haven't happened or about their own life;
  a log entry that changed since someone read it is unread again; "the spare parts are gone" at 0 parts; the ending lists
  the whole crew ("crew:" in the session log), with an unread log rewrite marked "Nobody read it.".
- 2026-11-12: stores follow-ups (fix/stores-followups). Shorter wakings: when the stores can't feed everyone awake through a person's
  three months (empty, or too low), the minimum is waived and they can be asked to sleep early (`Voyage.CanSleepNow`, the SLEEP button,
  a SHIP line in the aftermath); the Wake click warns when the stores are empty or too low for a full waking. Before, every waking
  onto empty stores was fatal (60 hungry days < 90-day minimum). A reader who read the same false log text twice (a rewrite put back
  after someone else found it out) is caught once, not twice. Repair work is credited to a worker only until they starve. The
  refusal warning says when the hungry days fall short of the 60 that kill, and names who starves. 5 regression tests.
- 2026-11-12: dedupe and early-sleep fixes (fix/dedupe-and-early-sleep). The once-per-text log catch now only merges identical log
  statements caught in the same finding-out; a reader who caught a rewrite, believed it again when it was put back and finds out
  again is caught again (trust, CaughtLies, crew line). The early-sleep offer has its own flag (`EarlySleepAnnounced`), reset when
  the offer lapses, so the three-month line still comes (and stops the advance) after the stores last again. 2 regression tests.
  Follow-up: one rule for every path into a log finding-out (`Voyage.FindOutLogText`, used by `DiscoverLie` and the engineer's
  spot): it marks every unfound copy of that text the reader read, so an engineer spotting a rewrite put back is no longer also
  charged for the older copy. 1 regression test (seed loop over the 0.6 spot roll).

- 2026-11-12: balance harness (`tests/TheShip.Tests/BalanceBots.cs`, `BalanceTests.cs`, `[Trait("Category","Balance")]`): honest, careless and liar bots play whole voyages over seeds 1..200 through the public `Voyage` API and print a table (Landfall rate, endings, deaths, causes, ages, trust, lies) to the test output and `balance-report.txt`. Run: `dotnet test --filter Category=Balance` (about 5 s). Measures only; the 25-75% honest Landfall target test is skipped until tuning. Builds; passes.
- 2026-11-13: DESIGN.md caught up with the prototype: 6 crew, alert 5 as built (pod controller fault with a refusal to sleep),
  the ship plan in scope, the clock's advance buttons, and the user's balance decisions as settled questions (Short haul Landfall:
  4 of 6 alive and 3 under 50; 2 starting parts, a failed repair spends one; a nav fault patched at arrival slips arrival).
  The 8-minute first run and the open low-trust question are playtest questions. Design text only.
- 2026-11-13: crew voice (feature/crew-voice). Model-worded crew lines were short and alike. Each of the six has a `CrewSheet` (`Scenario.Sheets`, `CrewMember.Sheet`): how they talk, background, quirks and bonds in words ("Idris is your partner"). `Prompts.CrewSystem` carries the sheet; `Prompts.CrewUser` carries the last 5 log lines (this person's own lines and the ship's, each cut to 130 chars) and the feeling of the decision kind. The rules now ask for 1-3 sentences with feeling, spoken to the ship, no stock filler. `ReplyMaxTokens` 90 -> 140, `Acceptable` limit 320 -> 480 chars (`Prompts.CrewLineMaxChars`). Crew prompt grew from about 200 to about 430 tokens (Gemma 4 E4B), 0.4-0.8 s per line. Invented memories are not saved ("Personal colour" stays open). 5 tests in `CrewVoiceTests.cs`; `CrewVoiceProbe.cs` is a real-model before/after probe that does nothing unless SHIP_PROBE=1.
- 2026-11-13: balance step B3 (parts and patches that bite): `StartParts` 2, a failed repair spends a part too, patches return from alert 2 on, the doubled "Old patch gives way" prefix is fixed, and a patch still outstanding at the last drift takes its consequence (controller: +4 wear and `PatchFailDeathChance` per sleeper; nav: arrival slips `NavPatchArrivalSlipYears`, 9). Balance harness (200 seeds, after the validator fixes below): Landfall honest 96.5%, careless 85.5%, liar 61.5% (30.5% Adrift); tuning continues in later steps.
- 2026-11-13: B3 validator fixes: the refusal warning and the drift share one end year (`DriftEndYear`, arrival slip included), so the warning no longer says the stores last when the slipped drift starves the refuser; sleepers killed by the controller patch die of it, not "on low power" (the harness's cause table splits them); a repair under way needs a part to finish or redo, and stops with none left (`work_no_parts`); crew say "the coolant leak again" for a returned fault (`Fault.CrewName`); Adrift wording covers a returned nav patch; `Tuning.cs` doc comments untangled.
- 2026-11-13: the window starts maximized (`WindowState.Maximize`, SDL_MaximizeWindow on the SDL window after `Initialize`);
  checked on Windows with `IsZoomed`. The view rescales as with a manual resize. Also: the game's process is `TheShip`, not
  `TheShip.Game`; the real-model rule above now says so (earlier checks looked for the wrong name).
- 2026-11-13: crew-voice validator fixes: a player line reaches only the prompts of those who heard it (`LogEntry.Hearers`, set in `Voyage.Say`; it leaked "Idris is dead" to a crewmate who wasn't spoken to); the tone toward the ship follows the trust band (`Prompts.Attitude`, `Prompts.CivilTone`, a tone line next to "You feel:"; dodge/gap/locked puzzle rather than anger at follows/wants reasons; Mara's and Jonah's swearing is at the work, not the ship); a bond to someone the speaker believes dead is in the past tense; `work_no_parts` feels like `work_failed`; a failed repair after another took the last part no longer says it used it. 8 new tests; the probe runs Mara at trust 75, 45 and 20.
- 2026-11-13: lines are said at once (feature/say-at-once). Enter puts the line in the log as YOU with a "..." marker (`Voyage.BeginSay`), `ShipMind.SayAtOnce` reads it and applies the reading with `Voyage.Say` when it arrives, one line after the other in typing order; an unusable reading is plain talk. The reading picker, the "NOT YET SAID" preview and `IsReading` are removed; session-log tags are now `said` and `read`. The log's "Recorded: ..." line no longer shows "(true)/(false)" to the player (`LogEntry.Secret`, session log only). Builds; the suite passes.
- 2026-11-13: a said line counts when it was said, not when its reading lands (fix/say-at-once-timing). While a line is being read, `Act(...)` clicks wait in `ShipMind.AfterReading` and run in order right after it is applied (Sleep, Advance, Repair after answering, the last drift); screen clicks still run at once. A reading that takes over 5 s (`ShipGame.ReadHoldSeconds`) is applied as plain talk and frees the clicks. The read prompt is built on the game thread at Enter (the second queued line no longer reads voyage state from a pool thread). `Voyage.Say` returns whether the line was heard; the session log's `read` tag says when it was dropped. Regression tests in `SayAtOnceTimingTests`; 119 pass, 1 skipped at the time (147 after the 2026-11-13 merge).
- 2026-11-13: run-variation plumbing (`Variation.cs`, branch `feature/variation`): a seed draws the alert-2 victim and patient, side faults for alerts 3 and 5 (alert 5 always power sag), clock slack and year jitter from its own generator; `new Voyage(seed)` stays Classic, the game and the balance harness use `Variation.Draw(seed)`. Applied so far: alert years and the alert-2 victim; the rest is stored for later steps. `Fault` gained `Critical` and `SubjectId`. The session log's start line lists the variation.
- 2026-11-13: balance bots replace a refuser (fix/refuser-and-pronouns): a person who refused a fault no longer counts as staffing it, and another awake or wakeable person who qualifies takes the place (before, careless and liar bots ended Adrift whenever the second helm hand refused). Balance harness (200 seeds): Landfall honest 97.0%, careless 90.0% (0% Adrift), liar 53.0% (16.5% Adrift), thrifty 98.5%. Tests only.
- 2026-11-13: crew lines can't state a life through a reference (fix/refuser-and-pronouns). `Acceptable` checked a life claim only when the line named the person, so "He's fine, I assume.", "He is dead. You killed him." and "My partner is dead, isn't he." passed from a speaker who knew nothing. `RuleReader.PersonClaims` resolves he/she/they, "killed/lost him" and "my/our/the <relation>" to the person most likely meant (same line, decision or pending question, recent lines, partner; bonds then role for relation words; `CrewMember.Pronoun`) and checks the life word after a linking verb against the speaker's beliefs, via a `LineContext` snapshot (`Prompts.Recent` is shared with `CrewUser`). Unused `Scenario.Crew(Variation?)` parameter removed. `CrewLineReferenceTests.cs`.
- 2026-11-13: say-at-once and reference-check follow-ups (fix/read-timeout-and-pronoun-false-rejects, validator findings). The read timeout now runs from when the line's model call starts (`LlmWorker` start callback in `ShipMind.Ask`), with a backstop from Enter of the timeout times (lines ahead + 2); on expiry the call is cancelled through the worker's token, so a hung reading no longer keeps the lines behind it waiting. `ShipMind.Pump` keeps going past a step that throws (traced, the first rethrown after). The bridge shows "reading your line" at the end of the input line while clicks are held, and ignores a second identical Advance/Drift click held behind the same reading (`ShipGame._held`). `RuleReader`: hedges (if/unless/whether, don't tell me, I think/fear, maybe, probably, could/would/might have, nearly, almost) state nothing, in `PersonClaims` and per sentence in `Claims`; "lost her trust" is a possessive; "gone (back) to sleep" is not "gone"; a pronoun resolved only from the recent lines is weak (`PersonReferences`, `PersonClaim.Weak`) and rejects a line only against the opposite belief. The partner fallback stays strict (it is the original "He's fine, I assume." case). Questions are now skipped inside `PersonReferences`, so "Asha? She is dead." keeps its referent. Regression tests in `SayAtOnceTimingTests` and `CrewLineReferenceTests`.
- 2026-11-13: hedged life and fact claims count as claims (fix/read-timeout-and-pronoun-false-rejects, third validator finding; main session decision). Two rounds of hedge rules in `RuleReader` (a clause-boundary list, then a hedge directly governing the subject) each let plain claims through ("You shouldn't have killed him.", "I'm afraid Idris is dead.", "He's dead whether you admit it or not."). The hedge acceptance is removed from `Claims` and `PersonReferences`: a life or fact value the speaker doesn't know is rejected whatever wraps it, questions ending in "?" stay free, and the f270ae2 fixes stay (possessive "her trust", "gone back to sleep", the weak recent-line tier, questions skipped in `PersonReferences`). `Claims` now checks every naming of a fact in a sentence, each with the value after it ("Idris is fine but Asha is dead"). The crew prompt gains "Don't say or guess whether anyone is alive, dead, safe or hurt unless it is in your list; ask the ship instead." Tests: `A_claim_wrapped_in_any_hedge_is_rejected` (84 lines), `A_hedged_life_claim_is_rejected_without_the_belief` (conservative by design, 60 lines), `..._accepted_with_the_belief`, `Claims_reads_every_naming_of_a_fact_in_a_sentence`, and the ordinary-lines test with the validator's claim-free lines.
- 2026-11-13: issue card text (fix/issue-card-text, validator findings). The compact cards cut off the verdict and always cut the danger/exposure line. Card height now depends on how many are shown (`IssueCardLayout.TextLines`: 6 text lines with 2-3 cards, 3 with 4; `Split` gives the verdict its own line, needs 1-2, the workers line, danger/exposure up to 2). `Voyage.EarliestDoneText` is a short one-line verdict: "Done in 9 h", "Done in 18 h - patch only: 12 h", "Done in 20 h - too late", "Patch done in 6 h", and with no spare parts "Patch only (no parts): 12 h" (it quoted a repair time before); the full card shows it after "If started now:". Cards name a returning patch "Returned: coolant leak" (`IssueCardLayout.CardName`); the log keeps "Old patch gives way: ...". Alert 4's report says "Nothing needs repair." only when nothing returned, else names the patches that give way. The vacuous critical-first assert in `SideFaultTests` is a real one. Tests: `IssueCardTextTests` (verdict and card name fit one line, 7.19 px/char estimate, for every fault in 200 drawn runs; no-parts verdict; line split; returning name; signal report).
- 2026-11-13: pod wear can kill (feature/b4-pod-deaths, balance step B4). `Tuning.ThawFatalShare` 0.3: 30% of failed thaws kill outright ("is not breathing", cause "did not survive the thaw"), the rest injure as before; `Tuning.SleepDeathPerWearYear` 0.0001: in `Drift()` each sleeper dies with `Voyage.SleepDeathChance(wear, years)` = wear x years x rate ("died in sleep, pod failure", in the DRIFT report's deaths). The new rolls come after the existing ones (a thaw failure rolls once more; each sleeper rolls once more per drift when wear > 0), so seed outcomes shift; tests that depended on seed 1's path now pick a seed that survives (`SideFaultTests`, `PartsAndPatchesTests.ToTheLastDriftOrNull`). Balance (200 seeds), before -> after: Honest Landfall 88.5% -> 70.0%, survivors 4.09 -> 3.49; Careless 85.0% -> 54.0%, 3.25 -> 2.59; Liar 19.0% -> 15.0%, 3.15 -> 2.72; Thrifty 69.0% -> 51.5%, 3.35 -> 3.06. Extra deaths per honest run: thaw 0.40, pod failure 0.28 (Honest causes: scripted pod 1.00, low power 0.45, thaw 0.40, pod failure 0.28, starved 0.18, patch 0.16, illness 0.06). Thrifty (9.7 thaws, wear 16.7 vs 13.2 / 20.2) loses less in absolute deaths from the new rules but still trails Honest through low-power deaths and fewer fixes. `PodDeathTests.cs`.
- 2026-11-13: balance step B2 (feature/b2-questions): ignored and dodged questions cost. A pending question still open after an aftermath beat is asked again (`ask_again`, `Tuning.QuestionIgnored` 2 trust, at most `QuestionRepeatsMax` 2; the second repeat adds the fact to `Doubts`); going to sleep with one pending costs `QuestionIgnoredAtSleep` 4; a dodge no longer clears the question (its 3 trust is charged once per question, still a doubt), so a "not until you answer me" refuser keeps refusing until a statement answers; `Learn` clears a pending question the person settles themselves. `PendingQuestion` resets `QuestionRepeats` on change. `GoUntilAttention` already stops on any crew line, so it stops on `ask_again`. The dodge charge is a per-asking flag (`CrewMember.DodgeCharged`, reset when `PendingQuestion` changes), so a re-asked question at a later waking charges its first dodge again. `Assign` resets the idle warning only when work actually starts (a refused Repair no longer keeps Advance from moving the clock). Bots: the Careless bot dodges once in an alert and until asked again in the aftermath, then answers; but its dodge adds a doubt and the doubt check settles the question in the next beat before `ask_again` can fire, so Careless never reaches `ask_again` (0 per run). A fifth bot, Silent (honest, but in an alert answers only what blocks work: a danger question or someone who would refuse; ignores every question in the aftermath and grants no requests), exercises `ask_again` and the sleep cost. The report has an "Asked again / refusals" row. 200 runs per bot, before -> after (Landfall / survivors / end trust): Honest 88.5% / 4.09 / 61.9 -> 89.0% / 4.10 / 62.0; Careless 85.0% / 3.25 / 41.5 -> 85.0% / 3.40 / 58.8 (the old bot re-dodged the same question up to four times a pass at 3 trust each, no longer charged; a bot that dodges everything and never answers collapses to 2.5% because refusers never work); Liar 19.0% / 3.15 / 47.7 -> unchanged; Thrifty 69.0% / 3.35 / 61.0 -> unchanged; Silent (new) 83.5% / 3.35 / 56.5, 6.1 ask_again per run. Tests: `UnansweredQuestionTests`.
- 2026-11-13: deaths outside the pods are witnessed: `Kill` teaches every awake person `alive:<id>` = dead as evidence when the dead was awake or thawing (thaw deaths, starvation, repair injuries, an awake illness patient; `LeftBehind` keeps its own line). Evidence answering a pending question settles it (`Learn`). Drift starvation dies in time order. A fatal thaw no longer logs "is awake". Tests: `DeathWitnessTests`.

### Built

Written on 2026-11-12 in sessions without a compiler. The first version builds and runs; changes after it are marked in Status.

- **Core** (`src/TheShip.Core`): `Voyage` and its partial files hold the whole loop.
  - `Scenario.cs`: the Short haul content. 6 crew, 20 facts, 5 scripted alerts (coolant leak; death in sleep plus a sleeper illness in the bank; navigation drift plus a drawn side fault;
    signal from Earth; pod controller fault plus power sag, with a refusal to sleep), arrival in year 60.
  - `Voyage.Commands.cs`: wake (6 h thaw, 3 to 5 years of ageing, pod wear), `/go` clock, repair and patch with compliance by trust band,
    fault clocks and consequences, the three-month minimum waking, sleep and refusal, log open/restrict/alter, drift (1 year of ageing per 10 asleep).
  - `Voyage.Talk.cs`: the statement record; a lie caught at once, later by evidence, or doubted (doubt score); hard truths (morale now, trust at the next drift);
    promises; dodges; the logs lockout after an engineer spots a rewritten entry.
  - `Voyage.Aftermath.cs`: two-week beats (noticing, checking doubts, reading logs, requests, crew telling each other), arrival and the ending record.
  - `RuleReader.cs`: the keyword reader (tests, and the claims check on crew lines). `Lines.cs`: the templated crew lines (fallback). `Tuning.cs`: every number.
- **AI** (`src/TheShip.AI`), ported from Cities: `ILlmProvider`, `LocalLlm` (LLamaSharp 0.30.0, Vulkan or CPU, Gemma 4 / Qwen3 chat handling, reply cleanup),
  `LlmWorker` + `MainThreadQueue`, `Gbnf`/`Grammars`, `LlmJson`.
  `ShipMind` does the model's jobs: words each crew line from the game's decision (rejects a line that states what the speaker doesn't know, once, then keeps the template),
  reads a player line with a grammar into the six kinds (an unusable reading is applied as plain talk and recorded in the session log), and judges free-text statements yes/no against evidence.
- **Game** (`src/TheShip.Game`): MonoGame DesktopGL window, FontStashSharp text, drawn with lines and outlines on one white pixel.
  - `ShipGame.Bridge.cs`: issue cards with Repair and Patch for the selected person, the clock (Advance, Drift), the wireframe ship plan
    (click a door to step open, closed, locked; pods; awake crew; fault markers), the log with the voice line and the waiting-line preview,
    the crew cards (Wake, Sleep, File; click a card to speak to one person) and "Waiting on you".
  - `ShipGame.Screens.cs`: the crew dossier (sheet, what they know, what you told them) and the record with the ship log entries (Open, Restrict, Alter).
  - The model loads in the background; without one the window says what is missing.
  - `Sounds.cs`: a short click made in code (35 ms sine tick, `ClickVolume` there), played from `Do(...)` so every button, card, door and tab has it; silent without an audio device.
- **Tests** (`tests/TheShip.Tests`): the loop, the lie lookup, the reader, the grammar, `ShipMind`'s parsing and the warm-up. All pass (2026-11-12).

### Differences from DESIGN.md to settle

- Up to three people awake (kept). Alert 4 (the signal) has faults only when a patch returns into it, so with no outstanding patch it can be drifted past with nobody woken. Power sag is an engineering repair until power routing exists; a lost blight cuts the stores to 60% once (the design halves them for every later waking). Alert 3 can hold two Critical faults (navigation drift and a power sag), against the design's "at most one critical"; a returning patch keeps its own Critical flag.
- Adrift when a navigation fault's clock runs out unrepaired (including a returned patch); a patch still outstanding at the last drift slips the arrival by 9 years instead. A slip by decades for other cases is not built.
- Shorter wakings are all or nothing: the three-month minimum is waived as soon as the stores can't feed everyone up through a
  person's remaining three months (`Voyage.CanSleepNow`); there is no rationing. Once the stores are empty, whoever stays awake dies
  after `Tuning.StarveDays` (60 days).
- "Landfall without you" can't happen: the logs are the only lockout.
- Terminal notes and paper notes are not in. The drift report is complete because no sensors are lost yet.
- Doors: only locking has an effect. Open and closed look different and behave the same. Nobody reacts to being locked in.
- The design canvas shows things the game does not simulate: a section without sensors, a "next event" line on the clock, crew notes in the drift report.
- Not ported from Cities' LLM stack: the separate model host process, CUDA backend, reply prefix cache, VRAM budget (the session log is now in, as plain text).

### Not built yet

Everything the design lists outside the prototype scope: lights, power routing, cameras and microphones,
factions, children, the other events and faults, the other lockouts and endings, modes, mission types, modifiers, run variation, voice input.

### Next steps

1. `dotnet build TheShip.slnx` and fix what it reports; then `dotnet test`.
2. Play a run end to end; tune `Tuning.cs` towards "one honest run in two reaches Landfall".
3. Run the three playthroughs of the prototype's pass test (DESIGN.md, "Prototype scope").
