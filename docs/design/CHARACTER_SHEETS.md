# Design — character sheets

**Status:** built 2026-08-06. Design written 2026-08-04; all decisions settled and shipped.
**Amended 2026-08-06** — decision 4 reversed, decision 7 added. See §9.

Authored identity for characters: who someone is, how they present, what they want, and how
they feel about the groups in the world. The equivalent of a SillyTavern character card, fitted
to an architecture that also has *played* state.

Supersedes the open questions in [`TODO_PLAYER_SHEET.md`](../todo/TODO_PLAYER_SHEET.md) — the
player sheet is this, with one half omitted.

---

## 1. The split that makes it fit

A character card is entirely static; everything on it was written by an author. This project
also has state the story changes every turn. A sheet is only the first kind:

| | authored — the sheet | played — canon |
|---|---|---|
| name, appearance, manner, wants | ✅ | |
| attitudes toward groups | ✅ | |
| mood, status, where they are | | ✅ |
| standing toward the player | | ✅ |
| what they know | | ✅ |

**Sheets are pack content and live beside `lore/`.** Same rule that makes lore shippable: a
world can be shared, edited between sessions, and version-controlled without carrying somebody's
playthrough inside it. And the same rule the player-rename bug established — *the story may
wound you, not redefine you*.

## 2. Prose, not fields

Decided. The model consumes prose either way — structured fields are flattened into text before
they reach it, so fields buy nothing for comprehension. What they cost is expressiveness:
`build: heavyset` loses "wipes the same patch of counter when he is thinking", which is the
detail that actually makes Hald land in the narration.

Fields are worth having only where *code* needs to read something. That is the split the lore
format already draws, and sheets should reuse it exactly:

```markdown
---
attitudes:
  kings-investigators: fears them, will not say the name aloud
  cult-of-the-blind: quietly devout, and ashamed of it
---

# Hald

Heavyset and watchful, with forearms like ham hocks and a publican's memory for faces. He
wipes the same patch of counter when he is thinking, and does not notice he is doing it.

## Manner

Speaks flatly and briefly. Answers a question he dislikes by changing the subject to your
drink. Loud only when frightened.

## Wants

For the well to stay shut and the village to stay ordinary. Will lie a long way to get it,
and is not good at lying.
```

Filename is the id, `#` heading is the name, body is what the narrator reads. Headings inside
the body are suggested rather than enforced — they give a future editor something to render
without forcing anyone to fill in boxes.

**Decided: extend the parser by exactly one nesting level.** `MarkdownLoreReader` is strict and
flat by design — three scalars and one list, no YAML dependency, unknown keys refused. Sheets
need `attitudes` as a map.

The alternative was flattening to `dislikes: kings-investigators, orcs`, which parses today and
loses the phrase. That phrase is the whole value: "fears them, will not say the name aloud" is
what the narrator would actually have used, and "dislikes" is not.

One level, and the same strictness — an unknown key is still an error, the reader still refuses
what it does not understand. **Not a step toward YAML**, and the next request for nesting should
be argued on its own merits rather than waved through as precedent.

## 3. Attitudes: toward groups, and toward anyone with a sheet

From the vision: *"NPC does not like King's Investigators, NPC does not like Orcs."*

Both are groups, and groups are lore entries with ids. So attitudes are uniform —
`character → lore id → a phrase` — reusing the namespace `Character.Knows` already holds. Orcs
become a lore entry the same way the Investigators are one.

**Attitudes toward individuals are included too**, pointing at the player or at any character
who has a sheet:

```markdown
---
attitudes:
  kings-investigators: fears them, will not say the name aloud
  player: dislikes him — he stole his sword, years ago now
  hedge-witch-morwenna: drinking companions their whole lives
---
```

An earlier draft excluded these, worrying about an N×N field mostly empty. **That concern was
wrong**, and worth recording as an error rather than quietly dropping: an N×N problem belongs
to *derived* data. Authored content is sparse by nature — nobody fills in a matrix, they write
the two or three relationships that matter, and only for a cast that already has sheets.

### The sheet holds the why; canon holds the standing

The examples above are not standing values. *"He stole his sword, years ago now"* is **history**
— a story hook the narrator can use, permanently true regardless of how the relationship
develops. `RelationshipToPlayer` is a number that moves every time the story turns.

| | where | changes? |
|---|---|---|
| "dislikes him — he stole his sword" | the sheet | never; it happened |
| `standing: -20, "wary of strangers"` | `seed.json`, then canon | every turn it should |

He may stop disliking you. He will always be the man whose sword you stole. Following decision
1, the *why* is authored identity and the *number* is starting state, so they sit in different
files without duplicating anything.

### What stays out: extracted relationship change

`relationship_changed` has fired **once across 253 turns and five sessions** — turn 11 of
`marrow-LLM-1`, where an innkeeper turned openly hostile in as many words. Everything else came
through unmoved: a lie exposed, open contempt for the crown, a man terrified into cooperation,
and a companion watching the player burn somebody alive.

Extraction cannot track standing — it accumulates across scenes and a per-turn extractor sees
one turn.

So sheets take the half that works, which is authoring. Making standing *move* correctly is the
reconciliation-pass problem, and stays out of this design entirely.

### Re-checked 2026-08-12, and the reason is sharper than "the model can't"

The evidence was gathered before measurements carried provider names, so it was re-examined
after a provider was found scoring 0% on scenarios another scored 100% on. **The conclusion
holds, and the mechanism turns out to be the opposite of what "never fires" suggests.**

**The capability is fine.** The `hostility` eval scenario — where the prose states outright
that a character's regard has changed — scores **10/10** on a healthy provider. The model emits
`relationship_changed` perfectly well when asked a question it can see the answer to.

**The trigger is what never occurs.** One firing in 253 turns of play, and the one that fired
was the case that looks like the eval: somebody announcing hostility in the scene.

The cleanest evidence is the contrast inside a single session. After 51 turns, every standing
sat at exactly its seeded value:

```
drinker-mabb      0    no strong feelings        (seed)
innkeeper-hald  -10    suspicious of strangers   (seed)
inspector-mona  100    likes and respects        (seed)
```

while **mood moved constantly** over the same turns — `terrified`, `enraged`,
`shaken, relieved`. Same prose, same extractor, same call.

**Mood is visible in one scene and standing is not.** That is the whole of it. Ordinary prose
does not announce that someone's regard has shifted; it shows a moment, and standing is the
integral of many moments. A per-turn extractor is structurally the wrong instrument, and no
prompt rule fixes a thing the input does not contain.

**A note on what cannot be tested.** The eval only covers the case where the prose says it
outright, and it passes. There is no scenario for the realistic case — resentment accumulating
across scenes — and there cannot usefully be one, because a scenario is a single turn by
construction. That is not a gap in the eval; it is the same finding stated from the other side,
and it is why this belongs to the reconciliation pass.

## 4. The player is a sheet with the second half missing

**Built 2026-08-06, and the mechanism turned out to need no special case.** Sheets match by id,
and the player's character id is `player`, so `characters/player.md` works for the same reason
`characters/innkeeper-hald.md` does. That falls out of the player being an ordinary
`Character` — the decision that keeps paying.

What a pack writes there is the **premise**: "you carry the crown's seal and the authority that
comes with it." The scenario's hook, not the player's identity. Character creation then supplies
who *this* investigator is.

**Attitudes are refused on the player's sheet**, at load, by name. They parse and validate and
are never rendered, so ignoring them would leave an author with a field that reads as working
and does nothing — the silent drop refused everywhere else here. A premise can say the same
thing in prose, which the narrator reads anyway.

The reasoning is the player's, and worth quoting: *player attitude is player attitude shown
through his play, not hard-wired into his sheet.*

**Still open, and a UI question rather than an architecture one.** A console prompt cannot take
a multi-paragraph sheet, so the player currently writes one line where an author writes
sections. The storage is already right — it is their `Character` record, which a pack can seed
and an editor can edit — so this waits for the UI rather than needing a new file category.

### The sheet and the opening prompts are mutually exclusive — settled 2026-08-06

They wrote the same two fields, in that order, and nobody had said which wins. The prompts ran
second, so a pack shipping `player.md` had its authored name overwritten *always*, and its
premise — "you carry the crown's seal" — overwritten the moment the player typed any
description at all. Both halves working exactly as written, and no symptom.

**A `player.md` replaces character creation entirely.** `WorldPack.AuthorsThePlayer`, and the
harness does not ask.

The direction follows decision 1, where it always did: *the sheet defines the character.* The
player was the last exception to that, for no reason beyond the order the two features were
built in.

What it buys is that both shapes are now expressible, by presence or absence of one file:

| | `player.md` | opening prompts |
|---|---|---|
| a named protagonist, visual-novel shape | ✅ | — |
| a blank slate the player invents | — | ✅ |

**Nothing is locked either way.** `/rename` already lets the player change their own name and
description mid-story while the story cannot, so an authored protagonist is a starting point
rather than a cage. The session says so on the way in, because a game that simply never asks
reads as one that forgot to.

**The seed still needs a `player` entry**, with a location — a sheet has nowhere to put one. So
a pack with `player.md` spans two files exactly as Hald does. That is the cost decision 1
already accepted, applied consistently rather than waived for one character.

**Changed 2026-08-12: `worlds/marrow` now ships a `player.md`.** It was held back as the
blank-slate world, and the first session played against sheets made the case for the other
shape — an authored protagonist with a companion who has an attitude toward them by name is
what the whole `{{player}}` mechanism was built for, and it reads better than a stranger.

What that costs is small and worth stating: the *manual* path through the opening prompts now
has no shipped world to exercise it. The branch itself stays covered by
`CheckAPlayerSheetReplacesCharacterCreation`, which loads the same pack twice, once with the
sheet and once without — deliberately built to cover both directions for exactly this reason.
A second pack would restore the manual coverage the day one exists.

### The original reasoning, unchanged

Consistent with the player being an ordinary `Character`, which has paid off repeatedly.

The player gets a sheet — name, appearance, manner, whatever the player wants the narrator to
know. They do **not** get authored attitudes toward groups, because that is what playing the
game decides. And their sheet is **always loaded**, where an NPC's is loaded when they are
present.

This also finishes the protection work: `/rename` already lets the player set name and
description while the story cannot. A sheet is the richer version of the same thing.

## 5. Loading

Partly solved already: only characters in the room reach the context, so presence is doing the
filtering that "load when required" describes.

The open question is size. A full sheet is several paragraphs; a crowded room with five NPCs is
five sheets in every prompt, every turn — on top of lore, which has the same problem deferred,
and loose items, which added a second contributor.

**Proposal: send the whole sheet for present characters and measure.** Budgeting introduces a
way to *silently* omit something, which is the single biggest source of "why did the AI forget
the Duke existed" in existing tools, and it is not worth building before there is a measurement
saying it is needed.

## 6. Sheet and seed — settled

**The sheet defines the character. `seed.json` holds only their starting state.**

```
worlds/marrow/characters/innkeeper-hald.md     who he is      (id, name, description, attitudes)
worlds/marrow/seed.json                        where he starts (location, mood, status, knows, standing)
```

`seed.json` drops `name` and `description` for any character with a sheet. Nothing is written
twice, so nothing can disagree — which is the failure the other options all shared, in different
disguises.

This is the pack/save split applied one level deeper: **identity is content, condition is
state.** A character's name is not a thing the world does to them, and their mood is not part
of who they are.

**The cost, stated honestly:** one character now spans two files, and adding a character means
touching both. That is the price of not having two places able to claim the same field, and it
is the cheaper mistake — a file split is visible, a silent disagreement is not.

The load path merges them: sheet first for identity, then the seed entry for state. A seeded
character with no sheet keeps working exactly as today, so no existing pack breaks.

## 6.1 Referring to other entities: `{{ }}`

Authored content has to name entities it does not own — Hald's sheet mentions Morwenna, the
Investigators' entry mentions the player.

**The justification is narrower than it first appears, and worth stating accurately.** An
earlier draft argued from "names are mutable and ids are not", the principle behind
`character_renamed`. That barely applies: characters *with sheets* have names their author
fixed, and `character_renamed` exists for characters discovered in play — "Shivering figure"
becoming Nessa — who have no sheet, because nobody can write a sheet for someone who does not
exist yet.

The real reason is the one SillyTavern has: **a pack author cannot know the player's name.**
That is `{{player}}`, and it stands alone.

`{{<entity-id>}}` survives for one genuine case — a pack shipping a deliberately anonymous
character ("The Hooded Stranger") whom the story later reveals, where other sheets referring to
them should follow. Real, rare, and cheap correctness rather than the main event.

So sheets and lore bodies may contain:

| form | resolves to |
|---|---|
| `{{player}}` | the player character's current name |
| `{{<entity-id>}}` | that entity's current name |

Three rules, each earned:

- **Resolved at context assembly, not at load.** Resolving once when the file is read would
  freeze the name and lose the entire point. Per-turn resolution means a rename flows through
  every sheet that mentions them.
- **Validated at load, loudly.** An unresolvable `{{id}}` fails the pack load naming the file
  and the id. It must never reach a prompt — an id in prose is the bug that forced the
  `ForNarration` / `ForExtraction` split ("the heavy oak door of the marrow-tavern flies
  outward"), and it has been paid for once already.
- **A closed set, not a template language.** Exactly these two forms; anything else is a load
  error. SillyTavern's macros grew conditionals, randomness and state lookups. Adding a third
  form should be a decision, not a discovery.

**`{{player}}` resolves to the name, not to "you".** A sheet is a description of Hald, not
narration: "wary of Pavel" is a fact about Hald that holds regardless of who reads it, while
"wary of you" makes the sheet's meaning depend on its reader. It also helps extraction, which
already holds `Pavel (id: player)` in its roster — "you" would make it infer the referent first,
and inferring is where deltas get lost.

### The consequence: character creation becomes a step

The seed currently ships `"name": "You"` for the player, so `{{player}}` would render *"Hald is
wary of You"* — exactly the confusion this decision avoids.

The fix is not a better default. **Names are fixed, for the player as much as for any authored
character**, so the player should write theirs before turn 1 — the same act the pack author
performed for Hald. Character creation is a step, not a fallback.

That default was harmless while the player's name appeared nowhere but their own record; sheets
are the first feature that shows it to somebody else. It also fixes something separate that was
never really defended: a new world currently opens with a character called "You" whose one-line
description nobody chose.

**Naming should be required rather than skippable.** Every alternative reintroduces the pronoun
problem, and it is one prompt at the start of a world.

## 7. What this does not include

- **Stats and abilities as numbers.** "Quick with a knife" belongs in prose; anything the
  *engine* reasons about is the dice-resolved-checks design, and answering it here would
  pre-empt that.
- **`character_described` interaction.** If a sheet is authored and the story can also revise a
  description, they will fight. Probably the story revises canon's `Description` and never the
  sheet — but that needs settling when `character_described` is built.

## 8. Decisions — all settled 2026-08-04

1. **Sheet vs seed** — the sheet defines the character, `seed.json` holds starting state only.
   Nothing written twice. §6.
2. **Parser nesting** — extend by exactly one level for `attitudes`, keeping the same
   strictness. The phrase is the value. §2.
3. **NPC-to-NPC attitudes** — yes, toward the player and toward anyone with a sheet. The N×N
   concern was wrong: it applies to derived data, not authored. §3.
4. ~~**A sheet without a seed entry** — defines the character as offstage, which
   `Character.LocationId` already supports and `/character` already does. Lets an author write a
   cast before placing them.~~ **Reversed 2026-08-06 — see §9.1.** The offstage character it
   created was unreachable.
5. **`{{player}}` resolves to the name**, not "you". A sheet describes a character rather than
   narrating to a reader. §6.1.
6. **Character creation is a required step at world start** — the player names and describes
   themselves before turn 1, exactly as the author did for Hald. Replaces the unchosen "You".

Ready to build. The one item larger than it looks is 6: it is the first thing a new world does
and needs a place in the harness that does not exist yet.

---

## 9. Amendments, 2026-08-06

Both found by walking the load path aloud after sheets shipped, not by a failing session.

### 9.1 Decision 4 reversed — a sheet must be placed in the seed

**Was:** a sheet with no seed entry creates the character offstage (`LocationId = null`).
**Now:** it fails the load, naming the file and saying the character is nowhere.

The offstage character it produced was **unreachable**, not dormant. Three exits, all shut:

| route | why it fails |
|---|---|
| the narrator introduces them | `AppendNpcs` filters on the player's location; someone nowhere is never in a scene |
| the player mentions them by name | *mention never creates or moves an entity* — measured 0/7, consistent across 21 runs |
| the player places them with `/character` | it only **introduces**; `AskId` refuses an id already in canon |

The extractor is the one thing that sees them, because `AppendKnownIds` lists every character
id — but it gets a bare slug with no name and no description, so nothing would make it emit
`character_moved` for a person it knows nothing about.

**The asymmetry that makes the reversal right.** `/character` still allows blank-means-offstage,
and should: a brother back home, a name from a rumour — someone the player invented by talking
about them, who is not anywhere yet. The player knows who they meant and can bring them up
again. An author who wrote a sheet and forgot the seat has no such memory, and no symptom
either: the pack loads, the character exists, and they never appear. That is the silent drop
this project refuses everywhere else.

So the rule divides on **who authored the character**, not on whether a location is known:

- **authored (a sheet)** — must be placed. Same shape as `RequirePlayer`.
- **played (`/character`)** — may be nowhere. Unchanged.

**Built slightly broader than written, on purpose.** A `seed.json` entry with
`locationId: null` and no sheet is unreachable for exactly the same reasons, and is authored by
exactly the same person. Refusing one and not the other would have been a rule about *files*
pretending to be a rule about authorship. So the shipped check is **every character in a seed
has a location** — `RequireEveryoneIsPlaced` — with the sheet case kept separate in
`ApplySheets` only because it is the one that can say the useful thing: *the sheet exists, the
seat does not.* Two errors, two messages, one rule.

In the player's words: *same as in any RPG, a character has to start somewhere, some area the
player can go to.*

**What this closes off, honestly.** Writing a cast before deciding where anyone stands now
means writing `seed.json` entries with placeholder locations, rather than sheets alone. That is
a worse authoring experience in a text editor and a non-issue in an editor UI, which is where
placement belongs anyway — a list of locations, and the character dropped into one. The CLI is
what made "offstage" read as a bug; a map would have made it read as an empty slot.

### 9.2 Decision 7 — ids are kebab-case, enforced

`warrior_mike` and `warrior-mike` are the same character to a human reader and two different
strings to everything else. A sheet named `warrior_mike.md` referenced from `seed.json` as
`warrior-mike` produces a character with no seat *and* an unresolvable seed entry, and the
diff between them is one glyph that is easy to look straight past while hunting for it.

The convention already exists — `innkeeper-hald`, `drinker-mabb`, `kings-investigators` — and
nothing enforces it. **Make it a load error:** an id must be lowercase letters, digits and
single hyphens, and must not start or end with one.

Applies to what an author types: sheet filenames, lore filenames, and ids inside `seed.json`.
The error names the file and shows the offending id.

**Open, and deliberately not decided here:** whether ids *proposed by extraction* are held to
the same shape. `Slug()` already produces kebab-case for the authoring commands, so the
question is only about the model's own `character_introduced` ids. Refusing them there is a
rejection cascade rather than a load error, which is a different cost — and it belongs with
`DeltaValidator`, not with this design.
