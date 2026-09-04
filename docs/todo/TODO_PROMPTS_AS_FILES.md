# TODO: Prompts as files, and per-pack narration voice

**Status:** DONE 2026-08-16
**Created:** 2026-08-16

Fourth piece of **Phase 1**. Two things that turn out to be one job: the pack-level narration
override designed in [`WORLD_PACKS.md`](../design/WORLD_PACKS.md) §5, and the standing
`TODO_FUTURE_WORK` item *"no prompt string lives in code"*.

They fit together because once the engine's prompts are files, a pack layering its voice on top
is just another file.

---

## What is wrong today

Every world is narrated by the same hardcoded voice:

> You are the narrator of a **dark fantasy** text RPG.

`the-last-lantern` is a 1940 noir detective story. `ashfall` is volcanic survival. Two of three
packs are being told they are something they are not, in second person present tense at two to
four paragraphs — all of it taste, all of it in a `const string`.

## The prompts, and which move

| prompt | lines | moves? |
|---|---|---|
| `LlmNarrator.SystemPrompt` | ~45 | **yes** |
| `LlmStateExtractor.SystemPrompt` | ~270 | **yes** |
| `OpenRouterClient.RepairInstruction` | few | **yes** |
| `DeltaSchema.Json` | ~240 | **no** — see below |

**`DeltaSchema` stays in code, deliberately.** Its descriptions *are* prompt engineering, so it
looks like it belongs here. But they are welded to the schema's structure, which must stay in
lockstep with `DeltaApplier` and the C# delta types — the file itself says *"adding a delta kind
means editing this and `DeltaApplier` together."* On disk, someone could edit a branch, the
applier would not know, and a delta kind would silently stop working. That desync cannot happen
today and this keeps it that way.

## Decisions

| question | decision |
|---|---|
| Where do engine prompts live? | **Files on disk**, found by walking up from the executable — the same mechanism `SettingsLoader` already uses, so the real files sit at the repo root and an edit needs no rebuild. |
| Missing file? | **Loud failure at startup**, like config. A narrator with no prompt is not a degraded narrator, it is an unpredictable one. |
| May a pack override extraction? | **No.** Locked in §5: narration is taste, extraction is correctness measured at 100%, and a pack quietly replacing it would invalidate every measurement while looking like a content change. |
| Does a pack replace or add to narration? | **Adds.** The engine keeps the rules; the pack supplies voice. |

### Why layering rather than replacing

The narrator prompt does two jobs in one blob. Taste — genre, length, tense, how much room a
scene gets. And **correctness**: never speak for the player, never rewrite their dialogue, never
write an internal id, characters know only what canon says they know, the state wins.

That second group is the engine's guarantees, and several exist because they broke first. The id
rule is why a turn once read *"the heavy oak door of the marrow-tavern flies outward"*, and why
`ForNarration` and `ForExtraction` are separate functions at all.

If a pack replaced the whole prompt, an author omitting one line would silently lose a rule that
cost real work — and it would look like a content change, not a bug. So the pack contributes a
voice section and the rules are not negotiable. Same principle already locked for extraction,
applied one level finer.

---

## Two safeguards

**The extraction prompt must move byte-identically.** It is the most measured artifact in this
project. Moving it from a const to a file must change nothing: extract, diff to prove equality,
then re-run the scored set pinned. If the number moves, the move was wrong.

**The eval must fingerprint the prompts it used.** This project's hardest-won rule is *a
measurement without a provider name is not a measurement.* Once prompts are editable files, a
measurement without knowing **which prompt** is equally meaningless — and unlike a `const` in a
commit, a file can be edited between two runs leaving no trace in the result. A short hash in
the eval output closes that hole before it opens.

## Build

- [x] `prompts/narration.md`, `prompts/extraction.md`, `prompts/repair.md` at the repo root
- [x] `PromptLibrary` in `StoryWeaver.Llm` — walks up from the executable exactly as
      `SettingsLoader` does, so an edit needs no rebuild. Fails loudly if absent.
- [x] `LlmNarrator`, `LlmStateExtractor`, `OpenRouterClient` read from it. **No prompt string
      remains in code** except `DeltaSchema`, reasoned above.
- [x] Proved the extraction prompt byte-identical — kept the const temporarily, compared
      character by character in a self-test, then deleted both. See below.
- [x] A pack may ship `prompts/narration.md`; it is appended as a voice section
- [x] A pack shipping `prompts/extraction.md` is **refused at load**, by name, with a message
      telling the author what to do instead
- [x] `--eval` prints the prompt fingerprint alongside the provider — `prompts   06f1e586`
- [x] Wrote a voice for `the-last-lantern` — lean prose, specific nouns over atmosphere,
      people who answer a different question rather than lie, and an explicit *nothing
      supernatural, ever*

## Self-tests

- [x] Missing engine prompt file fails loudly
- [x] A pack with no voice narrates exactly as before — `marrow` and `ashfall` ship none
- [x] A pack voice reaches the narrator **and** `"Never write an internal identifier"` is
      still in the same system prompt — the assertion that makes "added, not substituted" real
- [x] A pack shipping an extraction override fails the load
- [x] The fingerprint changes when a prompt file changes — and verified by hand too:
      appending a line moved it `06f1e586` → `b3538b3b`, reverting brought it back

## Verify

- [x] `dotnet build` clean, 0 warnings; 85 self-tests pass
- [x] Scored set, StreamLake n=5: **50/50 clean, forbidden 0.00, rejects 0.00.** The run that
      mattered — it proves the most-measured artifact in the project survived the move to disk.
- [x] Read the banner on `the-last-lantern`: pack name, version, author, prompt directory and
      fingerprint

## Close out

- [x] Devlog `2026-08-16_prompts-are-content.md`, `TODO_FUTURE_WORK.md`, no unchecked boxes

## Known limit, stated rather than discovered later

**The scored set cannot tell us whether a pack's voice hurts extraction.** Every eval scenario
uses fixed hand-written narration, deliberately, to remove the narrator as a source of variance.
So a voice producing prose that extraction reads worse — terser, heavy dialect, unusual
formatting — would score 50/50 and tell us nothing.

That is not a reason to avoid the feature. It is the honest boundary of the instrument, and it
belongs with the narration-eval question rather than being quietly assumed away.

## Not in this task

- **Hot-reload.** Cheap once the load path exists, and it deliberately breaks prompt caching,
  so it wants to be off by default and is its own decision.
- **`DeltaSchema` to disk.** Reasoned above.

---

## The byte-identity proof, and why it was worth the detour

The extraction prompt is the most measured artifact in this project — every extraction number
ever recorded was against that text. Moving it from a `const string` to a file had to change
nothing, and *had to* is worth proving rather than assuming: a stray re-indent or a lost blank
line would move a score by a point and cost a day finding out why.

So the const stayed, renamed `LegacyPrompt`, while a temporary self-test compared it against
`prompts/extraction.md` character by character and printed the first difference if there was
one. It passed. Then both the const and the test were deleted in the same session, which is the
half that matters — a scaffold left standing becomes a puzzle for whoever finds it next.

The scored set afterwards is the independent confirmation: 50/50, unchanged.

## Found while building

**The heredoc escape trap, for the fourth time.** Writing C# interpolated strings through a
shell heredoc turned `
` into real newlines and broke the file. It is logged in the devlogs
three times already. The fix is the same every time: use the Edit tool, or a C# raw string
literal.

**The file-lock trap, for the second time.** Started the eval in the background, then edited
code, then could not build — the running eval holds the binary. Both of these are now costing
minutes rather than being learned, which is the point at which they belong in a habit rather
than a devlog.
