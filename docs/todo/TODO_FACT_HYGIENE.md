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

## Build — sequenced cheapest first

- [ ] The knowledge-worthiness test in the extraction prompt, alongside the durability test
- [ ] Prompt line: a completed action is not a fact unless someone could later act on knowing it
- [ ] Prompt line: never write a fact about who knows something — emit `fact_learned`
- [ ] `entity_described` delta — drains the largest fixable category
- [ ] `source` on `FactEstablished` — the original request

## Measurement

Each step scores against the audit's own categories, which is why the audit came first.

- [ ] Eval: description-shaped prose does not produce a fact
- [ ] Eval: a completed action does not produce a fact
- [ ] Eval: a lie is attributed rather than stated as truth
- [ ] Re-run the audit against a fresh play session and compare the category split
- [ ] Full scored sweep, provider pinned — no routed sweep is evidence (CHALLENGES.md)

## Out of scope, logged

- [ ] **Deduplication.** Three facts describe the cult's location, two describe Shurus
      preserving followers, two describe the same creature being wounded. The extractor sees
      no semantic overlap and the validator catches only exact id collisions. This is what
      makes a fact store degrade slowly rather than visibly
- [ ] **Agreements as commitments.** `hald-agrees-to-guide` is durable and knowledge-worthy,
      but a promise is the kind of thing that gets broken, and canon cannot record that
