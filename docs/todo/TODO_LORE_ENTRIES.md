# TODO — lore entries

The fourth entity type: a named topic with a body of prose. Chosen as the first post-bootstrap
feature.

**Design doc first, no code** — the retrieval decision bakes itself into save files, so it is
settled before anything is written. See [`docs/design/LORE_ENTRIES.md`](../design/LORE_ENTRIES.md).

---

## Design

- [x] Write the design doc
- [x] Decide what a lore entry *is* and is not (carried forward — already settled)
- [x] Propose the entity shape
- [x] Work out where entries live: content vs state
- [x] Work out knowledge: one `Knows` namespace, learning via existing `fact_learned`
- [x] Work out retrieval: match sources, prompt position, budget, cut reporting
- [x] Work out what the extractor sees (ids and titles, never bodies)
- [x] **Answer the four open decisions in §7 of the design doc** — all settled as proposed:
      separate authored files, one `Knows` namespace, always-on injection first, markdown with
      the filename as the id

## Structural note

`docs/design/` is new. Design documents live there from now on — distinct from `todo/` (what
to do) and `devlog/` (what happened). Added for this task because the decision needed writing
down *before* any work, which neither existing folder is for.

## Build — ready to start

Design settled. In dependency order:

- [ ] `LoreEntry` in Core — `Id`, `Title`, `Body`, `Keys`, `Always`, `Priority`
- [ ] Markdown loader: one file per entry, filename is the id, `#` heading is the title,
      frontmatter for the rest. Strict, ~40 lines, no YAML dependency, loud on failure
- [ ] Global id uniqueness check against characters, locations and facts on load
- [ ] Dangling-id tolerance: a known lore id that no longer exists drops with a warning
- [ ] `DeltaValidator` rule: `fact_established` may not target a lore id
- [ ] `ContextAssembler.ForNarration` — bodies, into the volatile block, never mid-prompt
- [ ] `ContextAssembler.ForExtraction` — ids and titles only, never bodies
- [ ] `/lore` authoring command
- [ ] Eval: does the extractor emit `fact_learned` against a lore id unprompted? **Measure
      before assuming** — being wrong here costs a `lore_learned` delta kind
- [ ] Eval: does a character reference lore they do not know? The premise of the feature, and
      the same shape as the per-character fact knowledge that already works
- [ ] Devlog + `CHALLENGES.md` + `TODO_FUTURE_WORK.md` before commit

Deferred by decision 3, to be picked up when a world is big enough to prove the need:

- [ ] Keyword matching against player input and recent narration
- [ ] Token budget, priority ordering
- [ ] Report which entries fired and which were cut — **built with the budget, never after**
