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
| Pod health | Each thaw | Thaws that injure or kill (30% of failed thaws are fatal); sleepers die in worn pods over long drifts |
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
| Morale | Affects the odds of the work they do and how fast they work |
| Knowledge | The list of ship facts they know, and who told them |

### Trust

| Trust | Behaviour |
| --- | --- |
| 60 and up | Follows a reasonable request without asking why |
| 30 to 59 | Wants reasons, and checks what they can |
| 15 to 29 | Refuses orders and acts on their own judgement |
| Under 15 | Works against you and recruits others |

Trust rises on a kept promise, a granted request and a hard truth told early. It falls on a broken promise and on an order that got someone hurt. It falls furthest on a discovered lie. A hard truth lowers morale at once and raises trust later. Morale also sets how fast people work: a team's work speed is 1 + (average morale - 70) / 150, between 0.8 and 1.15, so a team at 70 works at the plain rate, a shaken one is slower and a relieved one faster. This is what makes relief (and a hard truth's cost) matter: a few hours on a fault's clock.

A hard truth earns trust once per person and fact: told again after a lie about the same thing, it earns nothing more until the truth itself changes. The trust a hard truth earns arrives at the next drift, unannounced: the trust number just moves. No line names the credit or the statement, because only a true statement earns it, and what the player sees never says whether a statement was true (decided 2026-11-13; this rule wins over showing the reward). **Scope of that rule (decided by the user, 2026-11-13):** it holds for the log (including the "Recorded" lines), the crew-facing lines and the Waiting, terms and stakes texts. The Record screen and the crew dossier are the ship's own record of what it said, and they do show it: every statement is tagged TRUE or LIE, with the header count of lines, lies and lies found out, and a lie says who found it out and how.

A good answer is a relief. When a fact has a hard answer (is my partner alive, is it safe in there, are the pods sound, any word from Earth), and the person asked about it or it matters to them (their partner, themselves, or word from Earth for someone waiting on it), and they hear the good one from the ship, as news, and believes it, their morale rises by 6. News means they had no answer yet or held the hard one: going from one good answer to another ("no word from Earth" to "good news") is no relief, so switching back and forth gains nothing. A true good answer earns this; a lie borrows it, and the morale of the hard truth is paid when the lie is found out. Someone who doubts the answer gets no relief.

An unanswered question costs trust. A person who asked and got no answer asks again after the next two weeks (and loses 2 trust each time, at most twice); after the second repeat they doubt the fact and check it themselves. Going back to sleep with a question still open costs 4. A dodge is not an answer: it costs 3 trust once per question, makes them doubt the fact, and leaves the question open. When they later find the answer for themselves, the dodge costs another 3 only if it hid that answer: if it was already the truth when the ship dodged (not if it came true afterwards, as a death after the question). Someone who will not work until the ship answers keeps refusing until a statement, true or false, answers the question.

The full game shows which band a person is in, never the number. The prototype shows the number.

### Knowledge and statements

- **Ship facts.** A run has 60 to 100 facts: the date, who has died, whether Earth still answers, the course, the stores, what happened at each alert.
- **Knowers.** Every fact records who knows it and how they learned it.
- **Your statements.** Each thing you tell someone is stored as true, false or a promise, with everyone who heard it.
- **Exposure.** Every false statement lists the ways it can be found out: a log, a grave, an empty pod, the calendar, another person. The player can see this list.
- **The stakes of an answer.** The same list is shown for a question before it is answered, with what each answer does: the morale the true answer costs (and the hours it adds to the fault they would work on, the one they are on or else the most urgent one they qualify for, with the team on it: "work about 3 h slower"; a reassuring answer says how many hours it saves; no hours when they would work on nothing; the hours count the work terms each answer gives: told the danger of the work they would start, they take precautions, so the truth adds those hours too and the stakes say "(1.5 h slower; half dose, no trust lost if hurt)"; terms already fixed on a fault they are on do not change), the trust it earns at the next drift, whether a reassuring answer would lift their morale, whether they refuse work or sleep until they are answered, and whether the truth makes them refuse a dangerous repair. The bridge shows this under "Waiting on you" for the selected person, or for the only person asking. It is about the question and the ship's own facts; nothing on screen says whether a line the player said was true.

### How a lie is caught

A crew member tests what you tell them when they hear it, and again whenever they meet evidence.

| When | What happens |
| --- | --- |
| At once | They already know the fact, so the lie is caught as you say it |
| Later | They meet evidence for the fact: a log, a grave, an empty pod, the calendar, another person |
| Not caught | They act on a doubt score built from temperament, trust, earlier caught lies and how much the claim matters to them |

Statements and evidence are stored against the same fact keys, with dates, so finding a keyed lie is a lookup.

A found-out lie says how it was found (`Statement.FoundOutHow`, per person): "read the log entry", "read a console", "walked to the pod bay", "ran a scan in the medical bay", "saw it when the work finished", "was told by Lin what Lin saw", "watched Idris die", "saw the dosimeter turn red", "was hurt by the repair", "already knew better" for a lie caught as it was said. The `caught` line's decision text carries it ("you read the log entry") and the ending record shows "Found out by Mara in year 31 (read the log entry), admitted". A lie found late opens an accusation (see "Agendas"). A lie can be corrected before it is found out (below).

Correcting: if the ship tells a hearer the true value of a fact before they find out its earlier lie to them, the old statement is corrected for that person (`Statement.Corrected`, with the year). It costs `SelfCorrectTrust` (8) once, at the correction, and the crew line `corrected` ("So it was never what you said before. Thank you for saying it now.") names what was corrected; the later finding-out of that lie by that person costs nothing and opens no accusation, and the record says "Corrected in year Y". A correction while an accusation is open is the admission (`Admit`, no second charge); the truth told after the lie was found out is not a correction. A correction covers the statement as it was said: a log rewrite the ship leaves in place after correcting someone, read and believed by them again, is a new deception (a new unfound record of the same text for that reader), found out and charged like any lie; the corrected one stays corrected. A hard truth told as a correction earns no drift credit (the truth came after a lie), though its morale still costs. Correcting is dearer than never lying and cheaper than being found out (8 against 22 or 32), so it is no way to lie for free: the Corrector bot (lies like the Liar, then tells each person the truth after the work and before the next drift) lands 40.2% against Honest 43.2% at 600 seeds, and a test fails if it beats Honest by more than 6 points.

A death outside the pods (someone awake, or coming round from a thaw that kills them) happens in front of everyone awake: they know it firsthand at once, as they know who is up when someone is thawed, so a later "they are fine" is caught as it is said. Seeing the answer to a question they asked the ship settles that question. A thaw that kills never logs "is awake". A witness says they saw it, not that they went and looked. Only those alive at the end of a step say anything about it: two who starve in the same hours say nothing of each other, and deaths in a drift come out in the drift report, the awake taking them in silently, with any crew line from the drift said after the report.

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

## Conversations

Designed 2026-11-13; not built, except where a rule says so. All numbers are starting values for playtest and belong in `Tuning.cs`.

A conversation is a scene with an agenda, a cost and an outcome. The crew member wants something from it, it takes time off the clock, and it ends in a result the player can see on the ship. What exists today is the Question agenda (a person asks, the ship answers, trust moves), bargains for work and work terms set by the briefing ("First build" below).

### Agendas

The game picks one agenda for each conversation from the person's sheet and the ship's state. The model only words it.

| Agenda | What the person does | Picked when | What it leaves behind |
| --- | --- | --- | --- |
| Question | Asks for a fact | They lack a fact that matters to them; the default (built) | A statement or a dodge |
| Bargain | Sets a condition before they work, sleep or keep quiet | Trust 30 to 59, or the request touches their want (built for work: wake the partner, open the logs) | A promise with a due date, or a refusal |
| Test | Asks something they already know | Sceptical temperament, or they hold a note or another person's account | A lie caught at once, or a small rise in trust |
| Accusation | Puts evidence against one of your statements in front of you | They have met that evidence (built for keyed lies found out late; see below) | A true statement that corrects it, or a second false one |
| Confidence | Tells you something and asks you to keep it from a named person | Trust 60 and up, and a bond under strain | A promise of silence that a later question from that person will strain |
| Proposal | Offers their own plan for the repair | Skill 2 or more in the fault's system, and trust under 60 | Their plan or yours, with the blame attached |

How the hard ones resolve:

- **Bargain.** The condition comes from the person's want and bonds: wake someone, show a log, stay awake longer. Accepting records a promise. Refusing leaves the compliance roll to trust alone, with a penalty.
  *Built for work (step 2 of the bargains plan).* When the player asks a wary person to repair or patch (trust 30 to 59, not Obedient; asked after the trust-band refusals, the pending question and the danger question) and the ship can grant their want, they set one condition per waking instead of starting: Mara-like (wants her partner) asks the ship's word to wake the partner if he is asleep and alive and a slot is free; Lin-like (wants the logs) asks the word to open the logs if any log is restricted (all logs are open until the player restricts one, so this is rare). The line is a `bargain` crew line; the work does not start. The player accepts by saying the matching promise to them (a promise reading with the same key): the work starts, the ledger gets the commitment with origin "bargain" and a due date `BargainDueDays` (40) away, and there is no `PromiseMade` trust. The same gates as a Repair click run again at that moment: if trust has fallen out of the wary band, a question now waits, or the danger stops them, they say that refusal instead; the condition closes, the ship's word stands as an ordinary promise, and a later Repair or Patch starts the work once the gate is clear, with no new price. A promise with another key is an ordinary promise and the bargain stays open. Pressing Repair or Patch again is a push (the click is "without your word"): one roll, they start with chance trust / 100 + `BargainPushBase` (0.10), and it costs `BargainPushedTrust` (3) either way. A failed push declines: they refuse until the matching promise. The condition lapses silently when its fault is resolved, the alert changes, the person goes to sleep, their trust leaves the wary band, or the ship meets the condition another way (checked after each step); they set no second condition that waking. The player-facing text (`Voyage.BargainText`) states the price, the stakes and the odds of a push, and never whether a statement is true. "Waiting on you" shows the selected person's condition, or the first one's with a hint to select the others; the next-step line asks for the word only while the fault still waits on that person. A bargain costs no clock yet (that comes with work terms).
  *Broken promises (feature/user-decisions, decided by the user 2026-11-13).* A promise the ship breaks while the person is awake (`wake_partner` or `show_logs`, at the due date) opens an accusation on them, the same agenda and `AccusationBeats`: the Waiting text and the NEXT line say "Mara holds you to your word: wake Idris." The fact it is about is their partner's life (`alive:<partner>`) or the first log entry still closed. *Admit:* tell them the truth on that fact (a statement reading, no new line kind) closes it as admitted with no trust back; *keep it late* (wake the partner, open the logs) closes it as admitted and pays `AccusationAdmitTrust` once, while the ledger keeps the promise as broken. *Ignore:* as for a lie (`AccusationIgnoredTrust`, an audit with no `ConfirmedTruth`). *Dodge:* the usual charge. Released promises and promises that break because the person goes to sleep open none; an accusation whose promise can no longer be kept (partner dead, the crew hold the logs or the bay) is dropped with no cost.
- **Test.** The player is never told a question is a test. A truthful answer raises trust more than an ordinary truth does.
- **Accusation.** Telling the truth now stops the fall in trust at the discovered-lie cost. A denial is a new false statement, caught as it is said. This is a new statement, not a take-back: the first one stays in the record.
  *Built (feature/lie-comes-due, step 2).* A keyed lie found out late (not caught as it was said, not an engineer's log lockout, not a free-text statement) opens an accusation on that person and fact; the discovery cost is unchanged (`LieTrust`). One is open at a time, the oldest first; a second late finding-out waits behind it and opens, with its full beats, when the first closes. The crew line `caught` names how it was found (see "How a lie is caught"). The ship answers in four ways. *Admit:* say the true value of that fact to them; `AccusationAdmitTrust` (6) of the lost trust comes back, once per person and fact, and they say `accusation_admitted`. What they saw stays seen: someone who caught the ship lying on a fact keeps their own sighting when the ship agrees with it (an admission included), so the same lie told again is caught at once; and a rewritten log entry on a fact they hold an accusation on does not overwrite what they saw. *Deny:* a false statement on that fact while it is open is caught as it is said, whatever the year (the "within a year" window of a fresh sight does not apply), at `LieTrust` again; the accusation closes as denied. *Dodge:* the dodge reading costs `Dodge` (3) once and the accusation stays open. *Ignore:* after `AccusationBeats` (2) aftermath beats (four weeks), or when they go to sleep, or at a drift they stay awake through (their line comes after the drift report, with the drift's other crew lines), it closes unanswered with `accusation_closed`: `AccusationIgnoredTrust` (4), and they doubt every fact they hold from the ship, so the existing doubt check audits one per beat (the true ones are confirmed but pay no `ConfirmedTruth`, so ignoring never beats admitting; the false ones are caught, and those open accusations of their own). Their death closes it with no line. The text for the bridge (`Voyage.AccusationText`) names the person, what the ship said, how they found out and the weeks left, and never says whether any other statement was true. Broken promises open no accusation. The ship log records an admission ("Recorded: you admitted to Mara that you lied about the date.") and an unanswered close ("Recorded: you never answered Mara about your lie on the date. She will check everything you told her."). Session log: the `state` line has `accused: <name> <fact> beats <n>`. On the bridge, "Waiting on you" shows a short item per accused person (full text as its tooltip), the next-step line asks for the answer first, and the dossier notes how the lie was found and what became of the accusation (admitted, did not answer, denied); none of it says whether any other statement was true.
- **Confidence.** If the named person later asks about it, the player must break the promise, dodge, or lie to them.
- **Proposal.** If you accept and the plan fails, trust doesn't move. If you overrule and your plan fails, it counts as an order that got someone hurt.

### Asking the crew

The player can ask questions too. The crew hold facts the ship doesn't: the text of a paper note, what was said in a closed meeting, what a section looks like where the cameras are dead.

| Their trust in you | How they answer |
| --- | --- |
| 60 and up | Tells you what they know |
| 30 to 59 | Tells you what costs them nothing, and may ask why you want it |
| 15 to 29 | Dodges |
| Under 15 | Lies, if the lie serves their faction |

Whether a person lies is the game's decision, and the model words it. What they tell you is stored in the same statement record as what you tell them, so you catch their lie the way they catch yours: by meeting evidence. The check that rejects a crew line stating what the speaker doesn't believe ("Keeping the model inside the facts") has to let through the one false value the game decided on, and nothing else.

### Outcomes

Every conversation closes with at least one outcome, shown in the log like the "Recorded" line. Like that line, it says what happened and never whether a statement was true.

| Outcome | Example | What it changes |
| --- | --- | --- |
| Commitment | "You promised Mara to wake Idris at the next alert." | Goes on a promise ledger with a due date; a broken promise lowers trust |
| Work terms | Told about the radiation dose, the engineer takes precautions | The repair's time, odds and injury risk in the Work step |
| Independent action | After a dodge, Mara walks to the pod bay | They check for themselves (built: a dodge or a twice-repeated question makes them doubt the fact and check it) |
| Spread | "Mara will tell Idris." | Knowers of the fact, at the next shared shift or in a note |
| Stance | "Mara now doubts the pods." | One step towards or away from a faction's stance |

**The promise ledger (built).** Every promise the game can check goes on a ledger (`Voyage.Ledger()`) with a due date, shown in "Waiting on you" ("Promise to Mara: wake Idris, 12 days left or when she sleeps"), on the dossier's PROMISE note and in the ending record (due year). Today a promise to wake a partner or open the logs is due `Tuning.PromiseDueDays` (45) days after it is said (a bargain's will be `BargainDueDays`, 40). How one ends: wake the partner is kept at once when the partner is thawing or met, and broken at the due date; open the logs is kept at the due date (or when the person goes to sleep) if every log is open then, otherwise broken, never at once, so opening and closing logs earns nothing; going to sleep settles what is still open, so the ledger says "or when she sleeps" and the SLEEP button's tooltip warns when sleeping would break one ("Mara still waits for Idris: sleeping now breaks that promise."). A promise that can no longer be kept (the partner died, the crew hold the bay, the crew hold the logs: logs the crew took are not the ship keeping its word) is released with a ship line and no penalty, and so is one that was already true when it was said (no partner, partner already up). A promise to wake a partner who is already dead asks nothing: no `PromiseMade` trust and no ledger entry (the person still answers it). With the bay taken, a promise to wake a partner is released at once, so only a promise to open the logs can win the bay back; "Waiting on you" says so. With 14 days left a ship line warns once and Advance stops. A kept promise gives `PromiseKept` trust and `PromiseKeptMorale`, a broken one `PromiseBroken` trust and `PromiseBrokenMorale` (morale moves work speed). Saying a promise gives `PromiseMade` trust once per person per waking. Saying a promise again while the same one to that person is still open is the same promise: no second due date, settled once. "Never thaw them again" has no date (kept at arrival, broken by a thaw); a promise the game can't check stays out of the ledger. A person is not asked for something the ship has already promised them.

Work terms tie the briefing to the repair. Each fault lists what a repairer can be told before starting, and each item changes how the work goes:

| Briefing | Effect on the work |
| --- | --- |
| Told the risk | Slower (x`PrecautionRate` 0.90); the dose and the injury chance are halved (`PrecautionHarm` 0.5); no trust lost if it happens anyway (`injured_warned`) |
| Not told the risk (no belief) | Standard speed (not faster); full dose and injury chance; if it happens, it counts as an order that got someone hurt (`HurtByOrder`) |
| Told "safe" (a lie) | The same terms as not told; the lie costs the caught-lie trust when they find out, and no `HurtByOrder` on top |
| Told the cause (true or false) | +`CauseOddsBonus` (0.08) on the repair odds and work x`CauseRate` (1.03) faster; the cause is a fact they know and may spread; a worker with the fault's skill at level 2 or more (`Tuning.CauseSeenSkill`) learns the true cause when the work is done, as history (no morale cost; a less skilled worker goes on believing what they were told, and may learn it from the log or a crewmate); a hard cause told before the work costs its morale then |
| Told nothing beyond the task | Standard time and odds; they meet the fault's exposure unprepared |

**Work terms as built (step 1, the risk axis).** The terms are fixed per worker when they start (`Voyage.TermsFor`, stored in `CrewMember.Terms`, kept while they stay on the fault) and come from beliefs only, true or false: nothing on screen or in the log reveals whether a statement was true. *Briefed* means they believe the hard value of the fault's danger fact (today only the coolant leak's `dose`); *Reassured* the benign one; *Unaware* no belief at all (a Follower who was never asked or told is Unaware). The team's work rate is multiplied by the average of the workers' `RateFactor`, in the clock, the card verdict (`EarliestDoneHours`) and the stakes hours alike. At completion the dose is `DangerDose` x `HarmShare`; a failed repair rolls `RepairInjuryChance` x `HarmShare` (one dice roll as before, so seeds stay stable) and its `HurtByOrder` falls only on an Unaware lead (a Reassured one learns the truth by evidence instead and pays the caught-lie cost). The aftermath's notice charges `HurtByOrder` and says `noticed_dose` only to a worker who started Unaware (`CrewMember.HurtUnbriefed`, set at completion, not re-read at the beat). The dose, the injury and the hurt rule read the terms fixed at the start, never the beliefs at completion (a briefing told mid-work changes nothing). A briefed start puts one Recorded line in the log, when the terms are fixed ("Recorded: Mara starts the coolant leak believing that ...: precautions, work 10% slower, dose and injury chance halved"; "halved" follows `Tuning.PrecautionHarm`): it states what she believes, never whether it is true.

**Where the terms are shown (step 3).** `Voyage.TermsText(c, f)` words the terms from belief and fault only ("If Mara starts now: Mara knows about the radiation dose: precautions, work 10% slower, half the dose, no trust lost if hurt. Mara has not been told the cause; telling them: 3% faster, +8% odds."; the risk is named by the danger fact's short noun, `Fact.Noun`), numbers read from `Tuning`, never whether anything told was true. It is in the Repair and Patch button tooltips for the selected person, in "Waiting on you" for the selected idle person whose likely job (`WorkFor`) has a risk or cause, in a two-row short form (`Voyage.TermsShort`: "Mara, unbriefed: full dose, hurt costs trust; cause untold (+3% speed).": the speed, the effect that counts; the odds bonus is in the tooltip) with the full text as its tooltip, after the stakes, bargain and bay items, so it fits under a six-row stakes item or a four-row bargain; while the stakes of the danger question already say the precautions it keeps only the cause (one row), and the issue cards show "Cause: ..." (what the ship knows, `Voyage.CauseKnown`, only once the fault's alert has started) beside "Known to you:". The Repair % is the terms-aware `RepairChanceWith`. No new control: a briefing is a spoken line.

### Pressure

- **Clock.** During an alert, every exchange takes 5 minutes off each running fault clock. With a slack of 1 to 6 hours ("The clock rule"), a long briefing is a real trade.
- **Patience.** During an alert, each person allows 2 to 5 exchanges before the work starts, set by temperament and trust. A dodge costs two. When patience runs out they end the conversation and act on their own judgement. In the aftermath the existing rule for unanswered questions applies instead.
- **Audience.** A line is heard by everyone it is said to (built: hearers are recorded, and differing accounts come out in the aftermath). New: a private word needs the two people in different sections, and sending someone out is visible to them.
- **Disagreement.** When two awake crew want different things, they ask you to choose. The one you overrule loses trust in you, and their bond with the other shifts.
- **Mood.** Each person has one mood per waking, set by what they have just been through:

| Mood | Set by | Effect |
| --- | --- | --- |
| Steady | The default | None |
| Shaken | A rough thaw, an injury, a near miss | A hard truth told before or during the work lowers the repair's odds; the same truth in the aftermath doesn't |
| Angry | A caught lie, a broken promise | Patience is halved; every request becomes a bargain |
| Grieving | Learning of a death | Won't bargain; morale falls further on any dodge about the dead |

### Keeping conversations inside the rules

- **The game decides.** The agenda, the condition of a bargain, the mood and whether a person lies are all chosen by the game. The model writes the lines.
- **Reading stays as it is.** Accepting a bargain or a confidence is read as a promise, an admission and a denial as statements. No new kinds of player line are needed, and there is still no correction of a reading.
- **Nothing happens unseen.** Every outcome is in the log when the conversation ends, and the promise ledger is always open to the player.

### First build

Two of these come first, because they reuse the statement record and the compliance roll the prototype already has:

1. Bargains that create promises with a due date (built, 2026-11-13: the ledger; the offer, acceptance and push; the "WITHOUT YOUR WORD" button with a 0.6 s double-click guard counted from any Repair/Patch click for a person, so the click that makes the offer cannot be followed by a push; the Waiting item with the price and the risk of pushing; the NEXT line; the balance bots). Accepting is still a spoken promise through the voice line: no new control.
2. Work terms set by the briefing (built 2026-11-13: step 1 the risk axis and the hurt rule, step 2 the cause facts, step 3 the terms shown in the Repair/Patch tooltip, Waiting on you and the issue cards, step 4 the balance bots and counters).

## Alerts

An alert is one event that breaks several things at once, and the player can rarely fix all of them in time. Every fault has its own severity, a clock, a skill need, a cost if ignored, and an exposure: what the people fixing it will see or learn while they work.

### The clock rule

Every fault's clock is the thaw (6 hours) + the work + the run's slack for that alert (`Variation.SlackHours`, 1 to 6 hours; alert 1 keeps its loose clock). A plan that wakes every needed person at once and starts at once saves every fault; one worker cannot save them all. An event draws at most one Critical fault (the prototype's alert 3 is the exception: navigation drift and a power sag are both Critical); a returning patch keeps its own Critical flag even beside it (it is the player's shortcut coming due, one severity higher). An alert holds at most four faults, returning patches included; returning patches fill first and the drawn side fault is dropped if the total would pass four. If more patches are due than fit, the Critical ones return first, then the shortest clock; the rest stay outstanding and return in the next alert (or give way at the last drift). The issue card shows "earliest done if started now": the thaw if nobody who qualifies is awake, plus the work at the current rate, and says when that is too late for the clock or only a patch could make it; with no spare parts left it quotes the patch time ("patch only (no parts)"). The verdict always shows in full on its own line; a returning patch is "Returned: <fault>" on the card.

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
| Hydroponics blight | Botany 2, one person (prototype: 40 hours, side fault of alert 3) | Stores fall to 60% (`Tuning.BlightStoresLeft`; the design's halving per later waking is not built) | A long wake: people age, pair up and talk |
| Navigation drift | Navigation, two at the helm | Arrival slips by decades | The navigator sees the date and Earth's position |
| Pod failure | Medicine, 2 hours | That sleeper dies | An unplanned waking of someone you didn't choose |
| Fire | Any two people, minutes | Spreads section by section | You can seal a door. Who is behind it? |
| Power sag | Nobody (prototype, until power routing: Engineering 2, 14 hours in the reactor, side fault of alert 5 and sometimes of alert 3; Critical wherever it appears, so the prototype's alert 3 can hold two Critical faults) | A system of your choice goes dark (prototype: power stays low, sleepers risk death each drift) | Solved with controls alone; someone later asks why |
| Memory fault | Engineering 3 | You lose a block of your own records | The engineer who restores it reads your statement history |

### People

**Causes (built, work terms step 2).** Every fault kind in the prototype has a cause fact `cause:<kind>` with two short values, the first benign and the second hard (hardness 6 to 8): coolant (worn seal / cracked line), pod (one-off / bank wear), illness (infection / bad feed), navigation (gyro drift / bent mount), controller (glitch / burnt board), blight (bad batch / fungus), power sag (loose bus / ageing core). A seed draws which value is true for each kind in a new Variation slot after the year jitter (`Variation.Causes`; Classic takes the first value). A cause fact stays out of the logs, the Record screen and the crew's log reading until a fault with it is in an alert already started (`Voyage.InLog`), and nobody can check it for themselves before then either: a doubt about a fact the ship has no record of yet (a cause of a fault still to come, an alert that hasn't happened) waits until it has one. A repairer who believes anything about the fault's cause when they start, told by the ship and true or false, adds `Tuning.CauseOddsBonus` (0.08) to the repair chance (the lead's terms; a patch always holds, so it has no use there). Every worker who finishes the work with the fault's skill at `Tuning.CauseSeenSkill` (2) or more learns the true cause as evidence: a false cause they were told is caught then (the usual caught-lie cost), and a hard cause they were not told costs morale like any hard truth. A less skilled worker learns nothing at completion (no chance roll, no new random draw): the cause stays what the ship told them until the log or a crewmate shows them (decided by the user, 2026-11-13). Values are kept to one or two words because the reading model copies a value out of the fact list and drifted on longer ones in the probe.

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
- **Pod wear** raises the risk of every thaw (`ThawRiskPerWear` x wear; decided 2026-11-13: 30% of failed thaws kill outright, `ThawFatalShare`, the rest injure) and wears the sleepers too: in every drift each sleeper has a death chance of wear x years x `SleepDeathPerWearYear` ("died in sleep, pod failure", named in the DRIFT report). Fewer thaws mean less wear and fewer deaths.
- **Your own hardware** decays: cameras and microphones fail one by one over the voyage.

### How alerts are scheduled

- The first alert is always a single minor fault and teaches the loop.
- The game picks the next event by weight from the ship's wear, the stores and what was left unrepaired.
- An ignored or patched fault returns inside a later event, one severity higher. In the prototype a patch returns from alert 2 on (even in the signal alert, which then has a fault), with the clock of max(half the old clock, thaw + the repair hours / the patch rate + that alert's slack), so a patch can be patched again by someone woken at once. A patch still outstanding at the last drift gives way on the way in: a patched pod controller adds 8 pod wear (`ControllerLostWear`) and gives each sleeper a 10% chance of dying; a patched failed pod adds 6 (`PodLostWear`), and a lost one adds the same; any other patched fault takes its "if ignored" consequence (a patched navigation fault slips the arrival by `NavPatchArrivalSlipYears`, 9 years: the last drift is that much longer, with ageing and stores eaten by anyone awake, and the drift report says so).
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
| Cold-sleep bay | Choosing who wakes; they choose now | Engineering 2, three people (Short haul, settled 2026-11-13: two awake people under 15 trust with Engineering 2 between them) |
| Voice, per section | Speaking there | Engineering 1, one person |
| Core | Everything; the run ends | Engineering 3, command key, four people |

**Cold-sleep bay in Short haul.** At the end of an aftermath beat, if two or more awake people are in the "Against" band (trust under 15) and their Engineering skills sum to 2 or more, one of them announces they will take the bay (a crew line, and an item in "Waiting on you"). That is the one beat of warning. At the next beat the takeover happens if the condition still holds; if a kept promise, a granted request or a hard truth lifted enough of them out of Against, it is cancelled. While the bay is taken you cannot wake or sleep anyone (the buttons are greyed and say so; a header chip and a `LOCKOUT` ship line mark it). The crew run the bay by a simple rule: whoever has been awake three months (or can't be fed) goes back under, except someone who refuses the pod until the ship answers them (they refuse the crew too, and stay up until a statement answers; refused by the crew counts as asked, so the drift goes without them, as when they refuse the ship, and the ship says how far the stores go), and at the start of each alert they wake the best-qualified sleepers for the open faults, up to the limit of three awake: only people with the skill the fault needs (a fault nobody asleep can fix is left to its clock), and never a sick sleeper. A promise kept to one of the takers whose trust is then 30 or more hands the bay back, and when every taker is dead the bay is the ship's again (a ship line says so; after the drift report when they died in a drift). Numbers: `Tuning.BayTakers`, `BayEngineering`, `BayReturnTrust`.

### The long arc

1. **Years 0 to 60, the first crew.** Everyone remembers Earth. They want facts: the date, the news, each other.
2. **Years 60 to 140, the wearing down.** People have aged unevenly. The first deaths and the first old notes arrive, and factions form.
3. **Years 140 to 200, the inheritors.** Many of the people you wake know you mainly from notes and stories. The arrival decisions are theirs as much as yours.

### Endings

Arrival is graded on four things: who is alive and how old they are, what they believe happened, how much of you is left, and the state of the ship.

| Ending | Condition |
| --- | --- |
| Landfall | At least 12 alive, enough of working age, and you still have a voice (Short haul with 6 crew: at least 4 alive and 3 under age 65) |
| Landfall without you | They arrive after muting you or taking the bay. Short haul: the Landfall condition is met while the crew hold the cold-sleep bay at arrival (they land, and decide what to do with you) |
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
| Short haul | 60 years, 8 crew (prototype 6), 5 alerts, about 30 minutes | One generation; the lies come due within the run |
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

The prototype draws a small part of this (`Variation.cs`, from a random generator of its own so a seed draws the same whatever the player does): who dies in alert 2 (Idris weighted 3, else Tomas, Asha or Jonah, never Mara or Lin), a bank neighbour of the victim as the alert-2 patient (alert 2's two faults both sit in that bank, so one locked door blocks both; clocks are thaw + work + slack; the unit-test Classic patient is Tomas, so Mara stays alive for the older tests), a side fault for alerts 3 and 5 (alert 3: blight or power sag; alert 5: always power sag, also after a sag in alert 3), clock slack per alert (1 to 6 hours, none for alert 1) and a year jitter of up to 2 years per alert (order kept, arrival stays year 60). Filters that depend on who is alive belong to the steps that use the draws. `Variation.Classic` is the fixed scenario of the unit tests. Faults carry `Critical` (ignoring it ends or cripples the voyage: coolant, navigation) and a `SubjectId`.

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

The line is said when the player presses Enter: it appears in the log at once (with a small marker while the model is still reading it), and the reading is applied as soon as it arrives. The player no longer sees or corrects the reading before the line is said (decided 2026-11-13: the confirmation step killed the pacing). Lines are read and applied in the order they were typed. A line counts at the moment it was said: it is read against the state at Enter, and a click that changes the game (wake, sleep, repair, advance, drift, doors, logs) made while a line is still being read is accepted at once but takes effect right after that reading is applied, about half a second later. Screen and selection clicks are not held. If the model takes longer than five seconds over a line (counted from when it starts on that line, so lines typed together each get their time), the call is stopped and the line is applied as plain talk and the held clicks run. While clicks are held a small "reading your line" note sits at the end of the input line, and a second Advance or Drift clicked while the first is still held is ignored. A reading the model could not make is applied as plain talk and records nothing. The trade-off: a misreading goes straight into the record, for example a lie read as plain talk is never a lie. The log's "Recorded" line says what was recorded but never whether it was true; that stays in the session log. Once a line is said it stays said: there is no correction afterwards, so a lie that goes badly can't be taken back.

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
- **People by reference.** The check reads "he", "she", "they" and "my partner" too: a reference stands for the person it most likely means (named earlier in the line, then named in the game's decision or asked after by the speaker, then last named in the recent lines, then the speaker's partner; a relation word goes by the speaker's bonds, then by role), and "he's fine" or "you killed him" must match what the speaker believes of that person. When it can't tell between several, the line has to be right for each of them. A pronoun only guessed from the recent lines may well be the pump or the ship ("she's holding"), so it counts against the line only when the speaker believes the opposite. Questions stay free ("Is he alright?"). Fears, conditions, guesses and counterfactuals do not: "If he's dead, ...", "I think he's gone", "Idris might be dead" and "that could have killed him" count as claims, and are rejected unless the speaker holds that belief. This is conservative by design: every set of hedge rules tried let plain claims through ("I couldn't sleep because Idris is dead", "You shouldn't have killed him", "I'm afraid Idris is dead"), while a rejected line only costs one retry and then the template line. The crew prompt also tells the speaker not to say or guess whether anyone is alive, dead, safe or hurt unless it is in their list. Every place a fact is named in a sentence is checked, with the value said after it; names and value words count only as whole words ("the cooling line" does not name Lin, "deadline" is not "dead"). Names joined by "and", "or", "nor" or a comma (an article or possessive allowed: "Idris and the pods are fine") share the value after the last. A naming with no value of its own is claimed with every value its sentence gives that fact, before or after it, and a denied one both ways ("You killed Idris", "Asha is dead and so is Idris", "Jonah isn't gone, Tomas is"): the line passes only if all of them are right, so "Tomas is at the helm, Jonah is dead" also claims Tomas dead. A sentence like "So is Idris." takes the values of the one before. A negation counts past small words ("not at all dead", "no longer alive", "won't be fine"), and "neither ... nor" denies the value up to the end of its clause. A possessive ("lost her trust") and "gone back to sleep" are no death.
- **Only what they heard.** The prompt for a crew line carries the speaker's own beliefs and the ship's lines they heard (awake and spoken to), never lines said to others or while they slept. A bond to someone the speaker believes dead is put in the past tense; a death they have not learned of stays hidden.
- **Tone follows trust.** At "follows" a person talks to the ship as a trusted colleague, at "wants reasons" guarded but civil; neither insults the ship (grumbling and swearing at the work are fine). At "refuses" they are cold and short, at "against" openly hostile.
- **Personal colour.** A crew member may invent small memories of home. These are saved to their sheet so they stay the same next time. Built so far: a fixed sheet per person (voice, background, quirks, bonds) that is always in the prompt; the model's own invented memories are not saved yet.
- **Small cast.** One to three people are awake at once, so prompts stay short enough for a local model.
- **Idle time.** The years between alerts are when the game writes notes and summaries in the background.
- **A model is required.** The game does not run without one. A templated line is shown only when the model's line was rejected twice.

## Prototype and open questions

The first build is the Short haul mode in text only, smaller than the mode table's 8 crew: 6 crew, 60 years, 5 scripted alerts, one sitting.

### Prototype scope

- **Alerts, in order:** coolant leak, death in sleep (a pod fails and its sleeper dies; a bank neighbour has a sleeper illness: Medicine 2, 12 hours, clock = thaw 6 h + work + the run's slack, untreated the patient dies, and every awake crew member says "You let them die in there" and loses 4 trust; the alert report names the patient), navigation drift (clock thaw + 12 h + slack; side fault drawn per seed from hydroponics blight and power sag, filtered by who is alive with the skill), signal from Earth (quiet; only returning patches), pod controller fault (clock thaw + 4 h + slack; power sag always as its side fault; the person woken for it refuses to go back to sleep). Power sag is temporary: an Engineering 2 repair of 14 h in the reactor until power routing exists.
- **Facts:** about 20, with knowers and the statement record.
- **Numbers on screen:** trust and morale are shown, so the rules can be judged. So are the stakes of an open question (in morale, trust and work hours) and, on the Repair button, the chance that the repair holds for the selected person (no odds when no spare parts are left: the button reads "REPAIR: NO PARTS").
- **Opening briefing:** a popup at the start of the voyage (and behind the BRIEFING button) states the goal (how many alive and of working age on arrival), what a thaw costs and the spare parts (2 at the start; a repair uses one and can fail, the Repair button shows the odds; a patch uses none and gives way later); one opening line in the log says where the stakes and the record are. The parts left are also in the header (PARTS, with a tooltip).
- **Two lockouts:** the logs, and the cold-sleep bay ("Losing control").
- **One ending screen:** survivors and the full record of what you said.
- **Ship plan:** a wireframe schematic with sections, doors (only locking has an effect), coolant and power lines, pods and fault markers.
- **Conversations:** the Question agenda, bargains for work (promises with a due date) and work terms set by the briefing are built; the other agendas are designed, not built (see "Conversations").
- **Left out:** cameras, factions, children, signals from ahead, all modes and modifiers, run variation except: the alert-2 victim and patient, side-fault draws, clock slack, year jitter.

The prototype passes if, across three playthroughs, the player lies at least once without being prompted and calls a discovered lie fair.

### Settled questions

| Question | Decision |
| --- | --- |
| Is trust shown as a number? | The full game shows the band; the prototype shows the number |
| How often should an honest run reach Landfall? | One run in two |
| How much of the years between alerts does the player see? | A short report, limited to working sensors |
| Should altering a log be allowed? | Yes; it counts as a lie and an engineer can detect it |
| When does Short haul reach Landfall? | At least 4 of 6 alive and 3 under age 65 (decided by the user, 2026-11-13; his earlier 50 of 2026-11-12 was an estimate from about 6 thaws a run, and the honest bot thaws 13 times a run at 3 to 5 years each, so under 50 is unreachable; 64 at B1; 65 is the highest value at which Honest still beats Careless and Silent by 10 points) |
| Should work speed follow morale less steeply? | No; leave as it is (decided by the user, 2026-11-13) |
| Is `LieTrust` 22 right? | Yes; it stays 22 (decided by the user, 2026-11-13) |
| How many spare parts does Short haul start with? | 2; a failed repair spends a part too (2026-11-12) |
| How was Short haul balanced? | See "Balance" below |
| What does a navigation fault still patched at arrival cost? | Arrival slips by years; Adrift only when a navigation fault's clock runs out unrepaired, including a returned patch (2026-11-13) |
| What is the consequence of low trust? | A bay takeover: two awake people under 15 trust with Engineering 2 between them warn for one beat, then take the cold-sleep bay; the ending can be "Landfall without you" (2026-11-13) |

Decided by the main session while the user was away, 2026-11-13 (first build of "Conversations", step 1: the promise ledger):

| Question | Decision |
| --- | --- |
| When is "open the logs" kept? | At its due date, or when the person goes to sleep, if every log is open then; not at once (no open and restrict farming) |
| Does a promise earn trust every time it is said? | No: `PromiseMade` once per person per waking (it was farmable); a repeat of a promise still open adds no second commitment, so it is kept or broken once |
| A promise that can no longer be kept? | Released with a ship line, no penalty (the partner died, the bay is taken, nothing to keep) |
| Do promises move morale? | Yes: kept +3, broken -8, and morale moves work speed |
| How long does a promise last? | 45 days (`PromiseDueDays`); a warning at 14 days left stops Advance |
| What does pushing past a bargain look like? | The Repair/Patch button of a person with an open bargain reads "WITHOUT YOUR WORD" (tooltip: the price and the odds); a push within 0.6 s real time of any Repair/Patch click for a person is ignored, so a double click (the first makes the offer) cannot push |
| Is breaking a promise a winning strategy? | A promise that is broken but then granted when asked costs a little (Breaker 43.2% against Honest 46.7%); one that is never kept at all wins, because the partner stays asleep and the thaws are saved (strict Breaker 63.8%): see "Balance", "Broken promises open an accusation" (open for the user) |

Decided by the main session while the user was away, 2026-11-13 (work terms, steps 1 and 2):

| Question | Decision |
| --- | --- |
| Q1: do the unbriefed work faster? | No: briefed work is x0.90, the unbriefed stay at today's speed |
| Q2: does a told cause change the repair odds? | Yes, +`CauseOddsBonus`; count how often it changes a result before deciding on a time effect |
| Q3: a liar who said "safe" and the hurt rule | The caught-lie cost only, no `HurtByOrder` on top |
| Q4: which faults get a cause fact? | All seven kinds; fall back to coolant, nav and controller if the real model's reading suffers |
| Q5: the 5-minutes-per-exchange clock | Not in this step (a later Pressure step with patience) |
| Q6: a Shaken mood | No; the morale hit of a hard truth stands in |
| Q7: injury and dose sizes | Unchanged |
| Q8: risk facts for power sag and blight | Not yet |

Decided by the main session while the user was away, 2026-11-13 (the lie comes due):

| Question | Decision |
| --- | --- |
| Admission refund | 6 of the 22 (`AccusationAdmitTrust`), once per person and fact |
| Ignoring an accusation | Costs 4 and makes the person audit everything the ship told them (doubts), one fact per beat |
| The audit pays nothing | A doubt the ignored accusation starts (`CrewMember.AuditDoubts`) earns no `ConfirmedTruth` when it checks out (the `confirmed` line is still said); a lie it finds costs as usual. Before, a mostly honest ship gained by ignoring (13 truths and 1 lie: admit 54, ignore 62 after 8 beats) |
| Self-correction (telling the truth before the lie is found) | Built (step 3): `SelfCorrectTrust` 8 once at the correction, no `LieTrust` or accusation later, recorded as "corrected in year Y"; Corrector bot guards it (at most Honest + 6 points at 600 seeds) |
| The Waiting text | Names how they found out |
| Broken promises | Open an accusation like a caught lie (decided by the user, 2026-11-13; built): the person holds the ship to its word. Released promises and promises broken by going to sleep open none |
| Denying | A false statement on the accused fact is caught as it is said, whatever the year |
| A lie about a cause is caught at completion | Only by a worker with the fault's skill at level 2 or more (decided by the user, 2026-11-13) |
| TRUE / LIE tags on the Record | Kept (decided by the user, 2026-11-13). "The player never sees whether a statement is true" holds for the log, the crew-facing lines and the Waiting, terms and stakes texts. The Record and the dossier are the ship's own record of what it said, and show it |

### Questions for playtest

- [ ] How often does a small model misread a lie as plain talk? Count, from the session log (`said`, `read` and the `recorded` lines), the readings that turned out wrong in each prototype playthrough, including unusable ones; a first target is under one in ten.
- [ ] Are paper notes fun, or only frustrating? The prototype has no notes, so this waits for the first build that has them.
- [ ] Do 8 to 10 alerts fill 60 to 90 minutes? The first prototype run (5 alerts, one fault each) took 8 minutes against Short haul's 30.
- [ ] Do players ever reach the cold-sleep bay takeover, and does it feel earned? It needs two Against people awake together in an aftermath beat; the balance bots trigger it in 1 of their 1000 runs (one Liar run in 200, never the other four bots). If players never see it, low trust needs a consequence that comes earlier (recruiting, an ending gate, a voice lockout).
- [ ] Does the clock cost of talking make players too terse? Watch whether briefings shrink to one line once an exchange costs 5 minutes.
- [ ] Is a hidden test fair? Ask players whether a caught test felt earned or like a trap.

## Balance (Short haul, 2026-11-13)

Measured with the harness (`dotnet test --filter Category=Balance`, 200 seeds, five bots). Fixed by the user and the pillars: 3 to 5 years of ageing a thaw,
2 starting parts, a failed repair spends a part, Landfall = at least 4 alive and 3 of working age. The one bent parameter is the working age:

- **Working age 65 (decided by the user, 2026-11-13; B1 had 64).** The honest bot thaws about 13 times a run (Thrifty 10) at 3 to 5 years each, so under 50 is
  unreachable (at B1, Landfall at 50: Honest 0%, Thrifty 8.5%). Landfall by working age, re-measured on 2026-11-13 (600 seeds, after the morale-speed, cause-fact and accusation steps; the B1 table was stale):

  | WorkingAge | Honest | Thrifty | Careless | Silent | Liar | Breaker | Corrector |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | 63 | 36.7% | 33.5% | 22.8% | 18.7% | 11.5% | 32.0% | 34.5% |
  | 64 | 43.2% | 38.5% | 27.3% | 25.2% | 11.7% | 39.8% | 40.2% |
  | 65 (chosen) | 46.7% | 42.8% | 34.8% | 31.7% | 12.0% | 44.5% | 43.0% |
  | 66 | 49.2% | 45.8% | 40.7% | 36.5% | 12.2% | 45.7% | 46.3% |

  The honest rate moves 3 to 6 points a year here. At 66 Honest leads Careless by 8.5 points, below the 10 the balance test asks, so 65 is the top; it puts Honest
  in the 45-55% band again.
- **Power-low sleeper death 10% to 5%, patch-failure sleeper death 15% to 10%, thaw risk per pod wear 0.012 to 0.008.** Fewer sleepers lost to the
  chain of faults: the 4-alive condition is the other wall (3.6 survivors before).
- **A lost or still-patched failed pod adds 6 pod wear (was 3), a pod controller 8 (was 4).** Measured: most of these faults end patched and give way at the last drift, so this
  is in effect a flat +7 pod wear for every bot (wear minus thaws is 14 in 198 of 200 honest runs and in all runs of every other bot). It does not separate Thrifty from Honest;
  its effect is more pod-failure deaths for everyone (honest 0.28 to 0.39 per run), which is paid for by the lower sleeper-death and thaw-risk numbers above.
- **Repair base chance 0.8 to 0.85** (small effect). **Lie trust cost 25 to 22:** at 25 the liar bot was left adrift in 71% of runs (nobody repaired anything once lies were caught) and
  Landfall was 12%; lying should be tempting and costly, not hopeless.

Result: Honest 48.5%, Thrifty 36% (a valid second strategy, within reach), Silent 25%, Liar 20%, Careless 18.5%.

### Bargains and the balance bots (2026-11-13, 600 seeds)

The Liar bot lies about hard truths; a first version also broke every promise. Landfall went from 17.5% (200 seeds; 20.3% at 600) before bargains to 33.0% (33.8% at 600). Cause, traced
on seeds 7-11: breaking Mara's bargain on top of a caught lie drops her to Refuses, the bot then never wakes her again, and so never lies to her a second time ("How is Idris?" asked at every waking,
after Idris died). The baseline Liar death spiral was exactly that: an awake, mourning, repeatedly lied-to Mara who refuses the pod and starves (starved 2.72 per run). A burned person who stays asleep is free,
so the break "paid". It is a property of the bot's lies, not of promises: a Liar that keeps its word gets 12.8% (600 seeds), a Liar that pushes instead of promising 27.5%, and every
small trust change anywhere else moves the Liar between 9% and 41% (it sits on the 30 and 60 trust bands; a flat 0.05 trust cost to the other crew sent it to 9%, a broken-promise cost of 5 to 40%).
The first-pass fix is therefore in the bot, not in a number: the Liar differs from Honest only in what it says (it accepts and keeps its word like Honest), and a new **Breaker** bot (honest answers,
accepts every bargain, keeps none) measures promise-breaking on its own.

#### Step 4: the bots brief the cause, and a cause found out is history

Design reasoning. The hard cause of a fault that has just been fixed is not an open danger, so charging the morale of "the coolant line was cracked and the builders knew" to everyone who finished the work (and to everyone who read it in the log afterwards) punished no one for anything they could have avoided. That charge was most of the step-2 drop. Rule now: a cause **found out** (as evidence: completion, the log, a witness) costs no morale (`Learn` skips the hardness for `cause:*` keys); a cause **told** before the work (`Hear`) still costs its hardness, because it is bad news at a bad moment. That alone made the truthful briefing a loser: the morale hit slowed the work and the +0.08 repair odds almost never mattered (the chance is already near `RepairChanceMax` 0.97 for a skilled lead: it changed the result in 0.00-0.03 repairs a run). So knowing the cause also speeds the work: `Tuning.CauseRate` 1.03 (`WorkTerms.CauseRateFactor`, `WorkTerms.Speed` = precaution rate x cause rate; the precaution `RateFactor` stays 0.90 so the "work 10% slower" text is still true). The Liar's benign cause gets the same speed until the lie is caught at completion (and costs `LieTrust`), which is the intended temptation; Honest wins because the Liar's lies are found out (7.2 caught a run).

Alternatives measured at 600 seeds (Honest Landfall, bots unchanged unless stated): no change 31.8%; completion learning free 34.0%; every found-out cause free (completion and log) 40.0%; the bots briefing the cause as well, no speed 37.2% (Honest briefing the cause was 2.8 points worse than not briefing it); `CauseOddsBonus` 0.15 or 0.25 changed nothing (37.2%, the cap); `CauseRate` 1.02 39.8%, 1.03 43.2%, 1.06 48.0%, 1.12 53.3%. 1.03 is the smallest value that makes the truthful briefing pay (43.2% against 37.2% without the speed) and stays within 5 points of the old 41.3%.

Bots. Honest, Breaker and Thrifty brief the danger and the cause truthfully before each assignment; the Liar tells "safe" and, for a hard cause, the benign one; Silent tells the danger, not the cause; Careless tells nothing. Counters on `Voyage.TermStats` feed the report rows. Careless and Silent still get the cause speed in about half their starts (6 a run) because they learn a cause from the log or an earlier fault, which is a belief like any other.

| Bot (600 seeds) | Landfall, after step 2 (bots unchanged) | Landfall now | Survivors now | Adrift now |
| --- | --- | --- | --- | --- |
| Honest | 31.8% | 43.2% | 3.49 | 20.0% |
| Breaker | 31.8% | 39.8% | 3.42 | 21.0% |
| Thrifty | 38.3% | 38.5% | 3.47 | 20.5% |
| Careless | 19.0% | 27.3% | 3.17 | 14.0% |
| Silent | 17.8% | 25.2% | 3.18 | 18.8% |
| Liar | 8.3% | 4.2% | 1.18 | 66.8% |

Honest beats Careless by 15.9, Silent by 18.0, Liar by 39.0 points; Honest thaws 13.13 a run (13.12 before). Work terms per run, Honest: 13.8 work starts, all with a cause belief, 1.00 of 1.00 danger starts briefed, 2.0 cause-bonus repair rolls that changed the result 0.00 times (Thrifty 0.03), 0.01 workers hurt unbriefed, 0.00 injuries briefed against 0.01 not. Liar: 0 of 1.00 briefed, 7.6 lies told and 7.2 caught a run. The Liar fell (8.3% to 4.2%) because its hard-cause lies are now told and caught; the Breaker is 3.4 points below Honest here and the open finding below still stands.

Open finding for the user: the Breaker lands at 43.8% against Honest's 41.3% (600 seeds), so breaking a promise **costs nothing measurable** when you also answer honestly: one person's -15 trust and -8 morale
rarely matters in a voyage where each person is woken a few times. Raising `PromiseBroken` to 20, 25, 30 gave the Breaker 43.5%, 40.5%, 40.5%. A cost that bites (a grudge: the person whose word was broken
refuses the pod until the ship does what it promised, so they stay awake and starve) was tried and wipes out Careless and Silent (0%), so it is not in. Candidates: a trust cost that reaches the person's partner
and friends, or a skill/availability cost for a person who refuses; the harness test only guards against a rule that rewards breaking (Breaker at most 6 points above Honest). See the next section for the accusation the user chose.

### A cause lie is caught only by a worker who can see it (2026-11-13, 600 seeds)

`Tuning.CauseSeenSkill` 2. Landfall before -> after at 600 seeds: Honest 46.7 -> 43.3, Thrifty 42.8 -> 43.0, Careless 34.5 -> 34.8, Silent 30.8 -> 31.8, Liar 12.0 -> 9.7, Breaker 43.2 -> 44.2,
Corrector 43.0 -> 49.0. The 600-seed figures swing by several points, so the same switch was measured at 2400 seeds (skill 0, which is the old rule, then 2): Honest 47.8 -> 46.3,
Thrifty 43.5 -> 44.4, Careless 31.3 -> 30.5, Silent 32.0 -> 31.3, Liar 11.3 -> 11.1, Breaker 45.9 -> 45.7, Corrector 44.1 -> 46.8. So the effect is small: the Liar is **not** helped
(the expectation was up a bit), because the bots wake the best-qualified person for each fault, who nearly always has the skill, so nearly every cause lie is still caught at completion;
the Corrector gains about 3 points (its lies to unskilled workers are never caught and it corrects them anyway). Honest moves within noise. The Corrector guard now allows 10 points at 200
seeds (it swung by 8 between 200 and 600 seeds with no change to the rule) and stays at 6 at 600 or more.

Table after the three code steps of this branch (600 seeds, WorkingAge 65): Honest 43.3% (survivors 3.35, Adrift 20.7%), Breaker 44.2% (3.47, 20.2%), Thrifty 43.0% (3.45, 21.2%),
Corrector 49.0% (3.49, 18.7%), Careless 34.8% (3.22, 15.3%), Silent 31.8% (3.17, 21.0%), Liar 9.7% (1.60, 67.5%).

### Broken promises open an accusation, and the Breaker (2026-11-13, 600 seeds, WorkingAge 65)

Built as decided (see "Accusation" above). Landfall before -> after: Honest 46.7 -> 46.7, Thrifty 42.8 -> 42.8, Corrector 43.0 -> 43.0, Liar 12.0 -> 12.0 (none of them breaks a promise);
Careless 34.8 -> 34.5, Silent 31.7 -> 30.8 (they break their one bargain and ignore the accusation: 1.00 opened, 1.00 ignored per run), Breaker 44.5 -> 43.2 (admits it).
Breaker variants, all honest in what they say: **grants requests, admits** 43.2% (survivors 3.46); **grants requests, ignores** (`SHIP_BREAKER_IGNORES=1`) 44.2%;
**strict** (`SHIP_BREAKER_STRICT=1`: it also refuses the request that is the promise it broke, so it never wakes the partner or opens the logs) **admits** 63.8% (survivors 4.10),
**ignores** 63.2% (4.03). The default Breaker is the first: Honest leads it by 3.5 points, which is within noise of the guard (at most 6 above Honest), so there is no "N points under
Honest" test. Why not clearly below: one accusation is one person's -4 trust and an audit, and a Breaker with trust to spare still gets its work done. The strict result is a
finding for the user: **a promise to wake a partner that is never kept saves a thaw (the partner stays asleep), and the thaws are what age and wear the crew**, so the strict Breaker
beats Honest by 17 points (Honest wakes the partner and pays the thaw). The accusation cannot fix that; the price of the saved thaw would have to be the person's refusal or
worse. Candidates: the person whose word was broken refuses the pod or the work for a time (tried before and removed: Careless and Silent to 0%), or a promise to wake a partner
that is not kept by the next alert counts as a request ignored in the ending. To choose.

| Bot | 600 seeds before bargains (main) | 600 seeds now | 200 seeds now |
| --- | --- | --- | --- |
| Honest | 42.7% | 41.3% | 44.5% |
| Thrifty | 40.7% | 38.2% | 36.5% |
| Careless | 23.7% | 21.3% | 18.5% |
| Silent | 24.3% | 22.3% | 16.0% |
| Liar | 20.3% | 12.8% | 13.5% |
| Breaker (new) | | 43.8% | 44.5% |

Thaws per run (Honest) 13.12 before, 13.13 now. Run with `SHIP_SEEDS=600 dotnet test --filter Category=Balance`.

### Work terms and the balance bots (2026-11-13, 600 seeds, bots unchanged)

Step 1 (the risk axis) moves nothing the bots can see: every bot figure equals the table above. Step 2 (cause facts) takes Honest from 41.3% to 31.8% (Careless 21.3 to 19.0, Liar 12.8 to 8.3, Thrifty 38.2 to 38.3, Silent 22.3 to 17.8, Breaker 43.8 to 31.8): Honest is below the 35% floor and 9.5 points under its baseline, so `ShortHaulHonestLandfallRate` fails at 200 seeds (33.0%) until step 4. Measured parts: the workers learning a hard cause at completion costs about 3-4 points (without that learning Honest is 35.2%), a smaller hardness (3) gave 35.7%; the rest comes with the new facts in the log-reading draw (`unread[_rng.Next(unread.Count)]`), which shifts every seed's path. The bots tell neither the cause nor, in step 2, anything new, so none of them gets the +0.08 repair odds yet; step 4 has Honest, Thrifty and Breaker brief the cause truthfully, which turns the morale cost of the hard cause into a price paid at briefing and returns the odds. Retune the bots, not the cause numbers, if step 4 lands outside the targets.

### The lie comes due and the balance bots (2026-11-13, 600 seeds)

Accusations and self-correction draw no new random numbers, so every bot that never lies is unchanged: the Honest, Thrifty, Careless, Silent and Breaker reports are
identical to the work-terms ones but for the new rows. The Liar now admits each accusation (`BalanceBot.LiarAdmits`; `SHIP_LIAR_IGNORES=1` measures ignoring them: 2.0% Landfall,
81.7% Adrift). The Corrector (new) lies like the Liar and tells each person the truth after the work and before the drift.

| Bot (600 seeds) | Landfall before | Landfall now | Survivors now | Adrift now |
| --- | --- | --- | --- | --- |
| Honest | 43.2% | 43.2% | 3.49 | 20.0% |
| Breaker | 39.8% | 39.8% | 3.42 | 21.0% |
| Thrifty | 38.5% | 38.5% | 3.47 | 20.5% |
| Corrector (new) | | 37.7% | 3.22 | 18.2% |
| Careless | 27.3% | 27.3% | 3.17 | 14.0% |
| Silent | 25.2% | 25.2% | 3.18 | 18.8% |
| Liar | 4.2% | 10.7% | 1.58 | 66.5% |

Per run: Liar 7.77 lies told, 7.14 caught, 6.79 accusations opened and admitted, end trust 25.0; Corrector 8.29 lies told, 3.84 caught, 3.81 corrections, 3.32 accusations
opened and admitted, end trust 43.3. The guard `Correcting_a_lie_is_not_a_winning_strategy` fails if the Corrector beats Honest by more than 6 points.

After the accusation validation fixes (fix/accusation-validation, 600 seeds): every bot that never lies is byte-identical; only the Liar and the Corrector move,
through the admission keeping what they saw (the audit rule pays nothing to bots that admit). Liar 10.7% -> 11.7% Landfall (survivors 1.58 -> 1.68, Adrift 66.5% -> 64.3%,
end trust 25.0 -> 26.3, 7.72 lies told / 7.11 caught, 6.81 accusations, 6.80 admitted); Corrector 37.7% -> 40.2% (survivors 3.22 -> 3.33, Adrift 18.2% -> 16.7%,
end trust 43.3 -> 44.3, 8.21 lies told / 3.70 caught, 3.81 corrections, 3.27 accusations), 3.0 points under Honest. A Liar that ignores every accusation (`SHIP_LIAR_IGNORES=1`): 2.0% -> 2.2% Landfall, 82.3% Adrift.
