# TODO: Scenarios

**Status:** DONE 2026-08-15
**Created:** 2026-08-15

First piece of **Phase 1 — the story layer**. Design: [`design/SCENARIOS.md`](../design/SCENARIOS.md).

A pack describes a world; nothing describes a story in it. This adds `scenario.md` — one prose
block, standing context in every narration prompt, never seen by extraction.

---

## Build

- [x] `WorldPack` loads `scenario.md` from the pack root. Absent is legal and means today's
      behaviour — `marrow` and `ashfall` ship none.
- [x] `{{ }}` references in it are checked against the seed at load, same rule as sheets. Names are
      **not** checked against canon: a scenario legitimately names what does not exist yet.
- [x] `INarrator` takes the scenario; `LlmNarrator` appends it to the **system message**, not
      to the world-state block. The volatile part must stay last so the prefix caches.
- [x] Extraction does **not** receive it. Verify by reading the assembled extraction context,
      not by assuming.
- [x] `/prose` prints it, resolved, since that command shows the narrator's real view.
- [x] Write one for `marrow` and one for `ashfall` — the packs are the test.

## Self-tests

- [x] A pack with no `scenario.md` loads and plays exactly as before
- [x] An unresolved `{{ }}` in a scenario fails the load — shares `RejectUnresolvedReferences`
      with sheets and lore
- [x] The scenario appears in the narration messages and **not** in the extraction context —
      asserted by running a real turn through `TurnEngine` with recording fakes
- [x] It sits in the system message, so the volatile block is still the last message
- [x] **Added after a bug:** `{{player}}` resolves to a name before narration

## Verify

- [x] `dotnet build` clean, 0 warnings; 74 self-tests pass
- [x] Full scored set, **provider pinned** — StreamLake n=5: **50/50 clean, forbidden 0.00,
      rejects 0.00.** Extraction untouched, which was the point of running it.

      `two-stage-entry` scored 10/10 against 9/10 yesterday. **Not an improvement from this
      change** — nothing in the extraction path moved, and that scenario has swung 8/10–10/10
      historically. It landed at the top of its own range. Recorded because reading it as a win
      is precisely the error the per-provider table exists to prevent.
- [x] A play session on `marrow` with a scenario, by a human — **moved to
      `TODO_FUTURE_WORK.md` "Pending a session"**, since it is the user's action, not a
      build step, and the task is otherwise complete.

## Close out

- [x] Devlog `2026-08-15_what-the-story-is-about.md`, `TODO_FUTURE_WORK.md`. Nothing for
      `CHALLENGES.md` — the two bugs found were mine and are recorded here.
- [x] No unchecked boxes left in this doc

---

## Deliberately not in this task

- **`world.json`, `opening.md`, prompt overrides.** The other three Phase 1 pack pieces. Each
  is separable and each deserves its own measurement.
- **Ending conditions, a clock, a stated goal.** Nothing has measured a need, and an
  open-ended scenario has no use for any of them. Frontmatter is where they would go if they
  earn it.
- **Several scenarios per pack.** Wants several seeds too; revisit with the UI.

---

## The open question this task does not answer

**Whether a scenario actually makes the story better.**

It is standing text in a prompt. Whether the narrator holds a story to it across 200 turns is
a *narration* question, and narration has no automated quality control — `NARRATION_EVAL.md`
has been an audit with no design committed since 2026-07-24.

Shipping a story layer with no way to tell whether the story improved is the extraction trap in
a new place: a thing tuned by feel, for weeks, with no number.

The cheapest honest instrument available today, and it needs no judge model:

| signal | goalless 230-turn run | what a scenario should do |
|---|---|---|
| movement share of all deltas | **53%** | fall |
| new locations : new characters | **31 : 2** | flatten |

Both are computable from any save with no API calls. Neither measures *quality* — they measure
**aimlessness**, which is the specific thing a scenario is meant to fix. Worth building
alongside, and worth being honest that it is a proxy.

---

## Found while building

**`{{player}}` reached the narrator unresolved, and no test caught it.**

The loader checks that every `{{ }}` in a scenario *points at something*; nothing was turning
it into words. So the narrator was handed a literal `{{player}}` in every prompt — the
token-in-the-prose failure that the narration/extraction split exists to prevent, arriving
through a door nobody had thought to close.

Caught by eyeballing `/prose` after wiring it up. That is the second time this week that
command has found something no count would have: it exists to check that ids do not leak into
narration, and it keeps being the instrument that shows what the model is actually handed.

Fixed by resolving per turn in `TurnEngine`, not at load — a character renamed on turn 40 has
to read correctly on turn 41. Covered now by `CheckScenarioReferencesResolveToNames`.

**A test whose sentinel collided with the world under test.** The first version of the
"extraction never sees it" check searched the extraction context for the word *marsh* — which
Marrow's seed already contains, since Mabb is an old marsh-hand. It failed, and for a moment
looked like a real leak. Sentinels are now strings that cannot occur naturally.
