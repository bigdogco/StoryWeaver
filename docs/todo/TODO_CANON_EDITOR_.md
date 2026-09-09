# Canon editor implementation

Approved 2026-09-09: implement all four forms in CANON_EDITOR_UI.md.

- [x] Core typed drafts, guarded capture/save, stale/missing checks and one-write corrections.
- [x] Desktop forms, searchable reference selection, common lore and authored sheets.
- [x] Edit entry points, Save/Cancel, warning/error handling and preserved play context.
- [x] Release build, documentation and manual review guide.

Structure: add correction types in Core and editor/picker controls in Desktop's existing
folders; no new projects, dependencies or save-format changes. Approved design governs
these additions. Automated verification is build only; runtime testing belongs to the player.

Implementation complete. Release build passed with zero warnings/errors. The player
reported manual testing looked good on 2026-09-09 and authorized commit and push.
