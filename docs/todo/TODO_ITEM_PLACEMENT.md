# TODO — status is absorbing placement

Found in the 50-turn `ashfall` session, 2026-08-12. Save: `saves/ashfall/` at turn 50.

**Not started. Needs a reproduction before anything is built** — the well established that the
lever is the seed, not the prose, and that a category with a structural explanation can still
refuse to reproduce.

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

## Steps

- [ ] Scenario: an item held by the player that the prose puts down, throws, or breaks
      somewhere specific. Scored on the **outcome** — the item ends the turn in the location,
      not in a hand with a story in its status field
- [ ] Measure before touching anything, provider pinned, error count read first
- [ ] Only then choose between the three above
- [ ] Check whether the same shape exists for characters: a `status` reading "fled into the
      tunnel" while `locationId` says otherwise. Not observed, cheap to look for

## Out of scope

- **The item table in `saves/ashfall` is not being repaired.** It is evidence, and the three
  wrong placements are the record of what happened
