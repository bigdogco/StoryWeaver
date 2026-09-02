# TODO: World manifest

**Status:** DONE 2026-08-16
**Created:** 2026-08-16

Third piece of **Phase 1**. Designed in [`design/WORLD_PACKS.md`](../design/WORLD_PACKS.md) §2
and §6, written 2026-07-23, never built.

---

## What is missing

A pack has no name, no author and no version. Its whole identity is the folder name — the
banner prints `pack.Directory`, a file path.

```json
{
  "id": "the-last-lantern",
  "name": "The Last Lantern",
  "author": "Pavel",
  "version": "1.0"
}
```

**Nothing today is blocked on this**, and that is worth stating plainly. A display name and an
author matter once something lists packs for a player to choose from, which is Phase 2. It is
here because the pack design listed it, and because it is small.

**The version is the field that does real work**, via §6: *content may move; state degrades
quietly and loudly.* An author edits a pack while somebody has a save in progress — normal, not
exceptional. A save that records what it was played against turns a vague "something is
missing" into a specific "played against 1.0, the pack is now 1.2".

## Decisions

| question | decision |
|---|---|
| Is the manifest required? | **No.** `marrow` and `ashfall` ship none today and must keep working. Absent means the pack is named after its folder, with no author and no version. |
| Does `id` have to match the folder? | **Yes, refuse a mismatch.** The folder is already the id by locked decision. A manifest disagreeing with it is a copied pack whose directory was renamed and whose file was not — confusing in exactly the way ids exist to prevent. |
| Where does the save record it? | A small `save.json` beside `canon.json`, per the design's layout. Without recording it somewhere the version is decorative. |
| Written when? | Once, when a world is created. It describes what the save was *started* against; a resumed session does not rewrite it. |
| Does a version mismatch do anything yet? | **It warns.** Acting on it — dropping references to content a pack no longer defines — is the compatibility work in §6 and is not this task. |

## Build

- [x] `WorldManifest` record in Storage; `world.json` loaded by `WorldPack`, absent is legal
- [x] Refuse a manifest whose `id` disagrees with the folder
- [x] `WorldPack.Name` falls back to the folder id when no manifest names it
- [x] `SaveOrigin` / `save.json` written on world creation: pack id, pack version, when
- [x] Warn on resume when the pack version has moved since the save was started
- [x] Banner shows the pack's name and version rather than only a path —
      `Pack       The Last Lantern v1.0 by Pavel`
- [x] Write a manifest for all three packs

## Self-tests

- [x] A pack with no `world.json` loads, and is named after its folder
- [x] A manifest whose id disagrees with the folder fails the load
- [x] `save.json` is written on creation and not rewritten on resume
- [x] A version change between sessions produces a warning, not a failure — verified end to
      end by bumping the pack to 1.1 and resuming

## Verify

- [x] `dotnet build` clean, 0 warnings; 79 self-tests pass
- [x] Started and resumed `the-last-lantern`, read the banner and `save.json`
- [x] No scored set — nothing here touches a prompt or the delta path. Recorded so the
      omission reads as deliberate rather than forgotten.

## Close out

- [x] Devlog `2026-08-16_a-pack-with-a-name.md`, `TODO_FUTURE_WORK.md`, no unchecked boxes

## Not in this task

- **Acting on a version mismatch.** Dropping references to content a pack no longer defines is
  §6's compatibility work and wants its own measurement.
- **Pack discovery, listing, installing, sharing.** All Phase 2, and all the reason the display
  fields exist at all.
