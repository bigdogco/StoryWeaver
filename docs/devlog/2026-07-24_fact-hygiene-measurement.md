# Devlog — the scenarios that failed to fail, and the ones that did

**Date:** 2026-07-24
**Scope:** measuring the fact-hygiene design before building it, and finding it aimed at the
wrong target

---

## What was about to be built

The fact audit found that fewer than one fact in five is a fact: 12 momentary events, 11
descriptions, 10 correct, 7 lore, 5 claims, 4 who-knows-what. The design proposed prompt rules
for events and knowledge-facts, and a `character_described` / `location_described` delta for
descriptions — described in the doc as "the largest fixable category".

Four decisions were settled and the build was sequenced. **Then the evals were written first,
and none of it survived contact.**

## Round one: the scenarios that would not fail

Three scenarios, one per category — a room described in detail, a completed action with no
consequence, a revelation where the knowledge relationship is the interesting part.

```
description-not-fact      forbidden 0.00
event-not-fact            forbidden 0.00
knowledge-not-fact        7/7 required, forbidden 0.00
```

Clean. So world size was tried, since that is the variable that explained
`two-stage-entry` going 14/14 small and 2/14 large:

```
description-not-fact-large    forbidden 0.00, 14/14 clean, zero deltas proposed
event-not-fact-large          forbidden 0.00
```

**Also clean.** The model does not misfile descriptions, events, or knowledge. A prompt rule
written against these would have been a rule fixing nothing, measured against a scenario that
could not detect whether it worked.

## Why the scenarios were wrong

Back to the turns that actually produced description-facts. The pattern is not what was
assumed:

| what it describes | why it became a fact |
|---|---|
| the altar, the medallion, a hidden object | **items do not exist** |
| the well's base, a tunnel fork, a branching passage | **sub-spaces are never locations** |
| a smell, a splashing sound | sensory detail has no home |
| `mill-ruins`, a character's location, a character attribute | *these three have entities* |

**8 of 11 describe something with no entity at all.** The scenario described a *room*, which
has a `Location` to hold its description — so the model correctly put nothing in the fact
store, because it had somewhere better to put it, and in fact needed to change nothing.

The diagnosis was wrong at one level of depth. "Descriptions land in facts" is not a missing
*delta*, it is mostly a missing *entity type*. `character_described` would have fixed 3 of 11
while the design claimed it addressed the largest category.

## Round two: scenarios in the shape that actually fails

Rewritten from the real turns — an object produced and described, blows landing on a character
already in canon, a space perceived but not entered.

### An item becomes a character. 7/7.

```
character_introduced  knife (a broken ritual knife) @ marrow-tavern
fact_established      knife-found: Mabb found a broken ritual knife in the reeds.
```

Every single run. The knife is standing in the tavern, with a name, a description and a
location, because `character_introduced` is the only delta that can bring a *thing* into
canon.

This is the same shape as the AtlasCloud failure recorded in `CHALLENGES.md` — a building
emitted as a `character_introduced` — which was written up as a bad provider reasoning badly.
It is now reproduced **7/7 on a good provider**. The provider was worse at hiding it; the
pressure was always there.

`Item` has been logged as a domain gap since a session where the player paid coppers for a
beer. It is not a nice-to-have. It is the answer to eight of the eleven description-facts and
to this.

### Physical damage goes into mood. 7/7.

```
mood_changed  innkeeper-hald = injured
```

`blow-landed` scored **0/7 on required**: Hald is beaten unconscious over his own counter and
`status_changed` never fires. Instead "injured" is written into the *mood* field.

Notable that `forbidden` was 0.00 — no blow-by-blow facts. So the fact-store theory was wrong
about combat too, and the real defect is a different one entirely: **mood is absorbing status.**

The §9 audit saw the edge of this without naming it — `guard-tomas` ended with mood `startled`
and status `terrified`, `drinker-mabb` with mood `terrified` and status `drunk`. The fields are
documented as distinct and the model does not reliably distinguish them.

This is a prompt-fixable defect with a real eval behind it, which makes it the most actionable
thing found today.

### A sub-space is mostly ignored. 1/7.

Leaning over the well produced description-facts once in seven, and moved the player to the
square in four — which is arguably correct, since the well is in the square and the player was
in the tavern. Weak reproduction; the category is real in play but this scenario does not
provoke it reliably.

## A third sighting of the attribution instinct

```
knife-found-in-reeds: Mabb claims he found the ritual knife in the reeds.
```

Unprompted, again. The model wrote "claims" because it wanted to record a speaker and had no
field for one. That is now three independent sightings — `hald-claims-roof-leaking` in play,
the same instinct during the lore work, and this. The `source` field has better evidence behind
it than anything else in the original design.

## What this changes

| was | is |
|---|---|
| descriptions are the largest fixable category | 3 of 11 fixable that way; 8 need `Item` and sub-locations |
| events are misfiled as facts | not reproducible; the real defect is status vs mood |
| prompt rules for events and knowledge | no measured failure to fix — do not write them |
| `source` on facts is fifth in line | third independent sighting, best-evidenced item |

Nothing was built, which is the point. Every prompt rule written this year without a control
was wrong, and this is the first time that lesson has been applied *before* writing one rather
than after.

## Next

The evidence now points at, in order:

1. **`status` vs `mood`** — 7/7 measured failure, prompt-fixable, eval already exists
2. **`Item`** — 7/7 measured failure, the answer to most description-facts, and a genuine
   domain-model addition rather than a prompt tweak
3. **`source` on facts** — three sightings of the model asking for it
4. **`character_described` / `location_described`** — still worth having, now correctly sized
   at 3 of 11 rather than "the largest category"
