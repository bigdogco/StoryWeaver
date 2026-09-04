# 2026-08-12 — a status is not a place

Third sighting of one pattern, fixed. The interesting part is that reproducing it took three
attempts, and the first two failed for reasons worth writing down.

TODO: [`TODO_ITEM_PLACEMENT.md`](../todo/TODO_ITEM_PLACEMENT.md).

---

## The failure

From the 50-turn `ashfall` session. Three `item_status_changed` deltas recording *where a thing
ended up*, each leaving the item held by a person while its status said otherwise:

```
t14  clay-cup-ale      = "shattered on the floor"                     still held by Behn
t29  old-mining-cable  = "severed and dropped into the shaft"         still held by Rook
t44  severed-chain     = "wrapped and knotted around the sluice gate" still held by Rook
```

Canon says Rook is carrying a chain that is knotted around a gate in another room.

**The session's single rejection is the same gap from the other side:** `item_moved loose-rock
→ null/null`, because the rock "dropped into the dark" and the schema requires *somewhere*.

Found by reading the item table, not the rejections — which were 1 in 132. **The rejection count
says nothing about this class of error**, because the delta is well-formed and the field is free
text.

## Three attempts to reproduce, and the two failures are the finding

| attempt | scenario | result |
|---|---|---|
| 1 | player hurls a held cup at the hearth, same room | **10/12** — barely fails |
| 2 | player slings it out of the door into the square | **12/12** — does not fail at all |
| 3 | **player knots a chain around a sluice gate** | **4/12** — reproduces |

Attempts 1 and 2 were written from *my summary of the bug* rather than from the bug. Going back
and reading the three real turns showed what they shared and what I had dropped: **a fixture.**

A cup on "the floor", a cable "in the shaft", a chain "around the gate" — **none of those is a
location you can move an item to.** So the model wrote the destination into the free-text status
field, where it fits and where nothing checks it. An object that simply lands somewhere is
handled correctly every time; the failure needs a destination that is not a place.

Attempt 2 also introduced a defect of its own. The prose had the player kick a door open, and
every run moved the player through it — **a scenario can fail for reasons its author put
there**, which is a second argument for reading the transcript instead of paraphrasing it.

This is the same lesson as yesterday's pre-flight script and this morning's test that passed on
the wrong exception, arriving a third time: *a check built from what you believe confirms what
you believe.*

## The fix

One prompt rule:

> A status is a condition, never a whereabouts. Broken, lit, soaked, ground to powder are
> statuses. On the floor, down the shaft, tied to the gate are placements, and a placement is
> `item_moved`.

**`object-leaves-the-hand` 4/12 → 12/12.** Neighbours unmoved: `object-examined`,
`wrong-object-acted-on` and `second-identical-object` all 12/12, 24/24 clean overall. Full
scored set re-run pinned: **50/50, required 100%, forbidden 0.00, rejects 0.00.**

## What was not built, and why that is the result

The TODO listed three candidates. Two are still unbuilt and should stay that way:

- **`item_lost`, for things that leave play.** Earned by exactly one observation — a rock
  rolling into the dark in fifty turns. One rejected delta is not a schema change.
- **A third placement kind for fixtures.** The chain is *inside the cellar it is chained
  within*. That is not an approximation, it is where the chain is. The gate belongs in the
  location's description, and it already is.

Both were plausible on the evidence and neither survived a reproduction that showed the prompt
was enough. **The reproduction is what stopped two schema changes**, which is a better return
than the fix itself.

## Verified

- `dotnet build` clean, `--selftest` all four suites pass
- `object-leaves-the-hand` 4/12 → 12/12, provider pinned, error count read before the score
- No collateral damage on the three neighbouring object scenarios
- Full scored set 50/50 clean

## Checked afterwards: characters do not have this problem

Every `status_changed` in all five saves, swept for whereabouts-shaped text. **253+ turns, one
candidate, and it is a false positive** — *"sinking deeper into bog, mud up to waist"* is a
condition, and the player was standing in a bog location that exists. No character status
disagrees with any `locationId`.

The reason turns out to be the same reason items *do* fail, which makes the fix feel less like
a patch. **A character always ends a turn in a place**, and `character_moved` names places. An
item can end a turn on the floor, down a shaft, or knotted around a gate — destinations the
schema cannot name at all, so the free-text field takes them.

The bug was never that items are special. It is that only items get destinations that are not
locations.

## Still open

- The `ashfall` item table stays wrong on purpose. It is the evidence
