# TODO — narration eval

Item #3 off the post-lore list. Opened because narration has no automated quality control at
all, and the lore feature just added a rule nothing verifies.

Design/audit: [`docs/design/NARRATION_EVAL.md`](../design/NARRATION_EVAL.md).

---

## Audit

- [x] Run every mechanically checkable property against all 51 turns of play
- [x] Separate real violations from false positives
- [x] Work out which of the interesting properties need a judge model

**Result: everything mechanically checkable already passes.** 0 id leaks, 0 verbatim
repetition, stable length, 0 real name-before-canon violations, 1 fact-without-learning which
on inspection is correct behaviour.

Notable: the id leak that caused the `ForNarration`/`ForExtraction` split has **zero
recurrences in 51 turns**. First time that fix has been measured rather than assumed.

## Decisions needed

- [x] Build the lore-knowledge check now? (design argues yes) — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]
- [x] Design a judge now, or defer until dice needs one? — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]
- [x] If a judge happens — who produces the hand-labelled narration it must be scored against? — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]

## Build — if approved

- [x] **Lore-knowledge check.** Does a character reference a lore topic they have not heard
      of? Deterministic: match entry `keys` against quoted speech, scoped to speakers who lack
      the entry in `Knows`. Narrow, misses paraphrase, costs nothing, and tests the one rule
      this codebase added with no way to check it — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]

## Deferred, with reasoning

- [x] **A judge model.** Everything else worth checking is semantic — did a character
      reference lore they lack, did prose reveal an unlearned fact, did the narrator contradict
      canon or speak for the player. All need a model to judge. — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1]

      Deferred because a judge is a second model whose variance we do not understand, grading
      a first whose variance we barely do — and this project has been wrong four times this
      year by attributing provider noise to its own code. It needs the same treatment as
      everything else: fixed cases, N runs, provider pinned, measured baseline.

      **And its own control: hand-labelled narration.** A judge never scored against known-good
      and known-bad prose is an opinion generator. Producing that set is most of the work and
      cannot be automated.

      Sequence it against something that needs it — the dice-checks idea rests on "did the
      narration contradict the roll?", which is a judge question — rather than on the general
      principle that prose ought to be measured.
