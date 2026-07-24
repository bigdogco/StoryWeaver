# Devlog — the seed leaves the code

**Date:** 2026-07-24
**Scope:** `seed.json`, pack id vs save id, and a format that already existed

---

## The audit that changed the estimate

This was queued as a medium-sized job: design a seed format, write a parser, port 285 lines of
C#. The audit took about ten minutes and found the format already exists.

**A seed is a `WorldState` with `turnNumber: 0`**, and `SaveJson.Canon` — the options that
write `canon.json` — already round-trip exactly that, converters included. No new format, no
parser, no schema, no validation code beyond one sanity check.

A consequence worth stating: **a save file is a valid seed.** "Start a new world from where
that one got to" is free, and `--write-seed` turns any world this codebase can build into a
starting pack.

## Generated, not transcribed

The seed was not hand-written. `--write-seed` serialises `WorldSeeds.Marrow()` to
`worlds/marrow/seed.json`, so the JSON and the C# fixture are provably identical rather than
approximately so.

Transcribing 285 lines by hand is exactly the task that silently loses a mood or a relationship
standing, and the resulting difference would surface later as an unexplained behaviour change
in whatever got measured next. The check afterwards was direct: a new world's `canon.json` is
byte-identical to the pack seed.

## Two identifiers that were one word

`PlaySession.WorldId = "marrow"` was doing two jobs: naming the authored world and naming the
save directory. Now `PackId` and `SaveId`, identical strings, separate constants.

Nothing behaves differently today, and that is the point — the existing 51-turn save resumes
untouched, canon unchanged, no migration. Supporting several playthroughs of one pack is now a
matter of choosing `SaveId` at startup rather than a change to how anything is stored.

## What the loader refuses

- **A missing pack is an empty pack.** A fresh clone must still play, so the built-in world
  stays as the fallback.
- **A seed that exists and cannot be read throws.** The opposite rule, and deliberately: an
  author who wrote a seed and silently got the built-in world instead would have no way to tell.
  The distinction is "content absent" versus "content wrong".
- **The player must start somewhere that exists.** Otherwise the story opens with "the player
  is nowhere yet", which is a confusing way to find a typo in a location id.
- **Turn number is forced to 0**, on read *and* write. Copying a save in as a seed is a
  plausible way to author a pack, and it must not open a new world at turn 51.

## What stays in C#

`WorldSeeds` remains, as the eval fixture. The derived worlds — `Marrow_Late`,
`Marrow_Anonymous`, `Marrow_AnonymousLate` — are built by mutating a base world, which C# does
well and JSON does not. They are fixtures, not content.

There is a second reason: an eval whose fixture can change because somebody edited a pack file
is measuring the wrong thing. The scored set has to be stable across sweeps or the recorded
baselines mean nothing.

## Notes

- **`/knows` is already redundant for authoring**, one day after being built. A seed carries
  `"knows": [...]` per character directly, which is what `WorldSeeds` always did in C#. Kept for
  mid-session use. Worth noticing that it was written to work around the absence of exactly the
  thing built today — the gap was real, the workaround was correctly scoped, and it retired on
  schedule.
- **Pinning the provider hit HTTP 429.** The regression sweep pinned to Baidu rate-limited after
  44 runs, all clean. Worth recording as a limit of the discipline `CHALLENGES.md` now
  prescribes: "hold the provider fixed" and "run enough samples" pull against each other on a
  single upstream.
- **`SaveJson` is `internal` to Storage**, so the pack reader could reuse it directly. Another
  case of an earlier decision paying out — putting save-format concerns in Storage rather than
  Core means the pack loader is a thin file rather than a second serializer.

## Results

```
self-test          10 delta checks + 16 lore/pack checks, all passing
new world          byte-identical to worlds/marrow/seed.json
existing save      resumes at turn 51, canon unchanged
scored set         no change expected — this touches no prompt and no delta
```

## Next

The pack now holds a seed and lore. Still hardcoded: the pack *root* and the save root are
constants resolved against the working directory, which is why `play.ps1` forces the cwd. The
opening message is designed and unbuilt, and it is the piece that would make a pack feel
authored rather than assembled.
