# Devlog — fifty turns, and what a guard proved

**Date:** 2026-07-23
**Scope:** §9 validation — the manual play session, and the design gaps it exposed

---

## The session

51 turns in `saves/marrow`, played straight through over two days. Final canon: 8 locations,
7 characters, 53 facts.

| | |
|---|---|
| Deltas applied | 209 |
| Rejected | 8 (0.16/turn) |
| No-ops | 9 |
| Turns changing nothing | 12 (24%) |

No crash, no corruption, no desync between history and canon. Every one of the 6 `player_moved`
deltas landed in the right room, including the multi-stage prose that produced the bug fixed
the day before — the fix held under real play, which is the only place it had ever failed.

Six of the eight rejections are the validator working: re-introducing a character who already
exists, re-establishing a fact already in canon. Exactly one lost information. On turn 23 two
`fact_learned` deltas referenced `player-emerged-from-mill`, a fact the model never established,
so Hald and his companion failed to learn something that really happened. The dependency-tier
sort cannot help here — there was no `fact_established` anywhere in the batch to sort ahead of.

24% of turns changing nothing is worth recording as normal rather than suspicious. Those are
conversations that revealed nothing, movement inside a known room, and description. The prompt
says "most turns should establish no facts"; the session agrees.

## The guard

The best result of the session is a walk-on part.

Tomas Reed was introduced on turn 10 and last appeared on turn **13**. On turn **51** he walked
back into the story:

> canon: *"A young guard stationed in Marrow Square. He wears stiff, cheap leather armor and
> carries a spear."*
>
> turn 51: *"the rustle of stiff leather announces Tomas Reed. The young guard stands near the
> boarded-up well, his knuckles white around the shaft of his spear."*

Right name, right armour, right weapon, right place — standing at the boarded well, which is
`marrow-square`, where canon had parked him for 38 turns. He kept the `terrified` status he
picked up on turn 11.

The narration window is 10 turns. Turn 13 was nowhere near the model's context. He came back
because the player walked into his location on turn 50 and the world-state block put him in
front of the narrator.

This is the whole thesis, demonstrated rather than asserted: **canon as the source of truth
means a character can leave the story for forty turns and return correct.** A larger context
window does not give you this — it defers it. Canon does not degrade with distance.

Worth stating the negative control, because it is in the same session: **Nessa came back wrong
in exactly the same way.** She has been named since turn 15, and the narrator still gets her
right — but only because the *fact* `figure-name-nessa` is doing the work her character record
should be doing. Her `name` in canon is still `"Shivering figure"`. Same mechanism, one entity
where it holds and one where it is papered over. Which leads to the real finding.

## Facts are absorbing everything the delta set cannot express

Three separate times, the model met a limit in the closed delta set and routed around it
through `fact_established`.

**A name reveal.** There is no rename delta. A character is introduced once, with a name, and
that name is permanent. So the anonymous figure in the cistern is `"Shivering figure"` forever,
and her real name lives in a fact:

```
figure-name-nessa | The young woman hiding in the cistern is named Nessa.
```

Anonymous-stranger-becomes-named-person is not an edge case, it is one of the most common moves
in the medium. Along with `figure-is-young-woman` and `figure-in-cistern-location`, three facts
exist to carry what are properly attributes of a character.

**A lie.** Facts have no truth value and no speaker, so a claim and a truth are stored
identically. Hald lied twice while stalling at the mill, and both are in canon:

```
hald-claims-roof-leaking | Hald claims the mill roof is leaking and he needed to check the foundations.
hald-looking-for-stray   | Hald is in the square looking for a stray.
```

The first one is interesting: the model wrote `claims` into the id and the text *itself*,
unprompted, because it had nowhere else to put the distinction. The second did not get that
treatment and is now simply false canon. A world model that cannot represent a lie will
eventually narrate one as true.

**Blow-by-blow.** Combat has no representation, so each exchange became permanent world truth:

```
drowned-follower-wounded-again   | ...struck deeply in the torso by the player's sword...
drowned-follower-wounded-again-2 | ...struck deeply in the side by the player's sword...
```

The `-2` suffix is the model resolving its own id collision. The creature is now dead, so these
are permanently true and permanently useless — sediment in the fact store, competing for
context budget with things that matter. The `status_changed` chain
(`wounded` → `severely wounded` → `dead`) already carried the real state correctly.

**These are one finding, not three.** The fact store is the only open-ended slot in the schema,
so it is where everything unrepresentable goes. That is a load-bearing observation for the
lore-entry design already queued: adding a fourth entity type without fixing the pressure just
gives the overflow somewhere new to pool.

## Two smaller things

**Location identity.** On turn 29 the model introduced `marrow-square-night` — "Marrow Square
(night)" — as a distinct location, its description carrying events rather than permanent
character ("the aftermath of a fight lingers: the smoldering corpse of Hald's companion"). On
turn 30 it then moved the player to `marrow-square`, correctly. It caught itself, and canon is
left with an orphan nobody ever entered. Time of day read as a different place. The general
question is the same one already logged for buildings: when does a described thing become an
entity, and when is it the same entity in different clothes.

**Two paths move the player.** Turn 47 moved the player with `character_moved` and
`characterId: "player"` rather than `player_moved`. It applied correctly — `world.Player` and
`FindCharacter("player")` return the same object, which is exactly the payoff of the earlier
decision to make the player an ordinary character. But the duplicate-detection keys differ
(`moved:player:X` vs `player-moved:X`), so a batch emitting both slips past the check, and any
rule the validator applies to one kind can be bypassed through the other. Harmless today,
asymmetric by construction.

**Player-authored canon is stored verbatim.** `/fact` text goes into canon exactly as typed and
reaches the narrator as authoritative prose, typos included:

```
cult-of-the-blind | Cult of the Blind is an encient cult worhiping an old god Shurus.
                    A violent and dakr god, aso is its members.
```

No harm observed — the narrator read through it fine. Recorded because the input path is
unvalidated and the output is permanent.

## What generalises

- **A play session finds different bugs than an eval, and they are better bugs.** The eval
  measures whether the model does what the schema allows. Play finds what the schema does not
  allow. Every finding above is the second kind, and none of them could have come from the
  scored set — the eval has no notion of a character who should be renamed.
- **Where a model works around your schema, read it as a specification.** `claims` written into
  a fact text, a `-2` suffix on a colliding id, a name stored as a fact — the model is naming
  missing features. It is a more reliable signal than an outright failure, because it looks
  like success.
- **The negative control was in the same session.** Tomas came back right and Nessa came back
  right-for-the-wrong-reason. Without auditing canon on disk, both look identical from the
  prose, which is the whole reason the prose is not the source of truth.

## Next

§9 closes the bootstrap. The queue, ranked, in `TODO_S9_VALIDATION.md`: character rename first,
then fact truth value and attribution, then location identity, then the movement seam.
