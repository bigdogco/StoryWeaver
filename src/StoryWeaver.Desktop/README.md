# StoryWeaver desktop shell

Avalonia 12.1.2 on the existing .NET 8 target. The desktop client can open a
real session from a selected workspace, but live turns and history rendering are
not connected yet.

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
  resumes the world recorded by that save. Library offers both routes. World Editor
  and turn commands remain disabled.
- Library selects a workspace that contains `worlds/` and `saves/`, then keeps
  authored worlds and saved playthroughs in separate views. Older saves without
  provenance are labelled **pack unknown** and require an explicit, unverified
  world choice before they can be resumed. The selected workspace path is retained
  with desktop preferences; content and saves stay in that workspace.
- Opening calls App's `SessionOpener` with the selected roots. It shows save-lock
  and settings errors, asks for a player name only when a pack does not author the
  player, and releases the pending lock if the form is cancelled. A live session
  projects its current canon into the existing world-information tabs and is
  disposed when it is closed, replaced or the app exits.
- Resizable narration and world panes, with an input composer below the transcript.
- Characters, Locations, Canon and Items tabs, each retaining list/detail navigation.
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

Next: real narration/history, turn/retry/reroll feedback, canon editing, pack
editing, and reliable backend narration-reference generation/persistence. The
shell's explicit sample links are not a solution to that last backend question.
Future mod UI remains part of the mod-system design.

Framework references: [Avalonia Desktop package](https://www.nuget.org/packages/Avalonia.Desktop/12.1.2),
[GridSplitter](https://docs.avaloniaui.net/controls/layout/panels/gridsplitter),
[Menu](https://docs.avaloniaui.net/controls/menus/menu).
