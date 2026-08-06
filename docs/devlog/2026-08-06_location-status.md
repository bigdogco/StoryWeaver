# 2026-08-06 — places get a status

The third `Status`. Characters had one, items had one, places did not — and the fact store was
absorbing the difference.

TODO: [`TODO_FACT_HYGIENE.md`](../todo/TODO_FACT_HYGIENE.md).
Reproduction: [`2026-08-06_place-changing-reproduces.md`](2026-08-06_place-changing-reproduces.md).

---

## The result

`deepseek/deepseek-v3.2`, pinned to DeepInfra, n=7 each. Before and after are the same
scenarios, the same prose, the same provider — only the schema changed.

| scenario | forbidden before | forbidden after | required after |
|---|---|---|---|
| `place-changing` | 7/7 | **0** | 7/7 |
| `place-changing-late` | 6/7 | **0** | 7/7 |

14/14 runs clean. Mean completion tokens fell from 465 to 236, because a status is one delta
where the misfiling was three or four facts with a `fact_learned` behind each.

What it now produces, every run:

```
location_status_changed  marrow-square = The black seepage from the well's cracks has
                                         stopped. The sound from the shaft is a churning,
                                         and the boards are bowing outward.
```

Against what it produced before, every run:

```
fact_established  well-fluid-stopped: When the bronze wire touched the boards over the
                                     well, the black fluid weeping from the cracks stopped.
fact_learned      player <- well-fluid-stopped
fact_established  well-sound-changed: The sound from the well shaft changed ...
fact_learned      player <- well-sound-changed
```

Six facts of that shape survive in a real 50-turn save. They are permanent records of things
that were true for one turn.

## What was built

Nine touch points, and listing them is the point — a delta kind is not one change:

| | |
|---|---|
| `Location.Status` | the field, defaulting to empty |
| `LocationStatusChanged` | the delta |
| `StateDeltaConverter` | the `kind` → type map |
| `DeltaSchema` | the branch the model actually sees |
| `DeltaValidator` | identity, no-op, and existence |
| `DeltaApplier` | applying it |
| `ExtractionEval` / `PlaySession` | two describe lines |
| `ContextAssembler` | rendering it back |
| `LlmStateExtractor` | the prompt rule |

**Defaults to empty, not to "normal".** Characters default to "normal" because a character
always has a condition. Most locations never have one, and `status: normal` under every room
would spend context to say nothing. It renders as `Right now: …` and only when set.

**Rendered back deliberately.** A status the narrator never reads would be a field that looks
like it works while quietly discarding what extraction learned — the same silent-drop shape
refused everywhere else here.

## The scoring rule targets the world, not the route

```csharp
new("the square's condition is recorded as its status",
    w => !string.IsNullOrWhiteSpace(w.FindLocation("marrow-square")?.Status)),
```

A `StateRule`, checking the world after the batch is applied. `location_status_changed` is the
obvious route; a place introduced carrying a status would be just as correct, and so would two
deltas that end in the same place.

**This is the mistake this project has made four times** — two-stage-entry movement, the Sera
fact rule, the ground-chunks rule, the speaker-learns rule — every one of them by writing down
the fix instead of the outcome. Writing it down has not been enough to stop it. Reading the
deltas behind a failing rule is what catches it, which is why `--show-deltas` was used on every
run here.

## The prompt rule states the test, not the taxonomy

> A fact is something that stays true and that a character could be told later. "The sound from
> the shaft became a churning" is the well's condition this turn and will be wrong the next; it
> is not knowledge anyone can carry.

The distinction that matters is not place-versus-thing, it is durable-versus-momentary. A rule
listing which delta to use for places would not transfer to the next case; this one might.

## Verified

- `dotnet build` clean, 0 warnings
- `--selftest` — all four suites pass
- Both scenarios, pinned, n=7: forbidden 0.00, required 100%
- Full scored set re-run pinned — **inconclusive, and honestly so. See below.**

## The regression check could not be completed, and the reason is worth recording

The full scored set came back dirty: `movement` 0/2, `new-character` 1/5, `hostility` 5/10,
with four calls timing out at 45s. On the face of it, a regression from this change.

**It is not.** Three runs, in order:

| build | provider | movement | new-character | hostility | timeouts |
|---|---|---|---|---|---|
| with `Location.Status` | DeepInfra pinned | 0/3 | 2/5 | 5/10 | 2 |
| **HEAD, in a worktree** | DeepInfra pinned | 0/1 | 3/5 | 5/10 | 4 |
| with `Location.Status` | routed | 0/2 | 4/5 | 5/10 | 3 |

The **pre-change build fails the same way**, which is the comparison that settles it. The spread
across the three columns is noise on top of a provider that is timing out on roughly a third of
calls; routed traffic lands on DeepInfra too, so there was no healthy upstream to escape to.

`forbidden` is **0.00 in every run of every build**. Whatever is wrong, this change is not
adding wrong output — the failures are all *missing* required deltas, which is what a timed-out
call looks like.

**What this does not license.** "No regression" is not established, only "no regression
attributable to this change under conditions bad enough to hide a small one". The scored set
needs re-running when the provider is healthy, and the recorded 50/50 baseline needs
re-establishing along with it — `hostility` missed its standing rule 5/5 on *both* builds,
which is consistent with `relationship_changed` never having fired in 102 turns of play and is
therefore probably long-standing rather than new.

**A fifth sighting of the provider hazard, in a new costume.** The previous four were routing
between upstreams with different quality. This one is a single pinned upstream degrading in
*latency*, which corrupts a regression check by turning healthy runs into missing-delta
failures. The rule survives contact and gains a clause: pin the provider, and check the
error count before reading the score.

## Still open

- **Deduplication is now visible from a second direction.** With the well's condition in status,
  a status overwritten every turn is correct. But `seepage-worsening` and a status saying the
  same thing can still coexist, and nothing detects it
- **Nobody has played this.** The narrator has never seen a `Right now:` line. Whether it reads
  as useful or as clutter is a question the eval cannot answer
