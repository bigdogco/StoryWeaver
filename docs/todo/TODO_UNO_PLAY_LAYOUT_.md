# TODO: Uno play layout

Started 2026-09-06. Requested: continue after the session-opening slice by
shaping the first durable Windows Uno play screen.

- [x] Replace the diagnostic-looking two-panel shell with a play-oriented layout.
- [x] Keep the transcript and command input as the primary surface.
- [x] Move session facts, current scene, visible cast and debug actions into a
      quieter side rail.
- [x] Make busy/status feedback visible without taking over the transcript.
- [x] Keep all gameplay/session work behind `StorySession` and `SessionOpener`.
- [x] Build the solution after the layout pass.
- [x] Update future work, devlog and mysite before commit.

Result: the Uno shell now has a more durable play layout: app/session header,
status pill, main transcript pane, bottom command bar and a side rail for scene
state, save path, refresh and debug views. It remains a thin client: all session
opening, canon refresh and turns still go through App/Core.
