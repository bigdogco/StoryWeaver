# Extraction prompt consolidation — attempted, measured, reverted — 2026-09-11

A negative result. `prompts/extraction.md` is unchanged from commit `0461529`; what this
entry records is the two attempts, what they measured, and why the file was put back.

Eval runs were raised from three to five for all of this, which was itself worth doing.

## Baselines at five runs, prompt v8 `c157d908`

| suite | required | clean | forbidden/run |
|---|---|---|---|
| regression | — | 48/49 (one 45s timeout, parallel-run contention) | 0.00 |
| discovery | 98% | 117/130 | 0.04 |

Five runs immediately separated two things three runs had mixed together.
`discovery-lore-topic` misses "Topic learned" 5/5 — a total failure, not the variance
between 6/9 and 8/9 it looked like at three runs, because the scenario's required score
keeps earning partial credit for the surrounding deltas while the rule itself never
passes. And `hostility` is a genuine marginal case at roughly one run in five rather than
the coin flip it appeared to be. Both corrections are in TODO_FUTURE_WORK.

## What was tried

**v9 `caecda87`** restructured. The `Critical rules:` list, which covers ids, movement,
items, facts and moods, had been sitting inside `## What counts as a fact`; it became
`## Canon rules`. The discovery material was a stray H1 appended to the file; it became
`## Player knowledge` with `## Final checks` holding only genuinely cross-cutting items.
Three copies of the movement rule became one — the original at lines 55-64 had survived
v5 untouched, so the v6 opening paragraph and the v8 checklist bullet were both restating
a rule that was already there. The disclaimer "nothing in this section restricts reporting
the player's own movement", written to patch around movement living in the wrong section,
was deleted along with the misplacement it apologised for. Four statements of the
location-identity rule, two of the evidence rule and two of the speaker-detail rule each
became one. Words fell 7.9%, characters 9.2%.

**v10 `24965111`** restored the concrete hotel example the compression had dropped, and
added a rule that had never been written down: witnessing someone identify themselves is
Observed, while Reported is for a claim about a third party.

## What happened

| discovery, five runs | clean | rules missing in 3+ of 5 |
|---|---|---|
| v8 `c157d908` | 117/130 | 1 — topic learned |
| v9 `caecda87` | 117/130 | 3 — hotel identity, name revealed, old alias retained |
| v10 `24965111` | 116/130 | 3 — ledger identity, reported purpose, public alias |

Every targeted edit worked. v9 fixed lore-topic, 5/5 missing to 1/5. v10 fixed hotel
identity, 5/5 missing to 20/20 required, and identification, 9/15 to 15/15, and finished
lore-topic at 15/15. And each one cost about as much somewhere else: v9 lost hotel
identity and identification, v10 lost the Reported path for objects and aliases, the
latter attributable to the same sentence that fixed self-identification.

Three consecutive iterations fixed their target and broke something else while the
aggregate stayed within one run. That reads as a roughly fixed error budget at this prompt
size, where editing redistributes failures rather than reducing them, and continuing to
v11 would most likely have traded the alias failures for two others. Reverted to v8 as the
version with the fewest hard failures, which was not the expected answer.

The regression suite was healthy throughout: v10 scored 48/50, with `player-arrival` and
`two-stage-entry` at 10/10 under every version including the ones with the duplicated
opening movement text removed.

## What survives

Deduplicate toward the concrete example, never toward the principle. The four statements
of the location-identity rule were not four copies of one sentence; one of them carried
the hotel example, and that example was the whole load. Its removal cost 5/5 runs and its
restoration recovered them immediately.

A green scenario is not evidence that the rule behind it is written down.
`discovery-identification` passed 15/15 under v8 with nothing in the prompt saying that a
person naming themselves is Observed rather than Reported. The fixtures encoded it, the
prompt never did, and a restructure that disturbed the surrounding wording exposed it.

The aggregate clean count is the wrong number to steer by. All three versions sit within
one run of each other on it while differing sharply in which rules fail. A headline that
does not move is a reason to read the per-rule misses, not a pass.

The single portable win, not applied here: the lore wording. Stating the rule once, as
"being told the substance of a listed lore topic is fact_learned against that topic's id;
a new fact stating the topic exists records nothing", took the scenario from 5/5 missing
to 15/15. The two competing statements in v8 are the cause of that defect. Porting that
one wording onto v8 and verifying at five runs is now the highest-value single edit
available, and is filed as such.

## Validation

No source changed. `prompts/extraction.md` is byte-identical to `0461529`. Eval artefacts
for v8 baselines, v9 and v10 on both suites are retained with raw responses, provider
labels and prompt fingerprints.
