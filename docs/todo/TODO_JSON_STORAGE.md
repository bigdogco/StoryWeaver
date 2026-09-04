# TODO: JSON storage (bootstrap §6)

**Status:** DONE (pending manual play verification by user)
**Created:** 2026-07-21

---

## Goal

Persist worlds to disk behind the existing `IWorldRepository`, so a session can be created,
played, saved, reloaded, and continued. This is bootstrap §6 — deliberately sequenced
*after* the turn loop (§7) so the save format is frozen around a domain model that has been
through real play, and after the extraction question was settled so the model it stores for
is no longer unproven.

The interface (`IWorldRepository`) already exists and is second-implementation-tested by
`InMemoryWorldRepository`, so no interface design is in scope — only a file-backed
implementation and wiring it into the harness.

## Design decisions

- **One directory per world:** `saves/{worldId}/`.
  - `canon.json` — the `WorldState`, rewritten whole each turn (small, bounded).
  - `history.jsonl` — one `TurnRecord` per line, append-only (grows without limit). JSONL
    is chosen over a JSON array so appending a turn is a single file append with no
    read-modify-write, which matches the interface's stated shape.
- **Atomic canon write:** serialize to `canon.json.tmp`, then `File.Move(..., overwrite)`.
  A crash mid-write leaves the previous save intact, per the interface contract.
- **Save-only JSON converters** (in `Storage`, not in Core's `StoryJson`):
  - Sort dictionary keys and set members on write → deterministic `git diff`.
  - Read dictionaries and `HashSet<string>` back with `OrdinalIgnoreCase` → fixes the
    comparer System.Text.Json drops on `init` collections (see findings below).
- **`[JsonIgnore]` on derived/computed properties** (`WorldState.Player`,
  `WorldState.PlayerLocationId`, `Character.IsPlayer`) so the save holds each fact once and
  stays a clean diff. These are Core edits, justified: a derived value does not belong in
  the persisted form.

## Findings that shaped the design

- **Comparer loss on load.** STJ constructs a fresh default-comparer collection when
  deserializing an `init` dictionary/set, dropping the seed's `OrdinalIgnoreCase`. A loaded
  world would match ids case-sensitively while a seeded one does not — a round-trip
  behaviour difference. Fixed by the save-only read converters.
- **Set enumeration is not stable.** `HashSet` order can change on resize, so a one-element
  change could rewrite a whole block of a save. Sorting on write keeps diffs meaningful.

## Tasks

- [x] `CaseInsensitiveDictionaryConverter` — factory for `Dictionary<string,T>`: sort keys
      on write, read into `OrdinalIgnoreCase`.
- [x] `CaseInsensitiveStringSetConverter` — `HashSet<string>`: sort on write, read
      case-insensitive.
- [x] `SaveJson` — Storage-local canon options (copy of `StoryJson.Pretty` + the two
      converters). History uses compact `StoryJson.Options` directly.
- [x] `[JsonIgnore]` on `WorldState.Player`, `WorldState.PlayerLocationId`,
      `Character.IsPlayer`.
- [x] `JsonWorldRepository` in `Storage` — Load/Save/AppendTurn/LoadHistory/ListWorlds,
      atomic canon write, JSONL history tolerant of a truncated trailing line.
- [x] Wire into `PlaySession`: load world if present else seed and save; per-turn autosave
      is already done by `TurnEngine`. Print the save location in the banner. Update the
      "nothing is saved" copy.
- [x] Change `PlaySession` command handlers to depend on `IWorldRepository`, not the
      concrete in-memory type.
- [x] `dotnet build` clean (warnings are errors).
- [x] **Offline round-trip check** (throwaway, scratchpad): 18 assertions covering the
      comparer fix, byte-identical load+save, derived props omitted, history delta
      round-trip, truncated-tail tolerance, ListWorlds, and missing-world → null. All pass.
- [x] Manual verify by user: play a turn, confirm files appear, quit, relaunch, confirm the world and turn count continue. **Done many times over** — seven saves exist and resume.


## Out of scope (stays in TODO_FUTURE_WORK)

- Prompt externalization to editable files + optional hot-reload (raised this session;
  distinct concern, logged separately so storage and prompts are not tuned at once).
- ~~SQLite turn log~~ — **dropped 2026-08-13**, storage stays JSON permanently.
- Richer authored character-sheet / lorebook fields (additive to this format later).
