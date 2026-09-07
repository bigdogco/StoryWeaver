# StoryWeaver desktop shell

Avalonia 12.1.2 on the existing .NET 8 target. This is the first Play shell,
not a playable graphical client yet. No API key is needed to review it.

From the repository root:

```powershell
dotnet run --project src/StoryWeaver.Desktop -- --preview
```

Omit `--preview` to start with the empty shell. Its **Explore a preview scene**
button loads the same illustrative scene. This data is owned by the preview and
is never opened as a StorySession or written to a save.

## What is implemented

- File, Edit, View, Story and Help menus. Session/file-authoring commands and Send
  are visibly disabled until their backend workflows are connected.
- Resizable narration and world panes, with an input composer below the transcript.
- Characters, Locations, Canon and Items tabs, each retaining list/detail navigation.
  Tabs wrap when the world pane becomes narrow. The desktop window has minimum
  dimensions of 800 × 560; this is not a mobile layout.
- Preview narration references select a tab and open the entity by kind and stable
  ID. The renderer never guesses targets from names. Unresolved references render
  as plain text; missing navigation targets produce a notice.
- Text editing through Edit and native text-box shortcuts. View controls for panel
  visibility, splitter reset and narration size. Settings offers System/Light/Dark.
- Presentation preferences saved under `%LOCALAPPDATA%/StoryWeaver/desktop-view.json`.
  These contain only the divider ratio, world-panel visibility, text size and theme.
  They do not contain drafts, credentials, world data or save paths. Drafts are
  retained while browsing, but are not saved when the application exits.

## Manual review

1. Open the preview. Confirm the menu bar is above both panes and the preview label
   is visible. File/session commands and Send must be disabled.
2. Type a draft, open Mona from the list, go Back, switch tabs and return. Confirm
   the selected detail is retained across tab changes and the draft is unchanged.
   In each tab's detail view, confirm fields have visible labels, such as
   **Mood: Uneasy**, **Connections: Marrow Square**, and **Condition: Intact**.
   Check long values wrap within the pane in both light and dark themes.
3. Click Mona, Hald and Marrow Square in the narration. Confirm the matching tab
   and details open without moving the narration scroll position or clearing input.
   Check that link text shares the surrounding baseline before, during and after
   hover, including at the smallest and largest narration text sizes.
4. Scroll the narration. Confirm the composer stays available below it. Resize the
   divider by dragging and by using Left/Right while it has keyboard focus.
5. Use View to hide/show the world pane and reset the split. Click a narration link
   with the pane hidden: the pane should reopen with the linked entity selected.
6. Use Edit's Cut/Copy/Paste/Select All and Undo/Redo on the composer. Text editing
   must not change the preview's story or world information.
7. Change text size and theme. Resize the window down to its minimum dimensions.
   Check wrapping, scrolling, tab visibility and light/dark contrast.
   Confirm list subtitles and detail summaries stay readable when unselected,
   hovered, selected and keyboard-focused, including after switching themes.
8. Close and reopen the application. Confirm view preferences survive. The draft
   should be empty because draft persistence is not implemented.
9. Start without `--preview`; confirm the empty-state message and preview button.

Build validation: `dotnet build StoryWeaver.sln --no-restore` completed with zero
warnings and zero errors on 2026-09-07. Runtime/UI review remains manual, by the
player. No automated tests or live model calls were run.

## Boundary and next work

Views handle Avalonia layout, text focus, menus and dialogs. ViewModels hold only
presentation state; Presentation records are immutable display values, not canon.
Preview supplies explicit sample references. Services stores desktop preferences.
The project references App for the next integration, but no App/Core session is
opened by the shell. No backend project references Avalonia.

Next: session opening/player creation, real narration and state projection,
turn/retry/reroll feedback and lifetime management, save/resume, canon editing,
library and pack editor workflows, and reliable backend narration-reference
generation/persistence. The shell's explicit sample links are not a solution to
that last backend question. Future mod UI remains part of the mod-system design.

Framework references: [Avalonia Desktop package](https://www.nuget.org/packages/Avalonia.Desktop/12.1.2),
[GridSplitter](https://docs.avaloniaui.net/controls/layout/panels/gridsplitter),
[Menu](https://docs.avaloniaui.net/controls/menus/menu).
