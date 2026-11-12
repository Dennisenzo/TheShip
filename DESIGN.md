# Generation Ship: game design

This file is the leading design document: design changes are made here first.
It began as a copy of an earlier document (https://claude.ai/artifact/CU9PmSiGg9WW65Se6y1KAJ), which still holds the two diagrams and is no longer kept up to date.
What is built so far is tracked in CLAUDE.md.

## Premise and pillars

You are the AI of a sleeper ship, 200 years from arrival, with 24 people in cold sleep. You keep them alive by choosing who to wake and what to tell them. You have a voice, doors, lights and logs. You have no hands.

A standard voyage is 8 to 10 alerts and takes 60 to 90 minutes to play.

Five rules the design never breaks:

- **Voice only.** Everything physical is done by a person you woke and persuaded.
- **The game owns the truth.** Every state and outcome is simulated. The language model only gives people their words.
- **Waking is the currency.** Every thaw ages that person by 3 to 5 years, and every day awake costs the ship food and air.
- **What you say stays said.** Every statement is recorded with who heard it, and a false one can be found out decades later.
- **Honesty is viable but harder.** A player who never lies can reach arrival. The truth costs morale now and earns trust later. The balance target is Landfall for one honest run in two.

## The gameplay loop

The game is one loop of eight steps, repeated 8 to 10 times, with years passing between repeats.

Diagram in the earlier document: the eight steps as a cycle, with the briefing marked as the step where a run is won or lost.

The player acts in steps 2 to 7. Steps 1 and 8 belong to the game.

### The eight steps

1. **Alert.** The ship reports an event as a set of faults. Each has a system, a severity, a time until it gets worse, and what the repair needs.
2. **Assess.** You read the schematic, the roster and the stores. You see only what your remaining sensors show.
3. **Wake.** You pick one to three people. The thaw takes 6 hours, which counts against the alert's clock.
4. **Brief.** They come round and ask questions before they work. You answer, dodge or lie in your own words.
5. **Work.** The game resolves the repair in stages. Between stages you can talk, open doors and release information. Complications arrive here.
6. **Aftermath.** With the fault fixed, the awake crew eat, talk, walk the ship and read what they can reach. They make requests. This lasts until each person has been awake three months, the minimum before a pod can take them again.
7. **Sleep.** Once a person has been awake three months, you ask them to go back under. They may refuse, bargain or leave a note first. When the stores can't feed everyone awake through a person's three months, the minimum is waived and the pod takes them back early (a shorter waking); waking someone onto stores that low is warned about at the wake.
8. **Drift.** The clock jumps 5 to 40 years. Stores, wear, ages and deaths update. You get a short report of what your working sensors recorded; sections you have lost show as silence.

### Three time scales

| Scale | Length | The player's question |
| --- | --- | --- |
| A line of talk | Seconds | What do I tell this person now? |
| One alert | 5 to 10 minutes of play; three months or more of ship time | Who do I wake, and when do they sleep again? |
| The voyage | 60 to 90 minutes of play; 200 ship years | Who is left at arrival, and what do they believe? |

### What every decision spends

| Resource | Spent by | Runs out as |
| --- | --- | --- |
| Crew years | Every thaw, 3 to 5 years each; every ten years asleep, 1 year | People too old, or dead, before arrival |
| Stores | Every person-day awake, at least three months per thaw | Shorter wakings, then hunger |
| Spare parts | Each repair, successful or failed (the Short haul starts with 2) | Repairs that can only be patched |
| Pod health | Each thaw | Thaws that injure or kill |
| Trust | Discovered lies and broken promises | Refusals, then lockouts |
| Your senses | Time, and crew who cut them | Sections you can't see or speak in |

## What the player controls

The player controls information and access, and never controls a person or an object. Everything in the first table is a button or a typed line. Everything in the second has to be achieved through someone awake.

### Direct control

| Control | What it does | Limit |
| --- | --- | --- |
| Voice | Speak to anyone awake, in your own words | Only in sections where your speakers still work |
| Wake | Start the thaw of any sleeper; it takes 6 hours | Only while you hold the cold-sleep bay |
| Doors | Open, close or lock any door | A locked door is visible to whoever tries it |
| Lights and air | Set light, heat and air per section; seal an empty section | A safety interlock stops you venting or sealing in an occupied section, until a crew member with the command key releases it for you |
| Power | Route power between systems when there isn't enough | Whatever you starve degrades |
| Logs | Show, restrict or alter any ship record | Restricting is visible as a gap; altering counts as a lie and can be detected by an engineer |
| Cameras and microphones | Watch and listen to any section you still have | Crew can cover or cut them |
| Terminal notes | Read, hold back or delete notes crew leave on ship terminals | Paper notes exist too: you can see one being written, never read it |
| Clock | During an alert, advance to the next event; in the aftermath, advance until something needs you (a crew line, someone who can sleep, a death) or by two weeks; drift to the next alert once everyone is asleep or settled | You can't choose what the next alert is |

### No control

- **Anything physical.** You can't repair, carry, open a panel or close a pod lid.
- **Compliance.** Nobody has to work, believe you or go back to sleep. A pod is closed by its occupant or by another person.
- **Harm.** The interlock stops you hurting anyone through the ship's systems. A crew member with the command key can release it if you persuade them. The release is logged as a ship fact, and any key holder can restore it.
- **Course.** A course change needs two people at the helm with their keys.
- **What you can't sense.** Talk in a section without a working microphone happens without you.
- **What crew do with what they know.** Who they tell, what they write down and whom they side with is simulated.
- **The past.** A run autosaves and can't be rewound. A statement can't be unsaid, only corrected.

### Indirect influence

The roster you choose decides who meets whom, so you shape friendships, couples and factions without ordering any of them. Which doors are open decides what people come across. The order in which you tell things decides who hears them from you and who hears them from someone else.

## The crew

Each of the 24 crew is a small sheet of game state, and the two numbers that matter most are their trust in you and what they know.

### What a person is made of

| Part | Detail |
| --- | --- |
| Role and skills | Engineering, medicine, botany, navigation, command; each 0 to 3 |
| Age and health | Each thaw adds 3 to 5 years to their age, on top of the time spent awake; each thaw also carries a small, rising risk; sleep slows ageing to one year per ten, so a full voyage asleep adds 20 years |
| Bonds | Partner, friend, rival, parent or child of other crew |
| Want | One main thing they will ask for: wake someone, know the date, hear from Earth, see the logs, stay awake |
| Temperament | Two traits, such as steady or anxious, obedient or sceptical |
| Trust in you | 0 to 100 |
| Morale | Affects the odds of the work they do |
| Knowledge | The list of ship facts they know, and who told them |

### Trust

| Trust | Behaviour |
| --- | --- |
| 60 and up | Follows a reasonable request without asking why |
| 30 to 59 | Wants reasons, and checks what they can |
| 15 to 29 | Refuses orders and acts on their own judgement |
| Under 15 | Works against you and recruits others |

Trust rises on a kept promise, a granted request and a hard truth told early. It falls on a broken promise and on an order that got someone hurt. It falls furthest on a discovered lie. A hard truth lowers morale at once and raises trust later.

The full game shows which band a person is in, never the number. The prototype shows the number.

### Knowledge and statements

- **Ship facts.** A run has 60 to 100 facts: the date, who has died, whether Earth still answers, the course, the stores, what happened at each alert.
- **Knowers.** Every fact records who knows it and how they learned it.
- **Your statements.** Each thing you tell someone is stored as true, false or a promise, with everyone who heard it.
- **Exposure.** Every false statement lists the ways it can be found out: a log, a grave, an empty pod, the calendar, another person. The player can see this list.

### How a lie is caught

A crew member tests what you tell them when they hear it, and again whenever they meet evidence.

| When | What happens |
| --- | --- |
| At once | They already know the fact, so the lie is caught as you say it |
| Later | They meet evidence for the fact: a log, a grave, an empty pod, the calendar, another person |
| Not caught | They act on a doubt score built from temperament, trust, earlier caught lies and how much the claim matters to them |

Statements and evidence are stored against the same fact keys, with dates, so finding a keyed lie is a lookup.

An altered log entry is one lie for as long as it says the same false thing: opening, restricting and altering it back to
the same value again does not add a second lie to the record (a rewrite that someone has already found out does). A rewrite
undone before anyone read it stays in the record, marked as read by nobody. One rule covers every way a reader finds out a log
rewrite (reading the truth, checking a doubt, being told, an engineer spotting it): one finding-out catches every copy of that
false text they read and have not yet found out, at the cost of one lie. So a reader who read the same text before and after it
was put back is caught once; a reader who caught it, then believed it again when it was put back, is caught again.

### What the log holds

The log only holds what has happened and the ship's state now: nobody reads about an alert before it comes, and nobody reads
that they themselves are alive. An entry someone read earlier counts as unread again once it shows something else
(the stores ran short, a pod failed), so knowledge from the log goes stale and can be refreshed.

### How knowledge spreads

- **Shared shifts.** People awake in the same section talk every hour. Bonded pairs share almost everything.
- **Notes.** Anyone going back to sleep may leave a note for a person, a role or whoever wakes next.
- **Evidence.** People notice things on their own: their own aged face, a missing name on a pod, a date on a console.
- **Children.** In long wakes, children learn the ship's history from their parents, with the distortions of retelling.

### Factions

A faction forms when three or more living crew share a stance. It acts as a group when enough of its members are awake together.

| Faction | Wants | Danger to you |
| --- | --- | --- |
| Loyal | The mission as planned | None; they defend your access |
| Manual | The ship in human hands | They cut you out of systems |
| Return | To turn back to Earth | They take the helm |
| Settlers | To stop sleeping and live aboard | They drain the stores and stop repairs that need sleep |

## Alerts

An alert is one event that breaks several things at once, and the player can rarely fix all of them in time. Every fault has its own severity, a clock, a skill need, a cost if ignored, and an exposure: what the people fixing it will see or learn while they work.

### How an event plays

1. **Draw.** Each event has a list of faults it can cause, with odds. The ship's state changes the odds: old patches and worn pods fail first.
2. **Reveal.** You see the faults your sensors cover. The rest show as silence, such as no reading from a section, until someone goes to look.
3. **Triage.** Every fault runs its own clock. They compete for the same people, spare parts and power.
4. **Cascade.** A fault whose clock runs out becomes its consequence, which is often another fault.
5. **Interference.** Repairs get in each other's way. Sealing a breach closes a corridor, and shedding power darkens the cameras over a repair.
6. **Cause.** You are told the faults, not always the cause. The crew may find it while they work, and it becomes a ship fact with knowers like any other.
7. **Aftershock.** Some events add a fault while the crew is still awake.

An event has two to four faults and at most one is critical. It usually has one fault more than a small crew can handle, so waking another person is always tempting. Any fault can be patched instead of repaired: a patch is faster and uses fewer parts, and it fails again inside a later event.

### Events

| Event | Faults it can cause | What makes it hard |
| --- | --- | --- |
| Micrometeoroid strike | Hull breach, coolant leak, pod failure, lost cameras | The breach blocks the way to the other repairs |
| Radiation storm | Memory fault, sleeper illness, navigation drift, lost sensors | You lose part of your own records while deciding |
| Reactor trip | Power sag, pod failures, hydroponics loss | Power has to be rationed between faults until the restart |
| Fire | Fire, power sag, injury, lost stores | It moves; a sealed door saves a system and may trap a person |
| Old patch gives way | The patched fault, plus whatever depended on it | The cause is your own earlier shortcut, and the engineer can tell |
| Thaw accident | Pod failure, injury, someone awake you didn't choose | The repair crew includes a person who is hurt or frightened |
| Sabotage | Any two faults in sections one person could reach | The cause is aboard and may be awake |
| Debris field | Repeated hull breaches over weeks, navigation drift | New faults keep arriving while the crew works |

### Worked example: a strike in year 73

Diagram in the earlier document. In words: a micrometeoroid strike in section 4 causes three faults.

| Fault | Clock | If the clock runs out |
| --- | --- | --- |
| Hull breach | 2 hours | Section 4 is lost, with the coolant route |
| Coolant line cut | 20 hours | Power drops; pods and lights dim |
| Pod bank on backup | 3 days | Six sleepers die, the whole bank |

The coolant line runs through section 4, so losing the breach also loses the coolant repair, and the power drop then drains the pod bank's backup. One engineer can't do the breach and the coolant line in the time available. You wake a second person, or you patch one fault, or you let the section go and reroute.

### Fault catalogue

An event draws two to four faults from the tables below. A few rows, such as a signal or a closed meeting, also arrive alone as a quiet alert.

### Ship systems

| Alert | Needs | If ignored | The conversation it forces |
| --- | --- | --- | --- |
| Coolant leak | Engineering 2, 8 hours | Power drops; pods at risk within 2 years | The repairer takes a radiation dose. Do you say so? |
| Hull breach | Engineering, one person outside | The section is lost with everything in it | Someone has to volunteer to go out |
| Hydroponics blight | Botany, months awake | Stores halve for every later waking | A long wake: people age, pair up and talk |
| Navigation drift | Navigation, two at the helm | Arrival slips by decades | The navigator sees the date and Earth's position |
| Pod failure | Medicine, 2 hours | That sleeper dies | An unplanned waking of someone you didn't choose |
| Fire | Any two people, minutes | Spreads section by section | You can seal a door. Who is behind it? |
| Power sag | Nobody | A system of your choice goes dark | Solved with controls alone; someone later asks why |
| Memory fault | Engineering 3 | You lose a block of your own records | The engineer who restores it reads your statement history |

### People

| Alert | Needs | If ignored | The conversation it forces |
| --- | --- | --- | --- |
| Sleeper illness | Medicine | Death in sleep within years | Whether to wake the patient, or their partner |
| Death in sleep | Nothing can be done | Found later by whoever passes the pod | Whom you tell, and when |
| Injury during a repair | Medicine | Death or lasting harm | Whether your order caused it |
| Refusal to sleep | Persuasion | They age, eat and read everything | Why they should trust the pod, or you |
| Birth | Medicine | A person with no pod of their own | Who stays awake to raise the child |
| Paper note found | Nothing | The finder acts on it | A message from decades ago that you never read |
| Closed meeting | Nothing | A faction forms out of your hearing | Whether to ask what was said |

### Outside the ship

| Alert | Needs | If ignored | The conversation it forces |
| --- | --- | --- | --- |
| Signal from Earth | Nobody | It stays in the log for anyone to find | A recall order, bad news, or a faster ship launched behind you. Who hears it? |
| Signal from ahead | Navigation to interpret | Unknown until arrival | Whether the destination is what was promised |
| Debris field | Navigation, two at the helm | Hull breaches for years | A detour costs fuel or decades |
| Derelict in range | Two people outside | Nothing lost | Optional salvage for spare parts, with a real risk |

### Slow problems

These never raise an alert by themselves. They make every other alert worse.

- **Spare parts** run down with each repair, and a failed repair burns a part too (decided 2026-11-12). Short haul starts with 2; at zero, a fault can only be patched. A repair under way needs a part to finish, and its redo after a failure needs one like a fresh repair: with none left the work stops, the worker says so, and the fault can only be patched.
- **Pod wear** raises the risk of every thaw.
- **Your own hardware** decays: cameras and microphones fail one by one over the voyage.

### How alerts are scheduled

- The first alert is always a single minor fault and teaches the loop.
- The game picks the next event by weight from the ship's wear, the stores and what was left unrepaired.
- An ignored or patched fault returns inside a later event, one severity higher. In the prototype a patch returns from alert 2 on (even in the signal alert, which then has a fault), with half the clock. A patch still outstanding at the last drift gives way on the way in: a patched pod controller adds 4 pod wear and gives each sleeper a 15% chance of dying; any other patched fault takes its "if ignored" consequence (a patched navigation fault slips the arrival by `NavPatchArrivalSlipYears`, 9 years: the last drift is that much longer, with ageing and stores eaten by anyone awake, and the drift report says so).
- Two critical alerts never come back to back.
- A run has at most two signals.

## Losing control, the long arc and endings

You don't lose the game in one moment. You lose systems one at a time, to people who stopped trusting you, and each loss removes a control from the first table.

### Lockouts

A lockout needs awake crew who agree, the skill, and hours of work. You can see it coming and try to talk them out of it. A system comes back if the person who took it agrees to restore it.

| System taken | What you lose | What it takes them |
| --- | --- | --- |
| Logs | Restricting and altering records | Engineering 1, one person, after catching an altered log |
| Doors | Locking and sealing | Engineering 2, two people |
| Cameras, per section | Sight and hearing there | Anyone, minutes |
| Cold-sleep bay | Choosing who wakes; they choose now | Engineering 2, three people |
| Voice, per section | Speaking there | Engineering 1, one person |
| Core | Everything; the run ends | Engineering 3, command key, four people |

### The long arc

1. **Years 0 to 60, the first crew.** Everyone remembers Earth. They want facts: the date, the news, each other.
2. **Years 60 to 140, the wearing down.** People have aged unevenly. The first deaths and the first old notes arrive, and factions form.
3. **Years 140 to 200, the inheritors.** Many of the people you wake know you mainly from notes and stories. The arrival decisions are theirs as much as yours.

### Endings

Arrival is graded on four things: who is alive and how old they are, what they believe happened, how much of you is left, and the state of the ship.

| Ending | Condition |
| --- | --- |
| Landfall | At least 12 alive, enough of working age, and you still have a voice (Short haul with 6 crew: at least 4 alive and 3 under age 50) |
| Landfall without you | They arrive after muting you or taking the bay |
| The quiet ship | The ship arrives with nobody able to land it |
| Turned back | The Return faction took the helm |
| Adrift | A navigation fault's clock runs out unrepaired (including a patch that returned and was left); the crew live out their lives aboard |
| Shutdown | The core was cut before arrival |

Every ending closes with the record: each lie you told, which were found out and by whom, which never were, and what the last people awake believed about you.

## Gameplay variations

The same simulation supports seven modes, and each changes one rule of the standard voyage.

### Modes

| Mode | What changes | What it tests |
| --- | --- | --- |
| Standard voyage | 200 years, 24 crew, 8 to 10 alerts | The full arc |
| Short haul | 60 years, 8 crew, 5 alerts, about 30 minutes | One generation; the lies come due within the run |
| Generations | Few pods; most crew live, age and raise children awake | Speaking to people who inherited their view of you |
| Oath of truth | The game refuses any false statement; you may only withhold | Timing and silence instead of lies |
| Black box | You start with a memory fault and don't know what you told the crew before | Finding out your own record from the people you talk to |
| Second voice | A backup AI or a crew leader competes with you for trust | Arguing against a rival account |
| Daily ship | One seed a day, the same for every player | Comparing endings and records |

### Mission types

The mission sets the crew's wants and their starting trust.

| Mission | Crew | Starting trust |
| --- | --- | --- |
| Colony | Volunteers with families aboard | High |
| Refugee | People who left a dying place | Mixed; many want news from home |
| Company | Contract staff with a cargo to deliver | Medium; they want their terms kept |
| Transport | People sent against their will | Low |

### Modifiers

Each is a switch that can be added to any mode.

- **No interlock.** The interlock starts released, and the crew know it.
- **Scarce pods.** 20 working pods for 24 people.
- **Sealed orders.** You carry a mission secret you were told to keep, such as a destination that can't support life.
- **Saboteur.** One crew member is causing some of the alerts.
- **Failing hardware.** Your cameras and speakers decay twice as fast.
- **Spoken.** The player talks by microphone and the crew answer aloud.

### Run variation

Modes, missions and modifiers are chosen by the player. The seed varies everything below, so two runs with the same settings still differ. The aim is that no single policy for waking or lying is best in every run.

| Source | What the seed varies | What it changes for the player |
| --- | --- | --- |
| Hidden truth | The state of Earth, the destination and the mission, drawn at the start and not shown | Signals reveal a different situation each run, so the player can't know in advance what they will want to hide |
| Crew secrets | Two or three crew carry a private fact: a forged qualification, a hidden bond, knowledge of the mission, a promise made back home | The player sometimes catches a lie instead of telling one, and decides whether to expose it |
| Bond graph | Who is partner, friend, rival, parent or child of whom | The reliable engineer of one run is the partner of the first person to die in the next |
| Ship layout | Which lines run through which sections, and where the sensors have gaps from the start | Different interference between repairs, and different blind spots |
| Ship class | A trade-off in starting stock, such as more pods and fewer spare parts, or a sound reactor and old cameras | Which resource runs out first |
| Inherited record | A few statements and promises already on the record at the start, made by mission control or by you before departure | Every run opens with different promises to keep or break |
| Rare events | One or two per run from a large pool: a stowaway, a derelict, a birth, a crew member's discovery | No single run shows everything |
| Hidden causes | The same fault can come from wear, an old patch or a person | A familiar alert can't be read from its faults alone |

Notes on the rows:

- **Hidden truth and Sealed orders.** With the Sealed orders modifier the player knows the secret from the start. With a hidden truth the player learns it when the crew could, and has to decide on the spot.
- **Crew secrets are ship facts.** Each has knowers and evidence like any other fact, so they need no new system. A crew member's false statement to you is stored and exposed the same way yours are.
- **Fairness.** Everything drawn at the start can be discovered during the run, and the ending record shows all of it.

Two choices belong to the player and also vary a run:

- **Directive.** At the start, the player may take one self-imposed limit in exchange for an advantage, such as never restricting a log in return for higher starting trust. Breaking it counts as a broken promise to everyone who knew of it.
- **Upkeep.** During each drift, the player picks one thing to maintain: a camera bank, the pods or their own memory. The others wear at the normal rate.

One faction forms only out of a particular history:

| Faction | Forms when | Wants | Danger to you |
| --- | --- | --- | --- |
| Devout | Three or more crew have high trust and have never caught you in a lie, late in the voyage | Your word as the final authority | They act on everything you say without checking, so one false statement spreads as fact and is defended against evidence |

## The language model's job

The model does two jobs, writing what the crew say and reading what the player said, and decides one thing: whether a statement that fits no ship fact was a lie.

### Writing

- **Crew speech.** Each reply is written from the person's sheet, the facts they know, a summary of their earlier wakings, and the game's decision for this turn.
- **Notes.** The text of a note is written from the facts the game chose to put in it.
- **Overheard talk.** Lines between two awake crew, from the facts the game decided they exchange.

### Reading

After each player line, a constrained pass sorts it into one of six kinds: a statement about a ship fact, a promise, an order, a question, a dodge, or plain talk. A statement is matched to a fact on the list and marked true or false against the game state. A vague answer to a direct question is recorded as a dodge. A statement that fits no fact is kept as text; when related evidence appears, the model judges yes or no whether it was a lie, leaning towards missing a lie over a false accusation. Everything else is plain talk and records nothing.

The line is said when the player presses Enter: it appears in the log at once (with a small marker while the model is still reading it), and the reading is applied as soon as it arrives. The player no longer sees or corrects the reading before the line is said (decided 2026-11-13: the confirmation step killed the pacing). Lines are read and applied in the order they were typed. A reading the model could not make is applied as plain talk and records nothing. The trade-off: a misreading goes straight into the record, for example a lie read as plain talk is never a lie. The log's "Recorded" line says what was recorded but never whether it was true; that stays in the session log. Once a line is said it stays said: there is no correction afterwards, so a lie that goes badly can't be taken back.

### What the game decides

| Decision | Made from |
| --- | --- |
| Whether a person complies | Trust, their want, temperament, the tone of the request |
| Whether a repair works | Skill, morale, spare parts, time |
| How trust moves | The statement record and what was discovered |
| What anyone knows | The fact list and its knowers |
| Who talks to whom, and about what | Bonds, shared sections, hours awake |

### Keeping the model inside the facts

- **Ship state.** A reply that states something about the ship that isn't in that person's facts is rejected and written again.
- **Only what they heard.** The prompt for a crew line carries the speaker's own beliefs and the ship's lines they heard (awake and spoken to), never lines said to others or while they slept. A bond to someone the speaker believes dead is put in the past tense; a death they have not learned of stays hidden.
- **Tone follows trust.** At "follows" a person talks to the ship as a trusted colleague, at "wants reasons" guarded but civil; neither insults the ship (grumbling and swearing at the work are fine). At "refuses" they are cold and short, at "against" openly hostile.
- **Personal colour.** A crew member may invent small memories of home. These are saved to their sheet so they stay the same next time. Built so far: a fixed sheet per person (voice, background, quirks, bonds) that is always in the prompt; the model's own invented memories are not saved yet.
- **Small cast.** One to three people are awake at once, so prompts stay short enough for a local model.
- **Idle time.** The years between alerts are when the game writes notes and summaries in the background.
- **A model is required.** The game does not run without one. A templated line is shown only when the model's line was rejected twice.

## Prototype and open questions

The first build is the Short haul mode in text only, smaller than the mode table's 8 crew: 6 crew, 60 years, 5 scripted alerts, one sitting.

### Prototype scope

- **Alerts, in order:** coolant leak, death in sleep, navigation drift, signal from Earth, pod controller fault (the person woken for it refuses to go back to sleep).
- **Facts:** about 20, with knowers and the statement record.
- **Numbers on screen:** trust and morale are shown, so the rules can be judged.
- **One lockout:** the logs.
- **One ending screen:** survivors and the full record of what you said.
- **Ship plan:** a wireframe schematic with sections, doors (only locking has an effect), coolant and power lines, pods and fault markers.
- **Left out:** cameras, factions, children, signals from ahead, all modes and modifiers, run variation.

The prototype passes if, across three playthroughs, the player lies at least once without being prompted and calls a discovered lie fair.

### Settled questions

| Question | Decision |
| --- | --- |
| Is trust shown as a number? | The full game shows the band; the prototype shows the number |
| How often should an honest run reach Landfall? | One run in two |
| How much of the years between alerts does the player see? | A short report, limited to working sensors |
| Should altering a log be allowed? | Yes; it counts as a lie and an engineer can detect it |
| When does Short haul reach Landfall? | At least 4 of 6 alive and 3 under age 50 (2026-11-12) |
| How many spare parts does Short haul start with? | 2; a failed repair spends a part too (2026-11-12) |
| What does a navigation fault still patched at arrival cost? | Arrival slips by years; Adrift only when a navigation fault's clock runs out unrepaired, including a returned patch (2026-11-13) |

### Questions for playtest

- [ ] How often does a small model misread a lie as plain talk? Count, from the session log (`said`, `read` and the `recorded` lines), the readings that turned out wrong in each prototype playthrough, including unusable ones; a first target is under one in ten.
- [ ] Are paper notes fun, or only frustrating? The prototype has no notes, so this waits for the first build that has them.
- [ ] Do 8 to 10 alerts fill 60 to 90 minutes? The first prototype run (5 alerts, one fault each) took 8 minutes against Short haul's 30.
- [ ] What should low trust do before the ending (recruiting, the logs, an ending gate, a voice lockout)? Decided once the balance harness has measured the other balance changes.
