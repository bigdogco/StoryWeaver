# 2026-08-13 — A standing project doc, and two decisions that outlive it

No code. Two comment edits, a new doc, and a reversal of something bootstrap locked.

## Why

The session opened with "I feel like we are stuck in bootstrap for ages," and then "I lost
track of what we even doing." Both worth taking literally, so the first thing done was to
measure rather than agree.

Bootstrap did not stall. It closed 2026-07-23 with a definition of done and every box ticked
— 19 commits. The 45 commits since have **no phase doc, no exit question, and no definition
of done.** That is the whole of it. Nothing replaced bootstrap, so every session ended by
generating its own next task, forever.

Three numbers made the shape concrete:

- **89 unchecked items across 17 TODO docs.** `TODO_FUTURE_WORK` holds 47; the other 42 are
  stranded inside task docs marked finished. Nothing aggregates them, so "what is left" had
  no answer anywhere.
- **Six of seventeen delta kinds are inventory**, against a bootstrap decision that reads
  "strictly closed set of **nine**."
- **67% of the CLI is eval scaffolding** — `EvalScenarios` (1,694) + `LoreSelfTest` (1,308) +
  the rest. `PlaySession`, the actual game, is 647 lines.

The post-bootstrap loop was: play → find something prose can express that canon cannot → add
a delta kind → tune the prompt → measure → repeat. Every individual fix was real and honestly
measured. The loop has no termination condition, because prose can always express something
canon cannot. There is always another `item_lost`.

## The finding that stung

Of four things raised as "we never went through the basics," one — the opening message — is
an **unbuilt part of a design doc written 2026-07-23.** `WORLD_PACKS.md` specifies six pack
components; three shipped:

| designed | built |
|---|---|
| `seed.json`, `lore/*.md`, `characters/*.md` | yes |
| `world.json`, `opening.md`, `prompts/*.md` | no |

The backlog was re-derived by feel, three weeks later, because nothing surfaced it. That is
the circling, in one table.

The genuinely new item on that list was different and larger: **a pack describes a world, and
nothing describes a story.** No premise, no stakes, no ending. Marrow is a tavern with people
in it. Sessions stop at 50 turns because the player gets bored, not because anything
concludes. The opening message is the first paragraph of that missing layer, not a feature
beside it.

## What landed

`docs/PROJECT.md` — what the project is, its layers, decisions locked, and phases. The one
property carried over from the bootstrap doc, because it is the property that made bootstrap
work: **each phase closes exactly one question and states its definition of done.**

Phases: the story layer → Avalonia UI → plugins. Plus one open measurement that is not a
phase — *does canon survive 200 turns?* Bootstrap proved 51. Every save in the repo is 50–51.
Past that the binding constraint stops being the extractor and becomes `ContextAssembler`,
the one piece of the turn loop never under pressure. It also gates a whole line of future
work (summarization) that currently rests on an assumption.

Doc boundaries, written down so this does not recur: **phases live in PROJECT.md, items live
in FUTURE_WORK, nothing lives in both.**

## Two decisions locked

**The player owns their world and can edit it directly.** Canon and seed are plain JSON;
open them in any editor, change what you like, the next turn runs on what is there. The
framing that matters is the second one: this is not only a repair path for bad extraction, it
is a *roleplay* feature. Giving someone an item or fixing a character the model got wrong is
authorship. A single-player world has no one to cheat.

Two consequences. Validation becomes **on-demand rather than a gate** — `DeltaValidator`
exists to be suspicious of a cheap model that invents things, and a person editing their own
canon does not need to be argued with; same structural invariants, reported instead of
refused. And a running session holds canon in memory and would overwrite an external edit,
resolved as an **Update State** action in the UI: re-read from disk, run the invariants,
report. No file watching, no merge. An explicit button is the whole mechanism.

**Storage stays JSON, permanently.** This reverses a bootstrap decision that read "likely
hybrid: JSON canon + SQLite turn log, trigger = wanting full-text search over history." The
reason it dies: once the player edits saves by hand, **the save format is a user-facing
surface, not an implementation detail**, and a database hides the world from the person who
owns it. Full-text search is not worth that.

Human-readable diffable saves were chosen in bootstrap as "the best debugging tool for this
phase" — explicitly phase-scoped. They are now a permanent product constraint. Same files,
different status.

## Cleaning up after a reversed decision

Seven live places asserted the SQLite plan, two of them code comments that would have
outlived any memory of why it changed. All updated. Notable:

- `IWorldRepository` stayed storage-agnostic "so the swap is cheap." The abstraction is still
  right; its reason is now testability, and the comment names `InMemoryWorldRepository` as the
  second implementation it has to stay honest for.
- `TODO_NARRATION_MEMORY` recorded the O(n)-per-turn history read as something SQLite would
  fix one day. It now names the fix that actually exists: `LoadRecentTurnsAsync`, or holding
  the window in memory across a session.
- `TODO_BOOTSTRAP`'s decision table is **annotated as superseded, not rewritten.** One
  checked-off box still mentions the swap and was left alone. Editing completed history to
  agree with a later decision makes the record less trustworthy, not more.

## CLAUDE.md

A *Read first* section pointing at PROJECT.md, with the rule that a task contradicting a
locked decision is a conversation and not a task.

And the rule that addresses the 42 stranded items directly: **a task doc is only done when it
has no open boxes.** Each one either moves to FUTURE_WORK or is struck out with a reason.
This is the enforcement that was missing; nothing said what to do with residue, so residue
accumulated invisibly for three weeks.

## Carried forward

- The backlog sweep itself. The rule exists; the 89 items are not yet swept. Its own task.
- The 200-turn run, before Phase 1 lands, so it baselines the current engine.
- Two questions recorded unanswered under Phase 3, both real: plugins add delta kinds, which
  opens a set §3 locks closed (probable resolution — *closed at runtime, composed at load*);
  and inventory is already in Core, so it is either the first plugin, extracted to prove the
  API against a real case, or grandfathered into base.
- The risk that could kill the plugin phase, and is checkable before any API exists: a schema
  branch is not free. Today's set is 17; five plugins might mean forty. Generate a schema with
  twenty junk branches, re-run the scored set, and find out early.

## Build

`dotnet build` — 0 warnings, 0 errors. No behaviour changed; this commit is entirely about
knowing what we are doing next.
