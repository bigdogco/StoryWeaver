# Narration link alignment

Started 2026-09-08 after the player reported that narration links sit above the
surrounding text.

- [x] Identify how Avalonia aligns embedded controls with text runs.
- [x] Give links their text baseline and keep the label stable on hover.
- [x] Build and document the manual alignment recheck.

Added Views/NarrationLinkButton.cs to the desktop presentation layer. The fix
uses measured font metrics, not a fixed vertical offset. The solution build has
zero warnings/errors. The manual visual recheck is recorded in FUTURE_WORK and
the desktop review guide. No UI tests, commit or push performed.
