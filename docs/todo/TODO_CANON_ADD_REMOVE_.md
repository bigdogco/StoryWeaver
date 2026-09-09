# In-session Add/Remove implementation

Approved 2026-09-09: implement `docs/design/CANON_ADD_REMOVE_UI.md` in full.

- [x] Implement complete-form creation, ID/reference validation and detached catalogs in Core.
- [x] Implement typed removal plans, precise cascades and stale-preview protection in Core.
- [x] Connect tab actions, Add forms and Remove previews with session/failure protection.
- [x] Build and review changes; document manual review steps and challenges.
- [x] Update design and backlog with implementation status and manual review handoff.

Implementation complete 2026-09-09. Release build passes with zero warnings/errors;
diff whitespace review is clean. No runtime tests or model calls were run by the
agent. The player reported UI testing looked good on 2026-09-09 and requested
committing the implementation. No new projects, packages or save-format changes.

No commit or push until requested by the player.
