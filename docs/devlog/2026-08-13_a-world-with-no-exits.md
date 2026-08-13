# 2026-08-13 — A world with no exits

A 150-turn `ashfall` run, played by a model, and the most productive session this project has
had. It was meant to answer one question and answered three.

## What it was for

`PROJECT.md` carries an open measurement: *does canon survive 200 turns?* Bootstrap proved 51.
Every save in the repo was 50–51 turns, so past that the binding constraint was expected to
stop being the extractor and become `ContextAssembler` — the one part of the turn loop never
under pressure.

It ran to 150 and stopped there because the story had visibly died.

## Answer one: canon survives, and the rejection rate falls

| turns | applied | rejected | turns changing nothing |
|---|---|---|---|
| 0–24 | 31 | 2 | 9 |
| 25–49 | 38 | 0 | 7 |
| 50–74 | 43 | 1 | 5 |
| 75–99 | 26 | 0 | 11 |
| 100–124 | 17 | 0 | 15 |
| 125–149 | **4** | 0 | **23** |

**Three rejections in 150 turns** — 0.02 per turn against the 51-turn session's 0.16. No
corruption, no canon/history desync, no drift.

Carry the asterisk honestly: the last fifty turns were degenerate, so context stopped growing.
This is eighty turns of story plus seventy of nothing, and therefore a *weaker* test of
context pressure than the number suggests. But there is no evidence here for summarization,
and some against — which is why `design/LONG_TERM_MEMORY.md` now says the whole line of work
is gated on this measurement rather than on picking between four approaches.

## Answer two: the world model can only build dead ends

The player reached `maintenance-shaft` on turn 80 and never left. The user read it as the game
looping on constrained struggle. It is not a narration failure.

`ContextAssembler` renders `Leads to:` from `Location.Connections`. The shaft's set was empty,
so the narrator was told: *you are in the maintenance shaft, right now fully buried beneath
compacted ash*, and nothing else. It then narrated a person sealed in with nowhere to go —
accurately, seventy times. **Canon was telling the truth about a world with no exits.**

The cause: **nothing in the delta set could ever connect two locations.**
`LocationIntroduced` carries id, name and description; no other kind touches the field. Every
location extraction has ever created was an orphan.

And this was never a two-observation hunch. Nine saves, both worlds, human- and model-played:
**33 orphan locations.** The only places in existence with exits are the two that came from a
hand-written seed. It reproduced in 100% of sessions ever played and nobody noticed, because
the symptom only becomes visible when a player walks somewhere they cannot narratively wander
back out of.

### The fix adds no delta kind

Someone who walked from A to B has demonstrated that A connects to B. That is entailment, not
judgement, so it is derived in `DeltaApplier.Connect` — the same reasoning, three cases up the
same switch, that records a character as knowing the fact they just asserted: *deriving it
removes the ambiguity rather than arguing with it.*

It is also the `item_lost` lesson applied on the first try rather than the second: **before
adding a delta kind, check whether the model already emits something that means it.** The
model emitted eleven correct `player_moved` deltas in this run. The graph was in the data the
whole time; nothing was reading it.

**Both directions, decided knowingly.** `Location.Connections` documents that connections are
deliberately not symmetric, because a one-way drop is a real thing. That is overridden for the
base game: the edge that unseals a room is the *return* one — walking ledge→shaft gives the
shaft nothing. A one-way drop is rare; a sealed room happened thirty-three times. A richer map
is plugin territory.

### Reproduced offline, not as an eval scenario

A departure worth naming. Every previous fix started with a scenario, because every previous
fix was about what the model emits. This one is not — extraction was correct every time. The
failure lives entirely in what *applying* a correct delta does, so a self-test is the honest
instrument, is deterministic, and costs nothing.

`CheckWalkingSomewhereConnectsIt` and `CheckAWalkedRouteIsTwoWay` both failed on HEAD before
the change.

**Verified against the real save**, which is the check that matters: replaying all eleven
`player_moved` deltas from the 150-turn history under the new rule leaves **zero orphans**, and
`maintenance-shaft` ends with three exits — ash pit, crawlspace, and the vent ledge it was
entered from. On turn 80 the narrator would have been told there was somewhere to go.

Scored set, pinned to StreamLake, n=5: **49/50 clean, forbidden 0.00, rejects 0.00** — the same
number, the same single `two-stage-entry` miss, and the same provider as this morning's run.
`movement` 5/5 and `player-arrival` 10/10.

## Answer three: three things this run exposed and did not fix

Logged rather than chased, per the standing rule.

**Facts died: zero established in 150 turns.** The one fact in canon is the seed's. Marrow's 51
turns produced 44. `mood_changed` fired once against Marrow's 46, and all three NPCs still sit
in the common room wearing their seeded moods. The cast was abandoned around turn 5, and canon
has nothing to say about a person alone in a hole. This is not obviously a bug — it may be an
accurate reading of a story with one character in it — but the shape is worth watching.

**Status has become prose.** The player ends as *"pinned deeper in ash, struggling to breathe,
scalded, choking on superheated grit"*; the shaft carries six clauses. The field was designed
to hold `"wounded"`. This is the events-in-status problem, previously logged at two
occurrences, now at scale.

**`location_status_changed` is 59 of 177 applied deltas** — a third of everything, 19 on the
shaft alone, largely re-describing the same trapped state. The delta shipped a week ago is now
the single most common output in the system.

## And the case for the phases got stronger

Two Phase-1-and-later arguments landed for free, from evidence rather than reasoning:

- **No ending condition exists.** Mara should have died or escaped around turn 120. Nothing in
  the system can conclude a story, so it ran another thirty turns and stopped only because a
  human got bored. That is the story layer's absence, measured.
- **Dice.** *Do I get out?* was asked roughly thirty times and the narrator decided by taste
  every time. `design/DICE_CHECKS.md` argues a roll is canon and the narrator must be told the
  outcome rather than asked for it; this is that argument with a number attached.

## Build

`dotnet build` clean, 0 warnings. All self-tests pass.
