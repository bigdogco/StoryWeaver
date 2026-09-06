# Uno transcript mode

The play screen now has explicit Transcript, State and Prose modes. The previous
layout had the debug views reuse the opening text block directly, which made
State and Prose useful for inspection but left no clear way back to active play.

The UI now keeps the recent transcript turns in page state and renders the main
pane from a small mode enum. Transcript shows the opening or resumed history plus
new turns. State renders `ContextAssembler.ForExtraction`; Prose renders
`ContextAssembler.ForNarration` and the resolved scenario. Refresh redraws the
active mode from the latest session world, and submitting a turn returns to
Transcript before appending the new narration.

This is still presentation-only. `SessionOpener` opens the session,
`StorySession` owns canon and turns, and Uno switches between views of those
objects.

Verification: `dotnet build StoryWeaver.sln` passed with zero warnings and zero
errors. A short Windows desktop launch stayed alive; the only startup output was
Uno's existing Win32 text-scale registry warning.
