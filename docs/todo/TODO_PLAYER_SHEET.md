# TODO — the player character sheet

Raised from play 2026-08-04: *"I feel like we need a character sheet for the Player — his name,
a bit of physical description and so on, so I can describe his abilities. Something for the LLM
to work with."*

The audit found the request sitting on top of a bug, now fixed: the player's identity was a
mutable field with no owner, and turn 38 overwrote both name and description. See
[`2026-08-04_second-session-audit.md`](../devlog/2026-08-04_second-session-audit.md).

**Superseded by [`docs/design/CHARACTER_SHEETS.md`](../design/CHARACTER_SHEETS.md).** The player
sheet turned out to be the general character sheet with one half omitted — the player gets no
authored attitudes toward groups, because that is what playing the game decides. Kept as a file
because the protection work below is done and belongs on the record.

---

## Done

- [x] The story cannot rename the player; the player can, via `/rename`
- [x] A name equal to the id is refused
- [x] The corrupted save repaired in place

## Questions — all answered by the design

- [x] **Is this a Character field, or its own thing?** — its own authored file in the pack,
      beside `lore/`. The player stays an ordinary `Character` in canon
- [x] **Does it apply to NPCs too?** — yes, that is the point. A sheet for the player and
      nothing for Hald was the asymmetry that made this a general feature
- [x] **Authored, extracted, or both?** — authored, never extracted. Same rule as lore, and the
      same rule the player-rename bug established
- [x] **Abilities: prose or structure?** — prose. Fields are flattened before the model reads
      them, so they buy nothing for comprehension and cost expressiveness. Numbers belong with
      dice-resolved checks
- [x] **Where does it live?** — `worlds/{pack}/characters/{id}.md`, not `seed.json`. Which
      raises the overlap question that is now decision #1 in the design

## Prerequisite worth noting

The `character_described` delta from the fact-hygiene work is the mechanism by which a
description can legitimately change during play. If a sheet is richer than one line, that delta
needs to know what part of it the story is allowed to touch — carried into the design as an
open interaction.
