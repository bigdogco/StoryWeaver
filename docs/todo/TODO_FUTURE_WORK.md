# Future Work

Ideas and deferred items. Not scheduled. Pull from here when starting a new task.

Anything explicitly out of scope for the current phase lands here rather than being
forgotten. See `TODO_BOOTSTRAP.md` for what is actually in flight.

---

## Deferred out of bootstrap

These were cut from the bootstrap phase deliberately — bootstrap exists to answer "is the
extraction pass reliable?", and none of these help answer it.

- [ ] **Avalonia UI** — decided as the UI framework, not yet built. Core must stay
      UI-agnostic so this stays a top-layer swap.
- [ ] **Streaming narration** — not implemented, but `ILlmClient` is shaped so the
      incremental form is the primitive and the whole-string call wraps it. Adding real
      streaming should then be a rendering change, not an architecture change.
- [ ] **World generation / lazy expansion** — generate a seed, not a world. Player walks
      toward an unnamed village → generate it, its notable NPCs, its tension, then write
      to canon so it is fixed forever after. Worlds get large by being played. Also
      solves the cold-start problem of filling 200 entries before you can start.
- [ ] **Lorebook retrieval layer** — keyword-triggered injection generated *from* the
      entity graph, not stored as a flat list.
- [ ] **Summarization / long-term memory** — see notes below on approaches.
- [ ] **Prompt caching optimization** — relevant once cost matters.
- [ ] **Packaging and distribution** — may never happen; it's a hobby project.

---

## Import / interop

- [ ] **SillyTavern / chub character card import (V2)** — JSON in a PNG `tEXt` chunk,
      base64-encoded. Parsing is trivial in C#; the work is entirely semantic mapping.
      `description` / `personality` / `scenario` map cleanly to the static block, but
      `state`, `knows`, `relationship`, and `location` have no source — an imported
      character arrives as a well-written husk needing an initialization pass.
- [ ] **Lorebook import** — worse fit. An entry is `keywords → prose`; there is no
      answer in the data for what entity type it is. Would need LLM classification.
- **Import only, never export.** Exporting to their format drops everything that makes
  the world a world.
- **Honest caveat on value:** most chub/janitor content is single-persona character-chat
  material, heavily NSFW-weighted. As seed material for an RPG *world* the hit rate will
  be low. Worth having eventually; not worth prioritizing.
- **The only thing this should influence now:** don't make the entity schema actively
  hostile to a later adapter.

---

## Long-term memory approaches

Lorebooks handle *world* facts. They do not handle *what happened*, which is harder.
Options, roughly in order of appeal for this project:

- [ ] **Structured state** — explicit JSON world state updated by a second model call,
      injected as a compact block. Deterministic, inspectable, user-editable, cheap in
      tokens. More work to build, needs a genre-fitting schema — which an RPG gives you
      largely for free. This is the direction with actual leverage.
- [ ] **Scene-indexed retrieval** — summarize per *scene* rather than per N messages,
      keep summaries addressable, retrieve whole scenes. Best fidelity-per-token of the
      options; least common in the wild.
- [ ] **Rolling summarization** — cheap and lossy; errors compound into permanent canon.
      Probably a component, not the answer.
- [ ] **Vector recall over history** — catches what keywords miss, but retrieves
      semantically-similar-but-irrelevant chunks constantly and returns fragments without
      temporal context. Mediocre in practice.

---

## Player-facing differentiators

Small things the incumbents mostly don't do, cheap to add once the foundations exist.

- [ ] **Show which lore entries fired and which were budget-cut.** Directly addresses the
      "why did it forget the Duke?" problem — see CHALLENGES.md.
- [ ] **Player arbitration of canon conflicts** — surface "canon says X, story said Y"
      and let the player decide. A feature, not a failure mode.
- [ ] **Inspectable extraction results** — let the player see what the world learned from
      a turn. Useful for debugging during development; potentially interesting to players
      afterwards.
- [ ] **Per-character knowledge as a visible mechanic** — the `knows` field already
      models secrets and lies. Surfacing it could be a gameplay feature, not just an
      implementation detail.

---

## Storage evolution

- [ ] **JSON → SQLite for the turn log.** Trigger: wanting full-text search over history.
      Likely end state is a hybrid — JSON for entity canon (small, diffable,
      hand-editable), SQLite for the turn log (grows unbounded, needs FTS, wants
      transactions). Keep `IWorldRepository` honest and this stays a weekend.

---

## Cost and quality tuning

- [ ] **A/B the extraction role's reasoning effort.** Per-role `reasoning` config is
      wired (`effort` / `maxTokens` / `exclude`), currently unset so models sit at their
      defaults. The obvious saving is turning extraction's effort down — but the probe's
      reasoning trace showed it doing genuinely useful work, including correctly deciding
      that Hald needed no `fact_learned` because he already knew the fact he was
      disclosing. Cutting effort may buy cost at the price of exactly the semantic
      accuracy that is already the weak link.

      **Measure, do not guess:** run the same fixed narration set at several effort levels
      and score deltas against a hand-written expected set. Needs §7 first, since a single
      probe call is not a sample.

- [ ] **Reconsider `maxTokens` per role once real turns exist.** Extraction was raised
      800 → 4000 to stop reasoning exhausting the budget. That number is a guess with
      headroom, not a measurement.

- [ ] **Measure cost per turn in currency, not tokens.** The smoke test showed extraction
      at ~35% of turn *tokens* against a design assumption of 5–10%, but the roles are
      priced differently, so the token ratio is not the cost ratio.
