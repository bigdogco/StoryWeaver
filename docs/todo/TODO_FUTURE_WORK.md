# Future Work

**The queue.** Everything that could be done and is not scheduled. Pull from here when
starting a task.

`docs/PROJECT.md` says what we are building and in what order; this says what is available to
pick up. **Phases live there, items live here, nothing lives in both.** Items owned by a
named phase are tagged `[Phase N]` so they surface when that phase opens instead of being
rediscovered by feel.

Reasoning long enough to be a document lives in `docs/design/`. Rules that are true every
time rather than things to do live in `PROJECT.md` §3 or `CHALLENGES.md` — a rule filed as an
unchecked box is a rule nobody will find.

Per `CLAUDE.md`: when a task doc closes, its unchecked items land here or are struck out with
a reason. Nothing is left stranded in a finished doc.

---

## Deferred out of bootstrap

These were cut from the bootstrap phase deliberately — bootstrap exists to answer "is the
extraction pass reliable?", and none of these help answer it.

- [ ] **Avalonia UI** — **[Phase 2]**, and now the whole of that phase rather than a line
      item. Core must stay UI-agnostic so this stays a top-layer addition.
- [ ] **Streaming narration** — not implemented, but `ILlmClient` is shaped so the
      incremental form is the primitive and the whole-string call wraps it. Adding real
      streaming should then be a rendering change, not an architecture change. **[Phase 2]** —
      there is nothing to stream into until there is a UI.
- [ ] **World generation / lazy expansion** — generate a seed, not a world. Player walks
      toward an unnamed village → generate it, its notable NPCs, its tension, then write
      to canon so it is fixed forever after. Worlds get large by being played. Also
      solves the cold-start problem of filling 200 entries before you can start.
- [ ] **Lorebook retrieval layer** — keyword-triggered injection generated *from* the
      entity graph, not stored as a flat list. Three parts, all of them absent today
      (`ContextAssembler` still dumps every entry into every prompt):

      - keyword matching against player input and recent narration
      - token budget with priority ordering
      - **report which entries fired and which were cut — built *with* the budget, never
        after.** A silent drop is the "why did it forget the Duke?" failure, and adding the
        report later means shipping the failure first

      Absorbed from `TODO_LORE_ENTRIES.md` 2026-08-13. Also the third contributor to context
      budgeting, alongside a full cast of sheets and loose items.
- [ ] **Summarization / long-term memory** — see the section below; gated on a measurement.
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

## Long-term memory

- [ ] **Summarization / long-term memory.** Four candidate approaches compared in
      [`design/LONG_TERM_MEMORY.md`](../design/LONG_TERM_MEMORY.md) — structured state,
      scene-indexed retrieval, rolling summarization, vector recall.

      **Gated on the 200-turn measurement in `PROJECT.md`, not on a decision.** The whole line
      of work assumes canon degrades over distance, and that has never been observed — 51
      turns is the longest session that exists. If canon holds, most of this is unnecessary,
      and the long run tells us *what failed*, which is what actually picks between the four.

---

## Player-authored canon — `/place`, `/character`, `/fact`

- [x] **Built 2026-07-21.** Prompted flow, ids suggested as slugs and shown before use, and
      everything routed through `DeltaValidator` + `DeltaApplier` + save. An authored character
      defaults to **offstage** (`LocationId` null) — a brother back home exists without being
      anywhere — and `/fact` asks whether your own character knows the truth you just wrote,
      since an author may record something their character has not discovered.

      Deliberately does **not** append a `TurnRecord`: the turn log feeds the narrator's prose
      memory window, and an authoring action has no prose. Canon already carries the change, so
      the narrator sees it through the state block on the next turn without being handed a
      fabricated narration line. A proper UI will log authoring separately.

      Original justification, kept because it is the reason the feature exists:

      **The gap it closes, measured.** Extraction never records a merely-*mentioned* entity —
      `player-place` 0/7, `player-absent-character` 0/7, `narrator-mention` 0/7. Say "I came
      from Astaria" and Astaria does not exist. The dividing line is **presence, not
      authorship**: walk somewhere new and it is recorded (`player-arrival` 14/14), mention it
      and it is not, and the narrator mentioning it fares no better than the player.

      **That behaviour is correct and should not be "fixed" in extraction.** A character
      *saying* something is not the same as it being true — players lie, misremember and
      boast, and NPC speech especially must not become world truth. The retired `player-claim`
      scenario recorded six models refusing it near-unanimously. The answer is not to weaken
      the rule but to give the player a door it does not apply to.

      **Why this is now more urgent than it looks:** the narration history window hides the
      gap. Say Astaria on turn 3 and the narrator references it correctly for ten turns —
      because it is sitting in the message window, not because anything recorded it. Then it
      slides out and Astaria ceases to exist, and the narrator can contradict the player's own
      backstory around turn 14. It looks like it works, right up until it doesn't.

      Implementation notes:

      - **Go through the normal delta path.** Build `LocationIntroduced` /
        `CharacterIntroduced` / `FactEstablished` and run them through `DeltaValidator` and
        `DeltaApplier`, so id uniqueness, cross-namespace collisions and the save are all
        handled by code that already exists and is tested. A second write path into canon is
        how the two disagree later.
      - Pairs with the "mentioned tier" idea below — entities that exist as
        referenced-but-unvisited, promotable when reached. That is the general answer to "when
        does a mentioned thing become real"; this is the deliberate, player-driven one, and the
        two are complementary rather than competing.

---

## Reroll a turn

**Partly built 2026-08-04: `/reroll` works on any turn that changed nothing** — about a quarter
of turns in a real session, and *every* turn where narration failed outright, since prose that
is not prose yields no deltas by definition. A turn that moved canon is refused with the reason
stated, and that half still needs the snapshot below.

The unlock was noticing the free subset: **a turn that applied no deltas needs no undo at all.**
Only the last history line has to go, which `ReplaceLastTurnAsync` already did. That was worth
more than it looked — it arrived the same day a narration failure printed a chain of thought
into a live story, which is exactly the case it covers.

Guarded by three self-tests: canon-moved is refused, the discarded prose is hidden from the
narrator, and the turn is replaced without advancing the counter.

- [ ] **Discard the last turn and narrate it again, on any turn.** The remaining half. Needs the
      canon snapshot described below.

- [x] ~~The feature every chat-RP site has, and the one people actually reach for when the prose
      is fine but *wrong*~~ — available now within the no-canon-change limit.

      Distinct from `/retry`, which is already built and only re-runs *extraction* over prose
      the player has already read. Rerolling changes the story, so it has to undo one.

      **The real argument for it: reroll is the only quality control narration has.**
      Extraction has an eval — objective right answers, scored, provider-attributed. Narration
      has nothing, because prose quality is taste and cannot be auto-scored. So the expensive,
      story-defining half of every turn has no automated check whatsoever, and a provider
      serving mediocre prose under a pinned model id would be invisible to us. Reroll is the
      human-in-the-loop substitute for the eval that cannot be written. That is a stronger
      justification than convenience, and it is why every chat-RP tool has one.

      **Temperature decides what each feature can actually fix.** Narration runs at 0.9, so a
      reroll genuinely resamples and will produce different prose. Extraction runs at 0.0, so
      `/retry` on a turn that *succeeded but got it wrong* will mostly reproduce the same
      deltas — it is reliably useful only when extraction failed outright, or when routing
      happens to land on a different provider. Worth stating in any UI so the two are not
      expected to do each other's job.

      **The obstacle: our deltas are not invertible.** `MoodChanged(hald, "wary")` does not
      record what the mood was before, and `FactLearned` does not record that the character
      did not previously know it. There is no way to compute an undo from the turn log, so
      rerolling needs a **snapshot of canon taken before the turn is applied**, plus dropping
      the last line of `history.jsonl` (which `ReplaceLastTurnAsync` already shows is
      workable).

      **This obstacle only applies to turns that changed something**, which is the observation
      that got the feature half-shipped without it. Measured at 23% of turns applying no deltas
      at all.

      Making deltas invertible instead — carrying the previous value on every delta — was
      considered and rejected: it doubles the schema surface the extraction model has to fill
      in correctly, to serve a feature the model should not be thinking about at all. The
      snapshot keeps the cost in storage, where it is cheap and testable.

      Design notes for later:

      - A single `canon.prev.json` written before each apply gives one level of undo, which is
        probably all anyone wants. Deeper history is what the turn log is for.
      - Reroll must **not** feed the discarded narration to the narrator on the retry, or it
        will anchor on the version being rejected. It is already excluded naturally, since the
        window is built from history and the record is dropped first.
      - Worth surfacing what the reroll changed in canon versus the discarded attempt — a
        reroll that produces different *facts* is more interesting than one that produces
        different prose, and it is a cheap window into extraction stability.

---

## Lore entries — the fourth entity type

**Built 2026-07-24.** Design: [`docs/design/LORE_ENTRIES.md`](../design/LORE_ENTRIES.md).
Devlog: [`docs/devlog/2026-07-24_lore-entries.md`](../devlog/2026-07-24_lore-entries.md).
Remaining work — keyed retrieval, budgeting, cut reporting, and the redundant-facts gap — is
tracked in `TODO_LORE_ENTRIES.md`. The notes below are the thinking that fed into it, kept for
context.

Added by the design pass and not captured below: a **Lore Writer** editor window is the
expected authoring surface once a UI exists, which is what makes markdown files the right
storage — the tool writes them, and they stay readable for diffing, sharing and hand-editing.
Once that window exists it should be the *only* writer, since a strict parser and a
hand-editor adding unknown keys are a bad combination.

- [x] ~~**A named topic with a body of prose, the way a DnD lorebook entry works.**~~ **Built
      2026-07-24.** The reasoning below is kept because it is why the feature has the shape it
      has; the design is in [`design/LORE_ENTRIES.md`](../design/LORE_ENTRIES.md) and the
      remaining retrieval work is the "Lorebook retrieval layer" item above.

      Raised by the
      user, and it is a better fit than the "faction with a standing toward the player" shape
      that was considered first — an organisation, a war, a religion, a bloodline is *reference
      material*, not a simulated actor.

      **Why this is not a `Fact`.** A fact is one proposition that is true or not, and it is
      deliberately nameless because a name would invite the extraction model to invent titles
      for statements. A lore entry is a *named topic with a body*. Crucially the nameless
      argument does not apply, because **lore is authored and never extracted** — a human
      writes the title. Shredding "the King's Investigators" into six atomic facts would be
      both tedious and lossy.

      | | `Fact` | Lore entry |
      |---|---|---|
      | shape | one proposition | named topic with a body |
      | name | deliberately none | the whole point |
      | origin | mostly extracted in play | authored only |
      | changes | established as the story runs | mostly static |

      **Decided: per-character knowledge, reusing `Knows`.** So one character has heard of the
      Investigators and another has not, and an NPC cannot reference an order they do not know.
      That is the mechanic separating this from a chat log, and secret organisations are
      exactly where it pays off. Consequence to design for: `Character.Knows` would hold ids of
      two different kinds, so either the id namespace stays globally unique (it already is —
      see `DeltaValidator.Taken`) or `Knows` splits.

      **The catch: this is what forces retrieval.** `ContextAssembler` currently dumps the
      entire world into every prompt. Fine for two locations and three characters, hopeless at
      forty lore entries. Adding lore is the point at which keyword-triggered injection stops
      being optional, and it brings the classic "why did it forget the Duke?" failure with it —
      hence the already-logged item about surfacing which entries fired and which were
      budget-cut.

      **One rule to hold:** lore is for things with *no* entity representation. Once something
      becomes a real `Character` or `Location`, that entity is authoritative. Otherwise a lore
      entry about Hald and the `Character` Hald drift apart, which is the exact incoherence the
      canon store exists to prevent.

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

- [ ] **The narration history window re-reads the whole turn log every turn.** `TurnEngine`
      calls `LoadHistoryAsync` and takes the last N, which is O(n) per turn against a file that
      only grows. Fine at bootstrap scale and deliberately not optimized yet, but it is the
      first thing that will actually hurt in a long world. Two cheap fixes when it does: keep
      the window in memory across a session and only read from disk on resume, or give the
      repository a `LoadRecentTurnsAsync(worldId, count)` that a seekable store can answer
      properly. The second is the better one either way.

- [x] ~~**JSON → SQLite for the turn log.**~~ **Dropped 2026-08-13.** Storage stays JSON
      permanently. A save is a surface the player opens and edits, not an implementation
      detail, and a database hides the world from the person who owns it. Full-text search
      over history — the only trigger this item ever had — is not worth that. See
      `docs/PROJECT.md` §3.

---

## Domain model gaps found in play

- [x] ~~**Items and inventory do not exist.**~~ **Built 2026-08-04** — see
      [`2026-08-04_items.md`](../devlog/2026-08-04_items.md). `Item`, four deltas, and the
      false-canon merge fixed at 14/14. *Inventory* as such is still absent: no quantity, no
      containers, no crafting. The original entry follows.

      A session had the player pay coppers for a
      beer; extraction correctly reported nothing, because there is nothing to report
      against. Not an extraction failure — a missing concept. Wants `Item`, ownership, and
      probably `ItemTransferred` / `ItemAcquired` deltas.

      ~~Deliberately deferred: adding it before the extraction quality question is settled
      would mean tuning two things at once.~~ **Promoted 2026-07-24.** The extraction question
      is settled (100% across 9 scenarios) and this is now the best-evidenced gap in the
      domain model:

      - `object-described` reproduces **an item becoming a `character_introduced`, 7/7** — a
        knife standing in the tavern with a name and a location, because that is the only
        delta that can bring a thing into canon
      - **8 of the 11 description-facts** in the 51-turn save describe something with no
        entity: the altar, the medallion, an object hidden in someone's coat
      - it retroactively explains the AtlasCloud "building as a character" failure, which was
        the same pressure surfacing on a worse provider

      Wants its own design pass. Open questions worth naming now: is an item an `Entity` with
      an owner, or a property of a character? Do items in a room need to be distinct from
      items held? Does an item need a description that can change, given that is the same gap
      §9 found for characters?

- [ ] **Buildings mentioned in prose are not locations.** A stranger kicked open the door
      of "one of the buildings" on the square; that building has no id and cannot be
      entered. Related to lazy world expansion — the general form is "when does a mentioned
      thing become a real entity", and answering it for buildings answers it for most
      scenery.

      **The character version, seen in live play 2026-07-24 (fresh save, turn 7).** Hald
      names "Reeve Silas at the hall on the high road" — a located, plot-bearing, absent
      person. The extractor did *not* introduce him (correct under the measured mention≠presence
      rule, 0/7), but then tried to record awareness of him with three `fact_learned reeve-silas`
      against a fact that was never established. The validator rejected all three — no
      corruption — but the reeve is now in the prose and absent from canon, so the narrator has
      nothing if the player follows the lead.

      This is the collision worth resolving: "don't invent entities for mentions" is right for
      scenery and wrong for a named NPC the story is actively pointing the player toward. The
      model clearly *wants* to record him and has no clean tool. `/character` is the manual
      workaround today. The real answer is probably a notion of an **offstage-but-named**
      entity — introduced without a location, which `Character.LocationId` already allows —
      triggered by naming rather than presence, for the narrow case of a proper-named person or
      place the prose commits to.

- [x] ~~**A character cannot be renamed.**~~ **Done 2026-07-23.** `character_renamed` carries
      an optional revised description; `/rename` is the authoring path. Ids are permanently
      opaque and names mutable, so no reference ever needs rewriting. `name-reveal` scores
      21/21 and the scored set is 100% across 9 scenarios. See
      `docs/devlog/2026-07-23_character-rename.md`.

      Still open, inherited from the same finding: `figure-is-young-woman` and
      `figure-in-cistern-location` remain *facts* carrying what are properly character
      attributes. A rename fixes the name; it does not stop description-shaped truths landing
      in the fact store.

- [ ] **Facts have no truth value and no attribution.** *Found in §9 play, 2026-07-23.* A lie
      is stored identically to a truth. The model already improvises around this — it wrote
      "claims" into both the id and the text of `hald-claims-roof-leaking` — but did not do so
      for the same character's other lie, which is now simply false canon.

      Wants a speaker/source on a fact and some notion of contested-vs-established. Interacts
      with per-character knowledge in a way worth thinking through: a character believing
      something false is a *feature*, and possibly the more interesting modelling than a global
      truth flag.

- [ ] **Momentary events land in permanent canon.** *Found in §9 play, 2026-07-23.* Two sword
      strikes on a creature that died two turns later are permanent world facts
      (`drowned-follower-wounded-again`, `...-again-2` — the suffix is the model resolving its
      own id collision). The `status_changed` chain already carried the real state. Facts are
      replayed into context and compete for budget, so this is sediment crowding out substance.

      Note this is the same pressure as the two items above, from a third direction: see
      *The fact store absorbs everything the delta set cannot express* in `CHALLENGES.md`.
      **Sequence the lore-entry work after this** — a fourth entity type added while the
      pressure is unrelieved just gives the overflow somewhere new to pool.

- [ ] **`relationship_changed` never fires in real play.** *Found in §9 play, 2026-07-23.*
      Zero across 51 turns, through an attempted murder and a coerced alliance. A stronger
      model emits it reliably on a scenario *built* to provoke it, so this is not a capability
      gap — standing moves by accumulation across many turns and a per-turn extractor sees one.

      Strongest candidate for the periodic reconciliation pass (compare canon against the last
      N turns) rather than more prompt work. `mood_changed` fired 46 times in the same session,
      which is the control: per-turn observables extract fine.

      **Confirmed 2026-08-04: still zero across a second 51-turn session** — 102 turns, two
      builds, two stories, not one `relationship_changed`. This is no longer a suspicion about
      a per-turn extractor; it is measured. Note the same session is *not* short of
      relationship material: Hald lies and is caught in it, Morwenna despises the King's seal,
      Silas is terrified into cooperation. All of it landed as facts and moods, none as
      standing.

      **Re-checked 2026-08-12, deliberately, and it survived.** The earlier evidence predates
      measurements carrying provider names, and was re-examined after one upstream was caught
      scoring 0% on scenarios another scored 100% on. Every saved session was swept:
      **one firing in 253 turns across five sessions** — `marrow-LLM-1` turn 11, an innkeeper
      announcing hostility in as many words, which is the case the eval scenario also covers.

      The re-check produced a better statement of the mechanism than "it never fires". The
      `hostility` scenario scores **10/10** on a healthy provider, so **the capability is
      fine** — what does not occur is the trigger. And the sharpest evidence is inside one
      session rather than across them: after 51 turns every standing sat at exactly its seeded
      value, while `mood` moved constantly over the same prose through the same call.

      **Mood is visible in one scene; standing is the integral of many.** No prompt rule fixes
      a thing the input does not contain, which is why this stays a reconciliation-pass item
      and not a prompt item.

---

## Dice-resolved checks

- [ ] **Resolve uncertain actions with a roll the narrator is told about, rather than letting
      it decide.** Full design in [`design/DICE_CHECKS.md`](../design/DICE_CHECKS.md): why a
      die roll is canon, why it should be a general *check* rather than a combat system, the
      double-counting hazard, and the two open questions (what losing costs, who sets the
      difficulty).

      **[Phase 3]** — under the base/plugin split this is not part of the base game. It is the
      archetypal plugin, and probably the first one designed from scratch rather than
      extracted, so it waits on Phase 3 saying what a plugin is.

      Carries the one genuinely new thing in it: *did the narration contradict the dice?* is
      objectively checkable, and would be the first property of **narration** that could be
      evaluated at all.

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

- [ ] **A portable `player.md` — a persona you carry between worlds.** Raised 2026-08-06, when
      the player sheet was settled as replacing character creation.

      A `player.md` currently belongs to a pack. The idea is that it belongs to *you*: a
      library of them, merged into whatever world you start, so the investigator you wrote once
      can walk into any pack that will have them.

      **This is the character-card ecosystem's actual strength**, and the one thing it does
      that world-pack tools do not — a persona is portable precisely because it carries no
      state. Worth taking seriously as a differentiator rather than as a convenience.

      What it needs first: a merge story (whose `player.md` wins when a pack ships one — the
      pack is telling you the premise, the persona is telling you who you are, and those are
      not the same field), and a UI, without which "merge a file into a pack" is a worse
      experience than editing the file. Both absent. Not now.

- [ ] **A world editor — placement is the case that argues for it.** Raised 2026-08-06 while
      settling what happens to a character sheet with no seat in the seed.

      The console can only ask for a location id and list the known ones, so "offstage" reads
      as a bug and a mistyped id reads as nothing at all. The same act in an editor is a list
      of locations with a character dropped into one, and the failure becomes an empty slot
      you can see. **The design was fine; the interface was what made it confusing** — worth
      remembering before the next feature gets redesigned to suit a text prompt.

      The shape the player described: create `warrior-mike.md`, seat him at `big-lake`, and
      he activates when the player reaches that location. Editing sheets, placing characters,
      writing lore, and drawing the location graph are all the same window.

      Prerequisite for nothing currently planned, and the reason 9.2's id enforcement is worth
      having *now* — an editor would never produce a malformed id, and until it exists a
      person typing by hand will.

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
- [ ] **`two-stage-entry-large` is still failing at 10/14** — a turn whose prose carries the
      player through an intermediate space into a room beyond. Fixed outright in a small world
      by the end-of-turn movement rule; a large world still gets it wrong 2/7, apparently
      because more plausible existing ids are available to settle on. Open.

**The rules for running it are not tasks and no longer live here.** Five operational ones —
re-run before trusting a change, read the per-provider breakdown, sample size is per provider,
world size is a variable, n=7 is not enough — moved to `CHALLENGES.md` 2026-08-13. The three
architectural ones — score outcomes not routes, a measurement without a provider name is not a
measurement, a schema branch is not free — are in `PROJECT.md` §3. They sat here as unchecked
boxes for weeks, which is where a rule goes to be forgotten.

### Automated provider calibration

- [ ] **Measure the providers behind a model and auto-fill a `providerIgnore` list.** The
      user's framing, and it is the right one: nobody can be expected to know which of a
      model's fourteen upstreams are good — it is hard enough to pick the model. If the answer
      has to be measured, the measuring should be automatic.

      **Why `requireParameters` is not enough.** It filters providers that do not *support* a
      parameter. Measured on 2026-07-21: `deepseek-v3.2` has 14 providers, 4 lacking
      `structured_outputs` and correctly excluded. But AtlasCloud *supports* the schema,
      returns *schema-valid* JSON, and picks the wrong delta branch — a building emitted as a
      `character_introduced`. No request parameter can prevent that. **Schema compliance and
      semantic quality are different properties, and only the first is enforceable.**

      Shape of the feature:

      1. Enumerate providers: `GET /api/v1/models/{author}/{slug}/endpoints`. Free, no tokens,
         returns `supported_parameters` per provider so ineligible ones are dropped up front.
      2. For each eligible provider, run the scored scenarios N times **pinned to it** via
         `provider.order` + `allow_fallbacks: false` — you cannot measure providers routing
         never sends you to.
      3. Score per provider, propose a `providerIgnore` list, show the table, let the user
         approve. Never exclude silently.

      Cost: ~10 providers × 8 scenarios × 5 runs ≈ 400 calls ≈ 8 cents. Cheap enough to re-run
      monthly.

      **Cheap is not the same as fast.** Measured while pinning: ~28s per call against a slow
      upstream, because pinning removes routing's load-balancing and you wait on whichever box
      you asked for. 112 calls took the better part of an hour. A full calibration is therefore
      a **background job with progress reporting**, not a blocking "test my providers" button —
      and providers should be swept concurrently, since the whole point is that they are
      independent.

      **This is what resolves the no-pinning objection.** Pinning is used only as a *test
      instrument* during calibration and never in the play path; what ships is an exclude
      list, which keeps every remaining provider and its redundancy, and degrades to today's
      unfiltered routing on a proxy that ignores the parameter. That is a far weaker
      commitment than depending on one provider being up.

      Caveats to design around:

      - Results mean "failed *our* extraction tests", not "bad provider". Say so in the UI.
      - **The list goes stale.** Providers redeploy; an excluded one may improve. Needs a
        timestamp, and re-validation rather than permanent blacklisting.
      - Calibration measures quality *per provider*; it does not predict your runtime mix,
        which is price-weighted and moves.
      - **Only works where there is an objective right answer** — extraction and worldgen.
        Narration quality is taste and cannot be auto-scored, so this never applies to it.
      - Shares everything with the model-comparison feature below; they are the same harness
        at two levels (which model, then which upstream of that model). Build them together.

### Ship the model comparison as a user-facing feature

- [ ] **Let users benchmark extraction models from inside the app.** The `--eval` harness is
      already the reusable asset from the bootstrap model hunt; exposing it turns "which cheap
      model reads my prose correctly?" into something a user answers for their own world and
      taste, not a decision baked into the binary. Ties directly to the existing per-role
      model config in settings — the natural flow is "pick a set of extraction models → run →
      see required/forbidden/rejects per model → set the winner."

      Design considerations, so this is not restarted from zero later:

      - **Scenarios are the hard part, not the runner.** Today's scenarios
        ([EvalScenarios](../../src/StoryWeaver.Cli/EvalScenarios.cs)) are hand-written against
        the one Marrow seed. A user's world is different, so a shipped version needs one of:
        curated genre-agnostic scenarios that travel; a way to *capture* scenarios from real
        play (a turn the user marks "extraction got this wrong/right" becomes a case — pairs
        well with the inspectable-extraction and canon-arbitration items above); or
        LLM-generated candidate scenarios the user vets. Capture-from-play is the most honest
        and reuses data already in `TurnRecord`.
      - **It spends real credits** — N runs × M models × scenarios. Surface estimated cost
        before running and actual after, per the "cost in currency, not tokens" item below.
        Not a background feature; an explicit, opt-in "test models" action.
      - **Results are point-in-time.** Provider routing drifts under a model id, so a saved
        result needs a timestamp and ideally the served-provider, and stale results should say
        so rather than be trusted as current.
      - **Prefer three sweeps over one big run** in the UI too — show the cross-run spread, not
        just an average, or it will mislead exactly the way n=7 did during bootstrap.
      - **Scoring must stay split the way it is now:** required scored *after* validation,
        forbidden scored on *raw* output — otherwise the validator hides re-introductions and
        the score lies. Whatever surfaces this must not "simplify" that away.

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
      probe call is not a sample. `--eval --providers <name>` now makes this measurable
      without routing noise, which it was not when this item was written.

- [ ] **Chain of thought for extraction — held in reserve, not adopted.** Raised by the user,
      who has had success anchoring models this way elsewhere. Legitimate, and there are three
      distinct forms; we already run two of them, one of which does less than it appears to.

      **What we have now:**

      1. *Native reasoning tokens* — the `reasoning` config above. Provider-side, invisible in
         the content, order-independent. Currently unset.
      2. *Per-delta `evidence`* — and it is **not** chain of thought. In `DeltaSchema` it is the
         **last** property of every branch, and models generate left to right, so `kind` and
         `characterId` are already committed by the time the justification is written. It is
         post-hoc rationalisation. Still valuable — it is how a wrong canon entry is traced back
         to what the model thought it saw — but it steers nothing.

      **The form that would actually anchor:** a top-level string emitted *before* `deltas`,
      forcing an analysis before the enumeration. Compatible with `strict: true`.

      > ⚠️ **The property-order trap, and it is ours specifically.** Providers do not preserve
      > property order — AtlasCloud emits alphabetically, which is the entire reason
      > `StateDeltaConverter` exists (`kind` arriving last broke System.Text.Json polymorphism).
      > Alphabetically **`deltas` sorts before `reasoning`**, so on some providers the model
      > would emit every delta and *then* "reason" about them: the CoT silently degrades into
      > post-hoc rationalisation, intermittently and only on some upstreams. Naming the field
      > `analysis` sorts it first, but depending on alphabetical luck is fragile. **Native
      > reasoning tokens sidestep this entirely** and are the safer form here.

      **Why not now:** extraction is at a verified 100% across three sweeps with forbidden 0.00.
      There is nothing on the current scenarios for it to fix, and that stability cost a day.

      **When it becomes right:** §9 and beyond. The 100% is eight scenarios on a two-location
      world; real play has more entities, longer narration, and a ten-turn history window — a
      much harder input, and CoT helps most exactly when the input gets messy. If extraction
      degrades there in ways narrow prompt rules do not fix, this is the next lever.

      **Not for narration.** Quality there is taste, cannot be auto-scored, and it is the
      expensive role — three arguments against, no measurement to settle it.

- [ ] **Reconsider `maxTokens` per role once real turns exist.** Extraction was raised
      800 → 4000 to stop reasoning exhausting the budget. That number is a guess with
      headroom, not a measurement.

- [ ] **Measure cost per turn in currency, not tokens.** The smoke test showed extraction
      at ~35% of turn *tokens* against a design assumption of 5–10%, but the roles are
      priced differently, so the token ratio is not the cost ratio.

---

## Swept in from finished task docs — 2026-08-13

Items stranded in task docs that were marked done. See
`TODO_BACKLOG_SWEEP.md` for the full triage of all 42.

### Phase 1 — the story layer

These finish the pack design written 2026-07-23, three of whose six components were never
built.

- [ ] **`world.json` manifest**, with a version a save can record. *(TODO_WORLD_PACKS)*
- [ ] **Opening message**, and the loader check that every name in it exists in the seed.
      *(TODO_WORLD_PACKS)*
- [ ] **Per-pack narration prompt overrides.** *(TODO_WORLD_PACKS)* — narration yes,
      extraction no; that split was already settled in the design. Overlaps with "Prompts as
      editable files" above, which is the general form.

### Phase 1 — narration eval

Every number this project has measures extraction. The half the player experiences has no
quality control at all. Design and open questions: `design/NARRATION_EVAL.md`.

- [ ] **Build the lore-knowledge check.** Does a character reference a lore topic they have
      not heard of? Deterministic: match entry `keys` against quoted speech, scoped to speakers
      who lack the entry in `Knows`. Narrow, misses paraphrase, costs nothing, and tests the
      one rule this codebase added with no way to check it. *(TODO_NARRATION_EVAL)*
- [ ] **Decide whether a judge model happens, or waits for dice to need one.** Everything else
      worth checking is semantic — did prose reveal an unlearned fact, did the narrator
      contradict canon or speak for the player. All need a model to judge, and a judge needs
      hand-labelled narration to be scored against, which nobody has produced.
      *(TODO_NARRATION_EVAL)*
- [ ] **Measure whether a character refuses to reference lore they have not heard of.** The
      premise of the lore feature, still unverified. *(TODO_LORE_ENTRIES)*
- [ ] **Does the narrator actually use sheet detail**, or only the one-line description? The
      point of prose over fields is expressiveness; if the body is ignored, the sheet design is
      wrong. *(TODO_CHARACTER_SHEETS)*

### Phase 2 — UI

- [ ] **Multiple saves per pack.** The ids are separated already, so this is a startup choice
      plus whatever the UI offers. *(TODO_WORLD_PACKS)*
- [ ] **Pack installing / sharing.** *(TODO_WORLD_PACKS)*
- [ ] **Surface rejected deltas prominently.** The last open box in `TODO_BOOTSTRAP`, and
      still true: a silently dropped delta is the same failure mode as a silently dropped
      lorebook entry. They are printed today, not prominent. Pairs with the
      inspectable-extraction item above.

### Unscheduled

- [ ] **`/item` authoring**, matching `/place` and `/character`. Extraction covers the observed
      cases; add it when a session needs to place an object by hand. *(TODO_ITEMS)*
- [ ] **`character_described` / `location_described` deltas.** Correctly sized at 3 of 11
      description-facts — real, and not the majority. *(TODO_FACT_HYGIENE)*
- [ ] **The knowledge-worthiness test** — decided and deliberately not written, because no
      scenario currently fails without it. Revisit if the category reappears in a fresh
      session. *(TODO_FACT_HYGIENE)*
- [ ] **Agreements as commitments.** `hald-agrees-to-guide` is durable and knowledge-worthy,
      but a promise is exactly the kind of thing that gets broken, and canon cannot record
      that. *(TODO_FACT_HYGIENE)*
- [ ] **Context size with a full cast of sheets.** Third contributor to the budgeting problem
      after lore and loose items, still unmeasured. *(TODO_CHARACTER_SHEETS)*
- [ ] **Record narration's provider.** Needs `INarrator` to return more than a string. Worth
      doing the day a narration eval exists and prose has a score to attribute — not before.
      *(TODO_PLAY_51_FIXES)*
- [ ] **Pack root as an explicit parameter, not the working directory.** Still a constant
      (`PlaySession.PackRoot`). *(TODO_WORLD_PACKS)*
- [ ] **`saves/` root configured rather than cwd-relative.** The reason `play.ps1` forces the
      cwd and harness testing needs a temp directory. *(TODO_WORLD_PACKS)*
- [ ] **Region- or world-level status.** Two facts out of eleven in one `ashfall` session
      wanted a condition wider than a room. **Carrying its threshold, not its hunch: build when
      it reproduces in a scenario *and* appears in a third session.** This is the exact
      arithmetic that got `item_lost` declined and then reversed. Confound worth stating —
      `ashfall`'s entire premise is one slow catastrophe, and a market town would probably
      produce none of these. *(TODO_ITEM_PLACEMENT)*

---

## Pending a session

Measurements blocked on a play session rather than on a decision. They are listed together
because otherwise each session ends without them being run — which is what has happened to
every one of them so far.

- [ ] **Re-run the fact audit against a fresh *human* session** and compare the category
      split. A model-played session scored ~68% correct against the human 55%; the model plays
      tidier than a person and the gap is the point. This is the measurement that matters.
      *(TODO_FACT_HYGIENE)*
- [ ] **Re-run the fact audit against a third session** — the object-fact share should fall now
      that items exist. *(TODO_ITEMS)*
- [ ] **Redundant facts alongside a correct `fact_learned`.** The model still establishes
      paraphrases of what a lore entry already says, despite a rule against it. Deliberately
      not chased with a second prompt rule — "add another sentence and see" is how this year's
      wrong conclusions started. *(TODO_LORE_ENTRIES)*
- [ ] **Does an id ever reach the prose through `{{ }}`?** The failure that forced the
      `ForNarration` / `ForExtraction` split. Validation should make it impossible; verify
      rather than assume. *(TODO_CHARACTER_SHEETS)*
- [ ] **Full scored set re-run, provider pinned**, whenever extraction changes. *(TODO_FACT_HYGIENE)*

---

## From the 150-turn run — 2026-08-13

Observed and deliberately not chased. See `devlog/2026-08-13_a-world-with-no-exits.md`.

- [ ] **Status fields have become prose.** The player ended as *"pinned deeper in ash,
      struggling to breathe, scalded, choking on superheated grit"* and the location carried six
      clauses. `Character.Status` was designed to hold `"wounded"`. This is the events-in-status
      problem at scale — previously logged at two occurrences, now the normal case in a long
      run. Wants a measurement before a prompt rule: **the last four attempts to tell the model
      what *not* to write into a field broke something else.**
- [ ] **`location_status_changed` is a third of all output.** 59 of 177 applied deltas, 19 on
      one location, largely re-describing the same unchanging state. Shipped 2026-08-06 and
      already the single commonest delta in the system. Related to the above: if status held a
      condition rather than a paragraph, it would not need rewriting every turn.
- [ ] **Facts and moods die when the player is alone.** Zero facts established in 150 turns
      against Marrow's 44 in 51; `mood_changed` once against 46. The whole cast sat untouched in
      the room they started in from turn 5. Possibly correct — a story with one character in it
      has little to record — but worth confirming it is the solitude and not a distance effect,
      which the next multi-character long run answers for free.
- [ ] **Check the domain model for other seed-only fields.** `Connections` looked populated in
      every hand-made test and was empty in every real world, because only seeds wrote it.
      Anything else with that shape has the same latent bug.

- [ ] **Character creation spins forever on closed stdin.** Found 2026-08-14 while testing the
      save lock: a pack with no `player.md` prompts for a name, and with stdin at EOF
      `Console.ReadLine()` returns null immediately and the loop reprints *"A name is required"*
      without end. Harmless interactively, and a hang for any agent-driven or piped run against
      a blank-slate pack — which is now a normal way this project is exercised. Wants an EOF
      check that exits with a message rather than looping.
