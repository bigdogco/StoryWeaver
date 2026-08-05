# TODO — items

The fifth entity type. Best-evidenced gap in the domain model.

Design: [`docs/design/ITEMS.md`](../design/ITEMS.md).
Measurement: [`2026-08-04_item-scenarios.md`](../devlog/2026-08-04_item-scenarios.md).

---

## Evidence

- `object-described` — an item becomes a `character_introduced`, **7/7**
- 8 of 11 description-facts in session 1 describe something with no entity
- **26 of 43 facts in session 2 mention an object — 60%.** That plot ran on a stone
- **False canon in play**: two objects with different origins, appearances and fates were
  merged, and canon recorded the wrong one being ground to powder
- The AtlasCloud "building as a character" failure is the same pressure

## Measured first

- [x] `scenery-vs-object` — **forbidden 0.00.** The "handled, not described" line is real and
      the model already finds it. The design's load-bearing assumption holds
- [x] `two-objects` — zero deltas. No conflation at rest; nothing to say without an item type
- [x] `wrong-object-acted-on` — zero deltas, with and without the plan in canon. **The play
      failure does not reproduce in a single turn**, and cannot serve as a regression test

## Decisions

- [x] Is "handled, not described" the right line? — **yes, measured**
- [ ] Four deltas, or fold `item_renamed` into `item_introduced`?
- [ ] Does the player get an inventory view, or does `/state` suffice?
- [ ] What happens to an item in a location nobody is in? (probably nothing, worth stating)

## Build

- [ ] `Item : Entity` — `LocationId` **or** `HolderId`, exclusive; `Status`
- [ ] Storage: items are canon, so they live in `canon.json` beside characters and locations
- [ ] Global id uniqueness — `DeltaValidator.Taken` gains a fifth namespace
- [ ] `item_introduced`, `item_moved`, `item_renamed`, `item_status_changed`
- [ ] Validator: an item must be somewhere or held, never neither, never both
- [ ] `ContextAssembler` — items in the scene for narration, ids for extraction
- [ ] Extraction prompt: the handled-not-described line, in the model's own terms
- [ ] `/item` authoring, matching `/place` and `/character`
- [ ] Self-tests for the exclusivity rule and id uniqueness

## Deliberately out of v1

Quantity, item properties/stats, containers, and crafting. Grinding is
`item_status_changed`; powder plus salt becoming paste is a system, not a delta, and v1 should
not guess at it. Stats belong with dice-resolved checks.

## Verify

- [ ] Full scored sweep, **provider pinned** — a routed sweep produced three phantom
      regressions during this work and all three were 15/15 when pinned
- [ ] Re-run the fact audit against a third session; the object-fact share should fall
