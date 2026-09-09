# StoryWeaver desktop shell

Avalonia 12.1.2 on the existing .NET 8 target. The desktop client can open a
real session from a selected workspace, render its opening and recent turns, and
send live player actions through the existing session layer.

From the repository root:

```powershell
dotnet run --project src/StoryWeaver.Desktop -- --preview
```

Omit `--preview` to start with the empty shell. Its **Explore a preview scene**
button loads the same illustrative scene. This data is owned by the preview and
is never opened as a StorySession or written to a save.

## What is implemented

- File, Edit, View, Story and Help menus. **New Playthrough** starts with a World;
  it requires an unused save ID. **Open Save** starts with a saved playthrough and
  resumes the world recorded by that save. Library offers Recent, Worlds and
  Playthroughs views. World Editor
  remains disabled. Story → Retry, Reroll, Update State and Check Canon are connected.
- Library selects a workspace that contains `worlds/` and `saves/`, then keeps
  authored worlds and saved playthroughs in separate views. Older saves without
  provenance are labelled **pack unknown** and require an explicit, unverified
  world choice before they can be resumed. The selected workspace path is retained
  with desktop preferences; content and saves stay in that workspace. Successful
  opens are remembered as recent playthroughs in desktop preferences.
- Opening calls App's `SessionOpener` with the selected roots. It shows save-lock
  and settings errors, asks for a player name only when a pack does not author the
  player, and releases the pending lock if the form is cancelled. A live session
  projects its current canon into the existing world-information tabs and is
  disposed when it is closed, replaced or the app exits.
- Resizable narration and world panes, with an input composer below the transcript.
- Characters, Locations, Canon and Items tabs, each retaining list/detail navigation.
  **Edit** opens the corresponding live-canon form with Save/Cancel. Changes affect
  this playthrough, not the pack, turn number or historical narration.
  Tabs wrap when the world pane becomes narrow. The desktop window has minimum
  dimensions of 800 × 560; this is not a mobile layout.
- Preview narration references select a tab and open the entity by kind and stable
  ID. The renderer never guesses targets from names. Unresolved references render
  as plain text; missing navigation targets produce a notice.
- Text editing through Edit and native text-box shortcuts. View controls for panel
  visibility, splitter reset and narration size. Settings offers System/Light/Dark.
- Presentation preferences and the selected workspace path are saved under
  `%LOCALAPPDATA%/StoryWeaver/desktop-view.json`. They do not contain drafts,
  credentials or world data. Drafts are retained while browsing, but are not saved
  when the application exits.

## Manual review

Canon editing (2026-09-09; no model calls):

1. Open a disposable playthrough, select an entity and click **Edit** beside Back.
   Confirm the playthrough label, read-only ID and appropriate fields. Preview Edit
   is disabled. Resize the form; fields scroll while Save/Cancel remain visible.
2. Open and cancel an unchanged form: Save should be disabled and no write occurs.
   Change a field, press Escape/Cancel/window close, choose Keep editing, then try
   Discard. The world and story draft must stay unchanged. Typing the original value
   back should disable Save again. Enter in a multiline field inserts a newline.
3. Character: change name, description, status, mood and location. Use Offstage/unknown.
   For an NPC, change relationship summary and standing; non-integer/out-of-range
   edits should show an inline message. The player's relationship field is hidden.
   Last-seen turn and the authored sheet are read-only; the sheet is pack content.
4. Knowledge: add/remove facts and explicit lore, filter selections out of view and
   back, then save/reopen. The selections must survive filtering. Common lore remains
   labelled as known by everyone even after removing an explicit learned membership.
   Expand a lore entry to read its text. A character description edit must not lose
   existing lore knowledge. The main detail summary still lists saved facts only;
   the editor shows the full explicit/common knowledge view.
5. Location: edit name, description, status and outgoing connections. Confirm saving
   A → B does not add B → A. Present characters are read-only context.
6. Fact: change its text and Known by selections. Confirm character knowledge updates,
   while fact ID, source and established turn remain unchanged. Rewording a fact
   updates its text for every existing knower without duplicating it.
7. Item: edit name, description and condition. Switch Held by character / At location
   and select a target; saving should clear the other placement field. Keep current
   placement should preserve it exactly. Missing a required selection shows a message.
8. In a disposable save, introduce unresolved knowledge/connection/location IDs with
   an external editor and use Update State before opening a form. The form must retain
   and label them, allow removing/replacing them, and not erase them on an unrelated
   edit. An item with both/neither placement remains unchanged unless explicitly fixed.
   Saving inconsistent state reports integrity warnings after saving, without repair.
9. Duplicate display names must have distinct picker IDs. If inspecting deliberately
   malformed canon with cross-kind collisions or a key/ID mismatch, edit the intended
   row and confirm only that entity changes; the ID/key themselves remain untouched.
10. Save and confirm selected details and narration links refresh, the story draft and
    narration position remain, and no turn is appended/advanced. Close/reopen the
    playthrough to verify persistence. Clean saves show a notice; warning reports
    are scrollable/selectable and list all current findings.
11. The modal editor blocks play/reload/switch actions. Closing the main window must
    refuse until the editor closes. During save, fields and closing are disabled.
    If a save failure occurs, the form retains copyable draft text and the desktop
    requires reopening before further mutations; closing the form does not roll back.

Backend review note: typed corrections also reject a missing/stale target under the
session guard, including concurrent fact-knower changes. That cannot normally be
triggered through the modal UI; external files follow explicit Update State and are
not watched or merged. No automated runtime harness was run for this task.

Release build passed with zero warnings/errors. The player reported canon-editor
testing looked good on 2026-09-09. Implementation/design: `docs/design/CANON_EDITOR_UI.md`.

Retry and Reroll (2026-09-09; these use the configured model APIs):

1. In the empty shell, preview and a new turn-zero playthrough, confirm both actions
   are disabled. Open a disposable save with a displayed turn; both become available.
2. Keep a draft, then use Retry. Confirm the draft and narration remain unchanged,
   the turn number does not advance, and the report shows the latest extraction's
   no-ops/rejections and the turn's cumulative applied changes. World details and
   narration links refresh. Reopen to verify the repaired turn was persisted once.
3. On a turn with applied changes, use Reroll: expect a refusal without a model call
   or transcript change. Retry that turn, then Reroll again: earlier applied changes
   must still prevent reroll even when Retry adds nothing or extraction fails.
4. On a turn with no applied changes, use Reroll. Confirm it replaces the last
   narration, retains the player input and draft, and does not append or advance a
   turn. Reopen to confirm the replacement persists. If the reroll applies changes,
   the next Reroll must refuse.
5. During either operation, Send and Story actions are disabled, and close/switch
   requests are refused. Reports support selecting/copying text, resizing and Escape.
6. Extraction failure retains the saved narration and reports the error. An unexpected
   exception requires reopening before Send/Retry/Reroll: these operations can fail
   saving without changing the turn number, and the API does not identify the failure
   stage. This conservatively includes narrator-call exceptions. Reload/check remain
   available but do not remove the reopen requirement.

Release build passed with zero warnings/errors. The player reported Retry/Reroll
testing complete on 2026-09-09; no automated tests or agent-run model calls were performed.

Canon actions (2026-09-09):

1. With no session or with the preview, confirm Update State and Check Canon are disabled.
   Open a disposable playthrough and run Check Canon. Its selectable, scrollable report
   checks current in-memory state with the pack's lore; it does not reload or write files.
2. Keep a draft and an entity detail open. Edit that entity's description in the save's
   canon JSON, then choose Update State. Confirm the report lists the changed entity,
   its details refresh, and the draft, selected tab and narration position are retained.
   Rename an entity and confirm narration links reflect current names.
3. Reload unchanged canon, then try a missing canon file and malformed JSON (restore it
   afterwards). Expect distinct unchanged/missing/error results; missing or unreadable
   files keep the current world. No automatic repair or save is performed.
4. Introduce a dangling location or knowledge reference in the disposable save and reload.
   Confirm the world is adopted with an integrity warning. Check Canon should report the
   same warning; valid lore references must not be reported as missing facts.
5. During a live turn, both actions must be disabled. During reload, Send is disabled
   and close/switch requests are refused. After an earlier partial-save failure, neither
   action removes the requirement to reopen before sending another turn.
6. Resize the result dialog and inspect/copy a long report. Close it with Escape or Close.

Release build passed with zero warnings/errors on 2026-09-09. The player tested
the canon actions and reported everything working on 2026-09-09.

Recent and reload update: open a playthrough from Worlds or Playthroughs, close
it, then confirm **File -> Library -> Recent** can reopen it directly. Reopen a
save with turns and confirm the opening appears before the recent turn transcript.
Missing recent workspaces/worlds/saves should stay selectable but refuse with a
clear Library message.

Character links also accept unique name parts (Eddie → Eddie Mercer). Titles are
excluded; shared surnames such as Vale stay plain. Location/item links still
require the full name. Check Eddie and Vivian in The Last Lantern narration.

Automatic links: opening and live/resumed narration link exact unique current
character, location and item names (case-insensitive, whole names). Click one to
open its detail tab. Duplicate names remain plain, and longer names take priority
over shorter matches. Aliases and historical names are not inferred. Links are
display-only and are rebuilt from current canon; saved prose is unchanged.

Send interaction update: submitted input immediately appears as "YOU · SENDING";
the composer clears and disables until completion. Failure removes that pending
entry and restores the draft. Authored opening paragraphs reflow across the pane
instead of retaining single source-file line breaks.

Send is now connected for live sessions (superseding the earlier disabled-Send
notes below). This uses the configured model APIs. On a disposable playthrough,
send an action and confirm one narration pair, one advanced turn and refreshed
details. While waiting, repeated Send must be disabled and close/switch must be
refused visibly. A new draft typed while waiting should survive completion.
Blank input and preview must not enable Send. Check a failed provider call keeps
the draft, and reopen a successful save to confirm the new turn is persisted.
Extraction failure retains narration with feedback. A save failure after canon
changes blocks further Send until reopening. Retry/reroll review is described above.

Transcript: start a new playthrough and confirm its authored opening appears with
resolved names. Resume a save with turns and confirm recent player actions and
narration appear in order, scrolled to the end. Reopening a turn-zero save should
still show the opening. Send remains disabled. History loading uses the configured
history window and reports unreadable history while keeping the session open.

Window placement: resize and move the main window and Library independently,
close and reopen each (including Library Cancel), then restart the app. Check
maximized reopening and restoring to the previous normal size. If using multiple
monitors, disconnect the saved monitor and confirm the window remains accessible.
Placement is stored in `window-main.json` and `window-library.json` beside the
desktop view preferences.

Resize Library: the active list should expand or shrink and scroll internally,
while workspace controls and action buttons remain visible. Minimum size is
520 × 650.

Library rows use compact spacing. Check playthrough sorting in both directions
for name, last saved date and turn count. Newest saved is the default; unknown
dates/counts sort last, and changing order preserves the selected playthrough.

Latest Library fixes: choose a workspace and Cancel, then restart and confirm it
is retained. Leave the new-save identifier blank and double-click a world to
create a dated save. Confirm both lists scroll internally without growing the
window, and playthrough rows show turns and last saved dates. Select a legacy
save, choose its world and double-click to resume; selecting the legacy row alone
must not crash. Explicit duplicate save identifiers must still be refused.

1. Start without `--preview`, choose **File → Library**, and select the repository
   root as the workspace. Confirm **Worlds** lists its packs with manifest name,
   author and version. Enter an existing save ID such as `marrow`: it must refuse
   to start and direct you to Playthroughs. Enter an unused disposable ID, then
   confirm it starts a new playthrough from the selected world.
2. Choose **File → Open Save**. Confirm it lists existing saves and opening `marrow`
   does not ask for a world because the save records it. Choose an entry labelled
   **pack unknown** and confirm the explicit legacy-world choice appears. Close the
   Library, reopen it and confirm the workspace is remembered.
3. Select `marrow`, open its existing `marrow` save, then confirm the window shows
   a live-save status and its Characters, Locations, Canon and Items are real world
   information rather than the preview. Close the playthrough with **File → Close
   Playthrough**, reopen it, then exit the app and reopen it to confirm the save
   lock was released each time. A save opened by another CLI or desktop process
   should show the held-save message in Library.
4. To review player creation, select `ashfall` through **New Playthrough** and enter
   a new, unused save id.
   Confirm blank names are rejected, Cancel returns to the shell without leaving
   the save locked, and Begin asks for no more desktop questions. This creates a
   real save when Begin succeeds; use a clearly disposable id for the review.
5. Open the preview. Confirm the menu bar is above both panes and the preview label
   is visible. File/session commands and Send must be disabled.
6. Type a draft, open Mona from the list, go Back, switch tabs and return. Confirm
   the selected detail is retained across tab changes and the draft is unchanged.
   In each tab's detail view, confirm fields have visible labels, such as
   **Mood: Uneasy**, **Connections: Marrow Square**, and **Condition: Intact**.
   Check long values wrap within the pane in both light and dark themes.
7. Click Mona, Hald and Marrow Square in the narration. Confirm the matching tab
   and details open without moving the narration scroll position or clearing input.
   Check that link text shares the surrounding baseline before, during and after
   hover, including at the smallest and largest narration text sizes.
8. Scroll the narration. Confirm the composer stays available below it. Resize the
   divider by dragging and by using Left/Right while it has keyboard focus.
9. Use View to hide/show the world pane and reset the split. Click a narration link
   with the pane hidden: the pane should reopen with the linked entity selected.
10. Use Edit's Cut/Copy/Paste/Select All and Undo/Redo on the composer. Text editing
   must not change the preview's story or world information.
11. Change text size and theme. Resize the window down to its minimum dimensions.
   Check wrapping, scrolling, tab visibility and light/dark contrast.
   Confirm list subtitles and detail summaries stay readable when unselected,
   hovered, selected and keyboard-focused, including after switching themes.
12. Close and reopen the application. Confirm view preferences survive. The draft
   should be empty because draft persistence is not implemented.
13. Start without `--preview`; confirm the empty-state message and preview button.

Build validation: `dotnet build StoryWeaver.sln --no-restore -c Release` completed
with zero warnings and zero errors on 2026-09-08. Runtime/UI review remains manual,
by the player. No automated tests or live model calls were run.

## Boundary and next work

Views handle Avalonia layout, text focus, menus and dialogs. ViewModels hold only
presentation state; Presentation records are immutable display values, not canon.
`WorkspaceLibrary` discovers files only; `SessionOpener` in App composes and opens
the real session. Preview supplies explicit sample references. No backend project
references Avalonia.

Next: entity creation/removal, pack editing, and reliable backend narration-reference
generation/persistence. The
shell's explicit sample links are not a solution to that last backend question.
Future mod UI remains part of the mod-system design.

Framework references: [Avalonia Desktop package](https://www.nuget.org/packages/Avalonia.Desktop/12.1.2),
[GridSplitter](https://docs.avaloniaui.net/controls/layout/panels/gridsplitter),
[Menu](https://docs.avaloniaui.net/controls/menus/menu).
