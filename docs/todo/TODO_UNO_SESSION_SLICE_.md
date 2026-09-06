# TODO: Uno session slice

Started 2026-09-06. Requested: continue the Uno Windows spike by opening a
real StoryWeaver session context instead of only previewing pack data.

- [x] Reference the App composition layer from the Uno shell.
- [x] Open the `marrow` pack through `SessionOpener.OpenAsync`, using the
      spike save id `uno-spike`.
- [x] Render opening/resume context, current location, visible characters and
      recent turns from the opened `StorySession`.
- [x] Add first command input wired to `StorySession.TakeTurnAsync`.
- [x] Report refusal/settings/opening failures in the UI without crashing.
- [x] Build the solution after the slice is wired.
- [x] Disable unused Uno HotDesign/app MCP development tooling after startup
      reported RemoteControl/DevServer connection failures.
- [x] Remove the template `UseStudio()` startup hook because it depends on the
      HotDesign tooling package.
- [x] Update future work and devlog before commit.

Result: the Uno shell is now a thin Windows client over the App/Core session
path. It opens `worlds/marrow` into `saves/uno-spike`, renders fresh or resumed
session state, can submit a first player turn, and exposes simple refresh/state
/prose views. The save id is deliberately separate from the normal `marrow`
save so a UI spike does not accidentally take over a CLI playthrough.
