# CLAUDE.md

The Ship: you are the AI of a sleeper ship and keep the crew alive by choosing who to wake and what to tell them.
C# / .NET 10, MonoGame DesktopGL, a local LLM through LLamaSharp (same stack and versions as the Cities repo).

**Read DESIGN.md first.** It is the leading design document: make design changes there. Keep this file's Progress section up to date:
add a line for every non-trivial change, and move items between "Built" and "Not built yet".

## Commands

```sh
dotnet build TheShip.slnx
dotnet test
dotnet run --project src/TheShip.Game -- [--seed <n>] [--llm <file.gguf>] [--gpu-layers <n>]
```

- The game needs a GGUF model in `src/TheShip.Game/Content/Models/llm/` (or `--llm`). Without one it shows what is missing and starts no voyage.
- Models: see the README.txt in that folder.

## Layout

| Project | What it holds |
| --- | --- |
| `src/TheShip.Core` | The simulation. No dependencies. Owns every state and outcome: crew, facts, statements, faults, time. |
| `src/TheShip.AI` | The language model: `ILlmProvider`, `LocalLlm` (LLamaSharp), `ScriptedLlm`, `LlmWorker`, GBNF grammars, prompts, `ShipMind`. |
| `src/TheShip.Game` | MonoGame window: a text UI with a typed input line. |
| `tests/TheShip.Tests` | xunit tests of the simulation and the reader. |

## Rules for the code

- The game owns the truth. The model writes crew lines and reads player lines; it never changes state.
  The one exception is the yes/no judgement of a free-text statement against evidence (DESIGN.md, "Reading").
- The game does not run without a model (decided 2026-11-12). What is left of the model-less path is for unit tests and safety nets only:
  `RuleReader.Read` drives the voyage in Core tests, `RuleReader.Claims` checks a model-written crew line against what the speaker knows,
  `Lines.cs` is shown when the model's line was rejected twice, and `tests/TheShip.Tests/FakeLlm.cs` stands in for the model in `ShipMind` tests.
- Model calls go through `LlmWorker` (one at a time, most urgent first); results reach the simulation through `MainThreadQueue`.
- Structured model output is forced with a GBNF grammar, and every string in a grammar is bounded.
- Numbers that tune the game live in `Tuning.cs`.

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

### Built

Written on 2026-11-12 in sessions without a compiler. The first version builds and runs; changes after it are marked in Status.

- **Core** (`src/TheShip.Core`): `Voyage` and its partial files hold the whole loop.
  - `Scenario.cs`: the Short haul content. 6 crew, 20 facts, 5 scripted alerts (coolant leak, death in sleep, navigation drift,
    signal from Earth, pod controller fault with a refusal to sleep), arrival in year 60.
  - `Voyage.Commands.cs`: wake (6 h thaw, 3 to 5 years of ageing, pod wear), `/go` clock, repair and patch with compliance by trust band,
    fault clocks and consequences, the three-month minimum waking, sleep and refusal, log open/restrict/alter, drift (1 year of ageing per 10 asleep).
  - `Voyage.Talk.cs`: the statement record; a lie caught at once, later by evidence, or doubted (doubt score); hard truths (morale now, trust at the next drift);
    promises; dodges; correcting a wrong reading (`Reread`); the logs lockout after an engineer spots a rewritten entry.
  - `Voyage.Aftermath.cs`: two-week beats (noticing, checking doubts, reading logs, requests, crew telling each other), arrival and the ending record.
  - `RuleReader.cs`: the keyword reader (tests, and the claims check on crew lines). `Lines.cs`: the templated crew lines (fallback). `Tuning.cs`: every number.
- **AI** (`src/TheShip.AI`), ported from Cities: `ILlmProvider`, `LocalLlm` (LLamaSharp 0.30.0, Vulkan or CPU, Gemma 4 / Qwen3 chat handling, reply cleanup),
  `LlmWorker` + `MainThreadQueue`, `Gbnf`/`Grammars`, `LlmJson`.
  `ShipMind` does the model's jobs: words each crew line from the game's decision (rejects a line that states what the speaker doesn't know, once, then keeps the template),
  reads a player line with a grammar into the six kinds (an unusable reading records nothing and asks for `/fix`), and judges free-text statements yes/no against evidence.
- **Game** (`src/TheShip.Game`): MonoGame DesktopGL window, FontStashSharp text. Status column (numbers shown), transcript, input line.
  Controls are typed: `/help` lists them. The model loads in the background; without one the window says what is missing.
- **Tests** (`tests/TheShip.Tests`): the loop, the lie lookup, the reader, the grammar and `ShipMind`'s parsing. Never run.

### Differences from DESIGN.md to settle

- Up to three people awake; alert 4 (the signal) has no fault and can be drifted past with nobody woken.
- Losing the navigation fault ends the run as "Adrift" (no arrival slip of years).
- "Landfall without you" can't happen: the logs are the only lockout.
- Terminal notes and paper notes are not in. The drift report is complete because no sensors are lost yet.
- Not ported from Cities' LLM stack: the separate model host process, CUDA backend, reply prefix cache, VRAM budget, session log.

### Not built yet

Everything the design lists outside the prototype scope: the ship schematic, doors, lights, power routing, cameras and microphones,
factions, children, the other events and faults, the other lockouts and endings, modes, mission types, modifiers, run variation, voice input.

### Next steps

1. `dotnet build TheShip.slnx` and fix what it reports; then `dotnet test`.
2. Play a run end to end; tune `Tuning.cs` towards "one honest run in two reaches Landfall".
3. Run the three playthroughs of the prototype's pass test (DESIGN.md, "Prototype scope").
