# Canon workflows and player discovery documentation

2026-09-09 16:37:55 Australia/Brisbane.

The player requested committing and pushing all current documentation only.

## Included

- Approved Add/Remove design, completed design/implementation task records and
  desktop manual review instructions.
- Player discovery proposal: Player/Author views, remembered observations,
  explicit discovery rules, initial knowledge, compatibility and author corrections.
- Clarification that there is no private LLM movement channel or offscreen simulation;
  an explicit NPC departure without an established destination needs nullable
  movement support, distinct from losing sight within the same location.
- Required discovery/departure eval plan covering actual canon, player-visible
  information, forbidden disclosures and existing extraction regressions.
- Updated challenges, Play/editor design cross-references and future-work tracking.

## Status and validation

The Add/Remove implementation exists locally and has passed a Release build with
zero warnings/errors. Its application source is intentionally excluded from this
documentation commit and remains uncommitted. The implementation status statements
in the included docs describe that local work; manual review is still pending.

Discovery remains a proposal. Its code, pack changes and eval fixtures are not
implemented, and no model evals or runtime tests have been run. Documentation
whitespace validation passes. No new build is needed for this docs-only commit.
