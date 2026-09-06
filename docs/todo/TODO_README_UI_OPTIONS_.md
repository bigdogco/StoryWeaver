# README refresh and UI options

Started 2026-09-05. Requested: bring README up to date and discuss alternatives
to Avalonia for a cross-platform C# client. The framework was undecided at the
start of the review; the 2026-09-05 follow-up decision is recorded below and
then explicitly reversed in the 2026-09-06 note.

**Follow-up 2026-09-05:** the player selected Blazor. This supersedes the review's
initial undecided status. Standalone application, Windows desktop first. MAUI Blazor
Hybrid remains the proposed host, tracked in the Phase 2 backlog. README and
PROJECT.md now record these decisions.

- [x] Refresh README status, architecture, pack documentation and CLI guidance.
- [x] Check the documentation diff and local links; no build needed for prose edits.
- [x] Compare current framework support against StoryWeaver's UI needs.
- [x] Update the backlog with the remaining documentation reconciliation.

Completed 2026-09-05. README local links resolve and the whitespace check passes.
No code changed, build or API eval run during the review.

Comparison: Avalonia and Uno provide C#/XAML clients; Blazor offers C#/HTML/CSS
with browser or desktop hosting; MAUI targets Windows/macOS and mobile without
an official Linux target; Godot C# is relevant if visual game presentation becomes
central. The comparison informed the subsequent decisions below; remaining work is
tracked under the Phase 2 UI backlog item rather than an unfinished review task.
The subsequent Blazor decision resolves the framework question. The player then
selected Windows desktop first; MAUI Blazor Hybrid is proposed as the host, with
implementation design still pending. The stale PROJECT.md session-ownership
paragraph is also corrected.

- [x] Record the Blazor decision and standalone requirement in current documentation.
- [x] Preserve host/platform selection as an explicit open implementation decision.
- [x] Record the player's follow-up: Windows desktop first; host remains proposed.

**Reversed 2026-09-06:** the player no longer wants Blazor as the UI. The current
Phase 2 guidance records the framework and host as undecided again; the comparison
above remains historical context only.
