# TODO — the player character sheet

Raised from play 2026-08-04: *"I feel like we need a character sheet for the Player — his name,
a bit of physical description and so on, so I can describe his abilities. Something for the LLM
to work with."*

The audit found the request sitting on top of a bug, now fixed: the player's identity was a
mutable field with no owner, and turn 38 overwrote both name and description. See
[`2026-08-04_second-session-audit.md`](../devlog/2026-08-04_second-session-audit.md).

---

## Done

- [x] The story cannot rename the player; the player can, via `/rename`
- [x] A name equal to the id is refused
- [x] The corrupted save repaired in place

## The actual feature

Protection is not structure. The player currently has `Name`, `Description`, `Status`, `Mood`,
`LocationId`, `Knows` — the same shape as every NPC. What was asked for is somewhere to put
*who this person is* in a way the narrator can use.

## Questions to settle first

- [ ] **Is this a Character field, or its own thing?** The player is deliberately an ordinary
      `Character` — that decision has paid off repeatedly and should not be undone lightly. But
      "a King's Investigator, scarred left hand, quick with a knife and bad at lying" is not a
      `Description`, which is one line and gets replayed into every prompt.
- [ ] **Does it apply to NPCs too?** A sheet for the player and nothing for Hald is an
      asymmetry that will be felt the moment somebody wants a detailed NPC. The lore design
      already rejected "lore entries about characters" on the grounds that an entity is
      authoritative about itself — the same argument applies here, and points at a richer
      `Character` rather than a parallel system.
- [ ] **Authored, extracted, or both?** Lore settled this cleanly: authored, never extracted.
      A sheet is probably the same — the story may wound you, not redefine you — which is the
      rule just enforced for the name.
- [ ] **Abilities: prose or structure?** "Quick with a knife" as free text costs nothing and
      the narrator will use it. Anything the *engine* must reason about — a dice check, a
      skill — wants structure, and that is the dice-resolved-checks design, not this one. Worth
      keeping the two apart until dice actually exists.
- [ ] **Where does it live?** Almost certainly the pack seed, since `seed.json` already carries
      the player's starting record. That makes a sheet an authored starting state rather than a
      new store.

## Prerequisite worth noting

The `character_described` delta from the fact-hygiene work is the mechanism by which a
description can legitimately change during play. If a sheet is richer than one line, that delta
needs to know what part of it the story is allowed to touch — which is another argument for
settling *authored vs extracted* first.
