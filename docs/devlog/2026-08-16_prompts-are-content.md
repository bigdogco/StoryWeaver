# 2026-08-16 — Prompts are content

Fourth piece of Phase 1, and two jobs that turned out to be one: the per-pack narration
override designed in `WORLD_PACKS.md` §5, and the standing item *"no prompt string lives in
code"*. Once the engine's prompts are files, a pack layering its voice on top is just another
file.

## The thing that was actually wrong

Every world was narrated by the same hardcoded line:

> You are the narrator of a **dark fantasy** text RPG.

`the-last-lantern` is 1940 noir. `ashfall` is volcanic survival. Two of three packs were being
told they were something they are not — in second person present tense, at two to four
paragraphs, all of it taste and all of it in a `const string` requiring a rebuild to change.

Now `worlds/the-last-lantern/prompts/narration.md` says lean prose, specific nouns over
atmospheric adjectives, people who answer a different question rather than lie outright, and an
explicit *nothing supernatural, ever*.

## Added, never substituted — and enforced

The narrator prompt does two jobs in one blob. **Taste**: genre, length, tense, how much room a
scene gets. **Correctness**: never speak for the player, never rewrite their dialogue, never
write an internal id, characters know only what canon says.

That second group is the engine's guarantees and several exist because they broke first — the id
rule is why a turn once read *"the heavy oak door of the marrow-tavern flies outward"*, and why
`ForNarration` and `ForExtraction` are separate functions at all.

So a pack **adds** a voice section; it cannot replace the prompt. An author omitting a line must
not be able to silently drop a rule that cost real work, in a change that looks like content.
The self-test asserts both halves: the pack's voice arrives *and* `"Never write an internal
identifier"` is still in the same system prompt.

And a pack shipping `prompts/extraction.md` is **refused at load, by name**, with a message
saying what to do instead. Narration is taste and belongs to the world; extraction is
correctness and is measured. A silently ignored override is worse than a refused one — the
author sees a file they wrote having no effect and concludes the feature is broken.

## The byte-identity proof

The extraction prompt is the most measured artifact in this project; every extraction number
ever recorded was against that text. Moving it from a const to a file had to change nothing —
and *had to* is worth proving, because a stray re-indent or a lost blank line moves a score by a
point and costs a day finding out why.

So the const stayed, renamed `LegacyPrompt`, while a temporary self-test compared it against
`prompts/extraction.md` character by character and stood ready to print the first difference.
It passed. Then **both the const and the test were deleted in the same session**, which is the
half that matters — a scaffold left standing becomes a puzzle for whoever finds it next.

The scored set afterwards is the independent confirmation: **50/50 clean, forbidden 0.00,
rejects 0.00.**

## A new rule, because the old one stopped being enough

This project's hardest-won measurement rule is *a measurement without a provider name is not a
measurement*. Prompts living in files opens the same hole from a second direction: unlike a
`const` in a commit, a file can be edited between two runs leaving **no trace in the result**.

So `--eval` now prints a prompt fingerprint beside the provider:

```
SUMMARY
==============================================================================
prompts   06f1e586
```

Verified by hand as well as by test — appending a line moved it to `b3538b3b`, reverting brought
it back. The hole is closed before it opened, which is the cheapest time to close one.

## What stayed in code, and why

`DeltaSchema`. Its descriptions *are* prompt engineering, so it looks like it belongs on disk
with the rest. But they are welded to the schema's *structure*, which must move in lockstep with
`DeltaApplier` and the C# delta types — the file itself says adding a delta kind means editing
both together. On disk, someone edits a branch, the applier does not know, and a delta kind
silently stops working. That desync cannot happen while the two are compiled together, and this
keeps it that way.

## The honest limit

**The scored set cannot tell us whether a pack's voice hurts extraction.** Every eval scenario
uses fixed hand-written narration, deliberately, to remove the narrator as a source of variance.
A voice producing prose extraction reads worse — terser, heavy dialect, unusual formatting —
would score 50/50 and say nothing.

That is not an argument against the feature. It is the boundary of the instrument, and it
belongs beside the narration-eval question rather than being quietly assumed away.

## Two traps, both repeats

**Heredoc escape mangling, fourth occurrence.** Writing C# interpolated strings through a shell
heredoc turned `\n` into real newlines and broke the build. Logged three times already.

**The file lock, second occurrence.** Started the eval in the background, edited code, could not
build — the running eval holds the binary.

Both are now costing minutes rather than teaching anything, which is the point at which they
stop belonging in a devlog and start belonging in a habit: use the Edit tool for C# string
literals, and do not touch the tree while an eval is running.

## Measurements

`dotnet build` clean, 0 warnings. 85 self-tests pass. Scored set 50/50, forbidden 0.00,
prompts `06f1e586`.
