# Uno Windows UI spike

Started 2026-09-06. Requested: park Linux setup for now and continue proving Uno
on Windows.

Mirrored to mysite StoryWeaver task id 145.

- [x] Commit and push the Linux-parking documentation update.
- [x] Mirror this task to mysite StoryWeaver.
- [x] Inspect the app/backend session-opening APIs.
- [x] Wire one small read-only backend surface into the Uno shell.
- [x] Keep gameplay and authoring policy out of the UI project.
- [x] Verify the Windows solution build.
- [x] Verify the Uno desktop launch smoke.
- [x] Update TODO_FUTURE_WORK.md and the Uno devlog with the outcome.

Done when the Windows Uno shell renders real repository data without starting a
turn or writing canon.

Outcome: the Uno shell now references `StoryWeaver.Storage`, loads
`WorldPack.Load("worlds", "marrow")`, and renders the pack opening, scenario,
player, NPCs in the player's starting location, current location, and world
counts. It does not open a save, create an LLM client, start a turn, or write
canon.
