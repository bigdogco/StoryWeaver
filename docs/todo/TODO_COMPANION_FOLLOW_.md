# TODO — Companion not moved in canon (the "Mona in the tavern" bug)

**Opened:** 2026-09-06
**Trigger:** while looking at the UI, the `uno-spike` save showed Mona treated as being in the
tavern; investigation showed canon correctly has her in Marrow Square while the narration
repeatedly placed her in the tavern beside the player.

## What this is

A companion the narrator keeps beside the player is not moved in canon. When the player moved
between rooms, extraction emitted only `player_moved`; no `character_moved` for the companion.
Canon and prose diverge on the companion's location. Full write-up in
[`../CHALLENGES.md`](../CHALLENGES.md) → *A companion narrated beside the player is not moved in
canon*.

Not a UI bug — the UI faithfully renders canon. The gap is a narration/extraction omission, with
its root in there being no concept of a companion following the player.

## Decision (locked for this task)

- The fix belongs in **extraction reconciliation**, not a deterministic apply-time follow.
  Whether a companion follows is a *choice* visible only in the prose (she could be told to
  wait), so it cannot be derived like `Connect`; the narration is the authority and canon must
  learn it via a `character_moved`.
- Supported by a light `narration.md` clarification so narrator and extractor agree on presence.
- **Prompt edits are HELD** behind PROJECT.md §3: reproduce in a scenario **and** get a second
  live sighting first.

## Tasks

- [x] Investigate the save; confirm canon is right and the narration/extraction diverged
- [x] Read the delta schema, applier, and both prompts; confirm `character_moved` is capable and
      the failure is a pure omission
- [x] Decide the fix layer (extraction reconciliation over deterministic follow)
- [x] Add reproduction seed `WorldSeeds.Marrow_WithCompanion` (player + Mona in the square)
- [x] Add diagnostic scenario `companion-follows` to `EvalScenarios.Diagnostics`, scored on Mona
      ending the turn in the tavern
- [x] `dotnet build` — clean
- [x] Log the challenge with the second-sighting threshold
- [x] Create this task doc and mirror to mysite
- [x] Run the reproduction sweep and confirm it fails today.
      **2026-09-06, deepseek-v3.2 / StreamLake, n=7: Mona left behind 6/7** (`required 8/14`,
      forbidden 0.00; the player's own move lands 7/7). Clean omission — no workaround delta.
      Note: a first draft with explicit "walks with you" prose scored 14/14 clean; the subtle
      "discovered present" prose (matching save turn 5) is what reproduces. First half of the §3
      gate met.
- [ ] **(manual, player)** Watch for a second live sighting in a fresh session with a companion
- [ ] **(optional)** Re-run pinned across a second provider / a couple more sweeps for a firmer
      baseline before the fix, per the eval-honesty rules (one routed sweep is one data point)

## Held pending the threshold above (do NOT do yet)

- [ ] Extraction-reconciliation rule in `prompts/extraction.md`: a character already in canon,
      shown present with the player somewhere canon does not place them, has moved there — on the
      existing "where do they END the turn / present vs merely mentioned" discipline
- [ ] `prompts/narration.md` clarification: established companions may accompany the player
      between locations; a character shown in a scene is really there
- [ ] Re-run `companion-follows` (and the full scored set as a regression guard) pinned by
      provider; promote `companion-follows` into `EvalScenarios.All` only once the behaviour is
      settled and passing

## Notes

- Consider whether a non-companion NPC the prose relocates should reconcile the same way (the
  rule above is general and would cover it); the mirror guard is `PlayerAbsentCharacter` /
  `NarratorMention`, which must keep passing so absent/mentioned characters are not dragged in.
