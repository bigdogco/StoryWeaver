# StoryWeaver

A long-form text RPG driven by an LLM, built around one idea: **canon and narration are
separate things.**

> **Status: early bootstrap.** There is no playable turn yet. The LLM client works against
> a real API and the domain model exists; the turn loop and storage do not. See
> [docs/todo/TODO_BOOTSTRAP.md](docs/todo/TODO_BOOTSTRAP.md) for exactly where things
> stand.

## The idea

Most LLM roleplay tools treat the chat log as the state of the world. Ask them what a
character knows and the honest answer is "whatever is still in the context window." That is
why they drift: characters forget, contradict themselves, and rediscover facts they
established an hour ago.

StoryWeaver keeps a structured entity store as the source of truth. Prose is a *rendering*
of that store, never the store itself. Each turn:

1. **Narrate** — assemble context from world state, ask a capable model for prose.
2. **Extract** — hand that prose to a cheap model, get back structured state deltas.
3. **Validate** — check the proposed deltas against canon.
4. **Commit** — apply them transactionally.

If a character was not in the room, they do not know what happened there. Knowledge is
per-character (`Character.Knows` holds fact ids), not a global blob, because that is the
difference between an NPC who feels simulated and one who feels narrated.

**The bet this rests on:** that a cheap model can reliably read creative prose and emit
correct structured state deltas. That is unproven, and the bootstrap phase exists to test
it before anything is built on top. Early results are split — schema compliance is solved,
semantic accuracy is not. See [docs/CHALLENGES.md](docs/CHALLENGES.md).

## Project layout

| Project | Responsibility |
|---|---|
| `StoryWeaver.Core` | Domain model and turn loop. No UI, no HTTP, no storage dependencies. |
| `StoryWeaver.Llm` | Provider abstraction, per-role model config, OpenRouter client. |
| `StoryWeaver.Storage` | JSON implementation of Core's repository interface. |
| `StoryWeaver.Cli` | Throwaway console harness. |

Dependencies point inward. `Core` references nothing.

## Stack

- **.NET 8** (LTS)
- **Avalonia** for the eventual UI — decided, not yet started
- **OpenRouter** for model access, configured **per role** (narration, extraction,
  summarize, worldgen) rather than per call site, since narration and extraction want very
  different models
- **JSON** storage for now — git-diffable saves are the best debugging tool at this stage.
  Likely moves to a hybrid (JSON canon + SQLite turn log) once full-text search over
  history is wanted.

## Getting started

Requires the .NET 8 SDK and an [OpenRouter](https://openrouter.ai) API key.

```bash
cp settings.example.json settings.local.json
```

Fill in your API key and a model id per role. `settings.local.json` is gitignored; the key
can also come from `STORYWEAVER_API_KEY` or `OPENROUTER_API_KEY`, which take precedence.

```bash
dotnet build
dotnet run --project src/StoryWeaver.Cli                  # validate config, print roles
dotnet run --project src/StoryWeaver.Cli -- --smoke       # 2 live calls
dotnet run --project src/StoryWeaver.Cli -- --probe-schema # 1 live call
```

The two flags spend real credits, which is why they are flags. `--probe-schema` checks
that your extraction model can emit the nine-branch delta union under
`strict: true` — **worth re-running whenever you change the extraction model**, since
schema support varies by model and even by which upstream provider OpenRouter routes you
to.

## A warning worth reading before you configure anything

OpenRouter load-balances a single model ID across several upstream providers, weighted by
price. **A provider that does not support a parameter silently ignores it** — no error, no
warning. An extraction call specifying `response_format` can come back as prose instead of
JSON at a rate that tracks provider pricing, with nothing in the response indicating why.

Set `"requireParameters": true` on any role that depends on `response_format` or
`reasoning`. Startup validation enforces this coupling so the mistake surfaces immediately
rather than as intermittent bad output weeks later.

That entry and several other sharp edges — including reasoning tokens silently eating the
`maxTokens` budget and returning empty content with no error — are documented in
[docs/CHALLENGES.md](docs/CHALLENGES.md).

## Documentation

- [docs/CHALLENGES.md](docs/CHALLENGES.md) — known risks, gotchas, and what has been ruled
  out. The most useful file in the repo.
- [docs/todo/TODO_BOOTSTRAP.md](docs/todo/TODO_BOOTSTRAP.md) — the current plan and its
  decisions.
- [docs/todo/TODO_FUTURE_WORK.md](docs/todo/TODO_FUTURE_WORK.md) — deferred ideas.
- [docs/devlog/](docs/devlog/) — a dated entry per commit, including the wrong turns.

## Testing

Manual, by design at this stage. `dotnet build` is the only automated check; the two live
flags above are the manual ones.

## License

Not yet chosen. Intended to be open source.
