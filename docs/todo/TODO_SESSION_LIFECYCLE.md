# TODO: Session lifecycle — opening a session belongs outside the clients

**Status:** DONE 2026-09-04
**Created:** 2026-09-04

Closes the last open item in [`design/CANON_OWNERSHIP.md`](../design/CANON_OWNERSHIP.md) §6.
`StorySession` owns canon once it exists; **getting one is still the console's private business.**

---

## What is in `RunAsync` before the loop

Thirteen steps, of which four are rendering:

| | |
|---|---|
| **policy** | default the save id to the pack id · acquire the lock, refuse if held · load the pack · load the prompts · wire the engine (voice, lore, sheets, scenario, opening, history window) · resume vs. fresh · does the pack author the player · write the first save · write `save.json` · compare pack versions |
| **interaction** | ask who you are, when the pack does not say |
| **rendering** | the lock-refusal message · the banner · the drift warning · opening scene vs. recent turns |

A window needs every policy item and needs them exactly. The engine wiring is the dangerous one:
get it subtly wrong and a pack silently loses its voice, which looks like the model being worse
rather than a missing argument.

## Three findings that shaped the design

**1. The seed fallback is dead weight.**

```csharp
WorldState world = loaded ?? pack.Seed ?? WorldSeeds.Marrow();
```

All three packs ship `seed.json`, and `WorldSeeds` is otherwise used **only by eval scenarios** —
instrumentation, the category that never migrates inward. That fallback was the one thing tying
session-opening to a CLI fixture, so it goes.

**2. Nothing could host this.** Opening needs Storage (`SaveLock`, `WorldPack`,
`JsonWorldRepository`, `SaveOrigin`), Llm (`PromptLibrary`, `OpenRouterClient`, narrator,
extractor) and Core. `Core` references nothing; Llm and Storage are siblings and cannot see each
other. **There was no project that could hold the sequence**, which is exactly why it stayed in
the CLI. Hence a new one.

**3. One step genuinely needs the client mid-way.** Everything else is a pure sequence, but
*"who are you?"* is not: a pack with no `player.md` has to ask. The console prompts; a window
wants a dialog.

## Decisions

| question | answer |
|---|---|
| Where does it live? | **A new `StoryWeaver.App` project**, referencing Core, Llm and Storage. Both clients reference it. It is composition — the layer that knows how to assemble a playable session out of the three libraries. |
| How is the interactive step handled? | **Two-phase.** `Open` returns either a ready session or a *needs-a-player* state carrying everything already loaded; the client supplies a name and description and completes it. |
| Why not a callback interface? | It inverts control and makes a UI implement something that blocks inside a load. Fine until it is not. |
| What about the lock refusal? | Returned as a refusal **with the holder's description**, not printed. The console renders it exactly as before. |

## Build

- [x] `src/StoryWeaver.App` project, referencing Core, Llm, Storage
- [x] `SessionOpener` — the whole sequence, no `Console` in it
- [x] Two-phase: an outcome that is *opened*, *needs a player*, or *refused*
- [x] The needs-a-player state carries the loaded pack and world, so completing it costs nothing
      and cannot re-run the load
- [x] Remove the `WorldSeeds.Marrow()` fallback from the play path
- [x] `StoryWeaver.Cli` references App; `PlaySession` renders the outcomes and prompts for a name
- [x] Everything the banner needs comes back on the outcome — pack, prompts, log path, resumed,
      turn number — rather than being recomputed

## Self-tests

- [x] Opening a fresh save with an authored player returns a ready session
- [x] Opening a fresh save with no authored player returns needs-a-player, and completing it
      produces a session with that name and description in canon
- [x] The completed player is persisted — a fresh open of the same save resumes rather than
      asking again
- [x] Resuming an existing save never asks
- [x] A save held by a live session is refused, and the refusal names the holder
- [x] `save.json` is written on a fresh open and not rewritten on resume
- [x] Pack drift is reported on the outcome rather than printed
- [x] Abandoning a needs-a-player state releases the save lock

## Verify

- [x] `dotnet build` clean, 0 warnings
- [x] All existing self-tests pass
- [x] By hand: fresh save on **`ashfall`** (asks, answered, persisted), resumed it (did not ask,
      "resumed at turn 0"), fresh save on `marrow` (authored player, announced, not asked), one
      real turn, and a clean exit. **Note the pack swap:** `marrow` ships a `player.md` — it is
      `ashfall` that does not, which is why the tests use it.

## Close out

- [x] Devlog, `CANON_OWNERSHIP.md` §6 closed, `PROJECT.md` layer table, `TODO_FUTURE_WORK.md`,
      no unchecked boxes

## Two bugs found while building this

**1. `ashfall` hung forever on closed stdin — fixed.** Character creation looped on a blank name
with no way to give up, so a piped or closed input span the prompt for ever *while holding the
save lock*. Logged during the lock work in August and never fixed, because the loop lived in the
console and looked like console business. The two-phase shape makes giving up ordinary: end of
input returns null, `PendingPlayer.Dispose` hands the save back, nothing is written.

Nearly shipped a half-fix: the first version printed *"the question was abandoned, the save was
given back"* and **never disposed the pending state**, so the run left a stale lock behind. The
comment was true about the design and false about the code. Caught by looking at the directory
rather than at the message.

**2. `SaveLock` could not see a second session in the same process — fixed.** It carried an
explicit exemption:

> *Our own id is not a conflict. A session that somehow re-acquires its own lock is taking back
> something it already owns.*

That was sound when one process meant one playthrough. **`StorySession` ended that** — sessions
are objects and several can exist at once, which is exactly why the statics were removed — so one
process opening the same save twice became a real scenario, and the file cannot tell it apart
from a session re-acquiring its own lock. Both look like our own process id.

In-process holders are now tracked in memory, where the answer is exact. `--force` still breaks
it, and has its own check so the new set cannot become a lock nothing can break.

**The pattern worth keeping:** enabling several sessions per process silently invalidated an
assumption written down somewhere else. The comment stating the assumption is what made it
findable — and it was found by a test that could not have been written before the feature that
broke it.

## Not in this task

- **The two-tier delta set.** Still undecided, and discussed separately.
- **Choosing a pack or save from a list.** *Multiple saves per pack* is queued; this task makes
  it possible by taking ids as arguments, and does not build a chooser.
