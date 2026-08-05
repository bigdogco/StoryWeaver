# Devlog — three scenarios before the item type

**Date:** 2026-08-04
**Scope:** measuring the item design's load-bearing assumption, and a correction

---

## The correction first

I told the player their witch confusion was a misreading — that there was only ever one stone,
and Morwenna had pointed at the capstone they already had. **That was wrong**, and the way it
was wrong matters.

Reading the prose rather than the facts:

| | the capstone | the witch's bundle |
|---|---|---|
| origin | pulled from the well, sunk in the blind pool | given by Morwenna, turn 42 |
| appearance | dark, slick, weeping black water | pale chunks, dry, salt-crusted, smelling of copper |
| fate | left on the counter | ground in the mortar, turn 49 |

Turn 49: *"The pale, salt-crusted chunks from the oilcloth clatter into the bowl."* Canon
records:

```
capstone-ground-to-powder: The weeping woman capstone was ground into a coarse,
                           glittering powder using Hald's mortar and pestle.
```

**That is false canon.** Two objects with different origins, appearances and fates were merged
into one, and the false fact then fed the narrator on every later turn.

My error was reading the *facts* — which are the merge — instead of the prose. The player
experienced the bug correctly and was told they had misremembered. Worth recording because the
audit method has a blind spot: **canon cannot show you a conflation, because the conflation is
what canon says.**

## The load-bearing question, answered

The item design rests on one line: **an item enters canon when it is handled, not when it is
described.** Prose is full of objects, and a canon that swallows every mug drowns.

`scenery-vs-object` puts one handed-over object in a room dense with furniture — four barrels, a
rack of eel-spears, a hearth iron, scarred tables, cheap clay mugs, grey rushes.

**Forbidden 0.00 across 7 runs.** Not one piece of scenery entered canon. And the model reached
for the item concept unprompted, with a holder, having only a fact to say it with:

```
fact_established  mabb-item: Mabb possesses a small item wrapped in oilcloth.
```

The line is real and the model already finds it. `item_introduced` can be built on it.

## The failure that would not reproduce

Two scenarios were written to reproduce the false-canon merge, and **both produced zero
deltas.**

`two-objects` — the capstone and the pale chunks side by side on a counter. Nothing recorded,
7/7. The model does not conflate them; it has nothing to say about objects at rest, which is
correct given no item type exists.

`wrong-object-acted-on` — one of the two ground in a mortar while the other visibly stays put.
Still nothing, 7/7. Then again with the plan already in canon (*"the weeping woman capstone
must be ground to powder"*, the fact that existed in play), on the theory that the action was
matched to a plan naming the wrong stone. **Still nothing.**

Three attempts, no reproduction. That is the honest result and it is worth more than a
fixture bent until it produced the expected answer.

**What it means for the design.** The merge in play came from somewhere these scenarios do not
reach: forty turns of accumulated canon in which the capstone is named in nine separate facts,
a narration window carrying its own prose, and an object that had been the subject of the story
for the entire session. A single turn cannot recreate that gravity.

So the item type is still justified — by `object-described` at 7/7, by 60% of one session's
facts mentioning objects, and by the false canon itself, which exists on disk regardless of
whether a scenario can provoke it. But **the merge cannot currently be used as a regression
test**, and claiming items fix it will not be verifiable by eval. That should be said plainly
rather than discovered later.

## A fourth sighting of the provider trap, caught by habit

The regression sweep after these changes came back looking alarming: `hostility` 0/5,
`new-character` 1/5, `two-stage-entry` 8/10. On a normal day that reads as a serious
regression.

It cannot have been one. This work added three *diagnostic* scenarios and a seed used only by
them — nothing in the extraction prompt, the schema, or the context assembler. The scored set
was untouched by construction.

Pinned to a clean provider: **15/15 on all three.** The sweep had routed 41 of 50 runs to
DeepInfra at 27/41 clean, plus two unattributed.

Recorded because the rule in `CHALLENGES.md` — *no single routed sweep is evidence about a
change* — was this time applied before drawing any conclusion rather than after being burned.
The cost was one pinned re-run. The alternative was an afternoon hunting a regression that did
not exist.

## What generalises

- **A scenario is a single turn; some failures are made of fifty.** The implicit-lore case was
  the same shape — a behaviour the eval could not see because the eval is one turn and play is
  a session. This is now twice. It is a structural limit of the harness, not a gap in any
  particular scenario, and it argues for the periodic-reconciliation idea from a third
  direction.
- **Failing to reproduce is a result.** The temptation was to keep adjusting the fixture until
  the failure appeared, which would have produced a scenario that measures the fixture rather
  than the model.
- **An audit cannot see a conflation.** It reads canon, and the conflation is what canon says.
  Only the prose disagrees, and only a human reading it noticed.

## Next

The design decisions in `ITEMS.md` are unblocked: the "handled, not described" line measured
clean, which was the one most likely to be wrong. Remaining questions are about delta shape
rather than feasibility.
