# TODO: Backlog sweep

**Status:** DONE 2026-08-13
**Created:** 2026-08-13

Retire the residue. `CLAUDE.md` now says a task doc is only done when it has no open boxes;
this is the one-off pass that makes that true of the docs written before the rule existed.

No code. Docs only.

---

## The problem

**42 unchecked items are stranded across 12 finished task docs.** Nothing aggregates them, so
"what is left" has no answer anywhere. That is what made three weeks of work feel like
circling.

A second problem found while triaging: **`TODO_FUTURE_WORK.md` is not a queue.** It is 46KB
holding three different kinds of content mixed together — actual backlog items, design
reasoning, and *practice rules* ("always read the per-provider breakdown", "score outcomes,
not deltas"). The rules are the important ones and they are the hardest to find, because they
are filed among things nobody is going to do.

---

## Triage of the 42

### Strike — already done (7)

Verified by later work, never ticked.

| item | why |
|---|---|
| `JSON_STORAGE` — manual verify persistence | seven saves exist and resume |
| `NARRATION_MEMORY` — manual verify resume | done in the 51-turn session |
| `CHARACTER_SHEETS` — build clean + self-tests | shipped 2026-08-06 |
| `CHARACTER_SHEETS` — full scored sweep, pinned | run many times since |
| `CHARACTER_SHEETS` — play session with sheets | marrow ships sheets; sessions played |
| `SECOND_PACK` — a short session on `ashfall` | two model-played runs happened |
| `WORLD_PACKS` — answer the four §8 decisions | settled by what shipped |

### Strike — not a task (4)

Statements and notes that were written as checkboxes. Striking them loses nothing; the text
stays where it is.

- `PLAY_51_FIXES` — "the 51-turn save is permanently provider-unknown". True, and nothing to do.
- `ITEM_PLACEMENT` — "note the confound before anyone acts on it". Noting it *was* the action.
- `WORLD_PACKS` — "`/knows` is now redundant for authoring". An observation.
- `CHARACTER_RENAME` — run `/rename` on `figure-in-cistern` in `saves/marrow`. A repair to an
  old save superseded by later worlds. Won't do.

### Move to FUTURE_WORK, tagged with the phase that owns them (10)

These are real, and PROJECT.md already says when they happen. Tagging is what stops them
being rediscovered by feel a third time.

**Phase 1 — story layer**
- `world.json` manifest, with a version a save can record
- Opening message, and the loader check that every name in it exists in the seed
- Per-pack narration prompt overrides

**Phase 1 — narration eval**
- The four `NARRATION_EVAL` open questions and its two build items (lore-knowledge check, judge model)
- Measure whether a character refuses to reference lore they have not heard of
- Does the narrator actually use sheet detail, or only the one-line description?

**Phase 2 — UI**
- Multiple saves per pack
- Pack installing / sharing
- Surface rejected deltas *prominently* — the last open box in `TODO_BOOTSTRAP`

### Move to FUTURE_WORK as unscheduled backlog (11)

- `/item` authoring, matching `/place` and `/character`
- `character_described` / `location_described`
- The knowledge-worthiness test — decided, deliberately unwritten, revisit if it reappears
- Agreements as commitments — canon cannot record a promise that gets broken
- Context size with a full cast of sheets — third contributor to budgeting, still unmeasured
- Record narration's provider — gated on a narration eval existing
- Pack root as an explicit parameter, not the cwd
- `saves/` root configured rather than cwd-relative
- Region/world-level status — carrying its stated threshold, not as a hunch
- Lore keyword matching · token budget · report what fired — **merge into the existing
  "Lorebook retrieval layer" item rather than adding three duplicates**

### New section — "Pending a session" (5)

Real measurements, blocked on a play session rather than on a decision. They are invisible
today, which means each session ends without them being run.

- Re-run the fact audit against a fresh **human** session and compare the category split
- Re-run the fact audit against a third session — the object-fact share should fall
- Redundant facts alongside a correct `fact_learned` — deliberately not chased with a second prompt rule
- Does an id ever reach the prose through `{{ }}`? Verify rather than assume
- Full scored set re-run, provider pinned

---

## Proposed restructure of FUTURE_WORK

Three kinds of content, three destinations:

| kind | example | goes to |
|---|---|---|
| actionable, unscheduled | "`/item` authoring" | **stays** in FUTURE_WORK |
| settled practice rule | "always read the per-provider breakdown" | `PROJECT.md` §3 if not already there, else `CHALLENGES.md` |
| design reasoning | the four long-term-memory approaches | `docs/design/` |

FUTURE_WORK then becomes what its name claims: a list of things that could be done next,
readable in one sitting.

**This is the part that needs approval** — it moves text between documents, and it is a
judgement call whether the reasoning is better preserved beside the item or in its own design
doc.

---

## Tasks

- [x] Strike the 7 done and the 4 not-a-task items, each with a one-line reason
- [x] Move the 10 phase-owned items into FUTURE_WORK, tagged by phase
- [x] Move the 11 unscheduled items, merging the three lore ones into the existing entry
- [x] Add the "Pending a session" section with its 5 measurements
- [x] Restructure FUTURE_WORK per the table above — approved and done. Narrower than proposed: see the note below.
- [x] Verify: zero unchecked boxes remain in any finished task doc
- [x] Devlog — `2026-08-13_the-backlog-sweep.md`

## Definition of done

`grep -c '^\s*- \[ \]'` returns 0 for every `TODO_*.md` except `TODO_FUTURE_WORK.md` and any
task doc actually in flight. Every item that survived is findable in one place.

---

## What the restructure actually did — narrower than proposed

Reading `TODO_FUTURE_WORK.md` end to end changed the plan. Most of its reasoning is *better*
beside its item than in a separate document: the player-authored-canon entry, the reroll
entry and the domain-model gaps each carry the measurement that justifies them, and splitting
those would leave a bare line nobody could evaluate. Only three moves were clearly right.

**Two design docs extracted**, both because the reasoning had outgrown a backlog entry:

- `design/LONG_TERM_MEMORY.md` — four approaches that were four checkboxes but are really one
  decision. Now one item, and it notes that the whole line of work is gated on the 200-turn
  measurement rather than on a choice between the four.
- `design/DICE_CHECKS.md` — the largest single entry in the file, and now tagged `[Phase 3]`,
  since under the base/plugin split dice are not part of the base game at all.

**Five rules moved to `CHALLENGES.md`.** The "keep it honest" list under the extraction eval
was mostly not tasks: *re-run before trusting a change*, *read the per-provider breakdown*,
*sample size is per provider*, *world size is a variable*, *n=7 is not enough*. Every one was
paid for in a wrong conclusion, and every one had sat as an unchecked box for weeks — which is
where a rule goes to be forgotten. The three architectural siblings were already lifted into
`PROJECT.md` §3 when that doc was written.

Only one item in that section was a real task and it stayed: `two-stage-entry-large` failing
at 10/14.

**One item was un-checkboxed rather than moved.** The lore-entry entry was marked `[ ]` while
the feature shipped 2026-07-24; the reasoning under it is history, not a plan.
