# Devlog — domain model, delta schema probe, reasoning control

**Date:** 2026-07-19
**Scope:** TODO_BOOTSTRAP §5, plus two findings that change §7

---

## Domain model (§5)

Six types in `StoryWeaver.Core`, which still references nothing:

`Entity` (id + name), `Character`, `Location`, `Fact`, `WorldState`, `StateDelta`.

Ids are human-readable slugs rather than GUIDs. They appear in prompts, in saved JSON, and
in logs — all three read by a human while debugging. A GUID is marginally safer against
collision and much worse at every job the id actually has.

**`Fact` is deliberately not an `Entity`.** Entities have a name distinct from their
description; a fact is only its text. A `Name` field would invite the extraction model to
invent titles for statements, and those titles would then surface in prose as if they were
established terminology.

**`MoodChanged` is separate from `StatusChanged`.** Mood turns over constantly, status
rarely. Collapsing them makes every flicker of feeling look like a material change to the
world, which matters once we are deciding what to re-inject into context.

**The delta set is strictly closed** — nine kinds, discriminated on `kind`. A generic
`{ entity, property, value }` patch was considered and rejected: a cheap model will
confidently write `character.mood.current` when canon says `mood`, no schema catches it,
and it lands as a silent no-op. With a closed set, an inexpressible change becomes a
*visible* failure, which at this stage is the point.

Every delta carries optional `Evidence` — the model's own justification, ideally quoting
the prose. It mutates nothing. It exists so a wrong canon entry six turns later can be
traced to what the model thought it saw.

## The schema probe

Added `--probe-schema`. Nine variants discriminated by `kind` is an `anyOf` in JSON schema,
and `strict: true` support for `anyOf` is less universal than for a flat object. The
earlier smoke test was weak evidence — it exercised a three-field flat object.

**Result: `anyOf` works.** Five well-formed deltas, correct branch per change, clean
round-trip into the Core types via `[JsonPolymorphic]`. The flat-object fallback is not
needed. Moved to Resolved in CHALLENGES.

### The first run failed, and the probe blamed the wrong thing

Run one returned "no message content" and the probe announced that `anyOf` was
unsupported. It was not. The raw response:

```
"completion_tokens": 800, "reasoning_tokens": 800
```

The extraction budget was 800, `deepseek-v4-flash` is a reasoning model, and thinking is
billed as output against the same allowance. It thought until the budget was gone and
returned nothing — HTTP 200, no error, a bland `finish_reason: "length"`.

The reasoning trace showed it working through the schema correctly the whole time.

Worth recording *why* this was convincing: the wrong conclusion was the thing I had most
recently been worried about. An empty response has several very different causes that are
indistinguishable at the call site, so it will reliably look like whatever is top of mind.

Three fixes:

1. Extraction budget 800 → 4000, in both settings files.
2. `OpenRouterClient.DescribeEmptyContent` now names the exhausted-reasoning case
   explicitly, so the next occurrence diagnoses itself instead of being re-derived.
3. The probe no longer editorializes about `anyOf` on any failure.

### Format is solved; meaning is not

Run two produced well-formed JSON and four real semantic errors from one paragraph:

- `fact_learned` for a fact with **no `fact_established`** — a dangling reference
- `location_introduced` for `marrow-cellar`, which the prompt listed as already known
- that location's `description` field filled with an *event* rather than a description
- no `mood_changed` for Hald, despite the clearest state change in the passage

This splits the reliability question cleanly: **schema compliance is solved, semantic
correctness is not.** Three of the four are mechanically detectable, which makes §7's
validation load-bearing rather than a safety net. The omission is the hard one — nothing
detects a delta that was never emitted.

One point in favour of the closed set: because `fact_learned` cannot carry fact *text*, the
model had to invent an id, which made the dangling reference visible. A generic patch would
have absorbed it silently.

## The player is a Character (§5 amendment)

Reserved id `Character.PlayerId` = `"player"`. The extractor reached for
`characterId: "player"` unprompted, before anything told it the player was addressable —
a decent signal about what is natural to express.

**`WorldState.PlayerLocationId` is now derived** from the player character's `LocationId`
rather than standing beside it. Two copies of one fact is two facts that can disagree after
any delta touching only one of them.

Costs accepted: `RelationshipToPlayer` is meaningless on the player's own record, and
`CharactersWithPlayer()` became `NpcsWithPlayer()` because every "people around you" query
must now exclude the player explicitly or they turn up in their own scene description.

## Reasoning control

OpenRouter exposes a `reasoning` parameter (verified against current docs rather than
recalled): `effort` (max…none), `max_tokens`, `exclude`. Wired as per-role config,
currently unset everywhere so models sit at their defaults.

Two traps documented in CHALLENGES:

- **`exclude: true` saves nothing** — strips reasoning from the response, not from the bill
  or the budget. `effort` is the cost control.
- **`reasoning` is a parameter, so it can be silently dropped**, exactly like
  `response_format`. Worse in one way: the output still looks right, so nothing prompts an
  investigation — you just quietly pay for reasoning you thought you turned off. Startup
  validation now rejects reasoning without `requireParameters`, and rejects a reasoning
  budget that leaves no room under the role's `maxTokens`.

Turning extraction's effort *down* is deferred to a measured A/B rather than guessed at:
the probe's trace showed reasoning doing useful work, including correctly withholding a
`fact_learned` for Hald because he already knew the fact he was disclosing.

## Next

§6 (storage) and §7 (turn loop), with validation now the interesting part of §7 rather
than a formality. The delta schema currently lives in `DeltaSchemaProbe` and moves to
prompt assembly when §7 is built.
