# 2026-08-14 — Two engines, one save

A 250-turn `ashfall` run was reported as "not very good." The cause was mechanical, not
aesthetic: **two CLI instances were playing the same save at once.**

## The diagnosis

There is a 15.5-minute gap at turn 151 — a restart. From that point every turn number appears
twice in `history.jsonl`:

```
line 150  t151  00:24:18  *I slog toward the half-buried milestone...*
line 151  t152  00:24:59  *I dig around its base for anything metal...*
line 152  t152  00:25:05  *I brace on the half-buried milestone and keep moving...*
line 153  t153  00:25:45  *I dig in the moving cinder for anything solid...*
line 154  t153  00:25:46  *I pocket the first thing I find and throw the second away.*
```

72 duplicated turn numbers, 323 lines for 250 turns. The pairs sit adjacent, median 23 seconds
apart, while consecutive *pairs* are ~40 seconds apart — two processes each taking about forty
seconds a turn, offset by a few seconds. Different player input, different narration, different
deltas, same number: two processes each loaded canon at N and both wrote N+1, overwriting each
other every turn for a hundred turns.

So it is not a 250-turn run. It is two ~125-turn sessions interleaved, each playing against a
world the other kept clobbering — which is exactly what "not very good" feels like from inside
the story.

**Nothing errored.** The run completed, the log had the right shape. It surfaced because the
prose felt wrong and someone read the timestamps.

## Why this got code rather than "don't do that"

The silence is the argument. A loud failure gets avoided twice; a silent one costs an entire
long run, and long runs are now the main way this project learns anything. It will also recur —
three agent-driven sessions so far, and "launch the CLI" is precisely the step an agent repeats
without noticing.

Worth being explicit about the standing rule this appears to break. *Do not build for a gap
until it reproduces in a scenario and appears in a second session* is about **model behaviour**
observed in play, where the danger is chasing noise. This is a deterministic engineering hazard
with a confirmed mechanism. One sighting is enough when you can point at the cause.

It also got more important yesterday: canon is now a file the player is expected to open and
edit, and the UI is getting an **Update State** button that re-reads from disk. "Who else has
this open" became a real question.

## What it cost, stated plainly

The 200-turn question in `PROJECT.md` is **still open.** This run cannot answer it, and mining
a contaminated save for rates would be worse than admitting that.

One result survives, because it lives in canon rather than in rates: **the connections fix
works.** 15 locations, 9 with exits, against 2 of 9 before. Five of the six remaining orphans
were introduced but never walked into — correct by design, since canon records passages
somebody used.

The sixth is itself a fingerprint of the corruption. `tight-downward-slope` was entered at t191
via `player_moved` and ended with no edges, which the derivation rule cannot produce. One
session recorded it; the other saved canon without it. A lost write.

That is worth keeping as a detection recipe: **duplicate `turnNumber`s in the history, plus a
lost write that should be impossible.**

## The guard

`SaveLock` in `StoryWeaver.Storage`: a `.session.lock` per save recording process id, process
start time, machine and open time.

- **Refused outright** when the holder is alive. Read-only was considered and rejected — a
  second session that cannot write is one whose narration silently diverges from the save,
  which is a subtler version of the same confusion.
- **Stale locks are taken silently.** A crash must never brick a world. Process id alone would
  not do: ids get reused, so a stale lock could name a live unrelated process and refuse
  forever. Start time is what makes "is this still that session" answerable.
- **`--force`** for the day the detection is wrong, printing what it steps on.
- **Deliberately not on `IWorldRepository`.** The turn engine has no business knowing about
  locks; a lock belongs to a *session*, which has a beginning and an end, and a repository does
  not.
- Acquired before the API client is built, so a refusal costs nothing.

**Not a guard against hand-editing.** A text editor takes no lock, and canon is meant to be
edited — the Update State button is the answer there. This stops two *engines* writing, which
is the case where both believe they are authoritative.

## On testing the thing that actually failed

The refusal self-test spawns a **real child process** and writes a lock naming it, rather than
forging a lock file with invented contents. The entire mechanism is "is that other session
still running" — a test that fakes the other session tests the parts that did not fail.

Four checks: a live holder is refused and named, a dead holder is taken, `--force` overrides a
live holder, and releasing makes the save available again. That last one matters more than it
looks: without it the guard would be worse than no guard, since every clean quit would leave a
world only `--force` could reopen.

Then verified end to end with two real CLI processes — the second refuses with exit 1 and names
the holder; both a clean `/quit` and a closed stdin release; a third session reopens.

## A wrong turn worth recording

The first end-to-end test appeared to show the lock surviving a session exit. I was one step
from writing "release is broken" when I checked instead.

It was not broken. The test used the `ashfall` pack, which ships no `player.md`, so it prompted
for a character name; with stdin at EOF `Console.ReadLine()` returns null immediately and the
loop reprinted *"A name is required"* forever. The session never exited, and was holding its
lock entirely correctly.

Two things out of that. The same pattern as the 150-turn run two days ago — *the thing that
looks broken is often reporting accurately about something else* — and a real robustness bug
now in `TODO_FUTURE_WORK`: **character creation spins forever on closed stdin.** Harmless
interactively; a hang for any piped or agent-driven run against a blank-slate pack, which is
now a normal way this project gets exercised.

## Build

`dotnet build` clean, 0 warnings. All self-tests pass.
