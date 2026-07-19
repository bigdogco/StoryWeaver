# Devlog — 2026-07-19 — Project bootstrap planning

**Status:** Planning and configuration only. No code yet.

## What happened

Went from an empty repo to an agreed technical direction and a reviewed bootstrap plan.

### Design direction settled

StoryWeaver targets the **long-form RPG** end of the LLM-fiction space, not the
character-chat end (chub / janitor / SillyTavern). The architectural consequence:

> **Canon and narration are separate.** The entity store is the source of truth; prose is
> a rendering of it. The chat log is disposable — losing it costs flavour, not canon.

This is the thing the incumbents cannot do, because in those systems the chat log *is*
the state, which is why they drift over long sessions. Three player-facing goals map onto
it: large worlds (typed entity graph, lazily expanded and written back to canon),
character memory (per-entity records with per-character `knows`, so knowledge is not
global), and consistency (a two-call turn — narrate, then extract state deltas from the
prose and commit them transactionally).

### Stack decisions

| Decision | Choice |
|---|---|
| Language / runtime | C#, .NET LTS |
| UI (later) | Avalonia |
| Storage (now) | JSON behind `IWorldRepository` |
| Storage (later) | Likely hybrid — JSON canon + SQLite turn log |
| LLM access | OpenRouter |
| Model selection | Per **role**, not per call site |
| LLM client | Hand-rolled `HttpClient`, ported from AI-Lord |
| First harness | Console |
| Secrets | Gitignored local settings file |

JSON over SQLite for now is a deliberate phase-appropriate call, not a judgement that
JSON is better: git-diffable saves are the best available debugging tool for the question
this phase exists to answer. SQLite wins on transactions, FTS, and unbounded growth — the
trigger to switch is wanting search over history.

### Verification pass (OpenRouter)

- **API shape** — OpenAI-compatible. Confirmed, no surprises.
- **Structured output** — `response_format` with `type: "json_schema"`, `strict: true`.
- **The find of the session:** OpenRouter load-balances one model ID across multiple
  upstream providers, price-weighted, and **a provider that doesn't support a parameter
  silently ignores it**. Without `provider: { require_parameters: true }`, extraction
  calls intermittently return prose instead of JSON with no error signal. Logged in
  CHALLENGES.md.

### Reuse found

`D:\Hobbies\Coding\AI-Lord\src\AILord\LLM\` already has a working OpenRouter client with
a shared retry budget, attribution headers, prompt/response logging, and a
content-validation-and-repair loop. That repair loop is the second line of defence for
unreliable structured output on cheap models. Porting rather than rewriting.

## Files added

- `docs/todo/TODO_BOOTSTRAP.md` — the bootstrap plan (approved, revised twice)
- `docs/CHALLENGES.md` — seeded with known risks
- `docs/todo/TODO_FUTURE_WORK.md` — seeded with deferred items
- `.gitignore` — .NET template plus `*.local.json`, `saves/`
- `settings.example.json` — committed template
- `settings.local.json` — gitignored, holds the real key (verified not tracked)

## Open

- Confirm the chosen extraction model supports `json_schema` through OpenRouter. Pavel is
  checking. If not, `require_parameters` and `responseFormat` flip together and the
  validator+repair loop becomes primary rather than backstop.
- Model IDs and API key to be filled into `settings.local.json`.

## Next

Solution scaffolding: `StoryWeaver.sln` plus Core / Llm / Storage / Console projects,
references pointing inward, `dotnet build` green on an empty scaffold.
