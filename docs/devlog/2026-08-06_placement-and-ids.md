# 2026-08-06 — placement and ids

Two load-time refusals in `WorldPack`, and one rule about who gets to name the player. All
three amend the character-sheets design shipped earlier today; none came from a failing
session. All three were found by asking what a piece of the design actually did and following
the answer.

Design: §9 of [`CHARACTER_SHEETS.md`](../design/CHARACTER_SHEETS.md).
TODO: [`TODO_CHARACTER_SHEETS.md`](../todo/TODO_CHARACTER_SHEETS.md).

---

## How it was found

Not by a bug report. By answering a question out loud.

The question was whether a character with a sheet but no `seed.json` entry ever appears in the
game. Decision 4 said they exist offstage, which sounded fine. Tracing what would actually
bring them into a scene turned up nothing:

| route | why it fails |
|---|---|
| the narrator introduces them | `AppendNpcs` filters on the player's location; someone nowhere is in no location |
| the player names them | *mention never creates or moves an entity* — 0/7, consistent across 21 runs |
| `/character` places them | it only **introduces**; `AskId` refuses an id already in canon |
| the extractor moves them | `AppendKnownIds` gives it a bare slug with no name and no description |

So the pack loaded, the character was in canon, and they could never appear. Not dormant —
**unreachable**. Same silent-drop shape as a discarded delta or a budget-cut lorebook entry,
both already written up as things not to do, and neither of the existing guards covered it.

Worth recording that the tool for finding this was a sentence, not a stack trace. The same was
true of the narration-history gap.

## What changed

### A seeded world places everyone in it

`ApplySheets` used to create the missing character with `LocationId = null`. It now refuses,
naming the file: *`warrior-mike` has a sheet but no place in seed.json.*

`RequireEveryoneIsPlaced` is the general form, and is **broader than the design said** — every
character in a seed needs a location, not only those with sheets. A `locationId: null` entry
with no sheet is unreachable for identical reasons and authored by the same person; refusing
one and not the other would have been a rule about files pretending to be a rule about
authorship.

Two checks rather than one because they can say different things. The sheet case knows
something the general case does not: *the sheet exists, the seat does not.* One rule, two
errors, two messages.

### `/character` deliberately untouched

Blank-means-offstage stays for characters invented in play — a brother back home, a name from a
rumour. **The rule divides on who authored the character, not on whether a location is known.**
A player who invented someone remembers them and can bring them up again; an author who forgot
a seat has no such memory, and no symptom either.

That asymmetry is the whole finding. A permissive rule inherited from one authoring path was
applied to another where nothing could recover from it.

### Ids are kebab-case, enforced

`EntityId` — lowercase letters, digits, single hyphens, no leading, trailing or doubled hyphen.
Applied to sheet filenames, lore filenames, and the character/location/item/fact keys in
`seed.json`.

The convention already existed and nothing enforced it. `warrior_mike` and `warrior-mike` are
one character to a reader and two strings to every exact-match comparison in the codebase — a
sheet filename against a seed key, a `{{ }}` reference against canon, an attitude target
against the lorebook. A sheet under one spelling and a seed entry under the other produce a
character with no seat and an entry nothing owns. Both halves load. Neither complains. And the
diff is one glyph in the middle of a word, which is the kind of mistake that survives being
looked for.

Checked before anything reads the ids, so a malformed one is reported as itself rather than as
the dangling reference it causes.

Hand-written rather than a regex: three conditions that read as their own specification, where
the pattern would have needed a comment saying the same thing.

### A player sheet replaces character creation

Third amendment, found the same way — by asking what happens when `characters/player.md` does
not exist, and discovering the interesting case was when it *does*.

A sheet and the opening prompts write the same two fields, and the prompts run second. So a
pack shipping `player.md` had its authored name overwritten every time, and its premise — *"you
carry the crown's seal"* — overwritten the moment the player typed any description at all. Both
halves working exactly as written. No symptom.

`WorldPack.AuthorsThePlayer`: ship the file and the harness does not ask. The direction follows
decision 1, where it always did — *the sheet defines the character* — and the player was the
last exception to that, for no reason beyond the order the two features were built in.

What it buys is that both shapes are expressible by the presence of one file: a named
protagonist with `player.md`, a blank slate without. `/rename` already keeps the first from
being a cage, and the session says so on the way in, because a game that never asks your name
reads as one that forgot to.

`worlds/marrow` deliberately ships no `player.md` — it is the blank-slate world, and the only
coverage the prompt path has.

## Found while building

**A check on shipped content needs a check against shipped content.** Every self-test here
builds a pack designed to fail, which says nothing about whether a tightened rule broke the
world in the next folder over. `CheckShippedPackLoads` loads the real `worlds/marrow` through
the real path — skipping rather than failing when run from a directory that has no `worlds/`,
since the harness has always been runnable from anywhere.

It reports `3 seated, 2 with sheets, 3 lore`, which is also the confirmation Hald and Mabb both
kept their seats.

## Left open

**Whether extraction-proposed ids get the same treatment.** `Slug()` already produces
kebab-case for the authoring commands, so the question is only about the model's own
`character_introduced`. Refusing there is a rejection cascade rather than a refused load — a
different cost, and it belongs with `DeltaValidator` rather than with this.

## Verified

- `dotnet build` — clean, 0 warnings
- `--selftest` — all four suites pass, including six new checks:
  - a sheet with no seed entry fails the load
  - a seeded character with no location fails the load
  - a seeded character with **no sheet** still loads untouched (the regression that would
    otherwise have been silent — tightening the sheet rule must not make a sheet mandatory)
  - the id accept/refuse table
  - a sheet filename with an underscore fails the load
  - a player sheet replaces character creation, **and only then** — both branches, because
    checking one would pass while the blank-slate path silently stopped asking anyone's name
  - `worlds/marrow` still loads

Untested by machine, as ever: an actual play session.
