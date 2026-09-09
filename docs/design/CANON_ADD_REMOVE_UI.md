# In-session Add and Remove

Approved for implementation by the player, 2026-09-09. Extends `CANON_EDITOR_UI.md`.
Implementation is tracked in `TODO_CANON_ADD_REMOVE_.md`.

Implemented 2026-09-09; Release build passes with zero warnings/errors. Manual review
is pending in `src/StoryWeaver.Desktop/README.md`. No runtime tests were run by the agent.
The full forms, player safeguard and removal rules below are implemented. The world
lists currently have no filter; their filter-preservation rule applies if one is added.
Reference pickers already filter without changing selections.

## Entry and shared behaviour

Each canon tab has an **Add character/location/fact/item** button in its list
toolbar, available even when the list is empty. Detail views gain **Remove** beside
Edit. Use text labels; removal must not be an unlabelled icon or a Delete-key shortcut.

Both actions require a live, idle session with no reopen requirement. Use the
existing resizable modal layout, scrolling content and fixed footer. The header
identifies the playthrough and says **Changes apply to this playthrough**.
The modal protects against turns, reloads, switching saves and closing the main window.
Core still guards every operation against other callers.

Drafts are detached values. Preserve story input, transcript prose and scroll
position. Successful Add selects the new entry and opens its details, clearing a
list filter if needed to reveal it. Successful Remove returns that tab to its list,
preserving its filter. Refresh all tabs and narration links after either action.
Neither action advances the turn, writes invented narration or calls a model.

## Add forms

Use the same field labels and searchable reference controls as Edit, with these
creation-specific rules:

| Kind | Fields | Initial values |
|---|---|---|
| Character | Name, ID, description, location, status, mood, relationship standing and summary, explicit fact/lore knowledge | Offstage / unknown; normal; neutral; standing 0 and no strong feelings; no explicit knowledge |
| Location | Name, ID, description, status, outgoing connections | Empty status; no connections |
| Fact | Fact text, ID, Known by | No knowers selected |
| Item | Name, ID, description, condition, Held by character / At location and target picker | Intact; no placement selected, so Add requires a choice |

Names and fact text are required; descriptions may be empty. Standing must be a
whole number from -100 to 100. References must resolve when submitted. Character
offstage is valid; an item requires exactly one holder or location. Connections
are **Reachable from here**, with no automatic reverse connection. No nested Add
dialogs: create a missing reference separately, then select it.

Knowledge includes saved facts and pack lore, showing common lore as already known
through the pack. Filtering never changes selections. Adding a fact does not
automatically teach it to the player or anyone nearby. Facts entered here are
authored world truth: SourceId is null, EstablishedTurn is the current turn.
For a reused ID previously held only as dangling knowledge, Known by remains
authoritative: memberships on unselected characters are removed, as explained in the form.
Character LastSeenTurn follows the current authoring path (current turn, including
offstage characters). These are stated defaults, not metadata correction inputs.

Adding a character creates save canon, not a sheet. If its proposed ID matches an
existing pack sheet whose character is absent from the save, show that sheet
read-only and explain that the existing authored identity will apply. No sheet,
lore, seed or other pack file is written. Facts are not new lore entries.

### IDs

Suggest an ID with Core's slug policy from the name or fact text. Keep updating the
suggestion until the player edits the ID themselves; **Use suggested ID** resumes
that behaviour. The field remains editable until creation, labelled **Permanent
after adding**. Display shape and collision errors beside it; never silently change
an entered ID or append a suffix on submission. Duplicate display names are allowed.

Core must check lowercase ASCII words/digits joined by single hyphens, and
case-insensitive availability across characters, locations, facts, items and pack
lore. Inspect both dictionary keys and stored IDs in malformed canon. Slug output
still needs validation: the current slugger accepts Unicode letters which the ID
validator does not. Empty or invalid suggestions require the player's correction.

Reserve `player` for the player character; ordinary Add cannot create another or
use that ID for another kind. The existing new-playthrough flow creates the player.
An ID absent from current canon may have appeared in history: reuse does not rewrite
that history and may make old references resolve to the new entry. Explain this
beside the permanent-ID field; no history scan or ID tombstone format is introduced.

### Submit and cancel

**Add character** (or the relevant kind) is enabled when the form is complete and
valid. One click submits the entire form as one guarded operation and one canon
save. A validation failure writes nothing and retains every draft value.
Errors discovered at submission appear in the form with field details where possible.

Cancel, Escape and window close discard an untouched form immediately; otherwise
offer Keep editing / Discard. Automatic default/suggestion updates do not count as
user edits on their own. Enter in multiline text inserts a newline. While saving,
disable controls and closing. There is no extra confirmation after Add.

## Remove preview

Clicking Remove asks Core for a detached plan for the exact kind, dictionary key
and stored ID selected. Show **Remove character · Hald?**, its selectable ID (and
dictionary key if different), followed by the concrete effects. Use names plus IDs
and list every affected entry in a scrollable area; counts may summarize but must
not replace the details. The footer contains **Cancel** and **Remove character**.

Proposed policy follows the existing removal helper for ordinary canon:

| Target | Changes shown before confirmation |
|---|---|
| Character | Remove character. Held items move to their recorded location when set, clearing holder. If offstage, items retain the now-missing holder reference. Facts attributed to the character remain, including their source IDs. |
| Location | Remove location. Its occupants become offstage. Remove incoming connections. Items at the location retain the now-missing location reference. Items held by occupants remain held. |
| Fact | Remove fact and its explicit knowledge membership from every character. |
| Item | Remove item; no other structured canon changes under the current model. |

Do not invent a destination, delete belongings, rewrite fact attribution or cascade
delete other entries. Preview unresolved placements honestly: **Lantern will still
refer to the removed holder**, rather than claiming its holder field becomes empty.
If a character has a missing location, show that dropped items will reference that
missing location. If an affected item already has both placement fields, show the
exact before/after values, including any location overwritten when dropping it.

Malformed canon needs additional precision. Dictionary keys are the lookup addresses;
stored entity IDs may disagree. Build cascades from actual reference resolution by
key, not by a bare ID shared with another kind. Show the mismatch explicitly.
References to a different key or to an already-unresolved stored ID remain untouched
and are reported. For fact/lore ID collisions, remove the canon fact's explicit
knowledge memberships as shown, but explain that common pack lore still applies.
The removal target itself must always be resolved by kind and key.

Always state that past narration, pack sheets and lore remain. Removal is a canon
correction, not an in-story death or destruction event; a retained sheet or history
may still cause the narrator to mention that person. There is no undo button in
this workflow. Cancel/Escape/window close dismiss the preview without another prompt.

**Approved player safeguard:** disable Remove on the player, with an explanation
that the playthrough needs its player character and Edit can correct them. This is
a deliberate UI restriction beyond the existing CLI helper, which only warns.
The new Core removal operation enforces it too. The player approved this restriction;
no change to the legacy CLI or manual JSON escape hatch is implied.

## Backend and failure handling

Desktop supplies typed drafts and renders results; it never mutates session.World.
No new projects, dependencies, save formats or model-visible delta kinds are needed.

Creation needs a complete-form Core operation exposed through StorySession. Reuse
authored delta validation/application for introductions and supported state changes;
apply any remaining authored fields with typed Core policy inside the same operation.
Validate the entire request before changing live state. Do not use the current
Authoring.CommitAsync partial-acceptance behaviour or chain AuthorAsync and EditAsync
saves. Existing story extraction retains its partial acceptance semantics.

The full-form operation must validate supplemental fields/references too and cannot
silently drop duplicate/no-op changes that would leave the requested form incomplete.
Recheck ID availability and reference identity under the guard. If referenced entries
changed since the form's catalog was captured, return a stale-draft result and retain
the draft rather than applying choices to a replacement entry.

Removal needs Core-owned preview and apply operations using the same consequence
calculation. Capture a revision of the target and every dependency relevant to the
plan, including new incoming references. Inside the guard, recompute and compare
before applying. A missing or changed target is a refusal, never a successful no-op.
A changed plan must display the refreshed preview and require another explicit Remove
click. Never apply consequences the player has not reviewed. This also covers typed
target protection, the player safeguard and malformed reference handling.

After successful creation/removal, run the existing lore-aware canon check. Clean
success shows a compact notice; findings open **Added/Removed with integrity
warnings**, containing all current findings, not just those introduced now. Removal
may intentionally leave unresolved item references; these warnings do not veto it.

A busy refusal preserves the draft/preview. A persistence exception can leave memory
ahead of disk: lock further mutations until reopen, keep Add drafts copyable, and
do not automatically retry removal or claim Cancel rolls it back. One canon save
does not promise a filesystem transaction. Closing a failed dialog performs no write.
External JSON edits retain the explicit Update State workflow, without merging.

## Review and implementation validation

The approved decisions are the player safeguard, the existing removal cascades
(especially items deliberately left with missing references), and the full creation
field coverage. Metadata correction remains separate future work. World Editor
authoring is a separate workflow for reusable packs.

The desktop README includes a manual review guide covering all four
Add forms, defaults, ID conflicts including items/lore, knowledge/common lore,
offstage and directed connections, complete-form rejection, empty lists, cancellation,
persistence, and selection/filter behaviour. Removal review must cover every cascade,
player protection, missing references, key/ID mismatches, cross-kind collisions,
unchanged history/pack content, stale previews and save failures. In-process stale
cases require a caller outside the modal UI; do not pretend ordinary clicking tests
them. Only dotnet build is automated, per project policy.
