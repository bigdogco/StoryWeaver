# 2026-09-02 — Update State, and a bug that was already there

Second piece of Phase 2, closing the last open question `PROJECT.md` recorded against the phase.
Still no Avalonia.

## The question, and why the parity rule did not answer it

*Is Update State UI-only, or also a CLI command?*

The rule locked earlier the same day rejected CLI/UI feature parity, so the tempting reading is
that it settles this: UI-only, done. It does not. **That rejection is about UI-shaped features**
— dragging a character onto a location has no honest slash-command form. Update State is a verb
with no arguments and no interaction, and would make a perfectly ordinary command. The rejection
makes UI-only *permissible*; it does not make it *correct*.

**Both**, and not for symmetry. The deciding fact is that the bug is in the CLI today, before
any UI exists, and the CLI is where the long runs happen — 200 and 250 turns, twice. Mid-run is
exactly when someone reaches into canon to fix something.

## The bug, reproduced rather than argued

§3 locks that the player owns their world and may edit it directly. A running session did not
honour that: it loads canon once and holds it in memory, so an external edit was invisible and
then overwritten by the next save.

That was a claim about the code, so it got measured. A live session, an external edit adding
`the-hand-edited-cellar` to `canon.json`, one real turn:

| | |
|---|---|
| without `/reload` | **the location was gone** |
| with `/reload` | it survived the turn |

Driving this needed a genuinely stale in-memory copy, which means editing the file *while* a
session runs — so the session was fed from `tail -f` on a command file rather than a pipe, and
the edit was made between commands. Worth recording: a FIFO does not work here, because the
shell holding the write end and the process holding the read end race for what gets delivered.

## Reported, never refused

`Core/CanonRefresh` re-reads, diffs, and checks; `/reload` renders it and a UI button will call
the same function. The checks warn and never block. `DeltaValidator` exists to be suspicious of
a cheap model that confidently invents things — a person editing their own canon does not need
to be argued with, and refusing their file because an item is in an odd state would be that
posture pointed at the wrong party.

**No file watching, no merge, no reconciliation.** An explicit action is the whole mechanism.
The alternatives trade a problem an obvious button solves for a class of problems nothing does.

## An error in PROJECT.md, found by specifying it

§3 listed the invariants as *"no dangling fact ids, no item both held and placed, no character
without a location."*

**The last one is wrong.** `Character.LocationId` is nullable precisely so a person can exist
offstage — a brother back home, a name from a rumour — and the authoring path offers exactly
that as *blank = unknown / offstage*. Implemented as written, it would have warned about every
correctly-authored offstage character on every reload.

The real rule is *a location, when set, names a real place.* Corrected in `PROJECT.md`, and a
self-test now asserts both halves — offstage is silent, dangling is not.

This is the second time this week that writing a feature down carefully found something wrong
that reading it had not. The first was authoring policy welded to `Console`.

## Two details worth keeping

**The diff compares serialized entities, not fields.** A hand-written comparison silently stops
covering any field added after it was written — the diff keeps passing and quietly misses the
new one, which is this feature's own failure mode one level up.

**String arrays are sorted before comparing.** `Knows` and `Connections` are sets; their
enumeration order differs between a set built by replaying deltas and the same set read from a
file. Without the sort, every reload would report every character as changed, and output that
cries wolf gets ignored — the lesson row 4 of the narration audit already paid for.

## The EntityId trigger has fired

`TODO_UI_BOUNDARY.md` deferred moving `EntityId` into Core with a trigger: *revisit when a
second Core caller needs to validate an id.* `CanonRefresh` is that caller — a hand-edited id of
`Warrior_Mike` is exactly the kind of thing a reload should warn about, and it cannot, because
the check lives in Storage and Core cannot reach it.

Not done here. It is a structural change and gets its own decision rather than riding inside a
feature, which is the same reason it was not done last time.

## Measurements

`dotnet build` clean, 0 warnings. Self-tests **107 pass, 0 fail** — up from 98, nine new ones on
the reload path. Four live API calls spent, all on the before/after proof above.
