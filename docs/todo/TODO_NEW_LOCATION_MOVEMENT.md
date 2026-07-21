# TODO: Movement to a new location

**Status:** DONE — `player-arrival` 0/7 → 14/14, baseline 86% → 97%
**Created:** 2026-07-21

---

## What this started as, and why that was wrong

It started as "moving to a new location fails 0/7". Two prompt fixes were written and
measured; both looked like they made things worse. Then a **control run with the untouched
prompt reproduced the same 0/7**, and a rerun twenty minutes later scored 7/7. Nothing in the
repository had changed.

The variable was upstream routing. See CHALLENGES, "Providers differ in semantic quality".
Three consecutive confident conclusions were drawn from provider noise. Only the control run
caught it.

## What the provider A/B established

Full scored set, same commit, same prompt, n=7, each pinned:

| scenario | via AtlasCloud | via Baidu |
|---|---|---|
| `revelation` | **0/21** | 18/21 |
| `movement` | 1/7, forbidden 7 | **6/6** |
| `hostility` | 3/12 | **12/12** |
| `new-character` | 7/7 | 6/6 |
| `player-arrival` | 1/14 | 6/12 |
| clean runs | 28/55, forbidden 0.35/run | 43/52, forbidden **0.00**/run |

**AtlasCloud is unusable for extraction** — 0/21 on `revelation` means it never once
established a fact or recorded who learned it, the single most important thing extraction
does. Excluded via `providerIgnore`.

## The bug that is actually real

On Baidu — 12/12 and 6/6 elsewhere — `player-arrival` still misses **"the player moved to the
mill" 6/7**.

Critically, the mill **is** recorded as a location. So the model correctly introduces the new
place and then **fails to emit the move into it**. That is far narrower than the original
framing, it is provider-independent, and it is precisely what the discarded "attempt 1" prompt
targeted (it scored 9/14 against a 4/14 control, a result thrown away on the mistaken belief
it had caused a regression elsewhere).

Also seen on Baidu: `revelation` misses "Hald is recorded as knowing it" 3/7 — the
speaker-learns rule is imperfect even on a good provider. Separate, smaller, tracked here so
it is not forgotten.

## Plan

- [x] Promote `player-arrival` from `Diagnostics` into the scored set `All`.
- [x] Record the serving provider on every eval run, with a per-provider breakdown.
- [x] `--providers a,b` to sample each upstream deliberately (test instrument only).
- [x] `providerIgnore` / `providerOrder` on a role.
- [x] Set `providerIgnore: ["AtlasCloud"]` on extraction.
- [x] **Re-establish the baseline** routed-normally with that exclusion. The recorded "100%
      across three n=7 sweeps" is void — it measured a routing mix we did not choose and
      cannot reproduce.

      **New baseline, 8 scenarios, n=7, 56 calls: 86% required, forbidden 0.00, rejects 0.00,
      107 tokens.** `movement` 7/7, `hostility` 14/14, `new-character` 7/7, `revelation` 19/21,
      `player-arrival` 7/14. Providers: StreamLake 39/48 clean, Baidu 8/8. All remaining
      failures are systematic — no provider variance left in the data.
- [x] Fix the missing move. It was **two** further faults, not one:

      **Our validator, rejecting correct output.** The model emitted both deltas in the order
      *move, then introduce*; `DeltaValidator` walked the batch in emission order and rejected
      the move for naming a location declared one line later. Fixed by sorting into dependency
      tiers before checking (locations/facts → characters → everything else). `OrderBy` is
      stable, so within-tier order is preserved, and the cascade is intact because tier 2 sees
      only what earlier tiers *accepted*. Verified offline, 15 assertions.

      The tell was `rejects 0.33/run` — 7 rejections across 21 runs, exactly the 7
      `player-arrival` runs. A required/forbidden score could not have shown it, since required
      is scored after validation.

      **The model substituting a familiar id.** With rejections gone the score still did not
      move: it introduced `old-mill` correctly then moved the player to `marrow-square`, a
      known place the narration never mentions. One narrow prompt rule ("a move must name the
      place the prose describes; never redirect to a different already-known place") took
      `player-arrival` to 14/14 with `movement` and `atmosphere` unchanged, and dropped
      completion tokens 165 → 85.

      **Final: 97% required, forbidden 0.00, rejects 0.00, 54/56 clean runs.**

- [x] Revisit the `fact_learned`-for-the-speaker miss.

      **Characterised before changing anything.** Over 21 pinned runs the miss was not random
      — it tracked how many facts the model emitted:

      | facts | runs | speaker recorded | miss |
      |---|---|---|---|
      | 1 | 14 | 13 | 7% |
      | 2 | 6 | 1 | 83% |
      | 3 | 1 | 0 | 100% |

      The player was recorded 21/21. The rule was never missing; it was applied once per
      *turn* instead of once per *fact*, and only the less obvious half — the speaker — was
      dropped as the list grew.

      Extended the existing bullet rather than adding a rule. After: **1/21**. Notably the
      model now splits *more* (21/21 runs multi-fact, up from 7/21) while getting the speaker
      right — better on both axes, since one speech does reveal several independently knowable
      truths, and recording them as a blob would make a character learn the body and the
      cover-up as one atomic unit.

      Regression checked deliberately: a rule encouraging more `fact_established` is how the
      junk-fact era began. `deflection` and `atmosphere` both stayed at `forbidden 0`.
      Completion tokens 116 → 140.

---

## Verified baseline (2026-07-21)

Three independent sweeps, 8 scenarios, n=7, normal routing, AtlasCloud excluded:

```
sweep 1:  100%   forbidden 0.00   rejects 0.00   56/56 clean
sweep 2:  100%   forbidden 0.02   rejects 0.02   55/56 clean
sweep 3:  100%   forbidden 0.00   rejects 0.00   56/56 clean
```

Identical scenario by scenario: `revelation` 21/21, `movement` 7/7, `hostility` 14/14,
`new-character` 7/7, `player-arrival` 14/14, and nothing forbidden in `deflection`,
`redescription` or `atmosphere`.

**Unlike the previous "100%", this one is attributable.** Sweep 2's single blemish localises to
one run served by **Google**, which emitted a forbidden `character_introduced`; StreamLake was
53/53, Venice 1/1, SiliconFlow 1/1, Google 0/1. Flagged, **not excluded** — n=1 is exactly the
evidence threshold that produced three wrong conclusions earlier the same day. It is a
candidate for the pinned A/B that convicted AtlasCloud, and a textbook case for automated
provider calibration.

## Method rule earned the hard way

**Re-measure the baseline before attributing any change to your own edit.** A control run is
not optional when the thing being measured is served by infrastructure you do not control.
