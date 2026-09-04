# 2026-09-04 — The instrumentation leaves the CLI

Third structural move of the day, and the largest: ~7,400 lines out of the CLI into a new
`StoryWeaver.Harness`. Nothing about how the game plays changed. What changed is what the CLI
*is*.

## The one sentence that forced it

The CLI is becoming client one of two — a basic console UI — and Phase 2 locked that a UI is a
thin layer that collects input, calls the engine, and renders what comes back. Hold the CLI to
that and a fact that used to be fine stops being fine: two-thirds of the CLI was instrumentation.
§2 had *excused* that ("eval scaffolding... never migrates inward") on the grounds that the CLI
is throwaway. But throwaway is not the same as thin. A client does not own the benchmark that
grades the engine, however disposable the client is.

So the excuse went, and the instrumentation with it.

## Why one project and not two

The first sketch was a narrow `StoryWeaver.Eval`. The player pushed on it correctly: if eval
leaves because the CLI must be thin, the self-tests leave for the identical reason. Splitting
them — eval in a project, self-tests still in the CLI — would leave the CLI half-thin, which is
the exact drift the boundary work exists to kill. One project takes all of it: the eval, eight
self-test suites, the live API probes, and the world fixture they share.

That also dissolved a question that had eaten two rounds of discussion — where `WorldSeeds`
should live so both the eval and the self-tests can reach one copy without a second that drifts.
Once the tests and the eval are in the same project, the seed just sits with them. The question
was an artefact of the narrow framing.

## The name

`Harness`, over the player's own `Test`/`Audit`. `Test`/`Tests` reads as an xUnit project and
would promise `dotnet test` these hand-rolled suites do not honour. `Audit` collides with the
canon audit — `CanonRefresh.Check` — that lives in Core on purpose. `Harness` is what it is.

## The one real refactor: the eval seam

Everything else was `git mv` plus a namespace line. The eval was not, because **eval is not
throwaway any more — it is going into the UI.** The player was clear: the window will show eval
results. That makes eval output UI-bound, and UI-bound output cannot be printed from a library,
or every client inherits the console's idea of how it reads.

So the eval was split down the middle:

- **The Harness scores.** `ExtractionEval.RunAsync` returns an `EvalReport` — pure data. The
  numbers a client would otherwise recompute (required rate, forbidden-per-run, the per-provider
  split) are computed properties on the report, because they are measurements, not presentation.
  `Problems` stopped being pre-formatted `"MISSED: ..."` strings and became structured
  `EvalProblem`s with a kind and counts, so a window can draw a missed rule and a failed call
  differently.
- **The client draws.** Live progress goes through an `IEvalObserver` the client supplies;
  `ConsoleEvalObserver` prints the running commentary, `EvalRenderer` prints the summary, and
  `EvalFormat` holds the wording both share. The console output is byte-for-byte what it was.

Live progress survives the split because the observer belongs to the client, not the library —
the runner reports events, the console decides they become lines.

The counter-case, recorded so it is not relitigated: the self-tests did **not** get this
treatment. They print. Nothing in a game UI renders a self-test, so a render-seam for their
output would be completeness for its own sake. The rule is not "no Console in the Harness" — it
is "output a UI renders must be separable from rendering." Eval qualifies; self-tests do not.

## Two things found by doing it rather than assuming

- **`SessionOpenerSelfTest` tests the App layer**, so the Harness references App, not just
  Core+Llm+Storage. No cycle — App does not reference back. Plan said three references; the build
  said four, and the build was right.
- **`ResponseSelfTest` cannot move.** Its own doc-comment already said why: it lives in `Llm`
  because the OpenRouter wire types are `internal` and should stay that way, and moving it would
  force them public. So it is the one documented exception — the Harness *invokes* it (it is
  public) through `SelfTests.RunAll()`, so `--selftest` still runs everything through one call,
  but the suite stays with the internals it checks.

## What the CLI is now

1,813 lines, down from 6,061. The turn loop, `/edit`, the authoring prompts, the dispatcher, and
the eval renderer — and the renderer is client-side by right, because the Harness scores and the
CLI draws. No harness type is defined in the CLI any more. It is, at last, a client.

## Measurements

`dotnet build` clean, 0 warnings. `--selftest` 132 assertions, exit 0 — the same 132 as before
the move, run now through the Harness's `SelfTests.RunAll()`. The live `--eval` is the player's
to run; build and self-tests are the automated coverage, and the eval's scoring code is unchanged
— only its output boundary moved.
