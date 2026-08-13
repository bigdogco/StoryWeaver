# TODO — character rename / identity reveal

First fix out of §9. A character introduced anonymously kept that name forever: Nessa was
named in the prose on turn 15 and is still `"Shivering figure"` in canon at turn 51. The model
routed around the gap by storing her name in a *fact*, which is why the narration read
correctly and only a canon audit found it.

**Decision: ids are permanently opaque, names are mutable.** `figure-in-cistern` stays her id
for the life of the world. This costs nothing because ids are already fully internal —
`ContextAssembler.ForNarration` sends names only, and that split exists precisely because the
narrator once wrote `marrow-tavern` into the prose. The extractor keeps seeing
`Nessa (id: figure-in-cistern)`: current name to match the prose, stable id to emit against.

`Entity.Name` was already `{ get; set; }`, documented *"May change; the id may not"* — the
domain model was built for this and only the delta set had not caught up.

---

## Core

- [x] `CharacterRenamed(string CharacterId, string Name, string? Description)` in `StateDelta.cs`
- [x] Register `character_renamed` in `StateDeltaConverter.KindToType`
- [x] `DeltaApplier` case — sets `Name`, and `Description` only when supplied
- [x] `DeltaValidator`:
  - [x] `Check` — character must exist, name must not be blank. **No** name-uniqueness rule:
        two guards may both be "Guard", and identity lives in the id
  - [x] `IsNoOp` — only when nothing at all would change, description included
  - [x] `Identity` — `rename:{id}:{name}`
  - [x] `Tier` — 2 by default, after `CharacterIntroduced`

## Schema and prompt

- [x] Schema branch in `DeltaSchema.cs`
- [x] Extraction prompt rule, naming the workaround explicitly ("a name is not a world truth")

## CLI

- [x] `/rename` authoring command; lists the cast, shows the id, states it is staying
- [x] Listed in `/help`
- [x] `Summarize` cases in `AuthoringCommands`, `PlaySession`, `ExtractionEval`
- [x] No-ops now surfaced in the authoring path rather than reported as "nothing added"

## Eval

- [x] `WorldSeeds.Marrow_Anonymous` / `Marrow_AnonymousLate` — placeholder id *and* placeholder
      name, matching what a real anonymous introduction produces
- [x] `name-reveal` scenario, scored on the outcome via `StateRule`
- [x] Forbids the two observed workarounds: a second copy of the person, and the name filed
      as a fact
- [x] `name-reveal-large` diagnostic against the big seed

## Verify

- [x] `dotnet build` clean — 0 warnings, 0 errors
- [x] `--selftest` — all serialization checks pass, including a rename with a null description
- [x] `name-reveal` 21/21 required, 0 forbidden, clean 7/7
- [x] `name-reveal-large` pinned to DeepInfra: 18/18, 0 forbidden
- [x] Full scored sweep, 9 scenarios: **100% required, forbidden 0.00, rejects 0.00** —
      baseline moves from 98% across 8 to 100% across 9
- [x] Devlog — `docs/devlog/2026-07-23_character-rename.md`
- [x] `CHALLENGES.md` and `TODO_FUTURE_WORK.md` updated

---

## Findings worth carrying forward

- **The two-stage-entry scoring bug, repeated.** The first forbidden rule flagged any fact
  mentioning "Sera" and fired 5/7 on `sera-knows-player: "Sera Voight knows who the player
  is"` — a legitimate fact that merely names her. Narrowed to match the *assertion* of a name,
  it drops to 0/7. Having written the lesson down did not prevent writing the bug again; what
  catches it is reading the deltas behind a failing rule before believing the rule.
- **World size was innocent.** Routed, large scored 5/7 forbidden vs 0/7 small — which looks
  exactly like a world-size effect. Pinned to DeepInfra it is 0/7; pinned to Baidu, 4/7. The
  small run had gone entirely to DeepInfra. Provider and size were confounded by routing.
- **Baidu files the name as a fact 4/7 — while also renaming correctly** (21/21 required).
  Redundancy, not corruption, so not added to `providerIgnore`. Left as input to the queued
  automated provider calibration.
- **The `(unreported)` provider returns malformed deltas.** Two calls today came back with
  `{type, id, name}` and `{type, id, content}` — `response_format` ignored outright. Both from
  the provider that also declines to report its own name.

## Left for the player

- [x] ~~Run `/rename` on `figure-in-cistern` in `saves/marrow` to fix Nessa~~ — **won't do, 2026-08-13.** A repair to a save superseded by later worlds.
