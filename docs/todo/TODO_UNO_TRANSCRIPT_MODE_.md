# TODO: Uno transcript mode

Started 2026-09-06. Requested: continue the Windows Uno play layout refinement
after the first durable layout pass.

- [x] Add an explicit Transcript mode beside State and Prose.
- [x] Keep recent turns in UI state so debug views can return to the active
      transcript.
- [x] Route submitted turns back to Transcript mode before appending prose.
- [x] Keep mode switching as rendering only; no gameplay logic moves into Uno.
- [x] Build the solution after the mode pass.
- [x] Update future work, devlog and mysite before commit.

Result: Transcript, State and Prose are now explicit modes. Recent turns are
kept in UI state, debug views can redraw after refresh, and submitting a turn
returns the main pane to the active transcript before showing new prose.
