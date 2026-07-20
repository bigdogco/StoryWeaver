# Devlog — JSON storage (bootstrap §6)

**Date:** 2026-07-21
**Scope:** file-backed `IWorldRepository`, atomic saves, persistent play sessions

---

## What landed

`JsonWorldRepository` in `StoryWeaver.Storage`, and the play harness now persists. A world
is saved after every turn (the turn loop already called `SaveAsync`/`AppendTurnAsync`, so
this was purely a matter of handing it a real repository), and a session can be quit and
resumed. Bootstrap §6 is done bar the user's own manual play-through.

Layout, one directory per world under `saves/`:

```
saves/{worldId}/canon.json      the WorldState, rewritten whole each turn
saves/{worldId}/history.jsonl   one TurnRecord per line, append-only
```

Canon is small and rewritten whole; history grows without bound and is only appended. JSONL
for history means a turn is a single file append with no read-modify-write of the whole log,
and keeps the later "move the turn log to SQLite" swap isolated — both straight from the
reasoning already written on the interface.

Atomic canon write is temp-file-plus-move: serialize to `canon.json.tmp`, then
`File.Move(..., overwrite: true)`. A crash leaves either the old save or the new one, never a
truncated file.

## Two bugs the format would have frozen in, caught before writing a line of it

Reading the domain model before implementing turned up two things that a naive
`JsonSerializer.Serialize(world)` would have baked into every save:

1. **System.Text.Json drops the collection comparer on load.** The seed builds
   `Characters`/`Locations`/`Facts` and the `Knows`/`Connections` sets with
   `OrdinalIgnoreCase`, but STJ constructs a *fresh, default (case-sensitive)* collection when
   it deserializes an `init` property. A freshly-seeded world matched ids case-insensitively;
   a reloaded one would not. That is the worst kind of bug — invisible until a case variant
   from extraction dangles in a loaded game but not a new one.

2. **`HashSet` enumeration order is not stable.** It can change on resize, so adding one fact
   to a character's `Knows` could rewrite that whole set's block in the file — noise in a
   diff that is supposed to be the primary debugging tool for this phase.

Both are fixed by two **save-only** converters (`CaseInsensitiveDictionaryConverter`,
`CaseInsensitiveStringSetConverter`) that sort on write and read back into case-insensitive
collections. They live in `Storage` and are added only to the save options — the model-facing
`StoryJson.Options` is untouched, so nothing about talking to models changes.

Also `[JsonIgnore]` on the derived `WorldState.Player`, `WorldState.PlayerLocationId`, and
`Character.IsPlayer`, so each fact is on disk once. The player is stored once inside
`characters`, not again under a `player` top-level field.

## Verification

The converters are the kind of code that either works or throws on first contact, so before
spending any API credits on a live play-through I compiled a throwaway checker against the
real `Core` + `Storage` projects (scratchpad, not committed). 18 assertions, all green:

- round-trip: turn number, characters, derived player location
- the comparer fix: `Characters`/`Facts` dicts and `Knows`/`Connections` sets all match
  ids case-insensitively after load
- **byte-identical** `canon.json` after a load+save cycle (stable ordering holds)
- derived props (`isPlayer`, `playerLocationId`) absent from the file
- history: two appended turns read back, a `MoodChanged` delta round-trips through the
  `StateDeltaConverter`
- a deliberately truncated trailing line in `history.jsonl` is skipped, good turns still load
- `ListWorlds` finds the world; a missing world loads as `null`

Build is clean, 0 warnings (warnings are errors).

## Notes for later

- `id`/`name` serialize after the derived-class fields (base-class members come last). Purely
  cosmetic and deterministic; not worth a `[JsonPropertyOrder]` pass.
- Save root is `saves/` relative to the working directory, printed in the banner. Fine for a
  console harness; a real UI will want a configurable location.

## Next

Two bootstrap items remain: §9's ~50-turn manual play session (now actually possible to do
across sittings, since the world persists), and folding its findings into CHALLENGES. After
that the bootstrap phase is complete and the deferred work — prompt externalization, world
generation, the lorebook/authoring layer — is next in line.

A separate note captured this session: **no prompt string should live in code.** The narrator
and extractor system prompts are still `const string`s; they were kept as code through
bootstrap because `--eval` measures the binary, but that reason has now expired. Logged in
TODO_FUTURE_WORK as its own task (editable prompt files + optional hot-reload), deliberately
not folded into storage.
