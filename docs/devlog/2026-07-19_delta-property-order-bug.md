# Devlog — the delta property order bug

**Date:** 2026-07-19
**Scope:** extraction deserialization, offline self-test

---

## Symptom

A scripted four-turn session failed extraction on three of four turns:

```
Deserialization of types without a parameterless constructor, a singular
parameterized constructor, or a parameterized constructor annotated with
'JsonConstructorAttribute' is not supported. Type 'StoryWeaver.Core.StateDelta'.
```

The one turn that "succeeded" returned an empty delta list. Every turn that produced an
actual delta failed.

## Cause

The model emitted properties in alphabetical order:

```json
{"characterId":"innkeeper-hald","description":"...","evidence":"...","kind":"character_introduced"}
```

`kind` last. System.Text.Json's built-in polymorphic deserialization requires the type
discriminator to be the **first** property in the object; it cannot dispatch when the
discriminator arrives later, so it falls back to the abstract base and throws.

**The schema was honoured perfectly.** Property order is not something JSON schema
constrains, and both orderings are valid JSON. The dependency on ordering was ours.

## This is the routing hazard wearing a different costume

The earlier `--probe-schema` run parsed fine. Same model id, same schema, different
outcome — because a different upstream provider served it. The failing response came from
DigitalOcean; the working one did not.

Generalised: **anything that depends on the shape of a response beyond what the schema
guarantees is a latent version of this bug.** The schema pins which fields exist and their
types. It says nothing about order, whitespace, or key casing, and OpenRouter's routing
means those can change between two otherwise identical calls.

## Fix

`StateDeltaConverter`, a hand-written `JsonConverter<StateDelta>` that parses the object,
finds `kind` wherever it sits (case-insensitively), and dispatches to the concrete type.
Replaces `[JsonPolymorphic]` entirely.

Two details worth keeping:

- The converter must be removed from the options used to deserialize the concrete type, or
  it re-enters itself and recurses forever. That inner options object is cached per source
  options in a `ConditionalWeakTable` — System.Text.Json caches type metadata against an
  options instance, so building a fresh one per delta would throw that away on every call.
- Unknown and missing `kind` **throw** rather than returning null. A null would be
  indistinguishable from the model reporting no changes, which is the silent failure this
  whole episode was made of.

Also added `StoryJson`, a single shared options object, because the extractor and the probe
had each built their own — exactly the drift that lets a converter get forgotten at one call
site.

## `--selftest`

Offline checks on delta serialization, no API calls, runnable before settings exist. Covers
kind-first, kind-last, kind-in-the-middle, odd casing, explicit nulls, a write/read round
trip, and that unknown and missing kinds are rejected.

It exists because this bug was invisible from the outside: a delta that fails to
deserialize is reported as an extraction failure, which reads as a model problem. A live
session was spent before the raw response was read.

## What the fixed run showed

Re-running the same script with extraction working:

- **The junk-fact fix holds.** Three turns of questions and deflections, zero facts
  established. The previous run minted `player-asked-about-well-rumor` and taught it to two
  characters.
- **The validator earned its keep.** Extraction attempted `character_introduced` for
  `innkeeper-hald` — a character listed in the known-ids roster on that same turn. Rejected,
  no damage. Prompt-level mitigation alone is not enough.
- **Relationship omission is now diagnosed rather than merely observed.** The narration said
  the innkeeper's "guarded suspicion hardens into something much colder", and extraction
  produced `mood = coldly hostile` with no `relationship_changed`.

  That is not the model ignoring the change — it is recording it in the wrong field. "Coldly
  hostile" is a relationship expressed as a mood, and nothing in the schema or the prompt
  distinguishes them: mood is "emotional register", relationship is "standing toward the
  player", and hardening hostility is honestly both. Given one slot that fits, it picks one.

  Thirteen turns across three sessions, zero relationship deltas. The likely fix is a
  defined boundary — mood is transient (this moment), standing is cumulative (how they
  regard you, persisting across scenes) — not the reconciliation pass previously assumed.

## Next

Sharpen the mood/relationship boundary, and decide whether a claim made by the player
enters canon as a fact.
