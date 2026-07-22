# StoryWeaver

A long-form text RPG driven by an LLM, built around one idea: **canon and narration are
separate things.**

> **Status: late bootstrap, playable.** The turn loop runs, worlds persist to disk and
> resume, and the narrator remembers recent turns. What remains is a long play session to
> see how it holds up over ~50 turns. See
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
correct structured state deltas. The bootstrap phase existed to test that before anything
was built on top.

**It holds.** `deepseek-v3.2` scores 100% on the eval's eight scenarios across three
independent sweeps, with nothing forbidden and nothing rejected, at roughly a hundredth of a
cent per call. Caveats worth keeping in view: that is eight hand-written scenarios on one
small world, and it required excluding one upstream provider that returned schema-valid
nonsense. See [docs/CHALLENGES.md](docs/CHALLENGES.md).

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

Then play:

```powershell
./play.ps1                     # play; creates saves/marrow, resumes it next time
./play.ps1 --selftest          # offline serialization checks, no API calls
./play.ps1 --eval --runs 7     # score extraction against the fixed scenarios
```

`play.ps1` is a thin wrapper around `dotnet run`. It forces the working directory to the
repo root, which matters because `saves/` is resolved relative to it — launching from
elsewhere would silently create a second, empty world instead of resuming yours. Any
arguments are passed straight through, so the raw form works too:

```bash
dotnet build
dotnet run --project src/StoryWeaver.Cli                   # validate config, print roles
dotnet run --project src/StoryWeaver.Cli -- --play         # play
dotnet run --project src/StoryWeaver.Cli -- --smoke        # 2 live calls
dotnet run --project src/StoryWeaver.Cli -- --probe-schema # 1 live call
```

In a session, write `*actions between asterisks*` and speech outside them. `/state` dumps
the world as the extractor sees it, `/prose` as the narrator does, `/raw` shows the last
extraction response, `/quit` ends it. Applied, no-op, and **rejected** deltas print after
every turn — a silently dropped delta is the failure that would otherwise take fifty turns
to notice.

Everything except `--selftest` spends real credits, which is why they are flags.
`--probe-schema` checks that your extraction model can emit the nine-branch delta union
under `strict: true`; `--eval` is the real test and is **worth re-running whenever you
change the extraction model, the prompt, or anything either depends on.**

## A warning worth reading before you configure anything

OpenRouter load-balances a single model ID across several upstream providers, weighted by
price. **A provider that does not support a parameter silently ignores it** — no error, no
warning. An extraction call specifying `response_format` can come back as prose instead of
JSON at a rate that tracks provider pricing, with nothing in the response indicating why.

Set `"requireParameters": true` on any role that depends on `response_format` or
`reasoning`. Startup validation enforces this coupling so the mistake surfaces immediately
rather than as intermittent bad output weeks later.

**And that only solves half of it.** `requireParameters` filters providers that *cannot*
honour a parameter. It does nothing about one that honours the schema and reasons badly
inside it. We measured a provider returning perfectly schema-valid JSON that scored **0/21**
on the scenario testing whether a disclosed secret is recorded — a building emitted as a
character, the wrong branch chosen every time. No request parameter can catch that; only
measurement can.

So a quality number for a hosted model means nothing without recording what served it. The
eval prints a per-provider breakdown for exactly this reason, `--providers a,b` samples each
upstream deliberately, and `providerIgnore` excludes the bad ones while keeping the rest.
Expect to need it, and expect the right list to differ per model and to go stale.

Those and several other sharp edges — including reasoning tokens silently eating the
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

Manual, by design at this stage. `dotnet build` is the only automated check, and
`./play.ps1 --selftest` runs the offline serialization checks without touching the API.

The real quality gate is `./play.ps1 --eval` — fixed scenarios scored as *required* and
*forbidden* rules rather than exact matches, run N times per model, with the cross-run spread
as the signal. It has repeatedly earned its keep: it killed a two-call redesign built on a
failure that turned out to be noise, caught three response-shape bugs that all presented as
"the model is bad", and exposed a validator bug of ours that had survived two prompt rewrites
aimed at the wrong thing.

## License

Not yet chosen. Intended to be open source.
