# 2026-08-06 — the well reproduces

`Location.Status` was blocked on "cannot reproduce it in a scenario". It reproduces. The block
was resting on a measurement nobody had re-run.

TODO: [`TODO_FACT_HYGIENE.md`](../todo/TODO_FACT_HYGIENE.md).

---

## The result

`deepseek/deepseek-v3.2`, pinned to DeepInfra, n=7 each:

| scenario | seed | forbidden | rejects |
|---|---|---|---|
| `place-changing` | base Marrow | **7/7** | 0.43 |
| `place-changing-late` | `Marrow_WellSignificant` | **6/7** | 0.00 |

Every run files the well's condition as facts — `well-fluid-stopped`, `well-sound-changed`,
`bronze-provokes-shaft`, `well-boarded-sound`. The same shape as the six the 50-turn session
produced, from prose that is a few hundred characters long.

## The block was a dated measurement wearing the clothes of a fact

`place-changing` reproduces on the **base seed** — the one the TODO recorded as scoring 0.00
twice, which is why the work was parked.

Why it differs is not established, and the honest answer is that it cannot be: items, `source`
and the delta-tier fix all landed after that measurement, and the provider it ran on was not
recorded. So this is a fresh result, not a refutation of an old one.

**The thing worth keeping is the shape of the mistake.** "Cannot reproduce" was written down
once and then read as a property of the world, when it was a property of one afternoon's
sweep against a pipeline that has changed several times since. It sat there for two days and
prevented the work. Measurements need dates and provider names attached, or they turn into
beliefs.

## The seed was built for the wrong reason and is worth keeping anyway

The plan was that the base seed was too thin — the well in play carried forty turns and a dozen
facts. `Marrow_WellSignificant` supplies that. Three things were actually wrong with running
that narration against base Marrow, and the first alone could have produced a null result:

1. **The player was in the tavern.** The narration is a well in the square being worked on,
   scored against a world where the player is somewhere else. A place the player is not
   standing in is background, and ignoring background is what `atmosphere` and
   `narrator-mention` establish as correct. The scenario was measuring the mention rule.
2. **The icon and the wire did not exist.** Same lesson as `Marrow_WithGrindingPlan`: a
   scenario that cannot express the right answer cannot show a wrong one.
3. **The well carried no weight.**

Since the base seed also reproduces, none of that was load-bearing for reproduction. The seed
earns its place on a different axis: base makes the model invent or move items that are not
there — rejects 0.43, which is measuring the validator — where the loaded seed gives it real
objects and rejects nothing. It is the cleaner instrument.

## A fixture must not contain its own answer

The first draft seeded the causal mechanism: *"tarnished metal touched to the well's cap
provokes an answer from the shaft below it."* That is the discovery the narration makes.

It scored **5/7 against the base seed's 7/7** — the model had been told the answer and mostly
restated it, emitting `bronze-provokes-shaft` as a near-duplicate of the seeded fact. Removing
it moved the score to 6/7, which confirms the diagnosis rather than merely being consistent
with it.

**A fixture supplies the weight behind a scene, never the scene's content.** Related to the
rule already written down twice — a fixture must not contain a plausible wrong answer a scoring
rule reads as right — but not the same one. This is a fixture containing the *right* answer,
which suppresses the failure instead of faking a pass.

## Next

`Location.Status` is unblocked, with a number to beat: **forbidden 7/7 → 0**.

The scoring rule must check the outcome — the condition ends up in status and not in facts,
whichever route the model takes there. A rule naming a specific delta is the mistake made four
times already, and it is the single most repeated error in this project.

## Verified

- `dotnet build` clean
- Both scenarios run pinned to one provider, per the rule that no routed sweep is evidence
- The seed correction re-measured rather than assumed: 5/7 → 6/7
