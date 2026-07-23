# TODO — §9 Validation: the 50-turn play session

The last bootstrap item. A real session played end to end, then read off disk and analysed.

**Session:** `saves/marrow`, 51 turns, 2026-07-22 → 2026-07-23.

---

## Analysis

- [x] Count applied / rejected / no-op deltas across the session
- [x] Read every rejection reason and classify benign vs. information loss
- [x] Trace all movement and introduction deltas in order
- [x] Audit final canon — characters, locations, facts
- [x] Verify the two-stage movement fix held in play
- [x] Establish whether long-range character recall came from canon or context

## Findings recorded

- [x] Devlog — `docs/devlog/2026-07-23_fifty-turn-validation.md`
- [x] `docs/CHALLENGES.md` — facts as a dumping ground, location identity,
      two paths to move the player
- [x] `docs/todo/TODO_FUTURE_WORK.md` — domain model gaps extended
- [x] `docs/todo/TODO_BOOTSTRAP.md` — §9 checked off

## Build

- [x] `dotnet build` clean — 0 warnings, 0 errors. Note: a running CLI holds the output DLLs
      and fails the copy step with `MSB3027`. Not a code error; quit the game, or build with
      `-p:BaseOutputPath=<elsewhere>`.

---

## Fixes queued out of this session

Ranked. None started — each wants its own task document.

1. [ ] **Character rename / identity reveal.** No delta can change a character's name, so
       Nessa is still `"Shivering figure"` in canon 36 turns after the prose named her. The
       model routed around it by writing the name into a *fact*. Cheapest fix, highest
       value, and play demonstrated the need three separate ways.
2. [ ] **Fact hygiene.** Facts have no truth value and no attribution, so a lie Hald told is
       stored identically to a truth. Combat blow-by-blow is landing in permanent canon.
3. [ ] **Location identity.** Time of day spawned `marrow-square-night` as a second,
       orphaned copy of a place that already existed.
4. [ ] **The `character_moved`-on-player seam.** Two delta kinds perform the same mutation
       and the duplicate check does not see across them.
