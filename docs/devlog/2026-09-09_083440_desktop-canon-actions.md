# Desktop Update State and Check Canon

Connected the two Story menu actions to session-owned operations. Update State
reloads canon, refreshes world details and narration links, and reports changes
and integrity warnings. Check Canon checks current in-memory canon using the
session's lore and guard, without reloading or writing files.

Reports are selectable, scrollable and resizable. Actions are unavailable without
a live session or during an operation. Reload retains the draft and restores the
narration scroll offset. Neither action clears the requirement to reopen after a
partial save failure. Missing or unreadable canon retains the current world.

Validation: Release solution build passed with zero warnings and errors;
`git diff --check` passed. The player manually tested the actions and reported
everything working, then authorized commit and push. No automated tests or
agent-run model calls were performed.
