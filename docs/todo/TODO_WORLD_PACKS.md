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

## Build later — when something needs it

- [ ] `world.json` manifest, with a version a save can record
- [ ] `seed.json` replacing `WorldSeeds` in C#
- [ ] Opening message, and the loader check that every name in it exists in the seed
- [ ] Multiple saves per pack — today `marrow` is both the world id and the save directory
- [ ] Per-pack narration prompt overrides
- [ ] Pack installing / sharing
