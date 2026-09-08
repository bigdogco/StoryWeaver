# Desktop Retry and Reroll

Connected Story → Retry and Reroll to the existing last-turn session operations.
The desktop retains drafts, replaces the displayed turn, refreshes world details
and narration links, and presents outcomes or refusals in a selectable report.
Actions are disabled without a displayed live turn, while busy, or when reopening
is required. Unexpected failures require reopening because last-turn operations
can partially save without advancing the turn number.

Fixed Retry replacing the applied-change history with only the latest attempt.
Earlier changes remain in canon and must continue to prevent Reroll; the repaired
record now retains them and appends newly accepted deltas. Existing records whose
earlier changes were already discarded cannot be reconstructed by this fix.

Validation: Release solution build passed with zero warnings/errors. The player
reported all manual testing complete on 2026-09-09 and authorized commit and push.
No automated tests or agent-run model calls were performed.
