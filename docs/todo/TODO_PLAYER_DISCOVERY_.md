# Player discovery implementation

Approved 2026-09-09: implement PLAYER_DISCOVERY.md and its required eval plan in full.
Older-save support is best-effort; discovery changes block Reroll. No snapshot undo.

- [x] Capture provider-labelled existing extraction baseline before schema changes.
- [x] Implement versioned discovery, presentation rules, safe Core projections and persistence.
- [x] Implement typed observations, evidence/reference validation, retry protection and NPC departures.
- [x] Separate narrator context; update extraction schema/prompts and author bundled starting knowledge.
- [x] Implement Player/Author switching, safe diagnostics/navigation and guarded author controls.
- [x] Implement discovery eval fixtures; run repeated provider-labelled discovery and regression evals.
- [x] Build, review implementation and document manual acceptance and measured limitations.
- [x] Update standing design/status, challenges and backlog.

- [x] Repair the extraction regression the v5 discovery prompt caused; re-run both suites.
- [x] Fix report-over-sighting labelling, player-facing reference resolution and the
      case-sensitive item comparison found reviewing the implementation.

Implementation and evaluation complete 2026-09-10. Prompt v8 `c157d908`: discovery
70/78 clean at 98% required, regression restored to 30/30 (29/30 and 28/30 on repeat
runs, within the suite's measured noise floor). The v5 regression drop was a prompt
regression introduced by this work, not a model limitation; the earlier devlog is
corrected in place. Model reliability follow-up, raising eval runs above three, prompt
consolidation and manual acceptance remain explicitly in TODO_FUTURE_WORK.md; these
results are not an all-pass certification.

No commit or push without the player's request. Desktop review remains manual.
