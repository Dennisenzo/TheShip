# Plan: make the lie tempting and the truth legible

Branch `feature/lie-temptation`. Written in a cloud session with no .NET SDK; built, tested and validated on 2026-11-13 (see "Status").

## Why

Read as a new player, the build teaches the wrong lesson about lying (review of 2026-11-13, commit 68147d9):

- A hard truth drops morale at once and gets a hurt reply. Its +8 trust lands silently at the next drift.
- A lie gets "accepts it" and nothing moves until someone checks, then costs 22 to 32 trust in one hit.
- The only place that shows how a lie can be found out is the record screen, after the lie is told.
- The lies that pay at once exist (a question that blocks work, the dose question, the pods question of a sleep refuser), but nothing on screen says so.
- The opening note does not say what winning is, what a thaw costs or that there are 2 spare parts.

So run one says "lying is free", run two says "never lie". The prototype's pass test needs a player who lies unprompted and calls the discovery fair.

## Steps

Each step is one commit. Numbers go in `Tuning.cs`.

1. **Stakes of an answer (Core).** `Voyage.StakesOf(person)` and `Voyage.StakesText(person)` for a person's open question, before anything is said:
   what the true answer costs in morale and in repair odds, that it earns trust at the next drift, whether a reassuring answer lifts morale,
   whether they refuse work or sleep until answered, whether the truth makes them refuse the repair, and how they could check the answer.
   `Voyage.RepairChance` is taken out of `CompleteRepair` so the odds shown are the odds rolled. `Voyage.CheckWays` is the fact half of `Exposure`.
2. **Relief (Core, the one rule change).** The good answer to a question a person asked, or that matters to them, lifts morale by `Tuning.ReliefMorale` (6)
   when it is news and they believe it. A true good answer earns it; a lie borrows it, and pays the hard truth's morale back on discovery (already built).
3. **Honesty credited at the drift (Core).** The drift report is followed by one line per person whose pending hard-truth trust arrived,
   naming what they were told. *Withdrawn after validation (user decision, 2026-11-13):* only a true statement earns that trust, so the line
   told the player which statements were true. The trust still arrives at the drift, but no line names it; "the player never sees whether a
   statement is true" wins.
4. **Bridge (Game).** "Waiting on you" shows the stakes for the selected person (or the only person asking); the Repair button shows the
   repair odds for the selected person; the opening notes state the goal, the cost of a thaw, the spare parts and where the stakes and the record are.
5. **Tests and documents.** `LieTemptationTests.cs`; DESIGN.md (Trust, Knowledge and statements, Prototype scope); CLAUDE.md Progress.

## Status

All five steps are done on the branch, one commit each. Found while doing step 1: the morale a hard truth costs is hidden by the repair
chance's 0.97 cap for a skilled lead (Mara, Engineering 3, on the level 2 coolant leak loses no odds at all), so the stakes text names
repair odds only where they really drop.

Built and validated on 2026-11-13: 0 warnings, the suite passes, and the balance report is unchanged (relief only moves morale, and
morale only moves the repair chance, which is capped at 0.97). Fixes after validation, one commit each:

- The drift no longer names the hard-truth credit (step 3 withdrawn): the trust arrives silently.
- Relief needs the earlier belief to be absent or hard: switching between two good answers ("no word" and "good news" from Earth) gave
  +6 morale each time.
- A hard truth earns trust once per person and fact (`CrewMember.HardTruthsCredited`) until the truth changes: a lie and the truth told in
  turn stacked 8, 16, 24 pending trust.
- The facts are synced where pod wear changes (a thaw's end, a lost pod or controller), not only at the end of a clock move.
- The Repair button reads "REPAIR: NO PARTS" (no odds) when no spare parts are left.

## Left out on purpose

- No new penalty on the truth (a "shaken" delay was considered): alert clocks have 1 to 6 hours of slack, and it could not be measured here.
- No change to `LieTrust`, `HardTruthTrust` or the doubt score.
- Nothing shown says whether a statement was true; the stakes are about the question, before the line is said.

## Still open

- Relief has no effect on any bot outcome yet (see above); a later step gives it a real one.
- Look at the bridge with two and three people asking in play: by measurement the stakes text is at most 6 of the 8 lines that fit.
