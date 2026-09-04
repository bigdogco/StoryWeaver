# 2026-09-02 — The UI boundary, drawn before the UI

Phase 2 opens with no Avalonia in it. The player asked a question first, and it was the right
one to ask before a window exists:

> If we decide that Avalonia UI is no good, changing UI will be simply rewriting UI part, not
> touching the core engine under it.

## Two commitments were merged in the question

Only one of them is worth making, and separating them is most of what this entry records.

**The one worth locking: a UI is a thin layer, never a driver.** No gameplay, narration or
authoring logic in a UI project. It collects input, calls Core, renders what comes back.

**The one rejected: CLI/UI feature parity.** *"Everything the UI can do can be done through a
`/command`"* sounds like the same idea. It is a tax on every future feature, and it is paid in
the wrong currency — it drags the UI down to whatever a text prompt can express. Dragging a
character onto a location has no honest slash-command form; the attempt produces `/place`, which
is precisely the interface that made a sound design read as a bug back on 2026-08-06.

The player's own restatement is better than the original and is what went into `PROJECT.md`:
*all gameplay and narration logic sits outside the UI; the UI is a thin layer, not a heavy
driver.*

**The CLI is not the API — Core is.** The CLI is the first client and is allowed to be a worse
one.

## Then the claim was checked rather than assumed

Half of it was already true. `TurnEngine` exposes three public methods, mentions no console, and
talks to models through `INarrator` and `IStateExtractor` — Core's own vocabulary, not a
provider SDK's. `PlaySession` is 740 lines of `Console.ReadLine` and banner printing wrapped
around those three calls. Abandoning Avalonia genuinely would not touch Core, Llm or Storage.

**Authoring was not, and this is the part that would have bitten.** `AuthoringCommands.cs` had
the rules and the prompting welded together: `CommitAsync` was validate-apply-save with
`Console.WriteLine` threaded through the middle, `Slug()` was the id convention, `AskId` carried
the collision rule, `Summarize` was the vocabulary for what a delta did.

A UI written today would have reimplemented all four. The two copies then drift on exactly the
two things that must not: **the shape of an id, and whether a save happened.** That is the same
failure the player was trying to avoid, one layer below where they were looking — which is the
general argument for checking a comfortable claim instead of agreeing with it.

`Core/Authoring.cs` now owns the policy and contains no `Console` at all. The console file kept
the conversation — what is listed before each question, what order things are asked in, how a
rejection reads on a terminal — and lost everything else. The split has a test: a form with
three text boxes asks these questions in no particular order and calls the same builders.

## One thing deliberately not fixed

`Slug` is in **Core**. `EntityId.IsWellFormed` is in **Storage**. They are two halves of one
convention — every slug must satisfy the check — and dependencies point inward, so Core cannot
reference it.

Moving `EntityId` is a structural change and gets its own decision rather than riding along
inside a refactor. The bridge is a self-test asserting eight slugs against `IsWellFormed`,
including the two cases with taste in them: an apostrophe is dropped rather than separated
(`kings-investigators`, never `king-s-investigators`) and a fact slug stops at four words. If
the halves ever disagree, a test fails instead of a save quietly acquiring an id that nothing
else can match.

## The one new behaviour

**A commit that accepts nothing now writes nothing.** Previously the save happened after the
accepted-count check by luck of ordering; now it is stated, and tested. Harmless today, wrong
anyway: once an editor window exists, the author may have the file open, and rewriting it for a
change that did not happen is how a tool eats an edit.

## Measurements

`dotnet build` clean, 0 warnings. Self-tests **98 pass, 0 fail** — up from 85, all thirteen new
ones covering the policy that just stopped having a single caller.

**Verified by hand the same day**, against a scratch save and at **zero API cost** — none of the
five commands touches a model. Canon on disk carried every change: the new place, a character
seated at `marrow-square`, the fact with the player knowing it, Mabb renamed with her
description preserved, Hald knowing the cult. The lock released and `save.json` recorded the
origin.

The error paths are the half worth reporting, because they are where a refactor of this shape
actually breaks. An id colliding with an existing location, then colliding again with a place
added ninety seconds earlier — re-prompted both times, nothing typed lost. Unknown character id
and unknown lore id both refused. And `/knows` listed `cult-of-the-blind (already knows)` on the
second run, which is the listing reading live canon rather than the seed.

## A papercut found, and deliberately not fixed

Once an authoring prompt has started, every line is answer text. Typing `/quit` at a description
prompt does not abort — it creates a location described as the literal string `/quit`.

Pre-existing, not a regression, and it took a malformed test input to hit. The fix is three
lines and it is still filed in `CHALLENGES.md` rather than written: a rule invented against a
self-inflicted input is precisely what `PROJECT.md` §3 warns about, and blank already cancels at
every prompt that says so.

It also **dissolves rather than transfers**. A form with a Cancel button cannot have this
failure, and authoring is where the UI arrives first — which is a small argument that the
boundary work was pointed the right way.
