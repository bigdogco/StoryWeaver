# Design — world packs

**Status:** design, no code. Written 2026-07-23, immediately before the lore build.

A **world pack** is everything an author writes: the seed world, lore entries, the opening
message, prompt overrides, a manifest. A **save** is everything a playthrough produces: canon
and history.

This document exists because lore entries are the **first authored content that is not code**.
Until now the only thing on disk was a save. Wherever lore files land becomes the de facto
layout, and moving it afterwards is a migration — so the shape is decided before the lore
build rather than after it.

---

## 1. The split: content and state

The same distinction the lore design drew, generalised to everything.

| | pack | save |
|---|---|---|
| holds | seed, lore, opening message, prompts, manifest | canon, history |
| written by | an author, in an editor | the engine, every turn |
| lifetime | static; versioned; edited between sessions | grows continuously |
| shared? | yes — this is the shippable artefact | no |

**Conflating them is the failure visible in the character-card ecosystem from the outside:**
you cannot update a world without breaking existing chats, and you cannot share a world
without shipping somebody's playthrough along with it.

## 2. Layout

```
worlds/marrow/            <- pack: content
  world.json              manifest — id, name, version, author
  seed.json               starting canon
  opening.md              the first thing the player reads
  lore/*.md
  prompts/*.md            overrides; absent means engine default

saves/marrow-01/          <- playthrough: state
  canon.json
  history.jsonl
  save.json               which pack, which version
```

Two identifiers that are currently one word: today `marrow` is both the world id and the save
directory. A pack id and a save id are different things, and separating them is what allows
more than one playthrough of the same world.

**Path resolution.** `saves/` currently resolves relative to the working directory — the reason
`play.ps1` forces the cwd, and the reason harness testing has to happen in a temp directory to
avoid corrupting a real world. Packs make path resolution more load-bearing, so this is the
moment to give both roots an explicit, configured base rather than an implicit one.

---

## 3. Who writes canon

Never previously written down in one place. There are five writers, and lore is the odd one.

| # | writer | what it may write | check |
|---|---|---|---|
| 1 | **the seed** | the whole starting world | authored; validated on load |
| 2 | **extraction** | any delta, every turn | `DeltaValidator`, three-way outcome |
| 3 | **the player** | `/place`, `/character`, `/fact`, `/rename` | same validator, same path |
| 4 | **`/retry`** | replaces the last turn's deltas | same validator; the one rewrite of history |
| 5 | **lore** | *nothing* | read-only into prompts; only `Knows` touches canon |

Writer 5 is the interesting one. **Lore never writes canon.** The entries live in the pack, and
the only trace they leave in a save is which characters know them — which is state, and arrives
through the ordinary `fact_learned` path like everything else. This is what the
`fact_established`-may-not-target-a-lore-id rule enforces.

Everything except the seed goes through `DeltaValidator`. That is the property worth protecting
as writers accumulate: **one gate, however many doors.**

---

## 4. The opening message

Those sites call it `first_mes`. Theirs is **prose with no state behind it** — it describes a
tavern, a person, a situation, and none of it exists anywhere. The model reads it as context and
improvises the rest.

Ours has to be the opposite: an opening message is a **rendering of the seed**, exactly as every
later turn is a rendering of canon.

### 4.1 The pair

Seed — the world existing, no prose:

```json
"characters": {
  "innkeeper-hald": { "name": "Hald", "locationId": "marrow-tavern", "mood": "guarded" },
  "drinker-mabb":   { "name": "Mabb", "locationId": "marrow-tavern", "status": "drunk" }
}
```

Opening — the first rendering of it:

> The door of the Drowned Crow swings shut behind you, cutting off the wind. Peat smoke hangs
> at head height. Behind the counter a heavyset man watches you cross the room without
> pretending not to, and in the corner an old marsh-hand is talking quietly to his mug.

The prose names a heavyset man and an old marsh-hand; both exist in the seed. Had the author
written *"a militia officer waits by the fire"*, there would be an officer in the story and
nobody in canon — the narrator would keep referring to them, the player would talk to them, and
extraction would have to invent them mid-scene.

### 4.2 The decision

**The pack ships both, and the loader checks them against each other.**

Every person and place the opening names must exist in the seed. This is enforceable precisely
because the opening is *authored* — unlike narration, it is fixed text available at load time,
so the check is a one-off cost with no per-turn price.

That turns the ecosystem's central weakness into something we enforce: **their opening message
is a promise the world cannot keep; ours is a promise the world is checked against.**

The check cannot be perfect — natural language does not surrender its entity list on demand, and
"a heavyset man" is not a string match for "Hald". Realistic form: warn on capitalised names and
obvious place references that resolve to nothing in the seed, and let the author confirm or fix.
A warning an author reads once beats a silent inconsistency the player meets at turn 3.

**Rejected alternatives:**

- *Engine generates the opening from the seed.* Guaranteed consistent, and the author loses
  control of the single most important paragraph in the experience — which would also differ on
  every new game.
- *Author writes only the opening, extraction derives the seed.* Write a paragraph, get a world.
  Tempting, and it puts extraction to work at exactly the moment there is nothing to validate
  against, which is where it is weakest. This is worth revisiting later as an *authoring aid*
  that produces a seed for a human to edit — never as the load path.

### 4.3 Open: is the opening turn 0?

It has no player input, so it does not fit `TurnRecord` as shaped. Probable answer: it belongs
to the pack, is replayed into the narration window as an assistant message, and is never a turn
— which keeps history strictly "things that happened in play". Not yet decided.

---

## 5. Prompt overrides

A pack shipping its own narrator prompt is how a world gets its own voice, and it is the natural
home for the already-logged "prompts as editable files" item.

**Proposal: a pack may override narration, and may not override extraction.** Narration is
taste; extraction is correctness, measured at 100% across nine scenarios, and a pack quietly
replacing it would invalidate every measurement we have while looking like a content change.
If per-pack extraction tuning is ever wanted, it should be additive — a pack contributes extra
rules, it does not replace the file.

---

## 6. Compatibility: packs change under live saves

An author edits lore, adds a location, rewrites the opening — while somebody has a save in
progress. This is normal, not exceptional.

The rule, already committed to for lore and generalised here: **content may move; state degrades
quietly and loudly.** A save referencing something the pack no longer defines drops the
reference with a warning. It is not corruption, and it must never be a crash.

The manifest carries a version so a save can record what it was played against, which is what
makes a warning specific enough to act on.

---

## 7. Scope — what to build now

**Almost none of it.** The point of this document is to make the lore build land in the right
place, not to build pack management.

Now, with the lore work:

- `worlds/marrow/lore/*.md` as the location for lore entries
- a pack root as an explicit parameter rather than an implicit working directory
- both roots configured, not cwd-relative

Later, when something needs them: the manifest, `seed.json` replacing `WorldSeeds`, the opening
message and its check, multiple saves per pack, prompt overrides, versioning, installing.

---

## 8. Decisions needed

1. **Does the pack ship an opening message at all in the first pass**, or does the seed stay in
   C# for now and the whole of §4 wait? (§7 assumes it waits.)
2. **Is the opening message turn 0 in history, or pack content replayed into context?** (§4.3
   leans to the second.)
3. **May a pack override the extraction prompt?** (§5 proposes no.)
4. **`worlds/` and `saves/` as siblings, or `saves/` inside the pack?** Siblings assumed above —
   it keeps a pack shippable without somebody's playthrough inside it.
