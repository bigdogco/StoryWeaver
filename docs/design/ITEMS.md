# Design — items

**Status:** design, no code. Written 2026-08-04.

The fifth entity type. Best-evidenced gap in the domain model, from four independent
directions.

---

## 1. The evidence

- **`object-described` scores 7/7 failure.** An object produced and described becomes a
  `character_introduced` — a knife standing in the tavern with a name and a location, because
  that is the only delta that can bring a *thing* into canon.
- **8 of the 11 description-facts** in the first session describe something with no entity: the
  altar, the medallion, an object hidden in a coat.
- **26 of 43 facts in the second session mention an object — 60%.** That story was *about* a
  stone. Items are not a side feature; the plot ran on one.
- **A player noticed unprompted.** Reported as the witch "giving a stone" that "got mixed up"
  with the capstone. There was only ever one stone. The confusion is real and it is not a
  memory failure — nothing in canon tracks objects, so *which stone is this* is unanswerable
  from the world state, and the prose is the only thread holding it together.
- **The AtlasCloud "building as a character" failure** is the same pressure surfacing on a
  worse provider.

## 2. What one object actually did

The capstone, traced through 51 turns. This is the design brief.

| turn | what happened | what it needs |
|---|---|---|
| 4 | called "old foundation blocks" — a lie | a **false claim** about an item |
| 9 | revealed as a carved capstone | **identity revealed** |
| 9 | sunk in the deep bog | **located** |
| 19 | more precisely, the blind pool | **relocated** |
| 35 | still there, weeping | state |
| 41 | must be ground and packed with salt | a plan, not a change |
| 48 | ground into powder | **transformed** |
| 48 | salt added → paste | **combined** |
| 49 | smeared into the joints | **consumed** |

Fifteen facts to track one object's life, none of which could say *where it is* or *what it now
is*.

Two observations that shape the design:

- **Identity revelation is the same problem `character_renamed` solved.** "Foundation blocks"
  becomes "a carved capstone" exactly as "Shivering figure" becomes "Nessa". The answer is
  already known: ids opaque, names mutable.
- **Ownership is not the primary axis — location is.** The capstone was never carried in the
  ordinary sense. It was in a well, then a bog, then a pool, then a mortar. A design built
  around "who has it" would fit inventory and miss the actual story.

## 3. The shape

```
Item : Entity
  Id           string    globally unique, like every other id
  Name         string    mutable — a thing can be revealed to be something else
  Description  string
  LocationId   string?   where it is, when it is not held
  HolderId     string?   who has it, when somebody does
  Status       string    "intact", "ground to powder", "burned"
```

**`LocationId` and `HolderId` are exclusive.** An item is in a place or in someone's hands,
never both and never neither — "nowhere" is how an object silently ceases to exist. Enforced in
the validator rather than by type, matching how `Character.LocationId` is already handled.

Deliberately absent from v1:

- **No quantity.** "Three coppers" is a different modelling problem to "the capstone", and
  conflating them produces a schema that serves neither. Money in particular probably wants its
  own answer.
- **No item properties or stats.** That is the dice-resolved-checks design, and it should not
  be pre-empted here.
- **No containers.** An item inside an item is a graph, and nothing in two sessions needed one.

## 4. Deltas

Four, mirroring the character set, which is deliberate — the extractor already handles that
shape at 100%.

| delta | mirrors | for |
|---|---|---|
| `item_introduced(id, name, description, locationId?, holderId?)` | `character_introduced` | a thing enters canon |
| `item_moved(itemId, toLocationId?, toHolderId?)` | `character_moved` | picked up, dropped, handed over |
| `item_renamed(itemId, name, description?)` | `character_renamed` | "foundation blocks" → "a capstone" |
| `item_status_changed(itemId, status)` | `status_changed` | ground, burned, broken |

**Transformation, combination and consumption are deliberately not deltas.** Grinding a
capstone to powder is `item_status_changed`. Powder plus salt becoming paste is a *new item*
plus two status changes, or a fact — and which of those is right is not obvious. That is
crafting, it is a system rather than a delta, and v1 should not guess at it.

## 5. The hard question: what is an item?

The same question already answered for characters and locations: **mention is not presence.**
Measured at 0/7 — a place the player names, a person they name, a place the narrator names in
passing, none enter canon.

The same rule should hold here, and it is more important, because prose is *full* of objects. A
taproom has mugs, barrels, a rag, a rack of eel-spears. If every noun becomes an entity, canon
drowns and the context block with it.

**Proposal: an item enters canon when it is handled, given, taken, or made to matter — not when
it is described.** The eel-spears on the wall are scenery. The knife Mabb unwraps and puts on
the table is an item.

This is a genuine risk to the feature. It is exactly the judgement the extractor is worst at,
and the failure mode is quiet: a canon slowly filling with mugs. **It wants an eval scenario
built to fail** — a scene dense with scenery containing one object that matters — before the
delta set is finalised.

## 6. What items would have fixed

Re-reading both sessions with the design applied:

- the witch confusion — two stones or one is answerable
- 8 description-facts from session 1
- a large share of the 26 object-facts in session 2
- `object-described` 7/7
- items becoming characters

What it would **not** fix: the contradictory claims about where the stone went. That is
`source` on facts, and it stays a separate problem.

## 7. Sequencing

Items touch the extractor's schema, which is the thing most at risk of regression, and the
scored set is currently at 100%. So:

1. **The scenery scenario first**, built to fail. If the extractor cannot tell a mug from a
   plot object, the feature needs a different shape before it is built.
2. `Item`, `LoreBook`-style, plus storage.
3. The four deltas, one at a time, measuring the full set after each.
4. `/item` authoring, matching `/place` and `/character`.
5. Re-run the fact audit against a third session and check the object-fact share falls.

## 7.1 Measured before building, 2026-08-04

Three scenarios written first. Devlog:
[`2026-08-04_item-scenarios.md`](../devlog/2026-08-04_item-scenarios.md).

- **`scenery-vs-object`: forbidden 0.00, 7/7.** One handed-over object in a room of barrels,
  eel-spears, a hearth iron, scarred tables and clay mugs. Not one piece of scenery entered
  canon, and the model reached for the item concept unprompted — `mabb-item: Mabb possesses a
  small item wrapped in oilcloth` — having only a fact to say it with. **The load-bearing
  question of §5 is answered: the line is real and the model already finds it.**
- **`two-objects` and `wrong-object-acted-on`: zero deltas, 7/7 each.** Three attempts to
  reproduce the false-canon merge from play, including one with the plan already in canon.
  None reproduced it.

**Consequence for the design.** The merge came from forty turns of accumulated gravity — nine
facts naming the capstone, a narration window carrying its own prose, an object that had been
the story's subject all session. A single-turn scenario cannot recreate that. So the item type
stays justified by `object-described` (7/7), by 60% of a session's facts mentioning objects, and
by false canon that exists on disk — but **the merge cannot serve as a regression test, and
"items fix it" will not be verifiable by eval.** Better said now than discovered later.

## 8. Decisions needed

1. ~~**Is "handled, not described" the right line?**~~ **Answered 2026-08-04: yes**, measured at
   forbidden 0.00. This was the one most likely to be wrong and it is not.
2. **Four deltas, or fewer?** `item_renamed` could fold into `item_introduced` if the extractor
   handles re-introduction as revision — but that was explicitly rejected for characters, and
   for good reasons.
3. **Does the player get an inventory view?** `/state` would show items by location; "what am I
   carrying" is a different question and probably wants its own command.
4. **What happens to an item in a location nobody is in?** Almost certainly nothing — it stays
   there and leaves the context block, which is correct and worth stating.
