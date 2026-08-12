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
- [x] **The other two candidates were not needed.** An `item_lost` for things that leave play,
      and a third placement kind for fixtures, were both plausible on the evidence and are both
      unbuilt. The chain ending up in the cellar it is chained inside is correct, and one
      rejected `item_moved → null/null` in fifty turns does not earn a schema change
- [ ] Check whether the same shape exists for characters: a `status` reading "fled into the
      tunnel" while `locationId` says otherwise. Not observed, cheap to look for

## Out of scope

- **The item table in `saves/ashfall` is not being repaired.** It is evidence, and the three
  wrong placements are the record of what happened
