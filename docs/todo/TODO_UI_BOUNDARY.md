# TODO: The UI boundary, and pulling authoring policy into Core

**Status:** built 2026-09-02 — one box open, awaiting the player's pass by hand
**Created:** 2026-09-02

First piece of **Phase 2**, and deliberately not a line of Avalonia. The question that prompted
it, from the player:

> If we decide that Avalonia UI is no good, changing UI will be simply rewriting UI part, not
> touching the core engine under it.

Worth settling before a window exists, because the answer is cheap now and expensive later.

---

## What the audit found

**The turn loop is already safe.** `TurnEngine` exposes `RunTurnAsync`, `ReExtractAsync`,
`RerollAsync` and knows nothing about a console; `INarrator` and `IStateExtractor` are Core's own
vocabulary rather than a provider SDK's. `PlaySession` is 740 lines of `Console.ReadLine` and
banner printing around those three calls. Throwing away Avalonia would not touch Core, Llm or
Storage.

**Authoring is not.** `AuthoringCommands.cs` is 388 lines in which the rules and the prompting
are welded together:

- `CommitAsync` is validate-as-authored → report → apply → save, with `Console.WriteLine`
  threaded through the middle of it.
- `Slug()` is the id convention — apostrophes dropped rather than separated, four words max.
- `AskId` carries the collision rule and the reason it is checked early.
- `Summarize` is the vocabulary for what a delta did.

A UI written today reimplements all four, and the two copies drift. That is the same failure the
player was trying to avoid, one layer below where they were looking.

## The distinction that settles it

Two things were merged in the question, and only one of them is worth committing to.

**Locked: the UI is a thin layer, not a driver.** No gameplay or narration logic lives in a UI
project; a UI collects input, calls Core, and renders what comes back.

**Rejected: CLI/UI feature parity.** "Everything the UI can do can be done through a
`/command`" is a tax on every future feature, and it is paid in the wrong currency. Dragging a
character onto a location has no honest slash-command form — the attempt produces `/place`,
which is precisely the interface that made a sound design read as a bug on 2026-08-06.

**The CLI is not the API — Core is.** The CLI is the first client and is allowed to be a worse
one.

---

## Build

- [x] `PROJECT.md` §3: lock the thin-layer rule, and record the parity rejection with its reason
- [x] `Core/Authoring.cs` — the policy, with no `Console` in it:
      - [x] `Slug`, moved verbatim, comments intact
      - [x] `IdConflict(world, id)` — the early collision check, returning a reason not a bool
      - [x] delta builders for place, character, fact, rename, knows
      - [x] `CommitAsync` returning `ValidationOutcome`, saving only when something was accepted
      - [x] `Summarize`
- [x] `AuthoringCommands.cs` reduced to prompting and printing
- [x] Nothing in the CLI constructs an authoring delta or calls `DeltaApplier` directly

## Self-tests

- [x] `Slug` output always satisfies `EntityId.IsWellFormed` — the two rules are the same rule
      in two projects, and this is what ties them without moving either
- [x] `IdConflict` fires across all three namespaces — characters, locations, facts
- [x] `CommitAsync` with nothing acceptable leaves canon untouched and does not save
- [x] A fact authored without player knowledge produces one delta, with it produces two

## Verify

- [x] `dotnet build` clean, 0 warnings
- [x] Existing self-tests still pass
- [ ] `/place`, `/character`, `/fact`, `/rename`, `/knows` behave identically by hand
      **— awaiting the player; manual testing is theirs and this refactor is exactly the kind
      that a build cannot vouch for.**

## Close out

- [x] Devlog, `TODO_FUTURE_WORK.md` updated, no unchecked boxes

---

## Known duplication, stated rather than discovered later

`EntityId.IsWellFormed` lives in **Storage**; `Slug` now lives in **Core**. They are two halves
of one convention — Slug's output must always satisfy IsWellFormed — and dependencies point
inward, so Core cannot reference the check.

Not resolved here. Moving `EntityId` to Core is a structural change and needs its own decision.
The self-test above is the cheap bridge: if the two ever disagree, a test fails rather than a
save quietly acquiring an id nothing else can match.

## Not in this task

- **Any Avalonia code.** This is the groundwork that makes the first window cheap to throw away.
- **Pack creation, lore writing, the location graph.** The CLI cannot do these at all, and under
  the rejected-parity rule it never needs to. They are UI features calling Core services that do
  not exist yet, and they wait for the phase proper.
