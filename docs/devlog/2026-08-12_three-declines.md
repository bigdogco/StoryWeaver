# 2026-08-12 — three things measured, three things not built

The last three open items, taken one at a time. Every one turned out to be a decline, and each
for a different reason. No code was written.

---

## 1. Extraction-proposed ids and the kebab-case check — **it never happens**

Authored ids have been checked since this morning: sheet filenames, lore filenames, seed keys.
The open question was whether the model's own ids should be too.

Every id reference in every save, swept: **1528 of them, zero malformed.** The model produces
kebab-case unprompted, and the schema's own examples — `cellar-poisoning`, `militia-woman` —
are quietly doing that work.

Adding the check would buy nothing and cost a rejection cascade: a refused
`character_introduced` takes every delta that references it down with it. **A guard against a
failure that has never occurred, whose own failure mode is worse than the thing guarded
against.**

If a future model starts emitting `Bloated_Man`, the answer is normalisation across the whole
batch, not rejection. That is for the day it happens.

## 2. The no-op-pickup tell — **too noisy to use**

The idea was good: an `item_moved` into a hand that already holds the item changes nothing, and
that means the model matched the wrong object. It is how the medallion merge could have been
caught.

**7 no-op `item_moved` across 250+ turns. Exactly one was the bug.** The other six are the
model harmlessly restating that a knife is still in Behn's hand, a coin still with Hald.

**One in seven precision.** A warning that is wrong six times out of seven trains you to ignore
it, and the merge it would catch is now fixed at 10/10 by a prompt rule anyway.

## 3. Deduplication — **the measurement argues against it, hard**

The oldest open item, logged since the first fact audit, and the one I expected to build.

All six saves swept for fact pairs above 0.45 token overlap: **10 pairs across 177 facts.**
Then every pair was read, which is the part that mattered:

| | pairs | may they be merged? |
|---|---|---|
| genuine restatements | **3** | yes |
| **contradictions** | 1 | **no — that is what `source` exists for** |
| before and after | 3 | no, both true at different times |
| identifications | 2 | no, the link is the plot |
| a revelation refining an earlier fact | 1 | no, and it is a `character_renamed` miss |

**Seven of ten must stay separate**, and a similarity pass would have destroyed all seven.

The clearest is a pair at 0.45:

```
blocks-taken-to-bog     The heavy thing pulled from the well was taken to the deep bog.
blocks-taken-to-quarry  The heavy thing pulled from the well was taken to the old quarry.
```

Two characters contradicting each other about where it went — **exactly the case attribution
was built to preserve.** Merging them would delete the disagreement the scene is made of.

And at 0.46:

```
boy-wore-bronze              The miller's boy wore a bronze chain with a reed clasp.
drowned-figure-bronze-chain  The drowned figure wears a bronze chain with a curled reed clasp.
```

The overlap *is* the identification. That is the plot.

**Surface similarity is not identity** — the same family as the scoring-rule mistake made four
times in this project: judging the shape of a thing rather than what it means.

The real duplicate rate is **3 in 177 facts, 1.7%**, and two of those three are symptoms of
bugs already fixed elsewhere (`well-fluid` / `well-fluid-stopped` is a location's changing
state, now absorbed by `Location.Status`; the twice-wounded creature is
momentary-events-as-facts). The residue does not pay for a mechanism that can silently merge a
contradiction.

Revisit only with a *semantic* check able to tell restatement from contradiction — a model call
per pair, and a different order of cost.

## What these three have in common

All three were logged as "probably worth building". All three survived months on that
description. **Each took one sweep of existing data to settle, and each answer was no.**

Two of them would have made things worse: a rejection cascade guarding nothing, and a
deduplication pass that eats disagreements. The third would have been noise.

Worth stating as a habit rather than three anecdotes: **a TODO item that has never been
measured is a hypothesis, and this project has now been wrong about four of them in two days**
— the offstage character that was unreachable, the well that "could not be reproduced",
`movement` that was never broken, and now these.

## Verified

Nothing to verify. No code changed; `dotnet build` and `--selftest` untouched and passing from
the previous commit.
