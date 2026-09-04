# TODO: Bootstrap

**Status:** DRAFT — awaiting approval
**Created:** 2026-07-19

---

## Goal

Get to a **single playable turn, end to end, in a console**, with world state persisted
between runs.

The one question this phase answers:

> Can a cheap model reliably read narrative prose and emit correct structured state deltas?

**Answered 2026-07-20: yes.** `deepseek/deepseek-v3.2` scored 100% required, 0 forbidden,
0 rejects across three independent n=7 eval sweeps, at ~90 completion tokens and ~$0.0001 a
call. Reached via `--eval` (see [ExtractionEval](../../src/StoryWeaver.Cli/ExtractionEval.cs))
after a long detour through single-sample comparisons that turned out to be noise. The
canon-vs-narration architecture stands. Details in the 2026-07-20 devlogs.

Everything else in StoryWeaver is built on top of that extraction pass. If it is not
reliable, the architecture needs rethinking — and we want to learn that now, cheaply,
before there is a UI or a schema to migrate.

## Non-goals for this phase

Explicitly out of scope. Listed so they do not creep in:

- Avalonia UI (decided, but comes later)
- World generation / lazy expansion
- Lorebook retrieval and keyword triggering
- Summarization or long-term memory
- SillyTavern / chub import
- Prompt caching optimization
- Packaging or distribution
- Unit test projects (per CLAUDE.md, testing is manual; build is the only automated check)

---

## Decisions locked

Recorded so we do not relitigate them later.

| Decision | Choice | Rationale |
|---|---|---|
| Language | C# / .NET LTS | Static typing, async, good serialization |
| UI (later) | Avalonia | Cross-platform, all-C#, no web toolchain |
| Storage (now) | JSON behind `IWorldRepository` | Git-diffable saves are the best debugging tool for this phase |
| Storage (later) | ~~Likely hybrid: JSON canon + SQLite turn log~~ **Superseded 2026-08-13: JSON permanently** | The save is a surface the player edits by hand, so it stays readable. See `docs/PROJECT.md` §3 |
| LLM access | OpenRouter | One integration, many models, easy A/B of extraction models |
| LLM client | Hand-rolled `HttpClient`, ported from AI-Lord | Two endpoints, plain JSON; an OpenAI-shaped SDK would add abstractions we don't need |
| Model selection | Per **role**, not per call site | Narration and extraction want very different models |
| Secrets | Environment variables only | Repo will be open-sourced |
| First harness | Console | Playable in days; keeps Core UI-agnostic |

**Core architectural principle:** canon and narration are separate. The entity store is
the source of truth; prose is a rendering of it. `StoryWeaver.Core` must never reference
a UI framework.

---

## Open items — need answers or verification before/during work

- [x] ~~**OpenRouter API specifics**~~ — RESOLVED: OpenAI-compatible.
      `POST https://openrouter.ai/api/v1/chat/completions`, `Authorization: Bearer`,
      standard `messages`/`choices[]`/`usage`. Optional `HTTP-Referer` / `X-Title`
      headers for app attribution. **Decision: hand-rolled `HttpClient` wrapper**,
      ported from the existing AI-Lord implementation (see §4).
- [x] ~~**Structured output approach**~~ — RESOLVED: `response_format` with
      `type: "json_schema"`, carrying `name`, `strict: true`, and a schema using
      `additionalProperties: false` + explicit `required`. **Critical caveat and its
      mitigation are in §4 and CHALLENGES.**
- [x] ~~**Model choices per role**~~ — RESOLVED: narration `qwen/qwen3.7-plus`,
      extraction `deepseek/deepseek-v4-flash`, summarize + worldgen
      `deepseek/deepseek-v3.2`. **Sub-question answered by the smoke test:**
      `deepseek-v4-flash` *does* honour `json_schema` through OpenRouter with
      `require_parameters: true` — schema-conformant JSON on the first attempt, no repair
      round-trip. The validator+repair path stays as the fallback it was designed to be.
- [x] ~~**Streaming for narration**~~ — RESOLVED: not implemented in bootstrap, but the
      interface is shaped to allow it later without a rewrite. See §4.

---

## Tasks

### 1. Repository scaffolding

- [x] Create solution `StoryWeaver.sln` (`src/` layout, matching AI-Lord)
- [x] Create projects:
  - [x] `StoryWeaver.Core` — domain model, turn loop. **No UI, no HTTP, no storage deps.**
  - [x] `StoryWeaver.Llm` — provider abstraction, role config, prompt assembly
  - [x] `StoryWeaver.Storage` — JSON implementation of Core's repository interface
  - [x] `StoryWeaver.Cli` — throwaway harness. **Renamed from `StoryWeaver.Console`:** a
        `StoryWeaver.Console` namespace shadows `System.Console`, breaking every
        unqualified `Console.WriteLine` inside the project.
- [x] Wire project references (Core depends on nothing; others depend inward)
- [x] `.gitignore` — .NET template plus `*.local.json`, `saves/`, `.env`
- [x] `.editorconfig` — formatting, file-scoped namespaces, `_camelCase` private fields
- [x] `Directory.Build.props` — shared `net8.0`, nullable, warnings-as-errors
- [x] Verify: `dotnet build` succeeds on empty scaffold (0 warnings, 0 errors)

**Target framework: `net8.0`.** Installed SDKs are 8.0 (LTS) and 9.0 (STS); no 10. .NET 8's
LTS window ends around Nov 2026 — bumping later is a one-line change in
`Directory.Build.props`.

### 2. Docs structure

Per CLAUDE.md:

- [x] `docs/devlog/` directory
- [x] `docs/CHALLENGES.md` — seeded with known risks (see below)
- [x] `docs/todo/TODO_FUTURE_WORK.md` — seeded with deferred items from this doc

### 3. Configuration and secrets

- [x] `settings.example.json` — committed template, no secrets
- [x] `settings.local.json` — gitignored, holds the real key (verified not tracked)
- [x] API key from settings file, overridable by `STORYWEAVER_API_KEY` /
      `OPENROUTER_API_KEY`; clear error message if absent
- [x] Config model binding + validation on startup (fail loudly, not at first API call).
      Aggregates *all* problems into one message rather than failing on the first.
- [x] Validation enforces the `json_schema` ⇒ `requireParameters` coupling — the guard
      against the OpenRouter routing issue, checked at startup rather than discovered
      as intermittent bad output later.
- [x] Settings file located by walking up from the executable, so the real file stays at
      the repo root and edits do not require a rebuild.
- [x] Harness accepts an optional settings path argument (for trying alternate configs)

### 4. LLM layer

**Port, don't rewrite.** A working OpenRouter client already exists at
`D:\Hobbies\Coding\AI-Lord\src\AILord\LLM\` (`LLMClient.cs`, `LLMRequest.cs`,
`LLMResponse.cs`). It carries a shared retry budget across transient and content
failures, OpenRouter attribution headers, prompt/response file logging, and — most
valuably — a **content-validation-and-repair loop** that re-asks the model with a
corrective instruction when the response fails a supplied validator. Start from that.

Changes needed when porting:

- [x] Decouple from `MCMSettings` (Bannerlord mod settings) → our own config model
- [x] Extend `ResponseFormat` from `json_object`-only to support
      `type: "json_schema"` with `name` / `strict` / `schema`
- [x] **Add `provider: { require_parameters: true }` on the extraction role** — see
      the caveat below. Per-role, not global.
- [x] Swap `Newtonsoft.Json` → `System.Text.Json` (no legacy constraint here)
- [x] Add per-role resolution: code asks for `LlmRole.Extraction`, config maps to model
- [x] Set an explicit `HttpClient` timeout (narration calls can run long)

> ⚠️ **The load-balancing caveat.** OpenRouter routes a single model ID across multiple
> upstream providers, price-weighted by default. **A provider that does not support a
> parameter silently ignores it** — no error, no warning. Without
> `require_parameters: true`, an extraction call can intermittently return prose instead
> of JSON depending on which provider won that request's routing, at a rate that tracks
> provider price distribution. This is close to unreproducible if you do not know it
> exists. Set `require_parameters: true` on any role that depends on `response_format`.
>
> The ported validator+repair loop is the second line of defence: even with routing
> constrained, a cheap extraction model may emit malformed JSON, and re-asking with a
> corrective instruction recovers it without failing the turn.

- [x] `ILlmClient` — minimal: take a request, return text or structured result
- [x] Role-based resolution: code asks for `LlmRole.Narration`, config maps to a model
- [x] **Shape the interface for future streaming.** Streaming is *not* implemented in
      bootstrap, but make the incremental form the primitive and the "give me the whole
      string" call a thin wrapper that accumulates it. Callers in Core use the simple
      form only. Adding real streaming later then becomes a console/UI rendering change
      rather than a change to `ILlmClient` and the turn loop — i.e. the two things
      everything else depends on. Cheap now, expensive to retrofit.
      **As implemented:** `CompleteAsync` takes an optional `Action<string>? onChunk`
      that today fires once with the full text. Chosen over `IAsyncEnumerable<string>`
      because the result also carries usage, serving model, and error state, and
      splitting those from the text stream complicates every caller.
- [x] Request/response logging to disk — **essential for debugging the extraction pass**
      (`ILlmLog` / `FileLlmLog`, one file per session)
- [x] Basic error handling: rate limits, timeouts, malformed responses. Also covers
      OpenRouter's habit of returning HTTP 200 carrying an error object, and
      distinguishes caller cancellation from `HttpClient` timeout (both surface as
      `TaskCanceledException`; only the latter is a failure result).
- [x] Smoke test behind a `--smoke` flag — two live calls, narration then
      schema-constrained extraction. Behind a flag because it spends real credits.

### 5. Domain model — minimal

Deliberately small. Expand only when a turn actually needs it.

- [x] `Entity` base — id, name. Ids are human-readable slugs, not GUIDs: they appear in
      prompts, saved JSON, and logs, all of which get read by a human while debugging.
- [x] `Character` — static description, state (location, status, mood),
      `knows` (set of fact ids), `relationship` to player, `last_seen`
- [x] `Location` — description, connections (plain id set; no direction/door model until
      a turn needs one, since movement is described in prose, not navigated)
- [x] `Fact` — a discrete piece of world truth that characters can know.
      **Deliberately not an `Entity`:** entities have a name distinct from their
      description, a fact is only its text, and a `Name` field would invite the extraction
      model to invent titles for statements.
- [x] `WorldState` — the entity graph plus turn counter, with scene queries
      (`CharactersIn`, `CharactersWithPlayer`, `KnownFacts`). Mutable by design; applying
      deltas lives in the turn loop, not here.
- [x] `StateDelta` — a proposed change, the output type of extraction. **Closed set of
      nine kinds**, polymorphic on a `kind` discriminator.

**Design note:** per-character knowledge (`knows`) is the field that makes NPCs feel
simulated rather than narrated. Do not collapse it into global state. It holds fact *ids*,
not text, so two characters cannot end up knowing different versions of the same fact.

**Decision — the delta set is strictly closed.** Enumerated kinds only; extraction is
constrained to them by JSON schema. A generic `{ entity, property, value }` patch was
rejected: a cheap model will confidently write `character.mood.current` when canon says
`mood`, no schema can catch it, and it lands as a silent no-op. With a closed set, a change
the model wants to make but cannot express becomes a *visible* failure — which at this
stage is the point, since it tells us what the world model is missing.

Kinds: `character_moved`, `player_moved`, `status_changed`, `mood_changed`,
`relationship_changed`, `fact_established`, `fact_learned`, `character_introduced`,
`location_introduced`.

`CharacterIntroduced` is separate from `CharacterMoved` so "the narration invented
someone" stays distinguishable from "someone walked in" — the first is worth watching, the
second is routine. Every delta carries optional `Evidence` (the model's justification,
ideally quoting the prose); it mutates nothing and exists so a wrong canon entry can be
traced back to what the model thought it saw.

### 6. Storage

- [x] `IWorldRepository` in Core — load, save, transactional commit of a delta set
- [x] JSON implementation in Storage (`JsonWorldRepository`)
- [x] Save format: one directory per world, human-readable, sorted keys so `git diff` is
      meaningful. Canon `canon.json` (whole), history `history.jsonl` (append-only)
- [x] Atomic write (temp file + move) so a crash cannot corrupt a save
- [x] Verify: interface exposes no JSON-specific types (the SQLite swap must stay cheap)

Two save-format bugs were caught and fixed before freezing it: System.Text.Json drops the
`OrdinalIgnoreCase` comparer when deserializing `init` collections (a loaded world would
match ids case-sensitively), and `HashSet` order is unstable across resizes (noisy diffs).
Both fixed with save-only converters that sort on write and read case-insensitive. See the
2026-07-21 devlog. Verified by an 18-assertion offline round-trip check.

### 7. Turn loop

The heart of the phase.

- [x] Assemble context from world state (naive for now: dump relevant entities).
      `ContextAssembler` prints **ids alongside names** — the probe showed that a model not
      told an entity's internal id invents a plausible slug and produces a dangling
      reference.
- [x] **Narrate** — call narration role, get prose (`LlmNarrator`)
- [x] **Extract** — call extraction role with the prose, get `StateDelta[]`
      (`LlmStateExtractor`)
- [x] Validate deltas against current canon; log conflicts (`DeltaValidator`)
- [x] Commit deltas transactionally (`DeltaApplier`, then save)
- [x] Append turn to history (`TurnRecord`, storing rejections and raw output too)

**Structure:** the turn loop lives in `Core` and reaches the models through `INarrator` /
`IStateExtractor`, which `Core` defines and `Llm` implements. `Core` still references
nothing.

**Ordering decision:** narration is shown to the player *regardless* of what extraction
does. Extraction failing is a canon problem, not a storytelling one — discarding good
prose because a second model could not parse it turns a silent bookkeeping error into a
visibly broken game. `TurnOutcome.ExtractionFailed` keeps "extraction died" distinct from
"extraction ran and its output was rejected"; those are different problems.

**Section 6 was deliberately deferred until after this**, so the save format is not
frozen around a domain model the turn loop is still reshaping. `InMemoryWorldRepository`
was written *before* the JSON one so that "the interface leaks no storage detail" is
tested by a second implementation rather than self-certified.

- [x] ~~**Author the extraction JSON schema for the closed delta set**~~ — done and
      verified by `--probe-schema`. Nine-branch `anyOf` under `strict: true` works on
      `deepseek-v4-flash`; the flat-object fallback is not needed. Schema currently lives
      in `DeltaSchemaProbe`; **moves to prompt assembly when §7 is built.**

**Validation is now the critical piece, not an afterthought.** The probe proved schema
compliance is solved and semantic correctness is not — in one call the extractor produced
a dangling fact reference, re-introduced a known location, filled a `description` field
with an event, and missed the most obvious mood change in the passage. Details in
CHALLENGES. Validation must therefore reject rather than trust:

- [x] Reject `fact_learned` referencing a `factId` not in canon and not established
      earlier in the same delta set (ordering matters within a batch)
- [x] Reject `*_introduced` for an id that already exists — the model re-introduces known
      entities when they are merely mentioned
- [x] Reject any delta referencing an unknown `characterId` / `locationId`
- [x] Reject `relationship_changed` targeting the player — a consequence of the player
      being an ordinary `Character`, enforced where the meaningless field would be written
- [x] **Rejections cascade.** Validation walks the batch in order against canon plus
      everything accepted *so far*, so a batch may legitimately introduce a character then
      move them — but if the introduction is rejected, the move is rejected too. Without
      that, rejecting a `fact_established` would leave the dangling `fact_learned` behind:
      exactly the failure being prevented.
- [x] ~~Decide how the **player** is addressed~~ — RESOLVED: the player is an ordinary
      `Character` under the reserved id `Character.PlayerId` (`"player"`). No
      player-specific delta kinds; every existing kind addresses them for free, which is
      what the extractor was already trying to do unprompted.

      **Consequence worth noting:** `WorldState.PlayerLocationId` was standalone mutable
      state and is now *derived* from the player character's `LocationId`. Keeping both
      would have been the same fact stored twice, and therefore the same fact able to
      disagree with itself after any delta that touched only one copy.

      Costs accepted: `RelationshipToPlayer` is meaningless on the player's own record
      (stays neutral, ignored), and every query meaning "the people around you" must now
      exclude the player explicitly — hence `NpcsWithPlayer()` rather than the old
      `CharactersWithPlayer()`.
- [x] Surface rejected deltas prominently in the harness. **Moved to TODO_FUTURE_WORK 2026-08-13 [Phase 2]** — printed today, not prominent; making it prominent is a UI job.


Conflict handling for v1: **log and surface, do not auto-resolve.** We need to see how
often and how badly it goes wrong before deciding what to do about it.

### 8. Console harness

- [x] Hardcoded starting scenario — two locations, two NPCs, and one fact that **exactly
      one NPC knows**, so per-character knowledge is observable. A scene with no secrets
      could not show whether knowledge stays where it belongs.
- [x] Read player input → run turn → print prose
- [x] `/state` — dump world state as the models see it
- [x] `/raw` — last raw extraction response
- [x] Applied, **no-op**, and rejected deltas printed after every turn. Shown by default
      rather than behind a command: a silently dropped delta is the failure that would
      otherwise take fifty turns to notice.
- [x] **Roleplay input convention.** `*actions between asterisks*`, speech outside them.
      Both authoritative; the narrator may not rewrite, paraphrase, or echo the player's
      dialogue, nor invent words or feelings for them. Plain instructions still work.

      Adopted *before* §9 deliberately — playing the 50-turn validation session in a style
      we were about to change would have made that session poor evidence.

      **This closed a real hole:** extraction previously saw only the narration. When the
      player writes an action the narrator does not restate — handing over an object,
      revealing something to an NPC — it happened, but canon never heard about it.
      `IStateExtractor` now takes the player input as well.

      Narration length stays at two to four paragraphs — briefly cut to one or two, and
      reverted as too thin. The agency rules that came in alongside it are kept, since they
      are what actually protects the player's turn: stop where the player would naturally
      act, leave the scene open, never resolve the encounter for them. Length is a taste
      call and belongs to the world author (see TODO_FUTURE_WORK), not to the code.
- [x] `/prose` — world state as the *narrator* sees it. Its own command because that view
      must contain no ids, and eyeballing it is the only check that the narrator cannot
      leak one into the story.
- [x] Load or create a world — resumes if `saves/marrow/` exists, else seeds and saves
- [x] Save on exit / autosave per turn — the turn loop persists every turn; quitting just
      reports where the world is saved

### 9. Validation

- [x] `dotnet build` clean — 0 warnings, 0 errors
- [x] Manual play session — **51 turns**, `saves/marrow`, 2026-07-22 → 2026-07-23
- [x] Review: did state stay coherent? How often did extraction get it wrong?

      Yes. 209 deltas applied, 8 rejected (0.16/turn), 9 no-ops, 12 turns changing nothing,
      no corruption and no history/canon desync. One rejection lost information; the other
      seven are the validator working. Every `player_moved` landed in the right room —
      the two-stage movement fix held under real play.

      The headline result is a walk-on: a guard last seen on turn 13 returned on turn 51
      with the right name, armour, weapon, location and status, against a 10-turn narration
      window. Canon does not degrade with distance, which is the entire thesis.

      Failures found are gaps in what the delta set can *express*, not extraction quality.
      Full analysis in `TODO_S9_VALIDATION.md`.
- [x] Record findings in `docs/CHALLENGES.md`
- [x] Devlog entry before commit (per CLAUDE.md) —
      `docs/devlog/2026-07-23_fifty-turn-validation.md`

---

## Known risks — seed for CHALLENGES.md

- **Extraction reliability is unproven.** The entire architecture rests on it. Small
  models may be inconsistent at structured output over creative prose.
- **OpenRouter silently drops unsupported parameters.** Requests are load-balanced
  across upstream providers (price-weighted); a provider lacking `response_format`
  support ignores it rather than erroring, so extraction intermittently returns prose.
  Mitigated by `provider: { require_parameters: true }` plus the validator+repair loop.
  Recorded here because the failure is intermittent, provider-dependent, and produces
  no error signal — worst possible debugging profile if encountered cold.
- **Silent lore drops.** Once budgeting exists, entries that do not fit are dropped with
  no signal. Surface this rather than hiding it.
- **Compounding summary errors.** Not in this phase, but a wrong fact committed to canon
  becomes permanent. Extraction validation is the defence.
- **Cache invalidation vs. dynamic injection.** Injecting lore mid-prompt destroys prefix
  caching for everything below it. Relevant once cost matters.
- **Cost per turn.** Two-plus model calls per turn. Needs measuring early.

---

## Definition of done

- [x] Solution builds clean
- [x] A world can be created, played for multiple turns, saved, reloaded, and continued
- [x] Character state visibly updates as a result of narration
- [x] Extraction results and conflicts are inspectable
- [x] All above tasks checked off, devlog written, `TODO_FUTURE_WORK.md` updated

**Bootstrap complete, 2026-07-23**, closed by the §9 play session. Two items remain checked-off
with caveats worth carrying forward rather than treating as done: rejected deltas are printed
but not yet *prominent* (§7), and `relationship_changed` has never fired outside a scenario
built to provoke it. Both are logged in `TODO_FUTURE_WORK.md`.
