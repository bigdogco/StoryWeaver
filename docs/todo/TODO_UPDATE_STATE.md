# TODO: Update State — re-read canon from disk, mid-session

**Status:** DONE 2026-09-02
**Created:** 2026-09-02

Second piece of **Phase 2**, and the last open question `PROJECT.md` recorded against the phase:
*is Update State UI-only, or also a CLI command?*

---

## The bug it closes, which exists today

`PROJECT.md` §3 locks that the player owns their world and can edit it directly. A running
session does not honour that. [`PlaySession`](../../src/StoryWeaver.Cli/PlaySession.cs) loads
canon once at startup and holds it in memory for the rest of the session:

```csharp
WorldState? loaded = await repository.LoadAsync(_saveId);
WorldState world = loaded ?? pack.Seed ?? WorldSeeds.Marrow();
```

So: play, alt-tab, fix a wrong mood in `canon.json`, come back, take a turn — the turn runs on
the stale in-memory copy and then **saves over the edit**. Silent, and it eats precisely the
repair the feature exists to allow. The session lock does not help; it stops a second engine,
not a text editor.

## The decision

**Both surfaces, and the argument is not symmetry.**

The parity rule rejected on 2026-09-02 says a UI feature need not have a `/command`. That
argument was about *UI-shaped* features — dragging a character onto a location has no honest
slash-command form. Update State is a verb with no arguments and no interaction. The rejection
makes UI-only permissible; it does not make it correct.

What tips it: **the bug is in the CLI today, before any UI exists**, and the CLI is where the
long runs happen. Reaching into canon mid-run is exactly when this matters.

Deciding it now also decides the return type. Build it after the UI exists and it comes back
shaped for a panel, at which point the CLI reimplements or does without.

**No file watching, no merge, no reconciliation.** An explicit action is the whole mechanism —
those alternatives are how a tool starts fighting its author.

---

## An error in PROJECT.md, found while specifying this

§3 lists the invariants as *"no dangling fact ids, no item both held and placed, no character
without a location."* **That last one is wrong.** `Character.LocationId` is nullable precisely so
a person can exist offstage — a brother back home, a name from a rumour — and
`AuthoringCommands` offers it as "blank = unknown / offstage".

Implementing it as written would have warned about every correctly-authored offstage character.
The real invariant is *a character's location, **when set**, names a real place.* Fixed in
`PROJECT.md` as part of this task.

## The invariants, as they actually are

Reported, never refused — the validator is suspicious of a cheap model, and a person editing
their own canon does not need to be argued with.

- [x] A character's `LocationId`, when set, names an existing location
- [x] An item is placed or held, never neither and never both
- [x] An item's `LocationId` / `HolderId` name an existing location / character
- [x] Every id in a character's `Knows` names an existing fact **or lore entry** — the two share
      one id namespace, so checking facts alone would warn about every known lore entry
- [x] A location's `Connections` name existing locations
- [x] Each entity's `Id` matches the dictionary key it is filed under — the hand-edit failure:
      rename the key, miss the field, and the entity becomes unreachable by its own id
- [x] The player exists

## Build

- [x] `PROJECT.md`: answer the open question, and correct the offstage-character invariant
- [x] `Core/CanonRefresh.cs` — re-read, diff, check, report. No `Console` in it
- [x] The diff compares **serialized** entities, so a field added later is covered automatically
      rather than silently omitted from the comparison
- [x] `/reload` in the CLI, rendering the report
- [x] `/help` lists it

## Self-tests

- [x] An external edit is picked up, and the report names what changed
- [x] Added and removed entities are reported
- [x] A dangling character location is warned about; an offstage character is **not**
- [x] A `Knows` entry pointing at a lore id is not a warning; one pointing at nothing is
- [x] An item both held and placed is warned about, and so is one that is neither
- [x] An id that disagrees with its key is warned about
- [x] A reload with no changes on disk reports exactly that

## Verify

- [x] `dotnet build` clean, 0 warnings
- [x] Existing self-tests still pass
- [x] By hand, **both directions measured** rather than argued. A live session, an external
      edit adding `the-hand-edited-cellar` to `canon.json`, then one real turn:
      **without `/reload` the location was gone**; with `/reload` it survived the turn. The
      reload itself reported `added place the-hand-edited-cellar` while the session was running.

## Close out

- [x] Devlog, `TODO_FUTURE_WORK.md` updated, no unchecked boxes

## Not in this task

- **Id well-formedness.** `EntityId.IsWellFormed` is in Storage and Core cannot reach it — the
  duplication logged in `TODO_UI_BOUNDARY.md`. A hand-edited id of `Warrior_Mike` will not be
  warned about here. **This is the second caller that wants it**, and the trigger for moving
  `EntityId` is now met rather than hypothetical.
- **Reloading history.** The turn log is a record of what happened, not an editing surface
  (§3). Only canon is re-read.
- **Reloading the pack.** Lore, sheets and prompts are loaded at startup. Re-reading those is a
  different action with a different blast radius, and no one has asked for it.
