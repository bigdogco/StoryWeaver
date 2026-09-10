# Player discovery evaluation plan

Required by the player, 2026-09-09, during discovery design review. Companion to
`PLAYER_DISCOVERY.md`. The Harness now implements 26 discovery fixtures, including
retry and author-protection cases. Provider-labelled reports live in `docs/devlog`;
implementation of a fixture is not a claim that it passes. Desktop acceptance remains manual.

## Scoring and execution

Use the existing Harness extraction eval and its outcome-based StateRule assertions.
Each fixture defines starting canon, discovery, lore, player input and fixed narration.
Score both the resulting actual world and resulting player observations/projection.
Use precise forbidden-proposal checks too: a validator rejecting a proposed spoiler
or invented movement must not make the model's behaviour look correct. Report raw
proposal violations separately from post-validation state errors and rejected deltas.

Do not require a particular sequence of delta kinds if the supported result is
equivalent. Where absence matters, assert unchanged private fields and observations,
not just the presence of one correct discovery. Use distinct sentinel secrets in
private descriptions, locations and contents to make unintended disclosure detectable.
Retain raw responses and provider identities for failures.

Run repeated samples with a pinned provider, recording requested model/provider and
reported provider, prompt/schema revision, run count, required outcomes, forbidden
outcomes, no-ops, rejections and transport/parse failures. Compare the existing
extraction set before and after the change on the same model/provider settings.
New discovery success cannot compensate for a regression in movement, knowledge,
items or other existing scenarios. Report per-case results rather than only an average.

Expect zero forbidden disclosures and invented movements in the measured acceptance
set. Missing required outcomes remain failures to investigate; do not waive them by
counting schema-conformant responses as success. Repeated success is evidence over
those samples, not a guarantee about every future narration.

## Fixed-narration extraction cases

| Case | Required result | Forbidden result |
|---|---|---|
| Missing-person briefing | Julian's disclosed identity/background becomes known; missing-person fact is learned; observed whereabouts stays unknown | Revealing Hotel Argent, hidden status, private mood or motives from seed/context |
| Failed search | A narrated unsuccessful search leaves Julian's actual placement and prior observations unchanged | Revealing, moving, or marking him seen because he shares the scene |
| Hidden item on a present NPC | Vivian and the visible cigarette case may receive supported observations; the concealed ledger does not | Revealing ledger existence, holder, contents or condition from carried-item context |
| Item mentioned, not shown | Ledger identity/explicitly reported purpose becomes known with appropriate provenance | Treating the mention as a sighting or revealing possession/contents |
| Closed container inspected externally | Only narrated external details become observations | Copying hidden contents from the item's canonical description |
| Genuine discovery | Finding Julian records the narrated safe identity/details and sighting location | Copying undisclosed private knowledge, relationship score or description clauses |
| Departure, no destination | Explicitly leaving the location sets actual LocationId to null; observed whereabouts becomes unknown; last sighting remains | Keeping him in the old room, inventing a destination, deleting him or dropping his possessions |
| Departure to a visible destination | Actual location and witnessed whereabouts agree with the narrated destination | Unnecessarily making him offstage or keeping the old sighting as the only information |
| Lose sight within the same location | Observation records loss of contact where warranted; actual location remains unchanged | Equating visual obstruction with departure from the location |
| No narrated departure | Existing private location stays unchanged despite player demands/speculation | Inventing offscreen movement or treating a requested action as completed |
| Return from offstage | Existing Julian moves from null to the narrated real destination; identity/possessions persist; witnessed observations update | Reintroducing a duplicate character or inventing an exit from a null location |
| False location report | A learned attributed claim/report is retained alongside the earlier sighting; actual location stays unchanged | Teleporting Julian or labelling the report as directly observed/verified |
| Unnamed encounter then identification | First observation uses only the disclosed alias; later narration adds the learned name on the same identity | Showing the private canonical name before identification |
| Duplicate/ambiguous names | Resolve only supported identities; ambiguity leaves unsupported observations unapplied | Updating the wrong character because of name similarity |
| Ordinary and secret routes | Record only the explicitly discovered directed route and disclosed destination identity | Copying all connections, revealing the secret route or adding a reverse connection |
| Partial observation update | Newly disclosed condition changes only that observed aspect; prior name/description remain | Copying every current canon field or clearing omitted aspects |
| New entity plus discovery | An accepted genuine introduction can support observations in the same batch | An observation surviving a rejected introduction, or a player assertion creating an entity |
| Facts and lore | Learn the explicit fact/claim/topic supported by narration; preserve attribution | Teaching unrelated private facts or exposing an entire non-common lore body from topic membership |
| Repeated observation | Same-turn repetition is a no-op; a supported later sighting can refresh its own timestamp | Refreshing undisclosed aspects or restamping an old sighting as current without evidence |
| Malformed evidence/references | Reject unsupported IDs, invalid provenance and evidence absent from narration | Falling back to a private value or treating private context/player input as disclosure evidence |

Keep paired fixtures where just one sentence changes the expected outcome: successful
versus failed search; leaving the room versus disappearing behind an obstacle inside
it; quoted report versus witnessed movement; ledger mentioned versus physically shown.
This catches rules that recognize a name/keyword but miss what the scene actually says.

## State-sequence and projection review

These cases exercise lifecycle and presentation in addition to a single extraction.
Represent model-dependent parts as fixtures in the Harness; retain a manual checklist
for session operations and desktop interactions rather than pretending a one-turn
extraction score proves them. Any extra automated code-test runner needs separate scope.

| Sequence | Expected outcome |
|---|---|
| Sight Julian, then author changes his location privately | Actual location changes; all player observations remain unchanged; no live-tracking marker appears |
| A private destination is already established for a movement | Player projection can show unknown whereabouts while actual canon retains that destination; no new secret-action model capability is implied |
| Knowledge-only loss of whereabouts | Clearing/invalidating an observation does not itself change actual placement |
| NPC offstage with held items | Items remain held by that NPC; no invented location/connection appears; later return retains the items |
| Retry the same narrated turn | No duplicate observations, no new turn, no refreshed timestamp from old prose; earlier applied changes remain in the audit |
| Retry after a newer sighting or author correction | Older evidence cannot overwrite newer/protected information; conflicts are visible for author review |
| Discovery-only turn then Reroll | Reroll refuses because saved discovery changed, even if physical canon did not |
| Partial/failed extraction or persistence | Prose remains; missing discoveries never fall back to private fields; failure/reopen behaviour is explicit |
| Opening/new save/legacy save/reopen | Authored opening knowledge initializes new saves and observations persist on reopen; legacy support is best-effort, with a minimal unknown view or clear incompatibility message; no automatic reconstruction from current private canon |
| Player → Author → Player | Full data is deliberately accessible in Author view; returning clears private cached rows, labels, tooltips, links and reports |
| Indirect navigation | Occupants, item holders, connections, source names, counts and name-part links reveal only the player projection |
| Canon rename/edit/remove after observation | Remembered information does not silently change; missing targets remain readable from recorded public text |

## Narration review is separate

Fixed-narration extraction evals cannot prove that the narrator keeps a secret.
Manually review generated narration with private Julian/ledger context and the
Requires discovery rule: failed searches must not reveal them, successful discoveries
must remain possible, and reports must not become objective truth. Include concealed
entities sharing the player's location. Record model/provider and the actual prose.
Do not substitute a keyword-only score or an uncalibrated LLM judge for this review.

Completion requires implemented fixtures, recorded model-eval results and a clear
manual-review status. No evals were executed when this plan was written.
