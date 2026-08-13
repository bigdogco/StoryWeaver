# TODO: Location connections

**Status:** DONE 2026-08-13
**Created:** 2026-08-13

---

## The failure

Found in the 150-turn `ashfall` run, 2026-08-13. From turn 80 the player sat in
`maintenance-shaft` and the last 25 turns produced **4 applied deltas across 25 turns, 23 of
which changed nothing at all.**

It was not a narration failure. `ContextAssembler.cs:78` renders `Leads to:` from
`Location.Connections`, and the shaft's set was empty, so the narrator was told the player was
in a sealed room, currently *"fully buried beneath compacted ash"*, with no way out. It
narrated exactly that, faithfully, seventy times.

**Nothing in the delta set can ever connect two locations.** `LocationIntroduced` carries
`LocationId`, `Name`, `Description` — no connections — and no other delta touches the field.
Every location created during play is therefore an orphan island, permanently.

## It is in every save

Not a two-observation hunch. Nine sessions, both worlds, human- and model-played:

| save | locations | with no exits | turns |
|---|---|---|---|
| `ashfall` | 9 | 7 | 150 |
| `marrow-old` | 8 | 6 | 51 |
| `marrow-LLM-2` | 8 | 6 | 50 |
| `marrow` | 7 | 5 | 51 |
| `marrow-2` | 5 | 3 | 51 |
| `ashfall-previous` | 9 | 7 | 50 |
| ...and 3 more | | | |

**33 orphan locations.** The only locations in existence with exits are the two that came from
a hand-written seed. Every location extraction has ever created has none.

## The design question — needs an answer before any code

Three ways to fill the field.

### A. Derive it from movement — no schema change

When `PlayerMoved` or `CharacterMoved` lands, record an edge between where they were and where
they went. Someone who walked from A to B has demonstrated A connects to B.

**This is entailment, not judgement**, and there is precedent for exactly this in
`DeltaApplier`: a character who asserts a fact is recorded as knowing it, derived in code
rather than asked of the model, because *"deriving it removes the ambiguity rather than
arguing with it."* That rule replaced a prompt rule that had started failing.

It also respects the project's hardest-won rule — **a schema branch is not free** — by adding
none. Same shape as the `item_lost` fix: the model already emits something that means this.

Checked against the actual run: the player entered the shaft from `upper-vent-ledge` (t65),
left to `ash-pit` (t71), to `maintenance-crawlspace` (t75), and back to the shaft (t80). Under
A the shaft ends the run with **three exits**, and the narrator is told about all of them.

**Cost:** only records passages someone has walked. A door you can see but have not used is not
in canon. Arguably correct — canon records what happened, not what is possible.

### B. A `location_connected` delta

Explicit, and can fire when a passage is *discovered* rather than used. Costs a schema branch,
and every branch has twice wrecked an unrelated scenario.

### C. Connections on `location_introduced`

Cheapest-looking and probably worst: the model would have to name the neighbour at the moment
of creation, which is when it knows least, and it cannot revise later.

**Recommendation: A**, and B only if a measured failure demands it.
**Decided 2026-08-13: A.** No schema change; derived in `DeltaApplier.Connect`.

### The sub-question that is genuinely open: symmetry

`Location.cs:33` says connections are deliberately **not** automatically symmetric — *"a
one-way drop or a locked-from-one-side door is a real thing worth representing."*

But the edge that would have saved this run is the **backward** one. Walking
`upper-vent-ledge → maintenance-shaft` gives the shaft nothing; it is the return edge that
un-seals the room.

- **Both directions:** fixes the observed failure, occasionally wrong about a one-way drop.
- **Forward only:** never wrong, and does not fix the failure.

Leaning both. A one-way drop is rare and a sealed room happened 33 times — and canon is now
hand-editable, so being occasionally wrong is cheap in a way it was not last week.

**Decided 2026-08-13: both directions, for the base game.** A fancier map — one-way drops,
doors, locked states — is plugin territory, not something the base game should carry.

## Plan, once the question is answered

- [x] Reproduce first — **offline, not an eval scenario.** The model already emits
      `player_moved` correctly (11 of them in the ashfall run); the failure is entirely in what
      applying it does. A self-test is the honest instrument and costs no credits.
- [x] Verify the reproduction fails on HEAD — both new checks failed before the change
- [x] Implement: `DeltaApplier.Connect`, called from `PlayerMoved` and `CharacterMoved`
- [x] Self-tests: `CheckWalkingSomewhereConnectsIt`, `CheckAWalkedRouteIsTwoWay`
- [x] Check the fix against the real save — replayed every `player_moved` in the 150-turn
      ashfall history under the new rule: **zero orphans, and `maintenance-shaft` ends with
      three exits** including the way back to the vent ledge
- [x] Full scored set, **provider pinned** — StreamLake n=5: **49/50 clean, forbidden 0.00,
      rejects 0.00.** Identical to this morning's run on the same provider, same single
      `two-stage-entry` miss. `movement` 5/5, `player-arrival` 10/10.
- [x] Devlog `2026-08-13_a-world-with-no-exits.md`, `CHALLENGES.md`, `TODO_FUTURE_WORK.md`

## Out of scope

- A rich exit model (direction, doors, locked state). `Location.cs` already argues movement is
  described in prose, not navigated on a compass.
- Backfilling the 33 orphans in existing saves. They are hand-editable now; not worth code.
