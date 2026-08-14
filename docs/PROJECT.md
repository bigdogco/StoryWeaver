# StoryWeaver — project

**Status:** approved 2026-08-13
**Created:** 2026-08-13

The standing reference for what this project is and where it is going. Written after three
weeks of post-bootstrap work in which every task was chosen by whatever the last play
session turned up, and nothing recorded the shape of the whole.

Read this first. It changes rarely — on a phase boundary, or when a decision is locked.

| doc | answers |
|---|---|
| **this** | what are we building, in what layers, in what order, and what does each phase prove? |
| `todo/TODO_FUTURE_WORK.md` | what *could* we do next — the unscheduled queue |
| `todo/TODO_{TASK}.md` | what is in flight right now |
| `design/*.md` | why a particular thing is shaped the way it is |
| `CHALLENGES.md` | what has bitten us, so it bites once |

Phases live here. Items live in FUTURE_WORK. Nothing lives in both.

---

## 1. What StoryWeaver is

A long-form text RPG driven by an LLM, built on one idea: **canon and narration are separate
things.** A structured entity store is the source of truth; prose is a rendering of it, never
the store itself. Each turn narrates from canon, extracts structured deltas from the prose,
validates them against canon, and commits them.

Most tools in this space treat the chat log as the state of the world, so the honest answer
to "what does this character know" is "whatever is still in the context window." That is why
they drift. Here, knowledge is per-character and permanent, and a character who was not in
the room does not know what happened there.

**The bet:** a cheap model can reliably read creative prose and emit correct structured state
deltas. Bootstrap existed to test that before anything was built on top. It holds — see §3.

It is a hobby project. Packaging and distribution may never happen.

---

## 2. Layers

Dependencies point inward. `Core` references nothing.

| layer | holds | settled? |
|---|---|---|
| **Core** | domain model, turn loop, validation, applier, context assembly | Mostly. The delta set and validator are stable and well-measured. `ContextAssembler` held at 230 turns (§4) but its *output* is now the weak point: 8.4KB of state per call, carrying names that collide. |
| **Llm** | provider client, per-role config, prompt assembly, extraction schema | Yes for the mechanism. The *prompts* are living text and change with every measured failure. |
| **Storage** | JSON canon + history, pack loading (seed, lore, sheets) | Format settled. **The pack is 60% built** — see Phase 1. |
| **Cli** | play harness, eval scenarios, self-tests | Throwaway by design. 6,061 lines, of which ~67% is eval scaffolding and 647 is the actual game. |
| **UI** | not built | Avalonia, decided, not started. See Phase 2. |
| **Plugins** | not built, not designed | See Phase 3. |

**What the base game is** (decided 2026-08-13): the narrator, and the canon it maintains —
places, characters, what they know, how they feel. That is the whole of it. Every other
system — dice, combat, inventory, quests, anything we invent — is a plugin. This is a
narrower base than what exists today; see Phase 3.

---

## 3. Decisions locked

Recorded so they are not relitigated. Changing one of these is a phase-sized event, not a
task-sized one.

**Architecture**

| decision | rationale |
|---|---|
| Canon and narration are separate; `Core` never references a UI | The entire thesis. Everything else is downstream of it. |
| The delta set is a **closed**, enumerated set discriminated on `kind` | A generic `{entity, property, value}` patch lets a cheap model write `character.mood.current`: no schema catches it, it lands as a silent no-op. With a closed set, a change the model cannot express becomes a *visible* failure. |
| The player is an ordinary `Character` under the reserved id `player` | Every delta kind addresses them for free, which is what the extractor was already trying to do unprompted. |
| Ids are opaque, permanent, human-readable kebab-case slugs. **Names are mutable, ids are not** | Ids appear in prompts, saves and logs, all read by a human while debugging. A rename must not orphan a reference. |
| Validation **rejects**; it never auto-resolves | We need to see how often and how badly it goes wrong before deciding what to do about it. |
| Rejections cascade, in batch order, against canon plus what was accepted so far | Otherwise rejecting a `fact_established` leaves its dangling `fact_learned` behind — exactly the failure being prevented. |
| Narration is shown to the player regardless of what extraction does | Extraction failing is a bookkeeping problem, not a storytelling one. |
| Per-character knowledge holds fact *ids*, never text | Two characters cannot end up knowing different versions of the same fact. |
| **The player owns their world and can edit it directly.** Canon and seed are plain JSON, opened in any editor or through the UI; the next turn simply runs on what is there | It is the repair path when the model writes something wrong — without it, one bad extraction is permanent. And it is a *roleplay* feature: giving someone an item, fixing who a character is, adjusting a state is authorship, not cheating. A single-player world has no one to cheat. |
| **Storage stays JSON. Permanently** | Formerly "JSON now, likely SQLite later." Reversed 2026-08-13: the save format is a user-facing surface, not an implementation detail, and a database hides the world from the person who owns it. Full-text search over history — the original trigger for the switch — is not worth that. |

**What editable canon means in practice.** Simple JSON modding: open the file, add an item to
a character, change a status, save. Nothing special happens; the state is just different when
the next turn is assembled. History is a log of what happened, not an editing surface — it is
not a target for this.

Two consequences to design for, not to solve here:

- **Validation becomes on-demand, not a gate.** `DeltaValidator` exists to be suspicious of a
  cheap model that confidently invents things; a person editing their own canon does not need
  to be argued with. Same structural invariants (no dangling fact ids, no item both held and
  placed, no character without a location), reported rather than refused.
- **A running session holds canon in memory and would overwrite an external edit.** Resolved:
  the UI owns this, as an **Update State** action — re-read canon from disk, run the
  invariants, report. Edit the file, press it, keep playing. No file watching, no merge, no
  reconciliation: an explicit button is the whole mechanism.

**Content**

| decision | rationale |
|---|---|
| **Pack** (content, authored, shippable) vs **save** (state, engine-written, private) | Conflating them is the visible failure of the character-card ecosystem: you cannot update a world without breaking existing chats, or share one without shipping somebody's playthrough. |
| **Sheet** (who someone is, permanently) vs **seed** (where they start) | A sheet owns the name and the identity; the seed places them and sets opening state. |
| Every sheeted character must be placed somewhere | Same as any RPG — a character has to start in a room the player can reach. |
| The pack may author the player (`player.md`); if it does not, character creation supplies one | The two cancel each other. `player.md` is the intended default. |

**Practice**

| decision | rationale |
|---|---|
| Scoring is **outcome-based**, never route-based | The single most repeated mistake in this project. What matters is that canon ends up right, not which delta got us there. |
| **A measurement without a provider name attached is not a measurement** | OpenRouter routes one model id across independent hosts of differing quality. Six separate sightings, twice misread as a code regression. Pin with `--providers`, and check the error count *before* reading the score. |
| **A schema branch is not free** | Branches compete for model attention, and prompt rules compete with each other — position matters more than wording. Adding a branch has twice wrecked an unrelated scenario. |
| Before adding a delta kind, check whether the model already emits something that means it | Rewriting an existing output is free; teaching a new one is not. |
| Do not build for a gap until it reproduces in a scenario **and** appears in a second session | The stopping rule that keeps the extraction loop from running forever. |
| Testing is manual; `dotnet build` is the only automated check | Per CLAUDE.md. Eval scenarios measure the model, not the code. |

**Stack**

C# / .NET 8 · OpenRouter, per-role models · JSON behind `IWorldRepository`, permanently ·
Avalonia for UI · secrets in env vars or gitignored `*.local.json` only.

---

## 4. Phases

Each phase closes **one question**. That property is what made bootstrap work, and its
absence is what made the three weeks after it feel like circling.

### Phase 0 — bootstrap ✅

> Can a cheap model reliably read narrative prose and emit correct structured state deltas?

**Yes**, answered 2026-07-20, closed 2026-07-23 by a 51-turn play session. 209 deltas
applied, 8 rejected, no corruption, no canon/history desync. The headline: a guard last seen
on turn 13 returned on turn 51 with the right name, armour, weapon, location and status,
against a 10-turn narration window.

### Closed measurement ✅ — was not a phase

> Does canon survive 200 turns?

**Yes.** Answered 2026-08-14 by a 230-turn model-played `marrow` run, on the third attempt —
the first two were invalid (one ended sealed in a room with no exits, one was corrupted by two
CLI instances sharing a save).

| | result |
|---|---|
| turns | 230, zero duplicate turn numbers |
| rejections | 23 total, ~0.1/turn, **flat across all five 50-turn blocks** |
| turns changing nothing | 9 / 7 / 6 / 7 / 10 per 50 — no collapse |
| narration | 1478 → 1417 mean chars; still coherent and in-character at t228 |
| locations | 33, 28 connected; every orphan introduced-but-never-entered, correct |

Canon does not drift, corrupt, or degrade with distance. **This is the thesis, at four and a
half times the distance bootstrap proved it.**

Two consequences. Summarization and long-term memory rested entirely on the assumption that
canon decays — it does not, so that whole line stays parked, and
`design/LONG_TERM_MEMORY.md` records the gate. And the ashfall run's near-total silence (2
facts in 150 turns) was **solitude, not distance**: this run, with a companion, produced 16
facts established and 32 learned.

What the run *did* surface is a different class of problem — canon stays correct while
becoming harder to narrate from. See `CHALLENGES.md`: a place can be introduced twice under
two ids, and names collide in the narrator's view because ids are deliberately stripped from
it. Neither is decay. Both get worse with length.

### Phase 1 — the story layer

> Can an author say what a story is *about*, and does the engine hold to it?

Today a pack describes a world and nothing describes a story. There is no premise, no
stakes, no dramatic question, no ending. Marrow is a tavern with people in it; nothing says
what the game is for. Sessions end at 50 turns because the player gets bored, not because
anything concludes.

This also finishes the pack design written 2026-07-23, of which three of six components were
never built:

| designed | built |
|---|---|
| `seed.json`, `lore/*.md`, `characters/*.md` | ✅ |
| `world.json` manifest | ❌ |
| `opening.md` | ❌ |
| `prompts/*.md` overrides | ❌ |

The opening message is the first paragraph of the missing layer, not a separate feature.

Narration eval belongs alongside this. Every number in this project measures the
bookkeeping; the half the player actually experiences has no quality control at all, and
shipping a story layer with no way to tell whether the prose got better repeats the
extraction trap in a new place.

**Open questions:** what a scenario consists of (premise? goal? ending conditions? a clock?)
· whether an ending is engine-enforced or narrated · whether narration is measurable at all
(see `design/NARRATION_EVAL.md`, an audit with no design committed).

**Done when:** a pack can state its premise and opening; the engine carries both into
narration; a session demonstrably plays toward the stated premise rather than drifting; and
we can say whether the prose is better than before, with evidence.

### Phase 2 — Avalonia UI

> Can someone who did not write the engine author a pack and play it, without the CLI?

The UI is largely a pack editor, which is why it comes after the pack format is settled —
building an editor for a format that is 60% unbuilt means building it twice. Several
problems already solved in validator code (placement, sheet management) are more honestly UI
problems.

`Core` is UI-agnostic by construction, so this is a top-layer addition, not a rework.

This is also where player editing becomes real. The CLI has a narrow version already
(`/place`, `/character`, `/fact`, `/reroll`); the UI is what makes canon editable as a matter
of course rather than as a repair command.

**Open question:** whether **Update State** also exists as a CLI command, or is UI-only.

**Done when:** create a world, author characters and lore, place them, play, save, resume,
and correct any of it after the fact — all without touching a terminal.

### Phase 3 — plugins

> Can a system be added from outside the base game without degrading extraction?

The base game is narrator plus canon core. Dice, combat, inventory, quests are plugins. The
shape wanted is **C# plus prompts**, for flexibility.

Two collisions to resolve before any API is designed:

- **The closed delta set.** Plugins that add state must add delta kinds, which opens the set
  that §3 locks closed. Probable resolution: *closed at runtime, composed at load* — base
  kinds plus each loaded plugin's declared kinds, assembled into one schema at session
  start. The model still sees an enumerated list; the original argument was against a
  *generic patch shape*, not against extensibility. Not yet decided.
- **Inventory is already in Core.** Six of seventeen delta kinds are items, plus `Item.cs`,
  applier cases, validator tiers and a large share of the eval scenarios. Under the
  base/plugin split above, that is a plugin sitting in the base game. Open: is inventory the
  *first plugin*, extracted to prove the API against a real case — or grandfathered into
  base, with plugins starting from dice?

**The risk that could kill this phase, and how to check it cheaply.** A schema branch is not
free. Today's set is 17 kinds; a player with five plugins loaded might see forty, and
extraction accuracy may not survive it. This is answerable *before* the API exists: generate
a schema with twenty junk branches and re-run the existing scored set. If the score
collapses, the plugin design changes shape — or the base has to get narrower still.

**Done when:** that measurement exists, and a system the base game knows nothing about can be
loaded, can write its own state, and leaves the scored set intact.

### Beyond

World generation and lazy expansion · summarization and long-term memory (gated on the open
measurement above) · SillyTavern / chub import · prompt caching · packaging. All in
`TODO_FUTURE_WORK.md`; none scheduled.

---

## 5. Housekeeping rule

The 42 unchecked items stranded across finished task docs happened because nothing said what
to do with them.

> **When a `TODO_{TASK}.md` is finished, every unchecked item either moves to
> `TODO_FUTURE_WORK.md` or is struck out with a reason.**

To be added to CLAUDE.md alongside the devlog rule if this doc is approved.
