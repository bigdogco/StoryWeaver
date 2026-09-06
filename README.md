# StoryWeaver

A long-form text RPG driven by an LLM, built around one idea: **canon and narration are
separate things.**

> **Status: Phase 2 — UI design.** Bootstrap and Phase 1 (the story layer) are complete.
> The CLI is playable, packs define a story as well as a world, and a recorded 230-turn
> session demonstrated persistent canon over long play. The client/backend separation is
> complete; the next phase is graphical authoring and play. See
> [docs/PROJECT.md](docs/PROJECT.md) for the standing decisions and phase goals.

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

**It holds well enough to build on.** Bootstrap closed with a 51-turn play session: 209
deltas applied, eight rejected, and no corruption or canon/history desync. A later 230-turn
run maintained canon without an increasing rejection rate. These are recorded observations,
not a guarantee of perfect extraction: omissions, duplicate entities and crowded narration
context remain known limits. Model quality also varies by serving provider. See
[docs/PROJECT.md](docs/PROJECT.md) and [docs/CHALLENGES.md](docs/CHALLENGES.md).

## Project layout

| Project | Responsibility |
|---|---|
| `StoryWeaver.Core` | Domain model, turn loop, validation, authoring and session ownership. No UI, HTTP or storage implementation dependencies. |
| `StoryWeaver.Llm` | Provider abstraction, per-role model config, OpenRouter client. |
| `StoryWeaver.Storage` | JSON canon/history persistence and authored pack loading. |
| `StoryWeaver.App` | Composes packs, prompts, provider and persistence into a playable session; returns data rather than prompting or rendering. |
| `StoryWeaver.Harness` | Extraction eval, offline self-tests, live API probes and shared fixtures. |
| `StoryWeaver.Cli` | First UI client: collects input and renders play and eval results. |

Dependencies point inward. `Core` references no other project. `App` opens sessions;
`Core/StorySession` owns canon and guards operations that change it. Clients call those
shared operations rather than implementing gameplay or authoring policy themselves.
CLI/graphical feature parity is not required. The graphical client is not built yet.

## Stack

- **.NET 8** (LTS)
- **Graphical UI:** undecided. Blazor was selected on 2026-09-05, then reversed
  on 2026-09-06 at the player's request. Phase 2 remains graphical authoring and play
  without a terminal, but no UI framework or desktop host is selected.
- **OpenRouter** for model access, configured **per role** (narration, extraction,
  summarize, worldgen) rather than per call site, since narration and extraction want very
  different models. Summarize and worldgen are reserved roles, not implemented features.
- **JSON** storage, permanently — a save is meant to be opened and edited by the person
  playing it. Giving someone an item, fixing a character the model got wrong, adjusting a
  state: that is authorship, not cheating, and a database would hide the world from its
  owner. Diffable saves happen to also be the best debugging tool there is.

## Packs and saves

Authored content lives in `worlds/<pack-id>/`; a playthrough lives separately in
`saves/<save-id>/`. A pack can define starting canon (`seed.json`), lore, character sheets,
the player, a standing scenario, an opening passage, a manifest and narration prompt
overrides. The opening leaves the recent-turn window; the scenario remains standing context.

Saves contain editable JSON canon and a history log. The player owns canon and can correct
it through authoring commands or by editing the file and explicitly reloading it. History
records play; it is not the canon-editing surface.

Phase 2 aims to make world creation, character and lore authoring, placement, play,
save/resume and correction possible without a terminal.

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
./play.ps1 --play --pack marrow --save marrow-second # separate playthrough
./play.ps1 --selftest          # offline self-test suites, no API calls
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

Play, extraction retries, narration rerolls, `--eval`, `--smoke` and `--probe-schema`
make paid API calls. Startup configuration display and `--selftest` do not.
`--probe-schema` checks that your extraction model can emit the current delta union
under `strict: true`; `--eval` is the real test and is **worth re-running whenever you
change the extraction model, the prompt, or anything either depends on.**

Use `/retry` to extract the last turn again without rewriting its prose. `/reroll` asks
for new narration, but is refused if that turn already applied canon changes. `/place`,
`/character`, `/fact`, `/rename` and `/knows` author canon through validated deltas;
`/edit` handles direct corrections the delta set cannot express and reports structural
warnings afterwards. `/help` lists the commands.

After editing `canon.json` externally, use `/reload` (the future UI's **Update State**)
before taking another turn. Make external edits while the session is idle: an in-flight
turn can overwrite file edits before a reload can read them. Sessions refuse competing
write operations and opening a save already held by another live session.

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

- [docs/PROJECT.md](docs/PROJECT.md) — standing reference: layers, decisions and phases.
- [docs/CHALLENGES.md](docs/CHALLENGES.md) — known risks, gotchas, and what has been ruled
  out. The most useful file in the repo.
- [docs/todo/TODO_BOOTSTRAP.md](docs/todo/TODO_BOOTSTRAP.md) — historical bootstrap work
  and measurements.
- [docs/todo/TODO_FUTURE_WORK.md](docs/todo/TODO_FUTURE_WORK.md) — deferred ideas.
- [docs/design/](docs/design/) — the reasoning behind individual designs.
- [docs/devlog/](docs/devlog/) — a dated entry per commit, including the wrong turns.

## Testing

Manual, by design at this stage. `dotnet build` is the only automated check, and
`./play.ps1 --selftest` explicitly runs the Harness's offline suites without touching the API.

The real quality gate is `./play.ps1 --eval` — fixed scenarios scored as *required* and
*forbidden* rules rather than exact matches, run N times per model, with the cross-run spread
as the signal. It has repeatedly earned its keep: it killed a two-call redesign built on a
failure that turned out to be noise, caught three response-shape bugs that all presented as
"the model is bad", and exposed a validator bug of ours that had survived two prompt rewrites
aimed at the wrong thing.

## License

Not yet chosen. Intended to be open source.
