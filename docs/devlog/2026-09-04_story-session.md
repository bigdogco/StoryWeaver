# 2026-09-04 — Core owns canon

Implements `design/CANON_OWNERSHIP.md` §4. Still no Avalonia, and the window is still a long way
off — this is worth having regardless of when it arrives.

## What was wrong

Canon was owned by a local variable in the console's play loop. Nothing else held a
`WorldState`: the repository is stateless and `TurnEngine` takes the world per call. So the
domain's central object belonged to a UI client, which contradicts the thin-layer rule locked two
days ago — on the *object* rather than on the rules.

And it left a real hazard with nowhere to live. A turn reads canon, awaits narration and
extraction, and only then mutates and saves: twenty to sixty seconds during which nothing owns
the thing being changed. Press Update State in that window and the reload swaps a reference the
in-flight turn is not holding, so the turn writes pre-edit canon back over it.

**You cannot enforce one-writer-at-a-time without something that owns the thing being written.**
That was the finding, and it is why the concurrency question and the ownership question turned
out to be one question.

## What was built

`StorySession` in Core: six operations behind one guard, taken without waiting so a second
operation is refused rather than queued. `SaveLock` already decided that posture one level out —
two engines on one save corrupted a 250-turn run silently, and the answer was to refuse.

Three things fell out of it that were not the point but are worth more than they cost:

**One refusal concept.** `SessionResult<T>` carries a value or a reason. Reroll's own refusal, an
empty history, and "something else is running" were previously a record field, a caller-side
check, and nothing at all. A caller now has one kind of no.

**Two operations lost their arguments.** `ReExtractLastAsync` and `RerollLastAsync` take no turn.
*The last turn* is a session concept and both clients were loading history to find it — and doing
so **outside** any guard, which is the same shape of bug one level down. It is now read inside.

**The statics are gone.** `_packId` and `_saveId` were static fields with a comment admitting a
UI would want them as parameters. Two playthroughs in one process are now possible, which is what
made *multiple saves per pack* awkward.

## The design's open questions, answered rather than deferred

I initially proposed a "deliberately narrow" version of this that left all three open. That was
wrong, and it is now a rule in `CLAUDE.md`.

**The session owns the save lock.** *"This save is mine for now"* and *"I hold canon for this
save"* are one lifetime. `SaveLock` stays in Storage; the session takes an acquired
`IDisposable`, so Core gains the ownership without learning the mechanism is a file.

That introduced a bug I nearly shipped: dropping the console's `using` meant an exception between
acquiring the lock and constructing the session — `WorldPack.Load` throws on malformed content,
and `Program.cs` handles that as ordinary user error — would leak the lock. The console keeps its
own `using`; `SaveLock.Dispose` is documented idempotent, so the second call is a no-op.

**`World` stays mutable and the convention is labelled rather than enforced.** An immutable
projection contradicts `WorldState`'s own design and is its own decision.

## The test that failed, and was right to

The race test asserted that an update refused mid-turn would be picked up once the turn finished.
It failed — and **the assertion was wrong, not the code**.

A turn saves the session's canon at the end, overwriting the file the external edit was made in.
The edit is gone before any later update can read it. **The guard prevents canon being
half-updated; it does not preserve an edit made while a turn is running** — the same consequence
as editing without asking for an update at all.

Still better than what it replaced, where the update appeared to succeed and was then silently
discarded. But it is a limit rather than a fix, and closing it properly means the turn noticing
the file changed underneath it, which is the file-watching family §3 rejects. The test now
asserts the real behaviour so it stays a known trade, and it is in `CHALLENGES.md` with a
trigger: revisit if someone actually loses an edit this way.

**A test whose failure teaches you the feature's boundary is worth more than one that passes.**

## Measurements

`dotnet build` clean, 0 warnings. Self-tests **116 pass, 0 fail** — up from 108, eight new ones
on the session. The guard needed a narrator that blocks on demand, because a real turn's window
cannot be entered deliberately and a fake that returns instantly never overlaps with anything.

By hand: `/place` authored through the session and persisted, `/reload` reported no change,
`/retry` and `/reroll` refused with the unified wording, one real turn narrated and saved with the
authored place intact, a second session on the same save refused and named the holder, and the
lock file gone after a clean exit. Two API calls.
