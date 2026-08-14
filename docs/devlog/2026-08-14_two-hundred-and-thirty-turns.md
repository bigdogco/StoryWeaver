# 2026-08-14 — Two hundred and thirty turns

A model-played `marrow` run, reported as "so bad at it." It is the most useful session this
project has had, and it closes the last measurement bootstrap left open.

No code. Findings only.

## The measurement, finally

> **Does canon survive 200 turns? Yes.**

Third attempt, first valid one. The first ended sealed in a room with no exits; the second was
two CLI instances corrupting one save.

| | result |
|---|---|
| turns | 230, **zero duplicate turn numbers** — the save lock held |
| rejections | 23 total, ~0.1/turn, **flat across all five 50-turn blocks** |
| turns changing nothing | 9 / 7 / 6 / 7 / 10 per 50 |
| narration | 1478 → 1417 mean chars; coherent and in-character at t228 |
| locations | 33, 28 connected; every orphan introduced-but-never-entered |

Canon does not drift, corrupt, or degrade. That is the thesis holding at four and a half times
the distance bootstrap proved it.

**And it killed a line of work.** `design/LONG_TERM_MEMORY.md` — four approaches, months of
assumed necessity — rested entirely on canon decaying with distance. It does not. The document
now opens with the negative result and is parked pending a *fresh* reason to exist, not a
longer run. Two runs past 150 turns have now failed to produce decay.

It also resolves an ambiguity from ashfall, where 150 turns produced 2 facts and 1 mood change.
That was **solitude, not distance**: this run, with a companion, produced 16 facts established,
32 learned and 35 mood changes.

## Why it still read badly — three causes, one of them new

### 1. No goal. The player says so in its own inputs.

```
t91   *I keep going until we hit another new location.*
t141  *I open the next crypt door, cellar hatch, or tower stair if it gives us new ground.*
t226  *I follow the next obvious sign if the story offers one.*
```

It is not roleplaying, it is requesting content — because nothing tells it what it is there to
do. **53% of all deltas are movement**, and the world grew as **31 new locations against 2 new
characters**: empty rooms, one after another.

Phase 1's absence, measured, on a second world. Nothing to fix here that the story layer is not
already for.

### 2. A place was introduced twice, under two ids

```
t16    stilt-hut               "half-swallowed by the fog... sits a dilapidated hut on stilts"
t226   dilapidated-stilt-hut   "the faint, looming silhouette of a dilapidated stilt-hut"
```

`DeltaValidator` refuses `location_introduced` for an existing **id**. It has nothing to say
about an existing **place** under a new id.

`stilt-hut` holds six connections and everything that happened there. `dilapidated-stilt-hut`
holds one connection and is empty — except the peat-creature is standing in it. Canon says the
story's monster is somewhere the player has never been.

**210 turns apart is the mechanism, not a detail.** The original had long since left the
narration window; the model was describing a hut across the water with no reason to think it
was already in canon. So this gets *more* likely as runs get longer, and it was invisible in
every 50-turn session ever played.

Logged with a threshold rather than fixed. The validator gap is provable; how often a model
does this is model behaviour, which is exactly what the standing rule exists to stop us
guessing at. Wanted: a diagnostic scenario, and a second sighting.

### 3. Canon can be correct and still unnarratable — and this one is new

The first failure in this project that lives entirely in the **rendering** layer.

Two locked decisions collide. Ids are unique and names are not. The narrator is shown names
only, deliberately, so ids never leak into prose. At 230 turns that produces:

```
Leads to: ... dilapidated stilt-hut, ... dilapidated stilt-hut, ...
Carrying: [31 items, including six different keys and four "stamped lead token"s]
```

**Canon is right.** `chapel-lead-token`, `crypt-stairs-lead-token` and `slipway-lead-token` are
three genuinely different tokens from three places — precisely the case the deduplication audit
protected, where 7 of 10 high-similarity pairs had to stay separate. The extractor did its job.

The narrator simply cannot write about objects it cannot tell apart, or an exit listed twice.

**Every mechanical measure of this run was healthy and it still read badly.** Delta counts
cannot see this class of problem. That is the sentence worth carrying forward.

Written up in `design/NAMES_ARE_NOT_UNIQUE.md`. No fix proposed — the most promising direction
is not about names at all but about *relevance*: 31 items are in the prompt because nothing
ever leaves inventory, and most of them have nothing to do with the scene. That shares an
answer with lorebook retrieval.

## One old finding is now settled

`relationship_changed` fired **zero** times again. Mona sat at her seeded 100 and Hald at his
seeded −10 through 230 turns while moods moved constantly. That is ~480 turns of evidence
across four sessions. No longer worth re-testing: mood is visible in one scene, standing is the
integral of many, and the answer is a reconciliation pass rather than a prompt.

## Method note

The diagnosis came from `/prose` — the narrator's own view of the world — not from delta
counts. The counts said the run was healthy, which it was. Reading what the model was actually
handed is what showed why it could not do anything good with it.

Worth remembering: `/prose` exists to check that no id leaks into narration, and it turned out
to be the better instrument for "why is the prose bad."

The save lock also earned itself in passing — it refused this inspection while the ChatGPT
session was still running, a day after shipping.
