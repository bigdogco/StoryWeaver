# Devlog — the split, rejected; the extraction model, chosen

**Date:** 2026-07-20
**Scope:** two-call extraction experiment, model selection, the bootstrap question answered

---

## The split, and why it lost

The n=7 sweep showed every model failing `movement` — emitting `location_introduced` for a
destination already in canon instead of reporting the move. Hypothesis: too many branches
competing in one decision. Fix: two calls, one for changes to existing entities, one for
movement and new entities, each with a smaller schema.

Measured against the single call, same models, same scenarios:

| model | single | split |
|---|---|---|
| deepseek-v3.2 | 98% | 69% |
| qwen3.7-plus | 84% | 51% |
| minimax-m3 | 73% | 57% |

**Worse across the board, and the detail said why.** For v3.2, movement and new-character
were already 7/7 in the single call — the split fixed nothing there — while `hostility`
collapsed from 14/14 to 5/14. Separating the concerns cost the model the whole-scene view it
had been using to judge emotional and relational change.

Deleted `SplitStateExtractor` and `SplitSchemas`. A clean, measured negative result; git
keeps the code if the idea ever earns a second look.

## The premise it was built on had already evaporated

The split existed to fix a movement failure seen at n=7. In the very next single-call run,
the same model scored movement **7/7** — no re-introductions at all. The failure did not
reproduce.

So it was variance, at the sample size I had called sufficient. I designed a fix for a
problem that was partly noise. The eval caught that in one run instead of letting it ship —
which is precisely the point of having built it, and the second time this week a confident
conclusion has failed to survive a repeat.

## The model, chosen

Single-call `deepseek-v3.2`, three independent n=7 runs:

```
run 1:  100%   forbidden 0.00   rejects 0.00   87 tokens
run 2:  100%   forbidden 0.00   rejects 0.00   96 tokens
run 3:  100%   forbidden 0.00   rejects 0.00   95 tokens
```

Identical scenario by scenario: revelation 21/21, movement 7/7, hostility 14/14 (standing
every time), new-character 7/7, zero forbidden or rejected across 147 calls.

This is stable, not lucky. `deepseek-v3.2` is now the extraction model. It beat
`deepseek-v4-flash` (the worst of the six, and what we had been running the whole time),
`v4-pro`, `minimax-m3`, `gemma-4-31b`, and `qwen3.7-plus`. About 90 completion tokens, no
reasoning overhead, roughly $0.0001 a call.

Changed in `settings.local.json` only; the example file uses a placeholder.

## What 100% does and does not mean

**Does:** the failure modes we actually hit in play — junk facts, re-introductions, missed
movement, missed relationship changes — are gone on a model that costs almost nothing, run
at temperature 0 with stable results across three sweeps.

**Does not:** the eval is seven scenarios on one small world. 100% means the *known* failure
modes are covered, not that extraction is perfect. New scenarios will find new gaps, and now
there is a place to add them. Part of the jump from 67% also came from retiring
`player-claim`, a scenario the design was wrong about — that improved the measurement, not
the model. Movement 7/7 and hostility 14/14 are genuine fixes regardless.

## The bootstrap question

> Can a cheap model reliably read narrative prose and emit correct structured state deltas?

**Yes.** That was the one question the bootstrap phase existed to answer, and after a lot of
wrong turns it has a measured, repeatable answer. The canon-vs-narration architecture stands.

## Next

Section 6 — JSON storage with atomic writes — now that the domain model has been through
real play and the extraction question is settled. The save format is safe to freeze.
