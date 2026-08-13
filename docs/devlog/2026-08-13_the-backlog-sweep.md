# 2026-08-13 — The backlog sweep

No code. The second half of the morning's work: `CLAUDE.md` gained a rule that a task doc is
only done when it has no open boxes, and this is the one-off pass that makes it true of
everything written before the rule existed.

## The count

**42 unchecked items across 12 finished task docs**, plus 47 in `TODO_FUTURE_WORK`. Nothing
aggregated them, so "what is left" had no answer anywhere — which is the mechanical reason
three weeks of work felt like circling, and why one item on this morning's list of "basics we
never went through" turned out to be an unbuilt part of a design doc from 2026-07-23.

Disposition:

| | count |
|---|---|
| already done, never ticked | 7 |
| not a task — a statement written as a checkbox | 4 |
| moved to FUTURE_WORK, tagged with the phase that owns it | 10 |
| moved to FUTURE_WORK, unscheduled | 11 |
| moved to a new *Pending a session* section | 5 |
| dropped — repair to a superseded save | 1 |

Every finished task doc now greps clean.

## The four kinds of thing that were mixed together

Triage was easy for 30 of the 42 and interesting for the rest, because sorting them exposed
that a checkbox had been doing four different jobs:

1. **A task.** Fine.
2. **A measurement blocked on a session**, not on a decision. Five of these, and they were
   invisible — which is exactly why every session so far has ended without any of them being
   run. They now have their own section, which matters more with a 200-turn run about to
   happen.
3. **A rule that is true every time.** *Read the per-provider breakdown. n=7 is not enough.*
   These will never be checked off, and filing them among things nobody intends to build is
   where a rule goes to be forgotten. Moved to `CHALLENGES.md`.
4. **A statement of fact.** "The 51-turn save is permanently provider-unknown." True. Nothing
   to do. Four of these, struck.

Only the first kind belongs in a queue. That distinction is the actual finding here, and it is
worth more than the tidier docs.

## The restructure was narrower than proposed

The plan approved this morning was to split FUTURE_WORK three ways: items stay, rules leave,
reasoning goes to `docs/design/`. Reading all 750 lines changed that.

**Most of the reasoning is better beside its item.** The player-authored-canon entry carries
the measurement that justifies it (`player-place` 0/7 against `player-arrival` 14/14). The
reroll entry carries why deltas are not invertible. The domain-model gaps carry the play
sessions they were found in. Split those out and what remains is a bare line nobody can
evaluate — the reasoning *is* the item.

So only three moves were made:

- **`design/LONG_TERM_MEMORY.md`** — four approaches that were four checkboxes but are really
  one decision. Writing it up surfaced something worth saying out loud: *StoryWeaver already
  is* the structured-state approach. Canon is the structured state, extraction is the second
  model call. What is missing is applying it to **events** rather than entities. Also now
  states plainly that the whole line is gated on the 200-turn measurement — if canon holds,
  most of it is unnecessary, and building first would be solving a problem never observed.
- **`design/DICE_CHECKS.md`** — the largest entry in the file, and under this morning's
  base/plugin split it is not part of the base game at all. Tagged `[Phase 3]`: it is the
  archetypal plugin, and it cannot be designed before Phase 3 says what a plugin is.
- **Five rules to `CHALLENGES.md`**, each paid for in a wrong conclusion. Their three
  architectural siblings were already lifted into `PROJECT.md` §3.

One item was simply un-checkboxed: the lore-entry entry sat as `[ ]` while the feature shipped
2026-07-24. The text under it is history, not a plan.

## Phase tags

The ten phase-owned items now carry `[Phase 1]` / `[Phase 2]` / `[Phase 3]`. Three of them are
the unbuilt pack components — `world.json`, `opening.md`, prompt overrides — which is the
whole reason this sweep was worth doing rather than deferring: they were rediscovered by feel
three weeks late once already.

## Build

No code touched. `dotnet build` unchanged and clean.
