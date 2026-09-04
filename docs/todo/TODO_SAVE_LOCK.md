# TODO: Save lock

**Status:** DONE 2026-08-14
**Created:** 2026-08-14

---

## The failure

A 250-turn `ashfall` run, 2026-08-14. The player reported it as "not very good"; the cause was
mechanical.

**Two CLI instances were playing the same save at once.** ChatGPT launched a second one after a
restart at turn 151, and from there every turn number appears twice in `history.jsonl`:

```
line 150  t151  00:24:18  *I slog toward the half-buried milestone...*
line 151  t152  00:24:59  *I dig around its base for anything metal...*
line 152  t152  00:25:05  *I brace on the half-buried milestone and keep moving...*
line 153  t153  00:25:45  *I dig in the moving cinder for anything solid...*
line 154  t153  00:25:46  *I pocket the first thing I find and throw the second away.*
```

72 duplicated turn numbers. Adjacent, median 23s apart, while consecutive *pairs* are ~40s
apart — two processes each loading canon at N, both writing N+1, each overwriting the other.
323 history lines for 250 turns.

**It is not a 250-turn run.** It is two ~125-turn sessions interleaved, each playing against a
world the other kept clobbering.

## Why this is worth code rather than "don't do that"

**The failure is completely silent.** Nothing errored, the run completed, the log looked the
right shape, and canon was quietly corrupted for a hundred turns. It surfaced only because the
prose felt wrong and someone went looking at timestamps.

That is the profile worth guarding. A loud failure you avoid twice; a silent one costs an
entire long run, and long runs are now the main way this project learns anything.

It will also recur — three agent-driven sessions so far, and "launch the CLI" is exactly the
step an agent repeats without noticing. A human double-clicking `play.ps1` gets the same
result.

**On the standing rule.** *Do not build for a gap until it reproduces in a scenario and appears
in a second session* is about model behaviour observed in play, where the risk is chasing
noise. This is a deterministic engineering hazard with a confirmed mechanism. One sighting is
enough when you can point at the cause.

**It also got more important yesterday.** Canon is now a file the player is expected to open
and edit, and the UI is getting an **Update State** button that re-reads from disk. "Who else
has this open" became a real question.

## It cost a real measurement

The 200-turn question in `PROJECT.md` is still open. This run cannot answer it, and mining a
contaminated save for numbers would be worse than admitting that.

One thing does survive, because it is visible in canon rather than in rates: **the connections
fix works.** 15 locations, 9 with exits, against 2 of 9 before. Five of the six remaining
orphans were introduced but never walked into, which is correct by design.

The sixth is itself evidence of the corruption: `tight-downward-slope` was entered at t191 via
`player_moved` and has no edge. Under the derivation that is impossible — one session recorded
it, the other saved canon without it. A lost write.

## Decisions

| question | decision |
|---|---|
| Refuse or allow read-only? | **Refuse outright.** A second session that cannot write is one whose narration silently diverges from the save — a subtler version of the same confusion. |
| Where does it live? | `StoryWeaver.Storage`, used by `PlaySession`. **Not on `IWorldRepository`** — the turn engine has no business knowing about locks, and it is the *session* that has a lifetime, not the repository. |
| Stale locks | The lock records process id and start time. A lock whose process is gone is stale and taken silently. A crash must never brick a save. |
| Override | `--force`, printing what it is stepping on. |
| `--eval`, `--selftest` | Untouched. Neither opens a save. |

**Process id alone is not enough** — ids are reused, so a stale lock could name a live unrelated
process and refuse forever. Recording start time as well makes "is this my process" answerable.

## Tasks

- [x] `SaveLock` in `StoryWeaver.Storage` — acquire, release, stale detection, `IDisposable`
- [x] `PlaySession` acquires after resolving the save id, before any API client is built, so a
      refusal costs nothing
- [x] `--force` flag, wired through `Program`
- [x] Refusal message names the holder and how to override
- [x] Self-tests, four of them. The refusal one spawns a **real** child process rather than
      forging a lock file — the whole mechanism is "is that other session still running", and a
      test that fakes the other session is not testing the thing that failed.
- [x] Verified end to end with two real CLI processes: the second refuses with exit 1 and names
      the holder; a clean `/quit` and a closed stdin both release; a third session reopens.
- [x] `dotnet build` clean, 0 warnings
- [x] Devlog `2026-08-14_two-engines-one-save.md`, `CHALLENGES.md`, `TODO_FUTURE_WORK.md`

## Out of scope

- Locking against hand-editing. A text editor holds no lock, and the **Update State** button is
  the answer there.
- Multi-machine or network shares. `saves/` is local.

## Found while building this

**Character creation spins forever on closed stdin.** A first test used the `ashfall` pack,
which ships no `player.md`, so it prompted for a name; with stdin at EOF the loop reprinted
*"A name is required"* without end and the session never exited — still holding its lock,
entirely correctly. I read that as a release bug in `SaveLock` before checking, and it was not.

Logged in `TODO_FUTURE_WORK.md`. It matters more than it looks: agent-driven runs are now a
normal way this project is exercised, and a blank-slate pack with piped input hangs.
