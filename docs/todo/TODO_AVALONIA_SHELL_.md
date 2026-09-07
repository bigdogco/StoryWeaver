# Avalonia desktop shell

Started 2026-09-07. The player approved adding StoryWeaver.Desktop and coding the
shell after the implementation proposal. Session opening, real turns, save/resume
and backend narration references follow this shell; they are not implemented here.

- [x] Add the desktop project to the solution with pinned Avalonia packages.
- [x] Implement menus, resizable story/world panes and an anchored input composer.
- [x] Implement tabbed entity lists/details with independent navigation state.
- [x] Provide clearly labelled illustrative preview data and explicit-ID links.
- [x] Implement text editing, view preferences and help; disable unavailable actions.
- [x] Persist presentation preferences separately from canon and save data.
- [x] Build the solution; document manual review steps and remaining Phase 2 work.

Structural addition: src/StoryWeaver.Desktop, with Views, ViewModels, Presentation,
Preview and Services folders. The desktop references App; no backend project
references Avalonia. No tests other than dotnet build, per CLAUDE.md.

Complete for the approved shell scope. `dotnet build StoryWeaver.sln --no-restore`
passed with zero warnings and zero errors. The app was not launched or UI-tested
by the agent; manual review belongs to the player. The guide is in
src/StoryWeaver.Desktop/README.md. Session integration, authoring, backend links
and manual review remain in TODO_FUTURE_WORK.md. No commit or push requested.
