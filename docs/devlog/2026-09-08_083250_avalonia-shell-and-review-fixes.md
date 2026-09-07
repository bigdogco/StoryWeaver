# Avalonia shell and manual review fixes

2026-09-08 08:32:50 Australia/Brisbane.

Added StoryWeaver.Desktop to the solution using Avalonia 12.1.2 and the existing
.NET 8 target. The shell provides the agreed menus, resizable narration/input
and information panes, independent tabbed list/detail navigation, explicit-ID
preview links, text editing, help and desktop view preferences. The preview is
illustrative and does not open saves or call models. Live sessions and authoring
remain the next integration work; their actions are disabled in this shell.

The player's manual review identified three presentation problems. Inline links
now report their label's measured text baseline and retain the same label while
hovering. Detail fields now show bold labels and values together. Secondary text
inherits its containing control's foreground with reduced opacity, so subtitles
follow theme and selection colours.

Updated the project/design documentation, completed task notes, manual review
guide, known challenges and remaining Phase 2 work. Backend libraries and save
formats were not changed.

Validation: the latest Release solution build passed with zero warnings and zero
errors. The preceding Debug build could not replace the executable held by the
player's running preview; that process was left untouched. Earlier Debug builds
passed before the final colour-style change. No automated UI tests or live model
calls were run. Visual rechecks remain recorded for the player. Reviewed the
staged changes and checked whitespace before committing.

The player explicitly requested committing and pushing this checkpoint.
