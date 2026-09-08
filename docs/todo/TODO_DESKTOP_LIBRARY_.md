# Desktop Library and session opening

Started 2026-09-08. The player approved the next Phase 2 task after completing
manual review of the Play shell: choose a pack and save, complete player creation
where needed, then open a real StorySession in the desktop client.

- [x] Inspect the existing App session-opening contract and CLI client flow.
- [x] Record the Library/opening workflow and the remaining desktop-path decision.
- [x] Decide the default pack/save workspace for the desktop application: a selected
  workspace folder containing `worlds/` and `saves/`.
- [x] Implement pack discovery, save selection and open/locked/error states.
- [x] Implement player creation, including cancellation that disposes PendingPlayer.
- [x] Hand the opened StorySession to Play and dispose it on close/switch.
- [x] Build and provide a manual review guide.
- [x] Separate authored-world selection from saved-playthrough selection after
  manual review found their combined form ambiguous.
- [ ] Player manual review of Library, player creation, session disposal and live state projection.
- [x] Make Library resizable with expanding list areas and fixed header/footer controls.
- [x] Compact Library rows and add ascending/descending playthrough sorting by last saved date, turns and name; preserve selection when sorting.
- [x] Persist a chosen workspace immediately, including Cancel and application close.
- [x] Generate an unused world/date/time identifier when the new-save field is blank.
- [x] Constrain Library lists and scroll within each list in a fixed-size window.
- [x] Show turn count, last saved time and recorded start time for playthroughs.
- [x] Handle empty legacy-world dropdown values safely and support double-click actions.

The first implementation must call SessionOpener and PendingPlayer; it must not
duplicate engine composition, save locking or player-creation policy in Desktop.
