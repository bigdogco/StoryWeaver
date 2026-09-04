# 2026-09-04 — The escape hatch gets a caller, and the delta tier is parked

Third piece of the day. The decision that shaped it was not to build something.

## The thing I should have noticed sooner

`StorySession.EditAsync` — direct canon editing, the escape hatch the player asked for — had
**no caller outside its own two self-tests.** Built, tested, unreachable.

That mattered because the argument for a second tier of delta kinds rested on it: *"using the
hatch is what will say which kinds are wanted."* The instrument meant to answer the question was
switched off, so choosing kinds now would have been choosing by imagination.

**Parked, and this is the caller that will say when to revisit.**

## A sharper point about what the tier is even for

Checking what a delta buys over the hatch narrowed it considerably. Neither leaves an audit
trail — `AuthorAsync` deliberately appends no `TurnRecord`, because the turn log feeds the
narrator's prose window. So the only real difference is **validated-before versus checked-after**.

For rewording a description that is nearly nothing: the entity exists, there is little to
validate. For **removal** it is the whole game — deleting a character orphans items they held and
knowledge that points at them, and through the hatch `Check` reports the wreckage after you have
made it.

So the honest version of the proposal is the inverse of the case that motivated it: **the tier is
worth it for destructive operations and weak for cosmetic ones.** Recorded rather than acted on.

## What was built

`/edit`, with four things the delta set cannot say: rewrite a description, reword a fact, make
somebody forget, remove something.

**The edits live in `Core/CanonEdits.cs`, not in the console.** Removing a character and
forgetting the items they held is policy — a console that gets the cascade wrong and a window
that gets it wrong differently is exactly the drift the boundary work exists to prevent. Each
edit returns an `Action<WorldState>`, which is what `EditAsync` takes, so every one runs inside
the single-writer guard and is followed by `CanonRefresh.Check`.

## What is repaired, and what is deliberately left

An edit fixes what has one right answer and lets the check report the rest.

| removing | repaired | left, and reported |
|---|---|---|
| a character | held items placed where they stood | items they held while offstage |
| a place | people become offstage; connections dropped | items lying there |
| a fact | dropped from everyone's knowledge | — |

The left-alone cases are the honest half. An item lying in a removed place has no right answer,
and inventing one would move somebody's belongings somewhere they never were.

## The warning, and why it is computed

The design note said a warning shown on every edit is clicked through by the third one, and one
that overstates teaches people to dismiss it. So: **it appears on removal only**, and it is
derived from canon rather than written in advance.

```
Removing drinker-mabb — Mabb: An old marsh-hand gone soft at the edges...

  !  An iron key (iron-key) is held by them, and will be left in marrow-tavern

  This cannot be undone — canon has no snapshot to go back to.
  Type 'remove' to go ahead, anything else to cancel:
```

Afterwards the key was lying in the tavern with no holder, and the character was gone. **The
warning said exactly what then happened**, which a general caution could never do — and a
self-test now holds those two in step, because a warning that drifts from the behaviour is worse
than none.

Confirmation is the word `remove`, not `y`. A `y` is muscle memory; typing the word is a
decision, and this is the one place in the console where that is worth the keystrokes.

## Also

`ConsolePrompt` now holds the ask/confirm helpers that `AuthoringCommands` had privately. A
second copy of "blank means cancel" that drifted from the first is the sort of thing nobody
notices until it eats an answer.

## Measurements

`dotnet build` clean, 0 warnings. Self-tests **132 pass, 0 fail** — up from 123, eight new ones
on the cascades. No API calls.
