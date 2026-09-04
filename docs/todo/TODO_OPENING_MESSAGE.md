# TODO: Opening message

**Status:** DONE 2026-08-16
**Created:** 2026-08-16

Second piece of **Phase 1**. Design already settled in
[`design/WORLD_PACKS.md`](../design/WORLD_PACKS.md) §4, written 2026-07-23 and never built.

---

## What happens today

`PlaySession.PrintOpeningScene` prints the **raw location description from the seed**, between
two dashed lines. No prose, no narrator, nobody mentioned.

A new player of `the-last-lantern` sees this, and nothing else:

```
A narrow private detective's office above the shuttered Orpheum cinema. Venetian blinds, a
scarred desk, a filing cabinet, and a window looking down on a blacked-out street. Rain ticks
against the glass.
```

**Vivian Vale and Eddie Mercer are both in that office in the seed** and neither is mentioned.
It is after midnight, a woman has walked in with a job, there is a missing husband and a black
ledger — and the player is shown furniture.

The sharp part: the *scenario* is in the narrator's system prompt from turn one, so the model
knows about Vivian and the ledger. **The player is the only participant who does not.** They
must type into a scene they cannot see, and the narrator answers as though they knew.

## Two decisions the design left open

### §4.3 — is the opening a turn? **No.**

It becomes the **oldest beat in the narration window**, prepended while the window still has
room, and it is never written to `history.jsonl`.

The argument that settles it: **the opening is content, not state.** Writing it as a turn record
would bake the text into every existing save, so editing `opening.md` between sessions would
leave old prose in old saves — a direct violation of the pack/save split the whole design rests
on. Content may change; history is what happened.

It also gets the lifetime right for free. The opening slides out of the window after ~10 turns,
exactly like any other prose, while the scenario persists — which is the distinction
`design/SCENARIOS.md` was built around.

Implementation falls out of that: a `StoryBeat` with an empty player input, and `LlmNarrator`
emits no user message for an empty input.

### The name check — build it, then measure the noise

The design asks for a warning when the opening names a person or place that does not exist:
*"warn on capitalised names and obvious place references that resolve to nothing in the seed."*

Two checks, very different confidence:

- **`{{ }}` references must resolve** — certain, cheap, same rule as sheets and scenarios.
- **Capitalised names that match nothing in the seed** — a heuristic, and the risk is noise:
  `Venetian`, `October`, `Orpheum` are all capitalised and none is a character.

Building both, then **running the heuristic against the three real packs before keeping it.** If
it cries wolf on real prose an author would write, it is worse than nothing and comes out. A
warning nobody trusts is a warning nobody reads.

---

## Build

- [x] `WorldPack` loads `opening.md`; absent is legal
- [x] `{{ }}` in it must resolve, same as scenarios
- [x] Capitalised-name warning, **measured before keeping it** — silent on all three real
      packs, and it fires on an invented `Captain Roy Halloran`. Multi-word capitalised runs
      only, so `Venetian` and `Orpheum` do not trip it.
- [x] The opening prints on a fresh session instead of the bare location description
- [x] It enters the narration window as the oldest beat, and is never a `TurnRecord`
- [x] `LlmNarrator` emits no user message for a beat with empty player input
- [x] Deleted `here ??= world.Locations.GetValueOrDefault("marrow-tavern")` — a pack-specific id
      sitting in engine code since bootstrap
- [x] Wrote one for `the-last-lantern` — **a draft, and the author's to rewrite.** It is the
      single most important paragraph in the experience and it is their world.

## Self-tests

- [x] A pack with no `opening.md` still starts, and shows the location description as before —
      covered by `marrow` and `ashfall` in the shipped-pack check
- [x] The opening reaches the narrator as the first assistant message on turn 1
- [x] It is absent from `history.jsonl` — the turn count after one turn is 1, not 2
- [x] It drops out of the window once enough real turns exist
- [x] An unresolved `{{ }}` fails the load — shares the scenario path

## Verify

- [x] `dotnet build` clean, 0 warnings; 76 self-tests pass
- [x] Scored set, StreamLake n=5: **50/50 clean, forbidden 0.00, rejects 0.00.** Extraction
      untouched, as intended.
- [x] Started `the-last-lantern` fresh and read it: Vivian in the chair with the cigarette
      case, Eddie pretending to search a drawer he already closed, past midnight on a blacked-out
      street. Against a paragraph about Venetian blinds and a filing cabinet.

## Close out

- [x] Devlog `2026-08-16_the-first-thing-you-read.md`, `TODO_FUTURE_WORK.md`, no unchecked
      boxes left here

## Not in this task

- **`world.json` manifest and prompt overrides** — the remaining two Phase 1 pack pieces.
- **Generating an opening from the seed.** Rejected in the design: guaranteed consistent, and
  the author loses control of the single most important paragraph in the experience.
