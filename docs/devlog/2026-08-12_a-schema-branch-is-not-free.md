# 2026-08-12 — a schema branch is not free

`item_lost` built, after being declined this morning and reversed this evening. The delta is
small. What it cost to add it the obvious way is the finding.

TODO: [`TODO_ITEM_PLACEMENT.md`](../todo/TODO_ITEM_PLACEMENT.md).

---

## Why it was reversed

Declined this morning on the grounds that one rejected delta in fifty turns is not a schema
change. A second `ashfall` session produced the second one:

```
t42  item_moved heavy-iron-key -> null/null
     "You hurl the iron key... vanishing into the glowing, lava-bright fissure."
     REJECTED: an item must be in a location or held by a character.
```

And this time the refusal had a visible cost. Canon went on recording:

```
heavy-iron-key -> waystation-cellar, "used to break the cellar lock"
```

The key is in lava. Canon says it is lying in a cellar where somebody could pick it up. The
rejection did not merely lose a delta — it froze a stale placement that then contradicted the
story for the rest of the session.

Two occurrences in two consecutive sessions, with a demonstrated cost, is a different case from
the one that was declined. Worth recording as a judgement that was too tight rather than
quietly changing course.

## The first build worked and broke something else

An ordinary new delta kind: record, converter entry, validator rule, applier case, schema
branch, prompt rule. `object-lost-for-good` went 0/6 → 10/10.

`object-leaves-the-hand` — which has nothing to do with lost items, and whose logic was not
touched — went from 16/20 to somewhere between 0/20 and 10/20:

| build | `object-leaves-the-hand` | `object-lost-for-good` |
|---|---|---|
| before any of this | **16/20** | 0/6 |
| + schema branch, prompt rule before the placement rule | 10/20 | 6/6 |
| + schema branch, no prompt rule | 2/20 | 10/10 |
| + schema branch, prompt rule after the placement rule | **0/20** | 10/10 |
| **no schema branch: rewrite + extended bullet** | **19/20** | **10/10** |

Read the middle three rows again. **Moving one prompt rule four lines down the list swung an
unrelated scenario from 10/20 to 0/20.** Removing the rule entirely, keeping only the schema
branch, gave 2/20. The placement logic is identical in all of them.

Two things follow, and neither was obvious this morning:

- **The `anyOf` competes for attention.** A branch the model will use twice in a hundred turns
  still sits in front of it on every call.
- **Prompt rules compete with each other, and position matters more than wording.** This is the
  third time this week a rule aimed at one case damaged a neighbouring one — the identical-object
  rule broke `object-examined` twice before landing.

## What works costs nothing

**The model was already emitting the right thing.** `item_moved` with no destination, unprompted,
in both real sessions and in most baseline runs. It did not need to be taught a new delta; it
needed the delta it already produces to be understood.

So there is no `item_lost` in the schema and no rule teaching one. Instead:

- `LlmStateExtractor.Normalise` rewrites `item_moved → null/null` into `ItemLost`, carrying the
  evidence text across as the reason
- the single existing bullet about items being in a location or held gained one sentence naming
  the exception — an edit, not an addition

`object-lost-for-good` 10/10. `object-leaves-the-hand` **19/20, better than the 16/20 it
started at**, which is what happens when you stop adding things that compete with it.

## The rule to carry forward

**Adding a delta kind has a cost paid by every other delta kind, and the cost is not visible
from the scenario you added it for.** Before the next one:

1. Check whether the model already emits something that means it. Rewriting an existing output
   is free; teaching a new one is not.
2. Measure at least one *unrelated* scenario before and after. `object-lost-for-good` was 10/10
   in three of the four builds above, including the two that were catastrophic elsewhere.

## Verified

- `dotnet build` clean, `--selftest` all four suites pass
- New self-test: a lost item leaves canon **and** leaves the batch's view, so a later delta
  naming it is refused rather than pointing at a ghost
- Full scored set, StreamLake, n=5: **49/50, forbidden 0.00, rejects 0.00.** The single miss is
  `two-stage-entry`, which has bounced 8/10–10/10 across samples all week
