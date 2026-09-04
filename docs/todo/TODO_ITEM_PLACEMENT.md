# TODO — status is absorbing placement

Found in the 50-turn `ashfall` session, 2026-08-12. Save: `saves/ashfall/` at turn 50.

**Fixed 2026-08-12. `object-leaves-the-hand` 4/12 → 12/12**, no collateral damage across
`object-examined`, `wrong-object-acted-on` and `second-identical-object` (24/24 clean).

It took three attempts to reproduce, and the two failures are the useful part — see below.

---

## The failure

`item_status_changed` is being used to record where a thing ended up. Three cases in fifty
turns, every one leaving the item **held by a person** while its status says it is somewhere
else:

```
t14  clay-cup-ale      = "shattered on the floor"                     still held by Behn
t29  old-mining-cable  = "severed and dropped into the shaft"         still held by Rook
t44  severed-chain     = "wrapped and knotted around the sluice gate" still held by Rook
```

Canon now says Rook is carrying a chain that is knotted around a gate in another room.

**The session's single rejection is the same gap from the other side:**

```
t32  item_moved  loose-rock -> null / null
     "The loose rock skitters around the corner... dropping into the dark."
     REJECTED: an item must be in a location or held by a character.
```

The model wanted to say *gone*. The schema requires *somewhere*. The validator was right — and
the model had nowhere correct to put it, so on the other three occasions it reached for status
instead and was not refused.

## Why this is the same shape as the well

Facts absorbed a location's changing state because locations had no `Status`. Status is now
absorbing placement because *"it left my hand and went somewhere vague"* has no clean
expression.

**The pattern, third sighting:** when the schema has no slot for something the prose keeps
saying, the model puts it in the nearest slot that will accept it, and the validator cannot
tell — because the delta is well-formed and the field is free text.

The first two were caught by auditing misfiled facts. This one was caught by reading the item
table and noticing a held object described as being on the floor. **Both were found by looking
at canon, not at rejections** — rejections were 1 in 132 this session, and the number says
nothing about this class of error.

## What is not yet decided

Do not guess between these before a scenario reproduces the failure:

- **Prompt only.** "Where a thing ends up is `item_moved`, never status." Cheapest, and the
  three cases above all have a real destination available — `waystation-common-room`,
  `secondary-shaft`, and wherever the sluice gate is
- **A way for an item to leave play.** `loose-rock` genuinely had no destination. An
  `item_lost` or a nullable placement with an explicit reason would cover it, and would be a
  schema change earned by exactly one observation so far
- **Items attached to fixtures.** `severed-chain` is knotted around a gate: not held, not
  loose on the floor, and arguably a third placement kind. Real, rare, and the most speculative
  of the three — a chain in a location whose description mentions the gate is probably enough

## Reproducing it took three tries, and that is the finding

| attempt | scenario | result |
|---|---|---|
| 1 | player hurls a held cup at the hearth, same room | **10/12** — barely fails |
| 2 | player slings it out the door into the square | **12/12** — does not fail at all |
| 3 | **player knots a chain around a sluice gate** | **4/12** — reproduces |

Attempts 1 and 2 were written from the summary of the bug rather than from the bug. Going back
and reading the three real turns showed what they had in common and what I had left out:
**a fixture.** A cup on "the floor", a cable "in the shaft", a chain "around the gate" — none
of those is a location you can move an item to, so the model wrote the destination into the
free-text status field, where it fits and where nothing checks it.

An object that simply lands somewhere is handled correctly, every time. The failure needs
somewhere that is *not* a place.

Attempt 2 also introduced an ambiguity of its own — the prose had the player kick a door open,
and every run moved the player through it. **A scenario can fail for reasons the author put
there**, which is its own argument for reading the real transcript instead of paraphrasing it.

## The fix

- [x] Prompt rule: *a status is a condition, never a whereabouts.* Broken, lit, soaked are
      statuses; on the floor, down the shaft, tied to the gate are placements, and a placement
      is `item_moved`. If an object ends the turn resting on, inside, or fastened to something
      in a room, it is in that room
- [x] ~~**The other two candidates were not needed.**~~ **Half reversed the same day.** A third
      placement kind for fixtures is still unbuilt and still unnecessary. `item_lost` was
      declined on "one rejected delta is not a schema change", then a second `ashfall` session
      produced a second one — a key hurled into a lava fissure — and that time canon was left
      **actively wrong**, still recording the key as lying in the cellar, retrievable, for the
      rest of the session. Built; see below

## `item_lost` — built 2026-08-12, and the shape of it is the finding

**`object-lost-for-good` 0/6 → 10/10, and `object-leaves-the-hand` 16/20 → 19/20.**

The first attempt was an ordinary new delta kind: a `StateDelta`, a converter entry, a
validator rule, an applier case, a schema branch and a prompt rule. It worked, and it
**wrecked an unrelated scenario**:

| build | `object-leaves-the-hand` | `object-lost-for-good` |
|---|---|---|
| before any of this | **16/20** | 0/6 |
| + schema branch, prompt rule before the placement rule | 10/20 | 6/6 |
| + schema branch, no prompt rule | 2/20 | 10/10 |
| + schema branch, prompt rule after the placement rule | **0/20** | 10/10 |
| **no schema branch: rewrite + extended bullet** | **19/20** | **10/10** |

**A schema branch is not free.** The `anyOf` competes for the model's attention, and a new
prompt rule competes with the rules already there — moving that one rule by four lines swung an
unrelated scenario between 0/20 and 10/20. Nothing about the placement logic changed in any of
those rows.

What works instead costs the model nothing, because **the model was already emitting the right
thing**: `item_moved` with no destination at all, unprompted, in both real sessions and in most
baseline runs. So there is no `item_lost` in the schema and no rule teaching it. The extractor
rewrites that output into `ItemLost` on the way in, and the one existing bullet about items
being somewhere gained a sentence naming the exception.

- [x] `ItemLost(ItemId, Reason)` in Core — validator, applier, converter. Removes the item;
      canon is what is true now, and the turn history keeps the record
- [x] **No schema branch and no new prompt rule.** Measured, not assumed
- [x] `Normalise` in `LlmStateExtractor` rewrites `item_moved → null/null`, carrying the
      evidence text across as the reason
- [x] The existing "every item is either in a location or held" bullet extended with the
      exception, rather than a new bullet added
- [x] Self-test: a lost item leaves canon **and** leaves the batch's view, so a later delta
      naming it is refused rather than pointing at a ghost
- [x] Full scored set 49/50, forbidden 0.00 — the one miss is `two-stage-entry`, which has
      bounced 8/10–10/10 across samples all week
- [x] **Checked: it does not happen to characters.** Every `status_changed` across all five
      saves swept for whereabouts-shaped text — 253+ turns, one candidate, and it is a false
      positive (*"sinking deeper into bog, mud up to waist"* is a condition, and the player was
      in a bog location that exists). No character status disagrees with any `locationId`

      **And the reason is the same reason items fail.** A character always ends a turn in a
      *place*, which `character_moved` expresses exactly. An item can end a turn on the floor,
      down a shaft, or knotted around a gate — destinations the schema has no way to name, so
      the free-text field absorbs them. The bug was never about items being special; it was
      about destinations that are not locations, and only items get those.

## Out of scope

- **The item table in `saves/ashfall` is not being repaired.** It is evidence, and the three
  wrong placements are the record of what happened

---

## Observed 2026-08-12, second `ashfall` session: nothing holds a region's state

Two facts in fifty turns are a whole landscape changing:

```
t4   quiet-settling-now      The Quiet is settling now.
t43  mountain-waking-up      The mountain is waking up.
```

Neither is durable — both will be wrong in an hour — and neither belongs to a location. They
are true of the whole map at once. Characters have `Status`, items have `Status`, locations
have `Status` since this morning. **A region does not**, so the fact store takes it. That is the
same shape as the well before `Location.Status`, one level up.

- [x] **Do not build on two observations.** **Moved to TODO_FUTURE_WORK 2026-08-13**, threshold intact: a region-level status is worth designing when it reproduces in a scenario *and* appears in a third session.



- [x] Note the confound before anyone acts on it: **noting it was the action.** Carried into the FUTURE_WORK item 2026-08-13.


