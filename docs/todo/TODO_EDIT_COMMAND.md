# TODO: Wire the escape hatch into the CLI

**Status:** DONE 2026-09-04
**Created:** 2026-09-04

`StorySession.EditAsync` — direct canon editing, for what the delta set cannot express — was
built with `StorySession` and **has no caller outside its own two self-tests.** So the path the
player asked for exists and is unreachable, and the argument that *"using the hatch is what will
say which delta kinds are wanted"* cannot start until something can use it.

The two-tier delta set is **parked** (2026-09-04): not built, and not to be built until something
needs a kind that does not exist. This is the instrument that will say when.

---

## What the hatch is for

The 17 delta kinds describe *things that happen in a story*. Authoring is not a story event, so
they have holes exactly where an editor needs them:

| | delta? |
|---|---|
| fix a description | none |
| reword a fact | none |
| make someone *un*-know something | none |
| remove something added by mistake | none |

## Decisions

| question | answer |
|---|---|
| Where do the edits themselves live? | **`Core/CanonEdits.cs`.** Removing a character and forgetting the items they held is *policy*: a console that gets it wrong and a window that gets it wrong differently is exactly the drift the boundary work exists to prevent. The CLI picks which edit and supplies arguments. |
| What shape? | Each returns an `Action<WorldState>`, which is what `EditAsync` takes. |
| One command or several? | **One `/edit`**, with a guided choice. Keeps the escape hatch to one place, and one place to warn. |
| When does the warning appear? | **Only on removal.** A warning on every edit is clicked through by the third one. Rewording a description corrupts nothing; the risk is concentrated in ids and references. |
| What does the warning say? | **What will actually happen**, computed from canon — *"the iron key is held by Hald and will be left held by nobody"* — not a generic "this may break your session". A warning that overstates gets dismissed. |

## What counts as an obvious repair

An edit fixes what has one right answer, and lets `CanonRefresh.Check` report the rest. It never
refuses — canon belongs to the player.

| removing | repaired automatically | left for the player, and reported |
|---|---|---|
| a character | held items placed where they were | items they held while offstage |
| a place | people there become offstage; connections to it dropped | items lying there |
| an item | nothing references items | — |
| a fact | dropped from everyone's `Knows` | — |

## Build

- [x] `Core/CanonEdits.cs` — describe, reword a fact, forget, remove, and the consequences of a
      removal computed *before* it happens
- [x] `ConsolePrompt` — the ask/confirm helpers, shared rather than copied out of
      `AuthoringCommands`
- [x] `/edit` in the CLI, calling `session.EditAsync`
- [x] Removal shows its computed consequences and asks for confirmation
- [x] `/help` lists it, and says what it is for

## Self-tests

- [x] Describing a place, a person and an item each rewrite the right field
- [x] Rewording a fact leaves who knows it untouched
- [x] Forgetting removes it from one character and nobody else
- [x] Removing a character places the items they held where they were
- [x] Removing a character who was offstage leaves their items held by nobody — **and `Check`
      reports it**, which is the honest half
- [x] Removing a place makes the people there offstage and drops connections pointing at it
- [x] Removing a fact drops it from every character's `Knows`
- [x] Consequences are computed correctly *before* the edit, and the edit then does exactly them

## Verify

- [x] `dotnet build` clean, 0 warnings
- [x] All existing self-tests pass
- [x] By hand: rewrote a place's description; removed a character holding an item and read the
      computed warning — *"An iron key (iron-key) is held by them, and will be left in
      marrow-tavern"* — confirmed, and canon afterwards had the key lying in the tavern with no
      holder and the character gone. **The warning said exactly what then happened**, which is
      the whole point of computing it from canon rather than writing a general caution.

## Close out

- [x] Devlog, `CANON_OWNERSHIP.md` §5 updated to say the hatch has a caller and the tier is
      parked, `TODO_FUTURE_WORK.md`, no unchecked boxes

## Not in this task

- **The second tier of delta kinds.** Parked deliberately. The point of this task is to make the
  question answerable rather than to answer it now.
- **Undo.** Deltas are not invertible and canon has no snapshot; this is the same obstacle
  `/reroll` already documents.
