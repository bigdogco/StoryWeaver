# TODO: Bootstrap

**Status:** DRAFT — awaiting approval
**Created:** 2026-07-19

---

## Goal

Get to a **single playable turn, end to end, in a console**, with world state persisted
between runs.

The one question this phase answers:

> Can a cheap model reliably read narrative prose and emit correct structured state deltas?

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
| Storage (later) | Likely hybrid: JSON canon + SQLite turn log | Trigger to switch = wanting full-text search over history |
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
- [ ] **Model choices per role** — Pavel selects these; he uses OpenRouter regularly.
      Leaning toward DeepSeek or similar for cost reasons (Anthropic pricing is not
      viable for a game loop with 2+ calls per turn). **Open sub-question:** confirm the
      chosen extraction model actually supports `json_schema` through OpenRouter — if it
      does not, the validator+repair path (§4) becomes the primary mechanism rather than
      the fallback.
- [x] ~~**Streaming for narration**~~ — RESOLVED: not implemented in bootstrap, but the
      interface is shaped to allow it later without a rewrite. See §4.

---

## Tasks

### 1. Repository scaffolding

- [ ] Create solution `StoryWeaver.sln`
- [ ] Create projects:
  - [ ] `StoryWeaver.Core` — domain model, turn loop. **No UI, no HTTP, no storage deps.**
  - [ ] `StoryWeaver.Llm` — provider abstraction, role config, prompt assembly
  - [ ] `StoryWeaver.Storage` — JSON implementation of Core's repository interface
  - [ ] `StoryWeaver.Console` — throwaway harness
- [ ] Wire project references (Core depends on nothing; others depend inward)
- [ ] `.gitignore` — .NET template plus `settings.local.json`, `saves/`, `.env`
- [ ] `.editorconfig` — basic formatting consistency
- [ ] Verify: `dotnet build` succeeds on empty scaffold

### 2. Docs structure

Per CLAUDE.md:

- [ ] `docs/devlog/` directory
- [ ] `docs/CHALLENGES.md` — seeded with known risks (see below)
- [ ] `docs/todo/TODO_FUTURE_WORK.md` — seeded with deferred items from this doc

### 3. Configuration and secrets

- [ ] `settings.json` — committed, no secrets, holds role→model mapping
- [ ] `settings.local.json` — gitignored, optional overrides
- [ ] API key read from environment variable only; clear error message if absent
- [ ] Config model binding + validation on startup (fail loudly, not at first API call)

### 4. LLM layer

**Port, don't rewrite.** A working OpenRouter client already exists at
`D:\Hobbies\Coding\AI-Lord\src\AILord\LLM\` (`LLMClient.cs`, `LLMRequest.cs`,
`LLMResponse.cs`). It carries a shared retry budget across transient and content
failures, OpenRouter attribution headers, prompt/response file logging, and — most
valuably — a **content-validation-and-repair loop** that re-asks the model with a
corrective instruction when the response fails a supplied validator. Start from that.

Changes needed when porting:

- [ ] Decouple from `MCMSettings` (Bannerlord mod settings) → our own config model
- [ ] Extend `ResponseFormat` from `json_object`-only to support
      `type: "json_schema"` with `name` / `strict` / `schema`
- [ ] **Add `provider: { require_parameters: true }` on the extraction role** — see
      the caveat below. Per-role, not global.
- [ ] Swap `Newtonsoft.Json` → `System.Text.Json` (no legacy constraint here)
- [ ] Add per-role resolution: code asks for `LlmRole.Extraction`, config maps to model
- [ ] Set an explicit `HttpClient` timeout (narration calls can run long)

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

- [ ] `ILlmClient` — minimal: take a request, return text or structured result
- [ ] Role-based resolution: code asks for `LlmRole.Narration`, config maps to a model
- [ ] **Shape the interface for future streaming.** Streaming is *not* implemented in
      bootstrap, but make the incremental form the primitive and the "give me the whole
      string" call a thin wrapper that accumulates it. Callers in Core use the simple
      form only. Adding real streaming later then becomes a console/UI rendering change
      rather than a change to `ILlmClient` and the turn loop — i.e. the two things
      everything else depends on. Cheap now, expensive to retrofit.
- [ ] Request/response logging to disk — **essential for debugging the extraction pass**
- [ ] Basic error handling: rate limits, timeouts, malformed responses

### 5. Domain model — minimal

Deliberately small. Expand only when a turn actually needs it.

- [ ] `Entity` base — id, name, type
- [ ] `Character` — static description, state (location, status, mood),
      `knows` (list of fact ids), `relationship` to player, `last_seen`
- [ ] `Location` — description, connections
- [ ] `Fact` — a discrete piece of world truth that characters can know
- [ ] `WorldState` — the entity graph plus turn counter
- [ ] `StateDelta` — a proposed change, the output type of extraction

**Design note:** per-character knowledge (`knows`) is the field that makes NPCs feel
simulated rather than narrated. Do not collapse it into global state.

### 6. Storage

- [ ] `IWorldRepository` in Core — load, save, transactional commit of a delta set
- [ ] JSON implementation in Storage
- [ ] Save format: one file per world, human-readable, stable key ordering so
      `git diff` is meaningful
- [ ] Atomic write (temp file + move) so a crash cannot corrupt a save
- [ ] Verify: interface exposes no JSON-specific types (the SQLite swap must stay cheap)

### 7. Turn loop

The heart of the phase.

- [ ] Assemble context from world state (naive for now: dump relevant entities)
- [ ] **Narrate** — call narration role, get prose
- [ ] **Extract** — call extraction role with the prose, get `StateDelta[]`
- [ ] Validate deltas against current canon; log conflicts
- [ ] Commit deltas transactionally
- [ ] Append turn to history

Conflict handling for v1: **log and surface, do not auto-resolve.** We need to see how
often and how badly it goes wrong before deciding what to do about it.

### 8. Console harness

- [ ] Load or create a world
- [ ] Hardcoded starting scenario (one location, two characters, a couple of facts)
- [ ] Read player input → run turn → print prose
- [ ] Command to dump current world state for inspection
- [ ] Command to show the last extraction result and any conflicts
- [ ] Save on exit / autosave per turn

### 9. Validation

- [ ] `dotnet build` clean
- [ ] Manual play session — target ~50 turns
- [ ] Review: did state stay coherent? How often did extraction get it wrong?
- [ ] Record findings in `docs/CHALLENGES.md`
- [ ] Devlog entry before commit (per CLAUDE.md)

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

- Solution builds clean
- A world can be created, played for multiple turns, saved, reloaded, and continued
- Character state visibly updates as a result of narration
- Extraction results and conflicts are inspectable
- All above tasks checked off, devlog written, `TODO_FUTURE_WORK.md` updated
