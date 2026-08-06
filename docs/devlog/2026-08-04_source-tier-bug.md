# Devlog — a stranger speaks and canon refuses to hear it

**Date:** 2026-08-04
**Scope:** a validator bug introduced with `source` and caught by a directed test session

---

## The session that found it

The second model-played session, run with `TEST_PLAYER_PROMPT.md` rather than unprompted. The
prompt worked:

| | run 1 (unprompted) | run 2 (directed) | human-2 |
|---|---:|---:|---:|
| deltas applied | 92 | **157** | 172 |
| turns changing nothing | 42% | **16%** | 27% |
| `player_moved` | 2 | **6** | 9 |
| locations discovered | 3 | **8** | 5 |
| characters | 3 | **10** | 6 |
| `status_changed` | 1 | **10** | 10 |
| items introduced / moved | 4 / 4 | **6 / 13** | — |
| **rejections** | 2 | **23** | 4 |

Coverage went from a cautious conversation to something comparable with human play on nearly
every axis. And the rejection count jumped by an order of magnitude, which is where the value
turned out to be.

## The bug

Turn 6, in the order the model emitted it:

```
character_introduced  older-man-square            ✅ applied
fact_established      well-sealed-air-smell       ❌ "source 'older-man-square' is not a character"
fact_learned × 4                                  ❌ cascade
```

The character was introduced *in that same batch* and *was accepted*. The fact quoting them was
rejected for naming somebody who "does not exist".

**Tier ordering.** `FactEstablished` sat at tier 0 — "depends on nothing else in the batch" —
and `CharacterIntroduced` at tier 1. So a fact was always judged before any character the same
batch introduced.

That was correct until `source` was added the previous day. The field gave `FactEstablished` a
reference to a character, and its tier was never revisited; the comment above it still asserted
the property that had just stopped being true.

**Sixteen of the twenty-three rejections were this one mis-tiering**, cascading through every
`fact_learned` that depended on the rejected fact.

## Why it is the same bug as last time

This is a near-exact repeat of the validator-ordering bug from July, which cost the most common
action in the game: the extractor emitted `player_moved` before `location_introduced`, the move
was rejected for naming a location that "did not exist", and exploring recorded nothing. The fix
then was dependency tiers.

The tiers were right. **Adding a field to a delta changed which tier it belonged in, and nothing
connected those two facts.** The comment documenting the invariant was the only guard, and a
comment cannot fail a build.

Restructured so the tiers state what each level may reference:

```
0  LocationIntroduced                     nothing
1  CharacterIntroduced                    a location from 0
2  FactEstablished, ItemIntroduced        a character from 1 (source, holder)
3  everything else                        anything above
```

Guarded by a self-test in the shape that broke: a stranger walks in, says something, and both
are recorded — the single commonest scene in the game.

## The other rejections, which are not bugs

- **`source` naming somebody not in canon** (4). An older man in the square speaks but was never
  introduced. This is the *mentioned-but-absent* gap, already logged, now with a new way to
  bite: attribution gives it a second surface. The fact survives with no speaker rather than
  being lost, which is the right degradation.
- **`item_introduced` with `locationId` and `holderId` both set to `player`** (2). The model
  treated the player as a place. Caught exactly as designed — this is the invariant that stops
  one object being recorded in two places.
- Re-introducing a known character or location (2). Routine, and the validator's job.

## What the directed prompt did not fix

**Zero malformed input**, despite an explicit instruction to be sloppy about one turn in ten.
Every input is well-formed with balanced asterisks. A model asked to write badly writes tidily
anyway, which is worth knowing: **fuzzing is not something a model-played session will do for
you**, and it remains a human-only behaviour.

Median input length dropped from 160 to 130 characters, so the "keep it short" instruction did
land. The sloppiness one did not.

## What generalises

- **A directed model session is a real coverage tool.** Nearly human-comparable on every
  measurable axis except messiness, and it found a genuine bug within six turns.
- **The bug it found was ours, introduced the day before, and invisible to the eval** — the
  scored set has no scenario where a character is introduced and quoted in the same turn.
  Fifty turns of aggressive play hit it immediately.
- **Adding a field can change a delta's dependency tier.** There is no mechanism connecting
  those; the only guard was a comment, and it was already stale by the time it was read. Worth
  checking the tier whenever a delta gains a reference to another entity.

## Results

```
tier fix           16 of 23 rejections eliminated
self-test          new: a character introduced and quoted in one batch is accepted
full scored set    49/50 clean, pinned; the single miss is two-stage-entry's known intermittent
```
