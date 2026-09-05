# Phase 2 documentation and UI direction

2026-09-05 21:25:48 Australia/Brisbane

The README still described late bootstrap and omitted App and Harness. PROJECT's
layer table recorded session ownership, but its Phase 2 prerequisite still called
that work proposed. Updated both to reflect the completed client separation and
the current authoring/play phase.

After discussing cross-platform C# UI options, the player chose Blazor for a
standalone application and Windows desktop first, replacing Avalonia. MAUI Blazor
Hybrid is the proposed Windows host; it is not yet a settled implementation design.
No UI project, host dependency or runtime upgrade was introduced.

Recorded the boundary explicitly: the UI owns presentation and interaction; App
composes sessions and Core owns gameplay and canon. Host-specific services should
remain outside shared Blazor components. Replacing a UI preserves the backend but
still requires rebuilding the screens and their interactions.

Reconciled stale backlog entries for narration prompt overrides, closed-stdin
character creation and pack-root parameters against completed work. Repaired two
eval source links after the Harness move. Added completed review/checkpoint notes
and kept pending UI design work in FUTURE_WORK.

Validation: reviewed documentation changes, checked local Markdown links and
git diff whitespace. No build, self-tests or paid model calls: documentation only.

The player explicitly requested committing and pushing these documentation changes.
