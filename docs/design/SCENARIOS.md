# Design — scenarios

**Status:** design settled 2026-08-15, no code. All open questions answered below.

The first piece of Phase 1. A pack describes a world; nothing describes a **story in it**.

---

## 1. The gap, measured

Two long model-played runs ended the same way, and the player's own inputs say why:

```
t91   *I keep going until we hit another new location.*
t141  *I open the next crypt door, cellar hatch, or tower stair if it gives us new ground.*
t226  *I follow the next obvious sign if the story offers one.*
```

That is not roleplaying, it is requesting content — because nothing tells the player, human or
model, what they are there to do. **53% of all deltas in the 230-turn run were movement**, and
the world grew as **31 new locations against 2 new characters**: empty rooms, one after
another.

Sessions end at 50 or 230 turns because someone gets bored, never because anything concludes.

---

## 2. What a scenario is

**The situation, and what is unresolved.** Why the player is here, what is wrong, what is at
stake. One prose block.

```
{{player}} arrived to investigate a mystery at a village.
```

Complete, at one sentence. So is this, at the other end:

```
{{player}} is living in a fantasy world. It is full of adventures and magic.
```

**Open-ended scenarios are legitimate and this is load-bearing.** A scenario with no dramatic
question has nothing to conclude, which is the standing argument against ever making an ending
condition or a clock mandatory. Some stories are a place to live in.

### What a scenario is *not*, and why ours is small

Other ecosystems bundle everything into one blob because a character card has no entity store —
there is nowhere else to put it. Taking a real example from that world (`The Lone Wanderer's
Echo`, a Fallout-inspired scenario) and asking where each part belongs here:

| their field | what it is | home in StoryWeaver |
|---|---|---|
| `rules.world` | vaults, radiation, lawless settlements | **lore** — built |
| `rules.characters` | who A.R.I.A. is, how she behaves | **character sheet** — built |
| `rules.player` | amnesiac, no skills, learns by discovery | **`player.md`** — built |
| `rules.narrative` | first person, visceral prose, pacing | **narration prompt override** — Phase 1 |
| `description`, `tags`, cover | shelf metadata | **`world.json` manifest** — Phase 1 |
| `rules.goals` | *why the Vault was abandoned; who {{player}} was* | **the scenario** |

Five of six already have homes. **Only the central conflict has nowhere to live**, and that is
the whole of what this adds.

The same test applied to two long "scenarios" from those sites — a medieval fantasy world with
guilds and half-breeds, a modern setting with policing and class — finds both are almost
entirely **setting**, which is lore. Long scenarios are mostly lore wearing a scenario's name.

**Length is the author's call and the loader does not police it.** Someone pasting two thousand
words gets a working game with a bloated prompt. Splitting a blob into lore, sheets and a
scenario is the import path's job (`TODO_FUTURE_WORK`), not a rule enforced at load.

---

## 3. Scenario vs opening — the distinction that matters

`WORLD_PACKS.md` already settled what an opening is: **a rendering of the seed, exactly as
every later turn is a rendering of canon.** The scenario sits beside it, and the difference is
lifetime.

| | what it is | lifetime | audience |
|---|---|---|---|
| **seed** | canon at turn 0 | becomes canon, then evolves | engine |
| **opening** | the first rendering of the seed | read once; **gone from the window after ~10 turns** | player, and the narrator briefly |
| **scenario** | what the story is about | **in every prompt, forever** | narrator |

**This is why a premise cannot just live in the opening**, and it is the mistake someone would
make first.

The failure is already documented, in the player-authored-canon notes: *say Astaria on turn 3
and the narrator references it correctly for ten turns — because it is sitting in the message
window, not because anything recorded it.* A premise written only into `opening.md` behaves
exactly like that: perfect for ten turns, then the story quietly forgets what it was about.

Which is roughly where both long runs began to drift.

---

## 4. Decisions

| question | decision | why |
|---|---|---|
| One field or several? | **One prose block** | Everything else already decomposes into lore, sheets and `player.md`. Fields invite filling in boxes instead of writing a situation — the same argument the sheet design made for prose. |
| How long? | **Author's call**, unpoliced | A scenario is one sentence or five paragraphs depending on the story. |
| Where does it go in the prompt? | **Appended to the system message** | `LlmNarrator` builds `System(prompt)` → history → `User(world state + input)`. The volatile part sits last so the prefix caches; the scenario never changes within a session, so it belongs in the stable half. Putting it in the world-state block would break the prefix every turn for nothing. |
| Does extraction see it? | **No** | Direct precedent: the narration window was withheld from extraction because *"feeding it prior turns invites it to re-extract old events as new deltas."* Hand the extractor "a child has gone missing" and it has every reason to emit `fact_established` for the premise on turn 1, and again whenever the prose brushes near it. The fact store already absorbs what the delta set cannot express. |
| Does it become canon? | **No** | It is present in every prompt regardless, so a fact buys nothing and costs the store an entry nobody learns and nothing revises. An author wanting the player to *know* something has `knows` in the seed — an explicit door that already exists. |
| Checked at load? | **`{{ }}` must resolve; names are not checked** | The opening is checked against the seed because it renders state that exists. A scenario legitimately names what does *not* exist yet — "the disappearances at the village" before any village is in canon. That is the story's future, not its present. But an unresolved `{{mona}}` in a prompt shown every turn is a bug that would run for 200 turns. |
| Optional? | **Yes** | `marrow` and `ashfall` ship none and must keep working unchanged. |
| Visible in `/prose`? | **Yes** | That command exists to show the narrator's actual view, and this is now part of it. |
| One per pack, or several? | **One, for now** | Several is more useful — a pack is a *world*, a scenario is a *story in it*, which is how tabletop separates setting from adventure. Deferred because several scenarios want several seeds (different stories start with different people present), and that is a structural change rather than a field. Revisit with the UI. |

---

## 5. Layout

```
worlds/marrow/
  world.json        manifest            <- Phase 1, not yet built
  seed.json         starting canon
  scenario.md       what the story is about     <- this document
  opening.md        the first thing read        <- Phase 1, not yet built
  lore/*.md
  characters/*.md
  prompts/*.md      overrides                   <- Phase 1, not yet built
```

At the pack root rather than in a directory, matching `seed.json`. If several scenarios ever
happen they move to `scenarios/`, and that is a migration worth paying then rather than a
directory holding one file now.

Plain markdown, no frontmatter. Frontmatter is where ending conditions, a clock or a goal would
go **if they ever earn it** — none of them has been measured, and an open-ended scenario has no
use for any.

---

## 6. What this does not settle

**Whether it works.** The scenario is standing text in a prompt; whether the narrator actually
holds a story to it over 200 turns is a narration question, and narration has no automated
quality control at all. `NARRATION_EVAL.md` has been an audit with no design committed since
July.

Shipping a story layer with no way to tell whether the story got better is the extraction trap
in a new place — a thing tuned by feel, for weeks, with no number. **The measurement is the
harder half of Phase 1 and it is not answered here.**

The cheapest honest check available today: the movement share and the
locations-per-new-character ratio. Both are computable from any save, both were extreme in the
two goalless runs (53%, 31:2), and neither requires a judge model. Not a quality measure — a
*aimlessness* measure, which is the specific thing a scenario is supposed to fix.
