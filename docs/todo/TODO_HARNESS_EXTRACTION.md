# TODO: Pull the instrumentation out of the CLI into StoryWeaver.Harness

**Status:** DONE 2026-09-04
**Created:** 2026-09-04

The CLI is becoming client one of two — a basic console UI — and Phase 2 locked that **a UI is a
thin layer, never a driver**: it collects input, calls the engine, and renders what comes back.
Nothing else belongs in it.

Today the CLI does not clear that bar. Roughly two-thirds of it is instrumentation — the
extraction eval, eight self-test suites, live API probes, the shared world fixture — sitting in
the CLI because that is where it was born, not because it belongs to a client. §2 currently
*excuses* this ("eval scaffolding... never migrates inward"), on the grounds that the CLI is
throwaway. Holding the CLI to the UI rules removes that excuse: throwaway or not, a client does
not own the benchmark harness.

So the instrumentation moves to its own peer project, and the CLI shrinks to the play UI it
claims to be.

---

## Why a project, and why this shape

- **The CLI is a client, so it cannot own instrumentation.** If the Avalonia window ever wants
  to run an eval — and it will (see below) — it must not have to depend on the CLI to do it. A
  client is not a library the next client reaches through.
- **One home for all of it, not a narrow `Eval` project.** If eval leaves because the CLI must
  be thin, the self-tests leave for the identical reason. Splitting them across "Eval project"
  and "still in the CLI" would be incoherent. One project — the harness — takes eval, self-tests,
  probes, and the fixture they share. This is also what dissolves the `WorldSeeds` question: the
  seed sits with the tests and the eval that both use it, no cross-reference, no second copy to
  drift.

## Decisions

| question | answer |
|---|---|
| Name? | **`StoryWeaver.Harness`.** `Test`/`Tests` reads as an xUnit/MSTest project — these are hand-rolled `--selftest` suites, and the name would promise `dotnet test`. `Audit` collides with the canon audit (`CanonRefresh.Check`) that lives in Core on purpose. `Harness` is accurate for eval + self-tests + diagnostics + fixtures and collides with nothing. |
| Where does it sit? | References **Core + Llm + Storage + App** — Storage because LoreSelfTest writes and reads packs, App because SessionOpenerSelfTest tests the session-opening layer. Peer to `App` (which does *not* reference back — no cycle). The CLI references it. |
| Does eval keep the score/render split? | **Yes.** Eval is not throwaway any more — it is going into the UI. So it is UI-bound output and must be Console-free: the runner returns a structured `EvalReport` and reports live progress through an `IEvalObserver`; the *client* renders. CLI renders it now, the window renders it later, each its own way. |
| Do the self-tests get the same split? | **No.** Self-tests, probes and the smoke test are dev-only pass/fail that no game UI ever shows. They keep printing directly. The rule is not "no Console in the harness" — it is "output a UI renders must be separable from rendering." Eval qualifies; the self-tests do not. A render-seam for self-test output would be completeness for its own sake. |
| Does `ResponseSelfTest` move too? | **No — documented exception.** It lives inside `Llm` because the OpenRouter wire types are `internal` and should stay that way; moving it would force them public. The Harness *invokes* it (it is `public`) so `--selftest` still runs everything through one entry point. |
| How much moves? | **Everything, one pass.** Piecemeal leaves the CLI half-thin, which is the drift the boundary work exists to prevent. |

## The eval seam, precisely

`ExtractionEval` today both scores and prints in one pass, and prints live as it goes across
minutes of real API calls. The split:

- **Harness** owns scoring. Runner returns `EvalReport` (public `ModelReport` / `ScenarioReport`
  / `RunScore`, pure data — the `Describe()`/`Shorten()` formatting comes *off* them). Live
  progress goes through `IEvalObserver` (model-started, scenario-scored, and the per-run proposed
  deltas that `--show-deltas` prints).
- **CLI** owns rendering. A `ConsoleEvalObserver` prints progress; an `EvalRenderer` prints the
  summary table, provider breakdown, and delta dumps from the returned `EvalReport`.

Live progress is preserved because the observer is the client's, not the library's.

---

## Tasks

### The project
- [x] Create `src/StoryWeaver.Harness/StoryWeaver.Harness.csproj` (refs Core, Llm, Storage)
- [x] Add it to the solution
- [x] CLI references Harness

### Move the fixture
- [x] `WorldSeeds` → Harness, made `public`; namespace `StoryWeaver.Cli` → `StoryWeaver.Harness`
- [x] Repoint `SeedWriter`, `LoreSelfTest`, `RerollSelfTest`, `EvalScenarios` to `Harness.WorldSeeds` (temp `using`; dropped when each file itself moves)

### Move the self-tests
- [x] `JsonSelfTest`, `LoreSelfTest`, `RerollSelfTest`, `AuthoringSelfTest`, `CanonRefreshSelfTest`,
  `CanonEditsSelfTest`, `SessionOpenerSelfTest`, `StorySessionSelfTest` → Harness
- [x] `SelfTests.RunAll()` aggregates every suite, including a call into Llm's `ResponseSelfTest`
- [x] `ResponseSelfTest` stays in Llm (exception recorded above)

### Move the diagnostics
- [x] `DeltaSchemaProbe` → Harness
- [x] `SeedWriter` → Harness
- [x] Extract the smoke test out of `Program.cs` → Harness (as `SmokeTest`; dead usings trimmed from Program.cs)

### The eval seam
- [x] `EvalScenario` / `DeltaRule` / `StateRule` / `EvalScenarios` → Harness, made `public`
- [x] Runner → Harness: Console-free, returns `EvalReport`, reports through `IEvalObserver`
- [x] `EvalReport` / `ModelReport` / `ScenarioReport` / `RunScore` public pure data; `Problems` now structured (`EvalProblem`), not strings
- [x] `IEvalObserver` defined in Harness
- [x] CLI: `ConsoleEvalObserver` + `EvalRenderer` (+ shared `EvalFormat`) own all the printing

### The CLI dispatcher
- [x] `Program.cs` reduced to: `--play` → `PlaySession`; `--selftest` → `Harness.SelfTests.RunAll()`;
  `--eval` → parse args, build `ConsoleEvalObserver`, call runner, render `EvalReport`;
  `--probe` / `--smoke` / `--write-seed` → Harness
- [x] Remove moved files from Cli; CLI now holds play UI + eval rendering (rendering is client-side) — no harness types defined in Cli

### Verify
- [x] `dotnet build` clean, 0 warnings
- [x] `--selftest` green, exit 0
- [x] Live `--eval` run — left to the player (their call; build + self-tests are the automated coverage)

### Docs
- [x] `docs/PROJECT.md` §2: rewrote the CLI row (no longer two-thirds instrumentation; dropped
  "never migrates inward"), add a Harness row
- [x] Devlog `docs/devlog/2026-09-04_harness-extraction.md`
- [x] `docs/CHALLENGES.md` — logged the mixed line-endings / no-`.gitattributes` issue that flipped CRLF→LF and broke rename tracking mid-move (recovered by restoring endings)
- [x] Checked off; no unfinished items (FUTURE_WORK unchanged — this task came from conversation, resolves no queued item; the window's own eval renderer is scheduled Phase 2 UI work)

---

## Out of scope, on purpose

- **Rewriting the self-tests.** They move as-is; this is not the moment to reshape them.
- **A second eval renderer.** The `EvalReport`/observer seam is built now because the window is a
  committed consumer; the window's own renderer is Phase 2 UI work, not this task.
- **Moving `ResponseSelfTest`.** It stays with the internals it tests.
