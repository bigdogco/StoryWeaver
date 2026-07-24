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
- [ ] **Answer the four open decisions in §8**

## Build now — with the lore work, not separately

- [x] Lore entries load from `worlds/{pack}/lore/*.md` — done 2026-07-24, the first pack exists
- [ ] Pack root as an explicit parameter, not the working directory. **Still a constant**
      (`PlaySession.PackRoot`), so a pack is found relative to the cwd exactly as saves are
- [ ] `saves/` root configured rather than cwd-relative — currently the reason `play.ps1`
      forces the cwd and harness testing needs a temp directory

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

- [ ] `world.json` manifest, with a version a save can record
- [ ] `seed.json` replacing `WorldSeeds` in C#
- [ ] Opening message, and the loader check that every name in it exists in the seed
- [ ] Multiple saves per pack — today `marrow` is both the world id and the save directory
- [ ] Per-pack narration prompt overrides
- [ ] Pack installing / sharing
