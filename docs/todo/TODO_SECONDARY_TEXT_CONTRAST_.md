# Secondary text contrast

Started 2026-09-08. The player reported black subtitles on dark information rows.

- [x] Fix the shared secondary-text style, including selected list rows.
- [x] Build and record the manual theme/selection recheck.

Complete. Removed the explicit foreground override; secondary text inherits the
containing control's text colour with 0.8 opacity. Release solution build passed
with zero warnings/errors. Debug build could not replace the executable because
the player's preview was open; that process was left running. Manual review is
tracked in FUTURE_WORK. No UI test, commit or push performed.
