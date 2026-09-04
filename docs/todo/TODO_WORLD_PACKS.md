# TODO — world packs

Raised while designing lore entries: lore is the first authored content that is not code, so
the on-disk layout gets decided now rather than migrated later.

Design: [`docs/design/WORLD_PACKS.md`](../design/WORLD_PACKS.md).

---

## Design

- [x] Separate content (pack) from state (save), and say why conflating them is the failure
      visible in the character-card ecosystem
- [x] Propose the directory layout
- [x] Write down **who writes canon** — five writers, one validator gate
- [x] Settle the opening message: pack ships prose *and* seed, loader checks them against
      each other
- [x] Prompt overrides: narration yes, extraction no
- [x] Compatibility rule: content may move, state degrades quietly and loudly
- [x] **Answer the four open decisions in §8** — settled by what shipped 2026-07-24.

## Build now — with the lore work, not separately

- [x] Lore entries load from `worlds/{pack}/lore/*.md` — done 2026-07-24, the first pack exists
- [x] `seed.json` — the pack is now the source of truth for a new world
- [x] Pack id and save id separated (`PackId` / `SaveId`). Identical strings today, so existing
      saves keep working; supporting several saves per pack is now a matter of choosing `SaveId`
      at startup rather than a change to how anything is stored
- [x] Pack root as an explicit parameter, not the working directory. **Still a constant**
      (`PlaySession.PackRoot`), so a pack is found relative to the cwd exactly as saves are — **moved to TODO_FUTURE_WORK 2026-08-13.**
- [x] `saves/` root configured rather than cwd-relative — currently the reason `play.ps1`
      forces the cwd and harness testing needs a temp directory — **moved to TODO_FUTURE_WORK 2026-08-13.**

## Built 2026-07-24

- [x] `WorldPack.Load(root, id)` — reads `seed.json` and `lore/`. A missing pack is empty, so
      a fresh clone still plays; a seed that exists and cannot be read throws, because an
      author who wrote one and silently got the built-in world would have no way to tell
- [x] Seed validation: the player must start somewhere that exists in the seed
- [x] `WorldPackWriter.WriteSeed` and `--write-seed`, which generated `worlds/marrow/seed.json`
      from `WorldSeeds.Marrow()` so the two are provably identical rather than approximately so
- [x] Turn number forced to 0 on both read and write — copying a save in as a seed is a
      plausible way to author a pack and must not open a new world at turn 51
- [x] Banner reports the pack directory and, on a new world, which seed it came from
- [x] Three self-tests: round trip, turn-0 enforcement, missing pack is empty
- [x] `WorldSeeds` kept as the eval fixture. The derived worlds (`Marrow_Late`, the
      `_Anonymous` variants) are built by mutating a base, which C# does well and JSON does
      not — and an eval whose fixture changes when someone edits a pack measures the wrong thing

**Verified:** a new world is byte-identical to the pack seed; the existing 51-turn save resumes
untouched with canon unchanged.

## Audit, 2026-07-24 — the seed format already exists

Checked before designing anything, and it changes the estimate: **`canon.json` is the seed
format.** A seed is a `WorldState` with `turnNumber: 0`, and `SaveJson.Canon` already
round-trips exactly that, converters included. No new format, no parser, no schema.

What the work actually is:

- `WorldSeeds.cs` is 285 lines of C# that becomes a JSON file
- the load path chooses `worlds/{pack}/seed.json` over `WorldSeeds.Marrow()`
- `WorldId` and the save directory are the same constant today
  (`PlaySession.WorldId = "marrow"`), so splitting pack id from save id is a real but small
  change — 8 call sites, all in `PlaySession`
- `LastSeenTurn` is nullable and absent from a seed, which is already correct

Consequence: **`/knows` stops being needed for authoring.** A seed carries
`"knows": ["cult-of-the-blind"]` per character directly, which is what `WorldSeeds` already
does in C#. `/knows` stays useful for mid-session authoring.

Two things the eval seeds need that a JSON seed does not cover: `Marrow_Late` and the
`_Anonymous` variants are *derived* worlds built by mutating a base. Those stay in C#, and
that is fine — they are test fixtures, not content.

## Build later — when something needs it

- [x] `world.json` manifest, with a version a save can record — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]
- [x] Opening message, and the loader check that every name in it exists in the seed — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]
- [x] Multiple saves per pack — the ids are separated now, so this is a startup choice plus
      whatever UI offers it — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 2]
- [x] Per-pack narration prompt overrides — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]
- [x] Pack installing / sharing — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 2]
- [x] **`/knows` is now redundant for authoring** — an observation, not a task. Struck 2026-08-13; kept for mid-session authoring.


