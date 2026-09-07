# Detail field labels

Started 2026-09-08. The player reported that values such as Marrow Square and
Uneasy do not visibly identify the type of information they represent.

- [x] Render explicit, readable label/value pairs in the shared detail template.
- [x] Build and record the manual recheck across information tabs.

Complete. Fields now render as a bold label, colon and value in one wrapping
TextBlock, using normal foreground contrast. Applies to all four tabs. The
solution builds with zero warnings/errors. Manual light/dark and narrow-pane
review remains in FUTURE_WORK; no UI test, commit or push performed.
