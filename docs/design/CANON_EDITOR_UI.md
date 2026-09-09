# In-session canon editing

Approved by the player 2026-09-09; implemented and manually reviewed the same day.
The player reported testing looked good. All four forms and their Core correction operations
are present. No new projects, dependencies or save-format changes were introduced.
Builds on PLAY_UI.md and the session ownership rules in PROJECT.md.

## Entry and layout

Add **Edit** beside **Back to list** in each entity detail view: Characters,
Locations, Canon and Items. Editing requires a live, idle session with no pending
reopen requirement. The player character uses the same character form.

Edit opens a resizable modal dialog. Initial size: 720 × 680, constrained
to the available screen. The header shows `Edit character · Hald`, the playthrough
name/save ID and **Changes apply to this playthrough**. The stable entity ID is
selectable and read-only. Display names remain editable.

Fields scroll within the dialog; **Cancel** and **Save changes** stay visible at
the bottom. Related fields have headings rather than another set of tabs. Opening
the form copies values into a draft; typing never changes live canon. The main
window remains visible behind it, retaining the story draft and selected details.

The modal interaction prevents taking turns, reloading or switching playthroughs
while a form is open. The session guard still owns the actual save operation.

## Forms

| Form | Editable fields | Read-only context |
|---|---|---|
| Character | Name; multiline description; location picker with **Offstage / unknown**; status; mood; relationship summary and standing; knowledge selection | ID; last-seen turn; authored sheet when available |
| Location | Name; multiline description; status; searchable selection of outgoing connections | ID; characters currently present |
| Fact | Multiline fact text; searchable selection of characters who know it | ID; source attribution; established turn |
| Item | Name; multiline description; condition; **Held by character** or **At location**, with the corresponding picker | ID; existing placement information if inconsistent |

Character relationship standing uses a whole-number input for the existing -100..100
scale, with the prose summary beside it. Hide relationship editing on the player:
the engine ignores the player's relationship to themselves. Preserve that stored
value. Status and mood remain free text, since canon defines no fixed vocabulary.

Descriptions edit the save's description, not a character sheet. A collapsible
**Authored character sheet** section displays the pack text, labelled as pack
content. A description correction must not imply that it changes the character's
authored identity or removes conflicting text from the sheet. Sheet editing belongs
to the World Editor; playthrough-specific sheet overrides are not currently a feature.

Facts have text, not a separate name. Editing a fact's text preserves its ID and
therefore updates what all existing knowers refer to. **Known by** edits the actual
character knowledge sets, not a second list stored on the fact.

Metadata boundary: IDs, source attribution, established turn and last-seen
turn are not form inputs. Source/turn correction would need a separately designed
metadata editing option; this proposal does not silently reinterpret those values.

## References and knowledge

- Pickers show friendly names/text plus IDs, search both and store the selected ID.
  Duplicate names are allowed. They must never select a target by name alone.
- Characters can be offstage. Items normally have exactly one placement: a holder
  or a location. Changing placement clears the other field as part of the same save.
- Location connections are explicitly **Reachable from here**. Selecting a location
  does not add the reverse connection. Editing the other location is a separate act.
- Character knowledge has searchable **Facts** and **Lore** groups. Save changes to
  explicit knowledge IDs. Lore titles and read-only text come from the loaded pack.
- Common lore appears separately as **Known by everyone · from world pack**. It
  cannot be forgotten with a save edit because the engine grants it through the pack.
  If the same ID is explicitly recorded as learned, that explicit membership can be
  removed, but the form explains that common knowledge still applies.
- Preserve unresolved IDs and show them as **Missing reference: <id>**, with a way
  to remove or replace them. Opening a form, filtering a list or editing unrelated
  text must never drop values the picker cannot resolve.
- For existing inconsistent item placement (both/neither), show the original state
  and offer an explicit choice to correct it. Leaving placement untouched preserves
  it and lets the integrity report explain the issue; no automatic repair on open.

## Save, cancel and outcomes

1. **Save changes** is enabled only when draft values differ from the original and
   no save is running. An unchanged form closes through Cancel without a write.
2. Input-format errors, such as a standing value that cannot be parsed, appear by
   their field. Do not clamp, replace missing references or silently normalize
   unrelated stored data. Structural canon findings are reported, not a veto on
   the player's correction.
3. Submit the whole correction as one session operation and one canon save. Do not
   save fields independently or run an AuthorAsync save followed by an EditAsync
   save; that could persist only part of the form.
4. Disable editing and closing while saving. On success, close the dialog, refresh
   world details and narration links, keep the current selection where it still
   exists, and preserve the story draft and narration position. The turn number
   and transcript prose remain unchanged; no narrator/extractor call is made.
5. A clean save shows a compact `Changes saved` notice. Integrity warnings open the
   existing selectable result report, labelled **Saved with integrity warnings**.
   Show all current findings; do not claim every finding was introduced by this edit.
6. A busy refusal or missing target leaves the form draft available with a clear
   message. It must not report a successful save of an entity that no longer exists.
7. A persistence exception leaves the draft available for copying and reports that
   saving did not complete. Since the current EditAsync mutates before saving, do
   not claim Cancel restores the world after this failure. Require reopening before
   further mutations; closing the failed form does not write it again.

Cancel, Escape and the window close button discard an unchanged draft immediately.
With unsaved changes, ask **Discard changes?**, offering **Keep editing** and
**Discard**. This is a form interaction, not another confirmation after Save.
Enter inserts a newline in multiline fields and does not implicitly submit the form.

## Backend boundary and implementation prerequisites

This is an explicitly labelled direct-canon correction surface, using the session's
EditAsync path and checked-after semantics. Story actions and ordinary delta-based
authoring retain their existing paths. No new model-visible delta kinds are needed.

Core must own a typed correction request and its application for each kind, including
knowledge-set updates and mutually exclusive item placement. Desktop owns field
layout, draft values and choice selection; it must not implement these world mutations
in a callback that assigns through session.World. Existing CanonEdits helpers can be
reused where their semantics fit, and extended within Core where needed.

Apply by entity kind and permanent ID (and original dictionary key if necessary for
malformed canon). Existing Describe/Remove helpers search character before location
before item by bare ID: an edited file with cross-kind ID collisions must not make a
location form modify a character. Missing targets need an explicit result rather than
the current silent no-op helper behaviour. Compare/check the target inside the guard.

The full form must preserve fields it does not edit. Backend corrections apply only
changed fields, resolving the target at save time. The form does not hold mutable
canon entities or overwrite an entire world snapshot. If an in-process caller changes
the target after the draft was opened, report a stale draft before applying anything;
retain the draft for review instead of overwriting those changes.

The editor needs a read-only catalog of pack lore and character sheets. Current
WorldPresentation only renders saved facts in character knowledge, so it is not a
complete source for knowledge editing. Obtain pack context through App/session-facing
data, not a second desktop filesystem loader. Common lore and explicit knowledge
must remain distinguishable.

External JSON editing keeps the existing explicit Update State workflow. There is no
file watcher or merge. This form edits the running session; external edits made after
its last reload are not automatically incorporated.

## Scope for review

The editor covers existing character, location, fact and item details,
including placement, connections and knowledge. Creation and deletion are separate
actions, not implicit effects of clearing a form. Add/Remove was subsequently approved
and implemented in `CANON_ADD_REMOVE_UI.md` on 2026-09-09; reusable pack authoring
remains future work. Removing a knowledge/connection selection edits that
reference; it never deletes the referenced entity.

The player approved the modal layout and field coverage, including read-only metadata
and the distinction between canon description and authored sheet. All four forms and
their Core operations are implemented. Manual review covers Save/Cancel, duplicate
names, missing references, common lore, directed connections, player/offstage handling,
unchanged saves and save failure; see the desktop README.
