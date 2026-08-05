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
- [x] `wrong-object-acted-on` — zero deltas before items existed, with and without the plan in
      canon. **Superseded 2026-08-04:** once items existed *and* the seed carried both objects,
      it reproduced the merge and then scored **14/14, forbidden 0.00**. The earlier conclusion
      that it "cannot serve as a regression test" was wrong — the fixture was missing the two
      objects, so the model had nothing to confuse

## Decisions — all settled 2026-08-04

- [x] Is "handled, not described" the right line? — **yes, measured at forbidden 0.00**
- [x] Four deltas, with `item_renamed` its own — mirrors the character set the extractor
      already handles at 100%, and re-introduction-as-revision was rejected for characters for
      reasons that apply here too
- [x] No `/inventory` — items appear in `/state` and `/prose`, held ones under their holder.
      A real inventory belongs in the UI; the harness is a debugging tool
- [x] Loose items are listed in the scene. An item that vanished from the narrator's view while
      still being in canon is the inconsistency this architecture exists to prevent

## Build — done 2026-08-04

See [`2026-08-04_items.md`](../devlog/2026-08-04_items.md).

- [x] `Item : Entity` — `LocationId` **or** `HolderId`, exclusive; `Status`
- [x] Storage: items are canon, so they live in `canon.json` beside characters and locations
- [x] Global id uniqueness — `DeltaValidator.Taken` gains a fifth namespace
- [x] `item_introduced`, `item_moved`, `item_renamed`, `item_status_changed`
- [x] Validator: an item must be somewhere or held, never neither, never both
- [x] `ContextAssembler` — items in the scene for narration, ids for extraction
- [x] Extraction prompt: the handled-not-described line, in the model's own terms
- [ ] `/item` authoring, matching `/place` and `/character` — **not built.** Extraction covers
      the observed cases; add it when a real session needs to place an object by hand
- [x] Self-tests for the exclusivity rule and id uniqueness

## Deliberately out of v1

Quantity, item properties/stats, containers, and crafting. Grinding is
`item_status_changed`; powder plus salt becoming paste is a system, not a delta, and v1 should
not guess at it. Stats belong with dice-resolved checks.

## Verify

- [x] Full scored sweep, **provider pinned** — 50/50 clean, 100%, no regression. Pinning
      matters: a routed sweep during this work produced three phantom regressions, all 15/15
      when pinned
- [ ] Re-run the fact audit against a third session; the object-fact share should fall
