# Devlog — the turn that ended in the wrong room

**Date:** 2026-07-22
**Scope:** multi-stage movement, world size as a variable, and outcome-based scoring

---

## From play

A real turn produced this:

```json
{ "kind": "player_moved", "toLocationId": "blind-channels-slipway",
  "evidence": "You step through the arching entrance, your boots immediately
               splashing into knee-deep, ink-black water..." },
{ "kind": "location_introduced", "locationId": "cult-altar-chamber", ... }
```

The new chamber was introduced correctly. The move went to the location the player was
*already in*. And `player-arrival` — the scenario built for exactly this — scores 14/14.

The evidence field is what cracked it. The model was not confused about the chamber; it quoted
the **first** paragraph and was reporting the entry into the outer ruins. That intermediate
space is never named as a place, so with no id to move to it reached for a known one.

## Two hypotheses, one wrong

**World size.** That session had 7 locations, 6 characters, 44 facts and a 10,000-character
context block. Every scored scenario runs against 2 locations and 1 fact. So `Marrow_Late` was
written — the same world grown to the size play actually reaches — and `player-arrival` run
against it word for word, changing only the seed.

First result: 5/14, apparently confirming it. **It was a fixture bug.** The large seed included
`mill-exterior` and `mill-ruins`, which collide with the destination the scenario walks to, and
the rule matched any id containing "mill" — so the model moving to a *pre-existing* mill scored
as a pass. A fixture must not contain a plausible wrong answer the scoring rule reads as right.

With the collision removed: **14/14 in both worlds.** World size alone does not do it. The
fourth confident hypothesis to die this week, and the only reason it did not get written up as
a finding is that the numbers looked odd enough to re-read the fixture.

**Prose shape.** The real narration has *two* movements — through an entrance, then a passage
opening into a chamber. `two-stage-entry` reproduces that shape with invented content:

```
player-arrival         (1 stage, small)   14/14
two-stage-entry        (2 stage, small)    6/14
two-stage-entry-large  (2 stage, large)    2/14
```

The shape is the trigger; size amplifies it.

## The scoring was wrong too

The first version of the rule forbade "any move to somewhere that is not the cistern", and the
deltas showed why that is wrong:

```
runs 1,2,6,7:  player_moved → well-tunnel        ...and nothing else
runs 3,4,5:    player_moved → well-tunnel
               location_introduced cistern
               player_moved → cistern
```

Runs 3–5 are **correct and complete** — two hops, applied in order, ending in the right room —
and the rule marked them as failures. Only 1/2/6/7 are wrong, because the player *ends* the
turn somewhere they are not.

That is a flaw in the harness, not the rule: scoring was predicates over **deltas**, and "the
player ends up in the cistern" is a property of the resulting **world**. `StateRule` now checks
the world after accepted deltas are applied, and `player-arrival`'s movement check moved over
to it as well — it could previously pass while the player ended the turn elsewhere, which is
precisely the bug that reached play.

## The fix

One rule:

> Movement records where someone ENDS the turn. If the prose carries them through more than one
> space — down a shaft, along a passage, into the chamber beyond — report where they finish.
> Reporting only the first step leaves them standing in a place the story has already left.

| | before | after |
|---|---|---|
| `two-stage-entry` | 6/14 | **14/14** |
| `two-stage-entry-large` | 2/14 | 10/14 |
| `player-arrival` | 14/14 | 14/14 |

Full scored set, routed: **98% required, forbidden 0.00, rejects 0.00, 55/56 clean.** The only
miss is the known intermittent `revelation` speaker-learns, which has been 0–2/7 across every
sweep. No regression.

`two-stage-entry` promoted to the scored set. `two-stage-entry-large` stays a diagnostic at
10/14 so the scored set remains a regression guard while the remaining gap stays visible.

## What generalises

- **A fixture must not contain a plausible wrong answer that the scoring rule reads as right.**
  Checking the fixture is part of reading the result.
- **Score the outcome where the outcome is the point.** Several valid delta sequences can reach
  the same correct world; judging them step by step punishes the right ones.
- **World size belongs in the test matrix.** A scenario that passes against two locations says
  nothing about the same prose against forty, and play happens in the second kind of world.
- The extraction prompt is now four rules heavier than the "known-good" version, every one of
  them added against a measured, reproduced failure with a control. That is the only reason to
  trust them — three rules written without controls earlier this week were all wrong.

## Next

The large-world remainder at 10/14, and `revelation`'s speaker-learns at 1–2/7. Then §9.
