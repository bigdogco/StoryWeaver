# TODO: Movement to a new location

**Status:** IN PROGRESS — premise revised after the provider investigation
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
- [ ] Set `providerIgnore: ["AtlasCloud"]` on extraction.
- [ ] **Re-establish the baseline** routed-normally with that exclusion. The recorded "100%
      across three n=7 sweeps" is void — it measured a routing mix we did not choose and
      cannot reproduce.
- [ ] Fix the missing move. Iterate **pinned to Baidu** so a prompt change is measurable
      without routing noise, then confirm on normal routing.
- [ ] Revisit the `fact_learned`-for-the-speaker miss (3/7 on Baidu) separately.

## Method rule earned the hard way

**Re-measure the baseline before attributing any change to your own edit.** A control run is
not optional when the thing being measured is served by infrastructure you do not control.
