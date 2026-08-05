# TODO — lore entries

The fourth entity type: a named topic with a body of prose. First post-bootstrap feature.

**Built 2026-07-24.** Design: [`docs/design/LORE_ENTRIES.md`](../design/LORE_ENTRIES.md).
Devlog: [`docs/devlog/2026-07-24_lore-entries.md`](../devlog/2026-07-24_lore-entries.md).

---

## Design

- [x] Write the design doc
- [x] Decide what a lore entry *is* and is not
- [x] Propose the entity shape
- [x] Where entries live: content vs state
- [x] Knowledge: one `Knows` namespace, learning via existing `fact_learned`
- [x] Retrieval: match sources, prompt position, budget, cut reporting
- [x] What the extractor sees (ids and titles, never bodies)
- [x] Answer the four open decisions — all settled as proposed

## Build

- [x] `LoreEntry` in Core
- [x] `LoreBook` — lookups, `Selected()` as the seam retrieval will slot into, `KnownBy`
- [x] Markdown loader: filename is the id, `#` heading is the title, strict frontmatter,
      no YAML dependency, loud on failure
- [x] Global id uniqueness — `DeltaValidator.Taken` extended to a fourth namespace
- [x] `DeltaValidator` rule: `fact_established` may not target a lore id
- [x] `FactLearned` accepts a lore id as readily as a fact id
- [x] `ContextAssembler.ForNarration` — bodies, in the volatile block
- [x] `ContextAssembler.ForExtraction` — ids and titles only
- [x] "Has heard of" per character, in both renderings
- [x] `TurnEngine` carries the `LoreBook`
- [x] `worlds/marrow/lore/` — the first pack, two entries
- [x] `/lore` — read-only; entries are authored in files, and the Lore Writer will own writing
- [x] `/knows` — grant a character knowledge of an entry (**not planned; needed immediately**)
- [x] `common: true` — everybody has heard of it. A second flag rather than a reuse of
      `always`, because retrieval and knowledge are different axes: a secret cult may be
      `always` and must not be `common`. **Derived at read time, never written to
      `Character.Knows`**, so the pack stays authoritative and no save holds a stale copy
- [x] `worlds/marrow/lore/kingdom-of-vaska.md` — the first common entry
- [x] Banner reports entry count and ids, and says so when zero
- [x] `LoreSelfTest` — 9 parser checks, the two validator rules, and two on common knowledge
      (that it reaches an untold character, and that reading it writes nothing)

## Verify

- [x] `dotnet build` clean
- [x] `--selftest` — 10 delta checks and 13 lore checks passing
- [x] `lore-learned` — **0/14, then 14/14 after one prompt rule** (pinned; see the provider
      note below)
- [x] `lore-not-established` — forbidden 0.00, 7/7 clean
- [x] Full scored sweep, 9 scenarios: 100% required, forbidden 0.00, rejects 0.00
- [x] End-to-end in an isolated directory: pack loads, `/lore` lists, bodies reach `/prose`
- [x] Devlog written

---

## Findings

- **The design's cheapest assumption was wrong, and measuring first is what caught it.**
  Learning lore via the existing `fact_learned` scored 0/14 — the model shredded the speech
  into new facts instead. The tell that it was a prompt gap rather than a design flaw was in
  the same output: it emitted `fact_learned` against the lore id for the *speaker* unprompted.
  One rule took it to 14/14, and no `lore_learned` delta was needed.
- **Two old decisions paid for this feature.** `Entity.Name` being mutable and
  `DeltaValidator.Taken` already enforcing global id uniqueness meant lore joined the namespace
  in one line. Same week as `ForNarration`/`ForExtraction` making stale ids harmless for the
  rename work.
- **Hald learns the order exists when shown a seal**, unprompted, every run. The mechanic
  works without being asked for.
- **The provider trap, fourth instance.** Adding the common entry appeared to drop
  `lore-learned` from 14/14 to 8/14 — exactly what a context-crowding regression would look
  like. Pinned: Baidu 14/14, Venice 10/14, on identical input. The earlier measurement had
  gone entirely to Baidu, and a provider new to the mix was the whole difference. **No single
  routed sweep is evidence about a change unless the provider mix is held fixed.**

## Fixed after the second play session

- [x] **Lore was never learned in play, despite scoring 14/14.** A 51-turn session about the
      Cult of the Blind taught it to nobody. `lore-learned` had a character *name* the topic
      and teach it; play speaks of the Drowned Father, the weeping woman and the tithe, and
      never says the label, because everyone present already knows what they are discussing.

      Two halves, neither sufficient alone: **keys in the extraction context** (0/14 → 8/14 —
      the extractor saw only the id and title, so "the Drowned Father" was an unrelated string)
      and **a prompt rule** naming the unnamed-topic situation (8/14 → 14/14). Verified on a
      second provider. See
      [`2026-08-04_implicit-lore.md`](../devlog/2026-08-04_implicit-lore.md).

- [x] `lore-learned-implicit` added as a diagnostic, in the shape play actually produces

## Still open

- [ ] **Redundant facts alongside the correct `fact_learned`.** The model still establishes
      paraphrases of what the entry already says, despite the rule telling it not to. The §9
      fact-store pressure from a fourth direction. Not chased with a second prompt rule —
      "add another sentence and see" is how this year's four wrong conclusions started.
- [ ] Measure whether a character refuses to reference lore they have not heard of. The
      premise of the feature, and it needs a *narration* eval, which does not exist — narration
      has no automated quality control at all.

## Deferred by decision 3

- [ ] Keyword matching against player input and recent narration
- [ ] Token budget, priority ordering
- [ ] Report which entries fired and which were cut — **built with the budget, never after**
