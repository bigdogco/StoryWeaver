# TODO: Move EntityId into Core

**Status:** DONE 2026-09-02
**Created:** 2026-09-02

Third piece of **Phase 2**, and the smallest. Deferred twice with a trigger; the trigger fired.

---

## Why now

`Authoring.Slug` (Core) produces ids. `EntityId.IsWellFormed` (Storage) checks them. They are two
halves of one convention, and `Core` references nothing, so the producer cannot see the checker.

Deferred on 2026-09-02 in `TODO_UI_BOUNDARY.md` with a stated trigger: **revisit when a second
Core caller needs to validate an id.** `CanonRefresh` is that caller — a save hand-edited to
`Warrior_Mike` is exactly what a reload should warn about, and it cannot.

Waiting was right. Moving it while writing the boundary refactor would have hidden a structural
change inside a task about something else, and the case for it was hypothetical until a caller
appeared.

## Measured before building

The reason to check first: `EntityId`'s own docs say it is for **authored** ids, and explicitly
leave open *"whether ids proposed by extraction should be held to the same shape."* If extraction
has been emitting ids the checker would reject, adding this warning floods every reload of every
real save — a check that cries wolf gets ignored, which is worse than no check.

**549 ids across all 11 saves. Zero malformed.**

So the check is silent on real data, and the open question in `EntityId`'s docs now has an
answer with evidence behind it: extraction already produces well-formed ids, in practice, over
eleven playthroughs.

## Build

- [x] Move `EntityId.cs` from Storage to Core, namespace `StoryWeaver.Core`
- [x] `WorldPack` keeps working unchanged — it already imports Core
- [x] `CanonRefresh.Check` warns about a malformed id
- [x] Correct the comments that say the check "lives in Storage and cannot be referenced" —
      `Authoring.Slug` and `AuthoringSelfTest`
- [x] Record the extraction finding in `EntityId`'s own docs, where the question was asked

## Self-tests

- [x] A malformed id in canon is warned about
- [x] The existing `Slug` bridge test still passes — now within one project rather than across two
- [x] Load-time enforcement still refuses a malformed pack id

## Verify

- [x] `dotnet build` clean, 0 warnings
- [x] All existing self-tests pass
- [x] The real saves still reload with no id warnings — through `/reload` itself rather than a
      script. Copies of `marrow` (230 turns), `marrow-old` (51) and `ashfall` (250), **229
      entities**, all three reporting *"Canon on disk matches this session"* and **no CHECK
      lines at all**.

      Worth more than the box asked for: this is the first time any of the seven invariants have
      run against real long-run canon rather than hand-built fixtures. They are silent on it,
      which is what a check on correct data should be.

## Close out

- [x] Devlog, `TODO_FUTURE_WORK.md`, `PROJECT.md` layer note, no unchecked boxes

## Not in this task

- **Rejecting malformed ids at runtime.** `EntityId.Require` throws, and it stays a *load-time*
  rule on authored files. In canon it warns, because canon belongs to the player and
  `CanonRefresh` reports rather than refuses.
