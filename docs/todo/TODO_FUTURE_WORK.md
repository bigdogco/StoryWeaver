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

## Domain model gaps found in play

- [ ] **Items and inventory do not exist.** A session had the player pay coppers for a
      beer; extraction correctly reported nothing, because there is nothing to report
      against. Not an extraction failure — a missing concept. Wants `Item`, ownership, and
      probably `ItemTransferred` / `ItemAcquired` deltas.

      Deliberately deferred: adding it before the extraction quality question is settled
      would mean tuning two things at once and knowing which caused what.

- [ ] **Buildings mentioned in prose are not locations.** A stranger kicked open the door
      of "one of the buildings" on the square; that building has no id and cannot be
      entered. Related to lazy world expansion — the general form is "when does a mentioned
      thing become a real entity", and answering it for buildings answers it for most
      scenery.

---

## Prompts as editable files

- [ ] **No prompt string lives in code.** Every prompt — narrator system prompt, extractor
      system prompt, the fact definition and NEVER-list, any corrective/repair instruction —
      must be an editable file the user can change without a rebuild. Code ships defaults
      (either embedded resources or files written on first run); the load path reads from
      disk and overrides them. This is the general form of the narration-style item below and
      of "the narrator prompt should be data, not a `const string`" — but it covers *all*
      roles, not just narration.

      **Hot-reload, at least optionally.** Once prompts are files, watch them and re-read on
      change (or re-read per turn behind a flag), so tuning a prompt does not mean restarting
      a session. Cheap once the load path exists; a `FileSystemWatcher` over the prompt
      directory is enough.

      Current violators to migrate: `LlmNarrator.SystemPrompt` and the extractor's system
      prompt, both `const string` today. Left as code for bootstrap deliberately — they were
      being tuned *as* code and `--eval` measures the version in the binary — but that reason
      expires once extraction is settled, which it now is.

      **Interaction with prompt caching:** prompts becoming per-world/per-session data is
      fine for caching as long as they are stable *within* a session; hot-reload
      deliberately breaks the prefix cache on change, which is the correct trade while
      tuning and should be off by default in normal play.

---

## Presentation

- [ ] **Output formatting is a UI-time decision, not a now decision.** The input convention
      (`*action*` plus speech) is adopted; narrator *output* stays plain prose. Because
      narration is a rendering of canon rather than the state itself, the convention can
      change at any point without breaking anything — unlike chat-log-as-state tools, where
      changing it leaves the history permanently mixed and the model reads that history
      back.

      Revisit once Avalonia can actually style it: italic action spans, coloured or
      attributed dialogue, NPC name emphasis. In a console, markup renders as literal
      asterisks, which is strictly worse than none.

- [ ] **Narration style belongs to the world author, not to the code.** Length is the
      obvious case — "one or two short paragraphs" is currently hardcoded in
      `LlmNarrator.SystemPrompt`, and it is a taste call, not a fact. A tense interrogation
      and a journey across a marsh want different pacing, and a comedic romp wants a
      different narrator entirely from a bleak horror.

      The general shape: **the narrator prompt should be data, not a `const string`.** Tone,
      register, point of view, verbosity, and content limits are all world-authoring
      parameters. Probably a narration-style block on the world definition, with a sensible
      default so a new world needs none of it. Possibly per-scene later — combat and
      travel genuinely want different lengths.

      Left hardcoded for bootstrap deliberately: there is no world-authoring format yet to
      hang it off, and inventing one to hold a single setting would be the wrong order.

- [ ] **Consider echoing the player's own line back into the transcript** so a session
      reads as a conversation rather than a sequence of replies. Presentation only; the
      input is already stored verbatim in `TurnRecord.PlayerInput`.

---

## The extraction eval

`--eval` (scenarios in [EvalScenarios](../../src/StoryWeaver.Cli/EvalScenarios.cs)) is now
the way any extraction change is judged. It earned its keep repeatedly — it killed a
two-call redesign built on a movement failure that turned out to be noise, and it caught
three response-shape bugs that all presented as "the model is bad".

Keep it honest:

- [ ] **Grow the scenario set as new failures appear in play.** 100% today means the *known*
      failure modes are covered on one small world, not that extraction is solved. Every real
      session that produces a wrong delta is a scenario worth adding — the `atmosphere` case
      (verbatim generated narration) already found things the hand-written ones missed.
- [ ] **Re-run before trusting any extraction change**, and before changing the extraction
      model. Provider routing drifts under the same model id, so the eval measures the model
      as actually served, and that can move.
- [ ] **n=7 was not enough to be safe.** A movement failure looked solid at n=7 and did not
      reproduce. For anything close, prefer three independent sweeps over one larger one — the
      cross-run spread is the signal, not a single average.

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
