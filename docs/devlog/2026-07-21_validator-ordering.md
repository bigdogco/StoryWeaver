# Devlog — three bugs wearing one costume

**Date:** 2026-07-21
**Scope:** movement to a new location, from 0/7 to 7/7

---

## The symptom

"Moving to a new location records nothing." One symptom. It turned out to be three unrelated
faults stacked on top of each other, and each one masked the next.

## Fault 1 — a provider returning schema-valid nonsense

Covered in the previous devlog. `AtlasCloud` scored 0/21 on `revelation` while returning
perfectly schema-conformant JSON with the wrong branch chosen. Excluded via `providerIgnore`.
Until this was removed, no measurement of anything else was stable enough to reason about.

## Fault 2 — our validator, rejecting correct output

With routing under control, the failure became deterministic: 0/7, every run. `--show-deltas`
put the raw proposal next to the validator's verdict:

```
REJECTED player_moved        -> old-mill
ok       location_introduced old-mill (Old mill)
```

**The model was right.** It emitted both deltas, correctly and completely — just in the order
*move, then introduce*. `DeltaValidator` walked the batch in emission order, rejected the move
for naming a location that "did not exist", and then accepted that very location one line
later.

The design note said validation is sequential so a batch may "introduce a character and then
move them". True, and it silently assumed the model would emit in dependency order. Across
providers, it does not.

**Fixed by sorting into dependency tiers before checking:** locations and facts first, then
characters (which may be introduced *into* a new location), then everything that references
existing entities. `OrderBy` is stable, so order within a tier is untouched, and the cascade is
unaffected — tier 2 still sees only what tiers 0 and 1 actually *accepted*, so a rejected
introduction still poisons every reference to it.

Verified offline, 15 assertions, no API spend: reversed order now passes, the working order
still passes, character-into-a-batch-declared-location passes, `fact_learned` before
`fact_established` passes, and every cascade case still rejects.

**The tell was a metric that existed for another reason.** `rejects 0.33/run` across 21 runs is
7 rejections — exactly the 7 `player-arrival` runs. A required/forbidden score alone could
never have shown this, because required is scored *after* validation and therefore sees only
what survived.

## Fault 3 — the model reaching for a familiar id

Fixing the validator stopped the rejections but the score did not move. `--show-deltas` again:

```
ok  player_moved        -> marrow-square      wrong destination
ok  location_introduced old-mill (Old mill)   correct
```

It introduces `old-mill` — it knows the mill is new and real — then moves the player to
`marrow-square`, an already-known place the narration never mentions. It substitutes a
familiar id for the new one.

Briefly *worse* than before: the wrong move used to be rejected, and now it is accepted, so a
wrong location lands in canon silently. The validator fix was still right; it just exposed
what was underneath.

One narrow prompt rule, measured on its own:

> A move must name the place the prose actually describes. When that place is new, introduce
> it and move to the id you just gave it — never redirect the move to a different place that
> happens to be already known.

Pinned to StreamLake against a control established three separate times at 0/7:
`player-arrival` **14/14**, `movement` 7/7, `atmosphere` clean, 21/21 clean runs, and
completion tokens fell 165 → 85 — the model stopped hedging between two candidate
destinations.

## The result

Full scored set, normal routing, 8 scenarios, n=7, 56 calls:

```
required 97%   forbidden 0.00   rejects 0.00   116 tokens

deflection      forbidden 0     revelation     19/21
movement        7/7             hostility      14/14
new-character   7/7             redescription  forbidden 0
atmosphere      forbidden 0     player-arrival 14/14

StreamLake  56 run(s), clean 54/56
```

Up from 86%. The only remaining miss is `revelation` failing to record the speaker as knowing
their own disclosure, 2/7 — tracked separately.

## What this cost, and why

Two prompt rewrites and a provider hunt preceded the actual diagnosis, and for most of the day
the model was the prime suspect for a bug in our own validator.

The rule I eventually kept *looks* like the one I threw away hours earlier, and it is worth
being precise that it is not. The discarded version said "introduce the place first, then the
move, in that order" — that was asking the model to sort its output around our validator's
limitation. It measured 0/7 and deserved deleting. The surviving rule says "name the place the
prose describes, do not redirect to a known one" — a genuine model behaviour, orthogonal to
ordering, which is now handled where it belongs.

Four things separated the attempt that worked from the three that did not:

1. **A control**, measured under identical conditions, repeated.
2. **Pinned to one provider**, so routing could not move underneath the experiment.
3. **One narrow rule**, so the result attributes to something specific.
4. **Our own bug fixed first**, so the model was not being blamed for our defect.

All four were missing this morning. `--show-deltas` is what broke it open — printing the raw
proposal beside the verdict, rather than a score that by construction cannot show output the
validator already discarded.

## Next

- `revelation` speaker-learns, 2/7.
- Then §9, the ~50-turn session, on a baseline that is finally attributable.
