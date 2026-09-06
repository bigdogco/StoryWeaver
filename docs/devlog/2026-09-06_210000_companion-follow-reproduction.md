# Companion not moved in canon — reproduction and log

While looking at the UI, the `uno-spike` save showed Mona as if she were in the tavern.
Investigation showed the opposite of a UI bug: canon correctly has her in Marrow Square, and the
UI renders canon faithfully. The narrator improvised her into the tavern beside the player on
turns 3 and 5 (she is a companion at standing 100), and extraction emitted only `player_moved`
on those turns — no `character_moved` for her. Canon and prose diverge on her location.

It is a pure omission: `character_moved` already exists in the schema and the applier derives the
connection edge, so nothing structural was missing. The narrator was handed a correct `##
Present` roster that did not list Mona and improvised a companion who should follow — the right
instinct with no delta to record it and no rule telling extraction to reconcile it.

## Decision

The fix belongs in **extraction reconciliation**, not a deterministic apply-time follow: whether
a companion follows is a *choice* visible only in the prose (she could be told to wait), so it
cannot be derived like `Connect`. The narration is the authority; canon must learn it via
`character_moved`. Prompt edits are **held** behind PROJECT.md §3 (reproduce in a scenario **and**
a second live sighting first).

## This commit — reproduction and log only, no engine/prompt change

- `WorldSeeds.Marrow_WithCompanion()` — player and Mona together in the square, one move from the
  tavern.
- `companion-follows` diagnostic in `EvalScenarios.Diagnostics` — the prose walks them both into
  the Drowned Crow; scored on the outcome (Mona ending the turn in the tavern), the two
  workaround introductions forbidden. Expected to fail today.
- `CHALLENGES.md` — new Open entry with the mechanism, the fix-in-extraction reasoning, the
  detection tell, and the second-sighting threshold.
- `docs/todo/TODO_COMPANION_FOLLOW_.md` — task doc; mirrored to the mysite StoryWeaver project.

`dotnet build` clean (0 warnings, 0 errors). The eval sweep itself is run manually.
