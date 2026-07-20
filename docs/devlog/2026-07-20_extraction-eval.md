# Devlog — the extraction eval, and three bugs it found

**Date:** 2026-07-20
**Scope:** eval harness, reasoning-field fallback, cross-namespace id validation

---

## Why this exists

A session was spent comparing prompt variants by running each once and reading the
difference. Then the *same* configuration, re-run, produced results just as different. Every
conclusion drawn that day was noise, confidently narrated.

`--eval` scores extraction against fixed scenarios, N runs each. Hand-written narration, no
narrator call: that removes the narrator as a variance source, halves the cost per run, and
makes models comparable.

Scored as **required** and **forbidden** rather than exact expected deltas — a model choosing
"wary" over "guarded" is not wrong, and exact matching would punish it.

## Three bugs, all ours

**1. Forbidden was scored after validation.** A model emitted `location_introduced` for a
location already in canon, the validator rejected it, and the scoreboard printed
`forbidden 0`. That reads as "the model behaved" when it means "the validator saved us" — and
it is exactly why the eval looked clean while live play was full of re-introductions.
Required is still scored post-validation (a rejected delta never reaches canon, so crediting
it would be wrong); forbidden is now scored on raw output. The two questions are different
and deserve different measurement points.

**2. The movement rule accepted only `player_moved`.** The player is an ordinary character,
so `character_moved` with their id says the same thing, and one model used it. A correct
answer scored as a miss.

**3. The client ignored the `reasoning` field.** MiniMax M3 via Parasail returned
`content: null` with perfectly formed delta JSON in `reasoning` — on 20 of 21 calls. We
recorded those as empty responses and scored the model near zero on an eval it was passing.

That third one is the third time this integration has punished an assumption about response
*shape* rather than content: property order, now response field, earlier silent parameter
drops. The schema pins which fields exist and their types. It guarantees nothing else, and
OpenRouter's routing means everything else can change between two identical calls.

## What the eval found

Six models, then three finalists at n=7 over eight scenarios.

**Overall scores are indistinguishable** — 68% / 67% / 64% for minimax-m3, deepseek-v3.2,
qwen3.7-plus. Ranking on that number would be meaningless. The per-scenario profiles are not:

| scenario | qwen3.7-plus | deepseek-v3.2 | minimax-m3 |
|---|---|---|---|
| revelation | 21/21 | 16/21 | 21/21 |
| hostility (incl. standing) | 14/14 | 8/14 | 9/14 |
| movement | 0/7 | 4/6 | 3/7 |
| new-character | 1/7 | 7/7 | 5/7 |
| forbidden / rejects | 0.00 / 0.00 | 0.09 / 0.16 | 0.07 / 0.09 |

**Qwen is excellent at semantics and useless at mechanics.** Perfect on revealed facts and
on relationship standing — the delta we had never once seen in live play — and near-zero on
"the player walked outside" and "a stranger enters".

This overturns an earlier conclusion. Relationship omission was blamed on the schema or the
prompt because six models all missed it. One model does it perfectly with the *current*
schema, so it is model-dependent and the schema is fine.

**`player-claim` is settled: models will not do it.** 0/7, 0/7, 2/7 across 21 samples. The
design decision that a player's assertion becomes a fact is being dropped rather than
prompted harder for.

**`atmosphere` came back clean.** Real generated narration — three paragraphs re-describing
every known character, copied verbatim from a live session — produced zero re-introductions
across all 21 runs. So the live spam was not prose volume. It was `deepseek-v4-flash`
specifically, which had the worst forbidden rate of the six and is the model we had been
running the whole time.

**Movement is a genuine structural problem.** Every model fails it, in the same way: emit
`location_introduced` for the known destination instead of reporting the move. A known
location described richly pulls models toward the "introduce" branch.

## Where this points

Not at picking a model — at splitting the call. One extraction for changes to existing
entities (mood, status, standing, facts), where qwen is already near-perfect; one for
movement and new entities with a 2–3 branch schema, where introduce-versus-move cannot be
confused. Possibly a different model per call.

The eval now exists to verify that rather than guess at it.

## Also in this commit

- **Cross-namespace id validation.** Extraction emitted `location_introduced` with the id
  `innkeeper-hald`, which existed as a character but not as a location. The per-type check
  passed and a bogus location entered canon. Ids must be unique across characters, locations,
  and facts.
- `--selftest` gains a case for it, and `WorldSeeds` is now shared between the play harness
  and the eval so they cannot drift.
- Token usage is surfaced through `ExtractionResult` so cost is comparable per model.

## A note on method

Reasoning tokens were 87% of extraction output on the old model: about 90 tokens of JSON
against 644 spent thinking. That reframes cost — the expensive part is thinking, not model
tier, so a pricier non-reasoning model can be cheaper than a cheap reasoning one.

Account-level "balanced" provider selection, rather than per-request provider pinning, turned
out to be the right lever: it shifts routing without putting a provider name in our payload,
so nothing here stops working behind a different proxy later.
