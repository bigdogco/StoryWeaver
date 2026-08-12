# 2026-08-12 — Marrow gets a cast

The pack files from the play session, committed. An authored player, a companion, and a seat
for her in the seed.

---

## What is in it

**`characters/player.md`** — Pavel. The pack now authors the player, so the opening prompts do
not run and the session says who you are instead.

**`characters/inspector-mona.md`** — a companion with attitudes toward two lore entries and
toward the player by name, through `{{player}}`.

**`seed.json`** — Mona seated in `marrow-square`, with a starting standing of 100.

## Why this is the right default now

It was held back as the blank-slate world. The 51-turn session made the case for the other
shape: Mona followed the player the whole way, her narrated voice stayed recognisably her
sheet's `Manner`, and *"she is {{player}}'s long time partner"* rendering as a real name is
precisely what the `{{ }}` mechanism was built for and had never actually been seen doing.

A world that demonstrates the features is worth more as the shipped example than one that
demonstrates their absence.

## What it costs, stated plainly

**The manual path through the opening prompts has no shipped world to exercise it.** The branch
is still covered — `CheckAPlayerSheetReplacesCharacterCreation` loads the same pack twice, once
with the sheet and once without, and it was built that way on purpose because "the interesting
failure is the one that still looks like it works". What is gone is a human being able to see
those prompts by running the game.

A second pack is the obvious fix and the obvious home for the blank-slate shape. Logged, not
built.

## What it does *not* cost, corrected

I twice warned that seating Mona would leak into the eval, since every scenario derives from a
Marrow seed. **That was wrong.** Scenario seeds come from `WorldSeeds.Marrow()` in C#, and the
comment there says exactly why:

> Hand-built rather than read from `worlds/marrow/lore/` deliberately: an eval that changes
> score because somebody edited a pack file is measuring the wrong thing.

The separation was designed in and holds. A pack edit cannot move a score. Worth recording that
the warning was unfounded, because a caution repeated twice starts to sound like a fact.

## Verified

- `worlds/marrow` loads — **4 seated, 4 with sheets, 3 lore**
- One authoring error was caught by the load refusals on the way in: the attitude key was
  written `{{player}}` rather than `player`. `{{ }}` belongs inside the phrase, never in the
  key. It failed the load naming the file and the id, which is the behaviour those refusals
  were built for, working on a real mistake rather than a synthetic one
