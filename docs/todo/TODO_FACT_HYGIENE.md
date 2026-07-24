# TODO — fact hygiene

Item #1 off the post-lore list. Opened as "facts need truth value and attribution"; the audit
found that is the fifth-largest problem, not the first.

Design: [`docs/design/FACT_HYGIENE.md`](../design/FACT_HYGIENE.md).

---

## Audit

- [x] Classify all 53 facts in the 51-turn save by what they actually are
- [x] Find why the extraction prompt's own test does not catch the two largest categories
- [x] Propose a test that explains all 53 rather than 40

**Result: fewer than one fact in five is a fact.** 12 momentary events, 11 descriptions,
10 correct, 7 lore, 5 claims, 4 who-knows-what, 2 names, 2 agreements.

## Decisions needed before code

- [ ] Is "a fact is something one character can know and another not know" the right test?
      Load-bearing for everything else
- [ ] `entity_described`: replace, append, or revise the existing text?
- [ ] Source on facts, truth value, or both? (design argues source only)
- [ ] One `entity_described` delta or two?

## Measurement — done first, and it changed the plan

Devlog: [`2026-07-24_fact-hygiene-measurement.md`](../devlog/2026-07-24_fact-hygiene-measurement.md).

- [x] Eval: description-shaped prose does not produce a fact — **forbidden 0.00, small and
      large world.** Would not fail
- [x] Eval: a completed action does not produce a fact — **forbidden 0.00.** Would not fail
- [x] Eval: a revelation does not produce a who-knows-what fact — **7/7 required, forbidden
      0.00.** Would not fail
- [x] Work out why: **8 of the 11 description-facts describe something with no entity at all.**
      The scenarios described a *room*, which has a `Location` to hold its description
- [x] Rewrite the scenarios in the shape that actually fails

**Result: the design was aimed one level too shallow.** "Descriptions land in facts" is mostly a
missing *entity type*, not a missing delta. `character_described` fixes 3 of 11, not the
largest category as claimed.

## What the reproductions found

- [x] `object-described` — **an item becomes a `character_introduced`, 7/7.** A knife standing
      in the tavern with a name and a location, because that is the only delta that can bring a
      thing into canon. Same shape as the AtlasCloud failure in `CHALLENGES.md`, reproduced on
      a good provider
- [x] `blow-landed` — **`status_changed` never fires, 0/7.** Hald is beaten unconscious and the
      model writes `mood_changed = injured`. **Mood is absorbing status.** Forbidden was 0.00,
      so the fact-store theory was wrong about combat too
- [x] `sub-space-described` — 1/7. Real in play, weakly provoked by this scenario
- [x] Third unprompted sighting of the attribution instinct: *"Mabb claims he found the ritual
      knife in the reeds"*

## Build — resequenced by evidence

- [ ] **`status` vs `mood` in the extraction prompt.** 7/7 measured failure, prompt-fixable,
      and `blow-landed` already exists to measure the fix
- [ ] **`Item` as an entity type.** 7/7 measured failure. The answer to most description-facts
      and to items-as-characters. Wants its own design pass — see
      `TODO_FUTURE_WORK.md`, where it has been logged since a player bought a beer
- [ ] **`source` on `FactEstablished`.** Three independent sightings of the model asking for it
- [ ] **`character_described` / `location_described`.** Still worth having, correctly sized at
      3 of 11

## Not building — no measured failure to fix

- [x] ~~Prompt line: a completed action is not a fact~~ — `event-not-fact` cannot reproduce it
- [x] ~~Prompt line: never write a fact about who knows something~~ — `knowledge-not-fact`
      scores clean
- [ ] The knowledge-worthiness test — **decided, and deliberately not written yet.** It is a
      good test and there is currently no scenario that fails without it. Revisit if the
      category reappears in a fresh play session

## Still true, still logged

- [ ] Re-run the fact audit against a fresh play session and compare the category split. This
      is the measurement that matters, and it needs a session played on the current build

## Out of scope, logged

- [ ] **Deduplication.** Three facts describe the cult's location, two describe Shurus
      preserving followers, two describe the same creature being wounded. The extractor sees
      no semantic overlap and the validator catches only exact id collisions. This is what
      makes a fact store degrade slowly rather than visibly
- [ ] **Agreements as commitments.** `hald-agrees-to-guide` is durable and knowledge-worthy,
      but a promise is the kind of thing that gets broken, and canon cannot record that
