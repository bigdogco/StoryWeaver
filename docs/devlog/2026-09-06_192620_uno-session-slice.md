# Uno session slice

The Windows Uno spike now opens a real StoryWeaver session instead of only
loading pack data. The shell references `StoryWeaver.App` and calls
`SessionOpener.OpenAsync` for the `marrow` pack, using a separate `uno-spike`
save id so testing the UI does not take over the normal CLI save.

The UI renders the session as data from the opener: fresh/resumed state, pack
scenario, current location, visible characters, recent turns and save location.
Refusals, settings failures and the "needs player" opening branch are rendered
as window state rather than thrown through startup.

The first command input is wired to `StorySession.TakeTurnAsync`. A submitted
turn disables the controls, waits for narration/extraction, appends the turn to
the story pane, refreshes the explorer from the session's canon, and reports the
applied/no-op/rejected counts or extraction failure. The refresh button calls
`StorySession.UpdateStateAsync`; the State and Prose buttons render the existing
Core context views for debugging the same distinction the CLI exposes.

One item deliberately remains outside this slice: when a pack has no
`characters/player.md`, App returns `PendingPlayer` and the CLI asks for name and
description. Uno now reports that condition, but does not yet have the dialog to
complete it. That has been moved to future work because `marrow` already authors
the player and the Windows session path is the immediate question.

Verification: `dotnet build StoryWeaver.sln` passed with zero warnings and zero
errors.

Follow-up: starting the Uno spike printed RemoteControl/DevServer/HotDesign
connection failures. Those are Uno development-tooling channels, not the
StoryWeaver session path, and this spike is not using them. Set the Uno project
to disable HotDesign, the HotDesign agent and app MCP support so the Windows
shell is quieter and the remaining startup output is easier to interpret. The
template's `UseStudio()` startup hook depended on that tooling, so it was
removed as part of the same cleanup.

Verification after the cleanup: `dotnet build StoryWeaver.sln` passed with zero
warnings and zero errors. A short Windows desktop launch stayed alive and no
longer printed the RemoteControl, DevServer or HotDesign connection failures.
The only remaining startup line was Uno's Win32 text-scale registry warning.
