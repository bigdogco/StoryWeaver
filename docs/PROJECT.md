# StoryWeaver — project

**Status:** approved 2026-08-13
**Created:** 2026-08-13
**Latest decision update:** 2026-09-06 — Uno spike continues on Windows; Linux parked

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

Dependencies point inward. `Core` references nothing; `App` composes the three libraries
beneath it and is what a client talks to.

| layer | holds | settled? |
|---|---|---|
| **Core** | domain model, turn loop, validation, applier, context assembly, **session ownership** | Mostly. The delta set and validator are stable and well-measured. `ContextAssembler` held at 230 turns (§4) but its *output* is now the weak point: 8.4KB of state per call, carrying names that collide. |
| **Llm** | provider client, per-role config, prompt assembly, extraction schema | Yes for the mechanism. The *prompts* are living text and change with every measured failure. |
| **Storage** | JSON canon + history, pack loading (seed, lore, sheets) | Format settled, pack complete (Phase 1). Holds persistence only: `EntityId` moved to Core 2026-09-04, since the id convention is domain, not storage. |
| **Cli** | play UI: the dispatcher, the turn loop, `/edit`, authoring prompts, and the eval *renderer* | Throwaway by design, and **client one of two** — it renders and prompts; `StorySession` owns canon. Now genuinely thin: the instrumentation moved to Harness 2026-09-04 when the Cli was held to the UI rules, so the old "two-thirds eval scaffolding" is gone. 1,813 lines, ~214 of them the eval renderer — client-side by right, since the Harness scores and the CLI draws. |
| **App** | composition: opening a session out of pack, prompts, provider and save | New 2026-09-04. Exists because opening needs Storage *and* Llm, and Core references neither — no other project could see both. Renders nothing, asks nothing. |
| **Harness** | instrumentation: the extraction eval, the self-test suites, the live API probes, and the shared world fixture | New 2026-09-04. Everything that measures the engine rather than plays it, pulled out of the Cli because a client — thin by rule — cannot own the benchmark. References Core + Llm + Storage + App (it tests all of them). The eval is UI-bound and returns a structured `EvalReport` a client renders; the self-tests and probes are dev-only and print directly. `ResponseSelfTest` stays in Llm, with the internal wire types it checks. |
| **UI** | spike only | Framework undecided. Uno Platform is under spike, focused on Windows first while Linux setup is parked. Blazor was selected 2026-09-05 and reversed 2026-09-06 at the player's request. See Phase 2. |
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
| **A UI is a thin layer, never a driver.** No gameplay, narration or authoring logic lives in a UI project. It collects input, calls Core, renders what comes back | Locked 2026-09-02, before any Avalonia existed, so that abandoning Avalonia costs a shell rewrite and nothing else. The turn loop already satisfied it; authoring did not, and that was found by looking rather than assumed — see below. |

### The UI boundary, and the parity rule that was rejected

Settled 2026-09-02, entering Phase 2. The question asked was whether the UI could be swapped
without touching the engine. Two different commitments were merged in it, and only one is worth
making.

**Locked: thin layer.** As above. `TurnEngine` already had it — three public methods, no console
anywhere, `INarrator` and `IStateExtractor` written in Core's own vocabulary rather than a
provider SDK's. `AuthoringCommands` did not: the id convention, the collision check, and
validate-apply-save all had `Console.WriteLine` threaded through them, so a UI would have
reimplemented them and the two copies would have drifted. Pulled into `Core/Authoring.cs`.

**Rejected: CLI/UI feature parity.** *"Everything the UI can do can be done through a
`/command`"* sounds like the same idea and is not. It is a tax on every future feature, and it
drags the UI down to what a text prompt can express. Dragging a character onto a location has no
honest slash-command form; the attempt produces `/place`, which is exactly the interface that
made a sound design read as a bug on 2026-08-06.

**The CLI is not the API — Core is.** The CLI is the first client, and it is allowed to be a
worse one.

**What editable canon means in practice.** Simple JSON modding: open the file, add an item to
a character, change a status, save. Nothing special happens; the state is just different when
the next turn is assembled. History is a log of what happened, not an editing surface — it is
not a target for this.

Two consequences to design for, not to solve here:

- **Validation becomes on-demand, not a gate.** `DeltaValidator` exists to be suspicious of a
  cheap model that confidently invents things; a person editing their own canon does not need
  to be argued with. Same structural invariants, reported rather than refused. Built as
  `Core/CanonRefresh.Check` 2026-09-02: dangling fact ids, an item both held and placed or
  neither, a location or holder naming nothing, a connection to a place that is not there, and
  an entity filed under a key that disagrees with its own id.

  **Corrected 2026-09-02.** This list previously read *"no character without a location"*, which
  is wrong: `Character.LocationId` is nullable precisely so a person can exist offstage, and the
  authoring path offers it as *blank = unknown / offstage*. Implemented as written it would have
  warned about every correctly-authored offstage character. The real rule is *a location, when
  set, names a real place.*
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
| **Build for observed failures, never for completeness.** A design doc listing six components is not six reasons to build | Added 2026-08-16, from the player: *"we are starting to go into a mode where we make features and not the game."* The rule above governs gaps seen in play. This governs the other direction — work chosen because a layer looks unfinished or a plan has an empty box. The last item of Phase 1 was going to be a narration eval, measuring prose nobody had complained about, which an audit had already found nothing wrong with. That is the same mistake one level up: building measurement in a vacuum. |
| **Playing is how features get chosen.** When the queue and the play sessions disagree, the sessions win | Every finding worth having in this project came from a long run: the world with no exits, two engines on one save, a place introduced twice, the story with no direction. None came from the backlog. |
| Testing is manual; `dotnet build` is the only automated check | Per CLAUDE.md. Eval scenarios measure the model, not the code. |

**Stack**

C# / .NET 8 for the engine · OpenRouter, per-role models · JSON behind
`IWorldRepository`, permanently · UI framework undecided · secrets in env vars
or gitignored `*.local.json` only.

**UI framework selection reopened 2026-09-06, at the player's request.** Blazor is
no longer the selected UI direction, and MAUI Blazor Hybrid is no longer the
proposed host. The application still needs to launch in its own desktop window
without requiring an external browser. Linux desktop support remains a reason to
prefer Uno over MAUI, but local Linux setup is parked; the spike now focuses on
making the Windows desktop UI useful enough to judge. The earlier Avalonia choice
also remains unselected.

The thin-client rule still applies. Presentation owns forms, layout and interaction;
App composes sessions and Core owns gameplay and canon. Replacing the UI should
preserve the engine and save format, but still entails rebuilding screens and their
interactions. Host-specific services must stay outside shared interaction logic.

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

### Phase 1 — the story layer ✅

> Can an author say what a story is *about*, and does the engine hold to it?

**Closed 2026-08-16.** A pack could describe a world; nothing could describe a story in it.
Four pieces, all of them finishing the pack design written 2026-07-23:

| designed | built |
|---|---|
| `seed.json`, `lore/*.md`, `characters/*.md` | ✅ (before this phase) |
| `scenario.md` — what the story is about | ✅ |
| `opening.md` — the first thing the player reads | ✅ |
| `world.json` manifest, and `save.json` recording what a playthrough began against | ✅ |
| `prompts/*.md` overrides, and every engine prompt out of code | ✅ |

The scenario and the opening are separated by **lifetime, not content**: an opening renders the
seed and slides out of the narration window after ten turns, a scenario is standing context
forever. That distinction is the phase's real result — a premise written only into an opening
works beautifully for ten turns and is then forgotten.

### What this phase did not answer, stated plainly

**Whether the prose is better.** The original "done when" asked for that with evidence, and we
cannot supply it. Recorded as unanswered rather than quietly rewritten to match what was built.

Two reasons, and the second is the one that matters.

`design/NARRATION_EVAL.md` audited all 51 turns of the first real session against every
mechanically checkable property — id leaks, repetition, naming before canon knew, facts
established without being learned. **Everything cheap already passes.** A rules-based narration
eval would score 100% on day one and say nothing. Everything actually worth checking is semantic
and needs a judge model, which is a second model's unaudited variance grading a first model's,
in a project that misattributed provider noise to its own code four times — and which needs
hand-labelled prose as its own control, which cannot be automated.

**And a narration eval is itself a feature built in a vacuum.** Nobody has complained about the
prose. The audit found nothing wrong. Building measurement for a problem no session has produced
is the same mistake as building features for one — see §3.

So it is deferred, and it should be sequenced against something that actually needs it: dice,
where *"did the narration contradict the roll?"* is the first objectively checkable property of
prose.

### Phase 2 — Graphical UI — **current: design**

> Can someone who did not write the engine author a pack and play it, without the CLI?

**Starting point for design (updated 2026-09-06).** No UI framework or desktop
host is selected. Blazor and MAUI Blazor Hybrid are explicitly reversed. Uno
Platform is being spiked as a desktop candidate, with Windows as the immediate
proof surface and Linux deferred until the local Linux environment is healthy.
Design must distinguish editing a reusable pack from editing a running save and
cover both authoring and play. The first questions are the workflows and screen
layout, then framework choice, host integration, session lifetime and
in-progress/failure feedback. The engine currently targets .NET 8; the Uno spike
targets `net9.0-desktop` so it can open in the installed Visual Studio 2022,
whose MSBuild is too old for .NET SDK 10.

**The pack format is now settled**, which is what Phase 1 was for: seed, lore, sheets,
scenario, opening, manifest, prompts. An editor built now is built once.

**And this is where the project stops adding to the engine and starts making the game
playable.** The CLI works and the base is right — a long session is coherent, canon holds at
230 turns, and a world can say what it is about. What it is not is pleasant to play or to
author, and that is the thing standing between here and the play sessions that pick every
feature after this. Under the rule in §3, that makes it the only sensible next phase.

The UI is largely a pack editor. Several problems already solved in validator code —
placement, sheet management — are more honestly UI problems, and the player said so at the
time: *"with proper UI it will be much clearer."*

`Core` is UI-agnostic by construction, so this is a top-layer addition, not a rework.

This is also where player editing becomes real. The CLI has a narrow version already
(`/place`, `/character`, `/fact`, `/reroll`); the UI is what makes canon editable as a matter
of course rather than as a repair command.

**Answered 2026-09-02: both, and the argument is not symmetry.** The parity rule rejected the
same day says a UI feature need not have a `/command` — but that argument is about *UI-shaped*
features, and Update State is a verb with no arguments. The rejection makes UI-only permissible,
not correct.

What tipped it: **the bug is in the CLI today, before any UI exists**, and the CLI is where the
long runs happen. Measured rather than argued — a session with an externally added location,
one turn taken without `/reload`, and the edit was gone; the same sequence with `/reload` and it
survived. Deciding it now also fixed the return type, which built after a UI would have come
back shaped for a panel.

`Core/CanonRefresh` re-reads, diffs and checks; `/reload` renders it and a button will call the
same function.

**Done when:** create a world, author characters and lore, place them, play, save, resume,
and correct any of it after the fact — all without touching a terminal.

**Session ownership prerequisite completed 2026-09-04:**
[`design/CANON_OWNERSHIP.md`](design/CANON_OWNERSHIP.md). `StorySession` in Core owns
canon behind a single-writer guard; App composes and opens sessions. Deltas are the
ordinary write path, with direct authoring as a labelled escape hatch for what the
delta set cannot express. UI edits must use the session's operations rather than
mutating its exposed world graph through bindings.

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
