# TODO: StorySession — Core owns canon

**Status:** DONE 2026-09-04
**Created:** 2026-09-04

Implements [`design/CANON_OWNERSHIP.md`](../design/CANON_OWNERSHIP.md) §4, and answers that
section's three open questions rather than deferring them.

---

## The problem, restated from the design

Canon is owned by a local variable in `PlaySession`. Nothing else holds a `WorldState` — the
repository is stateless and `TurnEngine` takes the world per call. So:

- **It contradicts the thin-layer rule on the object rather than on the rules.** A UI client owns
  the domain's central object and is the sole coordination point for every mutation.
- **The concurrency hazard has nowhere to be fixed.** A turn reads canon, awaits 20–60 seconds of
  network, then mutates and saves. `/reload` swaps a reference the in-flight turn is not holding.
  There is no object whose job is canon-for-this-session, so there is nowhere to put a guard.

## Decisions — the design's open questions, answered

| question | answer |
|---|---|
| Does the session own the save lock? | **Yes.** "This save is mine for now" and "I hold canon for this save" are one lifetime; splitting them was avoidance. `SaveLock` stays in Storage and is handed in as an acquired `IDisposable`, so Core gains the ownership without learning file layout. `StorySession` is `IDisposable`. |
| May a client mutate `World` directly? | **Exposed for reads, and honestly labelled.** Making `WorldState` immutable contradicts its own design and is a far larger change; it is recorded as a known limit rather than pretended away. Writes have two supported doors, below. |
| One session per process, or several? | **Several.** The two `static` fields in `PlaySession` become session state, which is what made multiple saves per pack awkward. |
| Refuse or queue a second operation? | **Refuse**, following `SaveLock` — a queued click the player has forgotten making is worse than a clear "a turn is in progress." |

## The two write paths, from the design's §5

**Deltas are the norm.** `AuthorAsync` takes deltas, validates them as authored, applies, saves.

**Direct editing is the labelled hatch.** `EditAsync(Action<WorldState>)` runs the caller's
mutation **inside the guard**, then runs `CanonRefresh.Check`, then saves. This is what makes §5
real rather than aspirational: the hatch gets the same single-writer protection and the same
after-the-fact validation as everything else, and the caller's only remaining job is the warning.

## Shape

```csharp
public sealed class StorySession : IDisposable
{
    public string SaveId { get; }
    public string PackId { get; }
    public WorldState World { get; }        // reads only, by convention
    public bool IsBusy { get; }

    Task<SessionResult<TurnOutcome>>      TakeTurnAsync(string input, ct);
    Task<SessionResult<TurnOutcome>>      ReExtractLastAsync(ct);
    Task<SessionResult<TurnOutcome>>      RerollLastAsync(ct);
    Task<SessionResult<RefreshReport>>    UpdateStateAsync(ct);
    Task<SessionResult<ValidationOutcome>> AuthorAsync(deltas, ct);
    Task<SessionResult<EditReport>>       EditAsync(Action<WorldState> edit, ct);
}
```

**`ReExtractLastAsync` and `RerollLastAsync` take no turn argument.** "The last turn" is a session
concept; both clients currently load history themselves to find it, which is session work sitting
in a client.

**One refusal concept.** `SessionResult<T>` carries a value or a reason, mirroring the existing
`RerollOutcome.Refused` shape. Reroll's own refusals ("that turn moved canon") fold into it, so a
caller has one thing to check rather than two kinds of no.

## Build

- [x] `Core/SessionResult.cs` — value or refusal, one concept
- [x] `Core/StorySession.cs` — owns `WorldState`, the lock, and the guard
- [x] Six operations, all behind one `SemaphoreSlim(1,1)`, refusing when held
- [x] `EditAsync` runs the mutation inside the guard, then checks, then saves
- [x] `UpdateStateAsync` swaps the world **inside** the guard — the race the design describes
- [x] `PlaySession` becomes a caller: rendering and prompting only, no ownership
- [x] The two `static` fields go
- [x] `AuthoringCommands` calls the session rather than committing on its own

## Self-tests

- [x] A second operation is refused while one is in flight, and says why
- [x] The guard is released after a failure, not just after success
- [x] **The design's exact race:** an update cannot land during a turn, and the turn completes
      with its own changes intact. The second half originally read *"and a turn does not
      overwrite an update"* — **testing disproved that**, and the test now pins the real
      behaviour instead. See below.
- [x] `EditAsync` persists the edit and returns the check warnings
- [x] `EditAsync` on a world it makes invalid still saves — reported, never refused
- [x] `AuthorAsync` with nothing acceptable writes nothing
- [x] Reroll refusing for its own reason and for busy are both `SessionResult` refusals
- [x] `ReExtractLastAsync` with no history refuses rather than throwing
- [x] Disposing the session releases the save lock

## Verify

- [x] `dotnet build` clean, 0 warnings
- [x] All existing self-tests pass
- [x] By hand: `/place` authored through the session and persisted, `/reload` reported no
      change, `/retry` and `/reroll` refused with the new unified wording, and **one real turn**
      narrated and saved with the authored place intact
- [x] By hand: a second session on the same save was refused and named the holder; the lock file
      was gone after a clean exit

## Close out

- [x] Devlog, `CANON_OWNERSHIP.md` updated to say what was built, `TODO_FUTURE_WORK.md`,
      `PROJECT.md` layer table, no unchecked boxes

## Found while testing: what the guard does not do

The race test initially asserted that an update refused mid-turn would be picked up once the
turn finished. **It failed, and the assertion was wrong rather than the code.**

A turn saves the session's canon at the end, so it overwrites the file the external edit was made
in. The edit is gone before any later update can read it. **The guard prevents canon being
half-updated; it does not preserve an edit made while a turn is running** — which is the same
consequence as editing without `/reload` at all, and already documented as such.

Refusing is still better than the alternative it replaced, where the update appeared to succeed
and was then silently discarded by the turn. But it is a limit, not a fix, and the test now
asserts it explicitly so it stays a known trade.

The thing that would actually close it — the turn noticing the file changed under it before
writing — is file-watching's neighbour, and `PROJECT.md` §3 rejects that class of solution.
Logged in `CHALLENGES.md` rather than built.

## Known limit, stated rather than discovered later

**`World` is a mutable graph and nothing at the type level stops a client writing to it behind
the guard's back.** The convention is that reads go through `World` and writes go through
`AuthorAsync` or `EditAsync`. Enforcing that in types means an immutable projection of the entity
graph, which contradicts `WorldState`'s "mutable by design" rationale and is its own decision
with its own cost. Recorded here so it is a known trade rather than an oversight.

## Not in this task

- **The two-tier delta set.** `CANON_OWNERSHIP.md` §5 marks it explicitly undecided, and it has a
  real cost — new kinds, new applier branches, and a guard so extraction cannot emit an
  off-schema kind. The hatch works without it, and using the hatch is what will say which kinds
  are actually wanted.
- **Session lifecycle.** Who constructs a session — resume vs. fresh, pack drift, who authors the
  player — is the next task. This one takes the lock as a parameter precisely so that question
  stays open rather than being answered badly here.
- **The authoring warning.** It belongs to whichever client offers the hatch, and the CLI does not
  offer it yet.
