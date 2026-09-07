# Play UI — first design

2026-09-07. Initial design from discussion with the player. **Avalonia UI is the
selected framework.** This document records the agreed direction; details marked
proposed or open still need design discussion before implementation.

## Purpose and scope

An RPG interface where the player can read and act while inspecting the world.
This is the Play screen design, not the whole of Phase 2. A library, save selection
and world-pack editor still need their own workflows. Phase 2 continues to include
creating and authoring worlds without a terminal.

## Agreed layout

The screen has two side-by-side panes separated by a draggable divider. Their
widths are adjustable; an equal split is not a fixed requirement.

| Left: story | Right: world information |
|---|---|
| Scrollable narration | Tab strip |
| Input composer below narration | Selected tab's list or detail view |

The input remains available below the narration. Information on the right is
divided into tabs, including Characters, Locations and Canon. Future mods should
be able to supply UI here; the extension mechanism belongs to the future mod design.

Characters use **a list with a detail view** for now. Selecting a character opens
their details in the same pane. Back returns to the list.

## Proposed interaction details

- Preserve list position and filters when returning from details. Each tab keeps
  its own selection and scroll position when switching away and back.
- Remember the divider position and set minimum usable pane widths.
- Keep the input composer anchored. Reading older narration should not be
  interrupted by automatic scrolling when a new turn arrives.
- Characters shows people present first, with access to the full cast. Details
  show the character sheet and current state.
- Locations shows the current place, connections and other known locations.
- Canon shows established facts and who knows them. **Facts** is a proposed clearer
  tab name, since characters and locations are canon too; the name is not settled.
- An **Items** tab exposes the item state already supported by the engine.
- Correction actions operate on the running save. Editing a reusable world pack
  is a separate authoring context and must be labelled accordingly.

## Narration links

The player wants clickable references to characters, places and other entities in
narration. A link selects the appropriate right-hand tab and opens that entity's
details without losing narration position or unfinished input.

Proposed presentation: subtle accent styling, underline on hover, keyboard focus
and activation, and a small descriptive hover preview. Hover must not be the only
way to obtain information.

Links must target **permanent entity IDs**, not mutable display names. The UI must
not guess identity by turning every matching name into a link: names can collide,
change, or refer to something that does not yet exist in canon. Unresolved or
ambiguous references should remain readable plain text. A reference whose target
no longer exists should report that gracefully, without silently opening another
entity. Following a link never creates an entity or changes canon.

**Open backend design:** how narration supplies reliable entity references, when
they become available relative to extraction, and how references survive save and
resume. Also decide what happens to older narration without reference metadata.
No markup format, model-output change or save-format change is selected here.

## Turn feedback and session ownership

Proposed: keep compact turn status near the narration, visible regardless of the
selected tab. Narration remains visible when extraction fails. Rejected state
changes and failures need a clear indication with inspectable details.

Busy state, retry/reroll availability, save errors and closing during an operation
must reflect backend outcomes. The UI must not invent rollback or recovery rules.
An **Update State** action exposes the existing reload operation for external
canon edits and displays its results.

Avalonia owns layout, bindings, navigation and interaction state. App composes and
opens sessions; Core's StorySession owns canon and mutations. Editing must call
session operations rather than mutate the exposed world graph through bindings.
The UI must not own gameplay, authoring policy or entity-resolution policy.

## Next design decisions

1. Draw the Play wireframe, including list, detail, busy and failure states.
2. Settle tab names, initial contents and in-session correction interactions.
3. Design reliable narration references against the existing narration pipeline.
4. Choose visual styling, keyboard navigation and behaviour at narrow window sizes
   or with more tabs than fit, including future mod tabs.
5. Design library, save selection, player creation and pack-authoring workflows.
6. Select Avalonia version, runtime requirements, desktop paths and session lifetime
   before scaffolding. This document does not approve a runtime upgrade.
