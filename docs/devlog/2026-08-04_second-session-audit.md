# Devlog — the second fifty-one turns

**Date:** 2026-08-04
**Scope:** auditing a fresh 51-turn session against the first, and four findings from play

---

## The comparison

Same starting world, same length, one build apart. The first session ran before rename, lore,
packs, and the status/mood fix; the second ran after.

| | session 1 | session 2 |
|---|---|---|
| turns | 51 | 51 |
| deltas applied | 209 | 172 |
| rejected | 8 | **4** |
| no-ops | 9 | 14 |
| turns changing nothing | 12 (24%) | 14 (27%) |
| `status_changed` | 6 | **10** |
| `relationship_changed` | 0 | **0** |

### Facts, classified the same way

| | session 1 | session 2 |
|---|---|---|
| **durable world truth (correct)** | **10 of 53 — 19%** | **24 of 43 — 55%** |
| momentary events | 12 | 9 |
| descriptions | 11 | 6 |
| lore paraphrase | 7 | (folded into the above) |
| claims with no source | 5 | 4 |
| names | 2 | **0** |
| who-knows-what | 4 | **0** |

**Fact quality nearly tripled.** Two categories went to zero — names, because
`character_renamed` now exists, and who-knows-what, which had been four facts duplicating
deltas that already fired. Descriptions nearly halved.

`status_changed` firing 10 times against 6 is the mood/status fix showing up in play. Physical
harm now lands in the field built for it.

**`relationship_changed` is still zero after 102 turns across two sessions.** That is no longer
a suspicion. Standing accumulates across a scene and a per-turn extractor sees one turn, so no
prompt rule will reach it — the reconciliation pass is the answer.

## The four findings from play

### 1. Nobody learned the cult, and the eval cannot see why

Zero characters know `cult-of-the-blind` after 51 turns, despite the entire story being about
it. The lore *worked* — the narrator used the Drowned Father, the weeping woman, the tithe
throughout — but nothing was ever recorded as learned.

The cause is a gap between how the eval poses the question and how play does. `lore-learned`
has Hald say *"There's an old faith out in the fen — the Blind, folk call them"*: a topic named
and taught. It scores 14/14. **Real play never names the topic.** Characters speak of the
Drowned Father and the capstone and the debt; nobody says "the Cult of the Blind" as a thing to
be told about.

So extraction only recognises learning when the prose hands it a labelled topic, and the eval
was built on the one shape that does. The lore was absorbed into the world's texture instead —
which is *good narration* and invisible bookkeeping.

Worse, the content leaked back into facts anyway: `capstone-sealed-shrine`,
`tithe-drowned-men` and `stone-is-a-lock` all paraphrase what the entry already says. The
redundant-facts gap, confirmed in play.

**This is the most important finding of the session**, because it is a measurement failure
rather than a code failure: a scenario scoring 14/14 on a behaviour that never occurs.

### 2 and 3. The witch, and both are the missing `Item`

The player reported the witch "forgetting" a promise, and a stone she gave getting "mixed up"
with the capstone.

Reading the turns: **there was only ever one stone.** Turn 42, Morwenna says *"Grind that to
powder"* — pointing at the capstone the player already had. The confusion is real and it is
not a memory failure. **Nothing in canon tracks objects at all.** No item has an id, an owner,
or a location, so "which stone is this" is unanswerable from the world state, and the prose is
the only thread holding it together. An oilcloth bundle appears at turn 49 with no recorded
connection to anything.

The forgotten promise is the same shape from a different angle. `morwenna-can-bind-well` is a
fact — she *might* be able to. That she *agreed to* is not modelled, so nothing tracks an
outstanding commitment. Already logged as "agreements as commitments"; play confirms it matters
enough to be felt as a character flaw.

**Both are `Item`, which is now the best-evidenced gap in the domain model twice over** — a
7/7 eval failure and a player noticing it unprompted in play.

### 4. The player had no protected identity, and the story took it

Reported as "we need a character sheet". The audit found something worse than a missing
feature.

On turn 38 the extractor emitted `character_renamed` **on the player**, replacing the name
`"You"` with the literal string `"player"` — the id — and wiping `"A traveller, recently
arrived in Marrow"` with `"burned, with blistered and stained hand from contact with the black
water"`.

Two destructive halves. The name became a database key. The description became a passing
injury, which was *already correctly recorded on `status`* by the very fix shipped the day
before — so the same event was written twice, once into the field built for it and once over
the player's identity.

**Fixed immediately**, with three rules and six self-tests:

- the story cannot rename the player;
- the player still can, through `/rename`;
- a name equal to the id is refused, since that is the model echoing the key back rather than
  writing a name.

The second rule needed a decision. `/rename` routes through the same validator, so blocking the
player would have blocked the player *from themselves*. Routing authoring around the validator
was rejected — it would give the world a second way to change, which is how two paths start
disagreeing about ids and collisions. Instead `Validate` gained an `authored` flag: **one gate,
and it knows who is knocking.**

The save was repaired in place; the burn stays on `status`.

## What this says about the character sheet

The request was for a player description the model can work with. The audit shows the deeper
problem: **the player's identity was a mutable field with no owner.** Any turn could overwrite
it, and one did.

Protecting it is now done. Giving it structure — a name, an appearance, abilities the narrator
can use — is a separate and reasonable feature, and it wants the same treatment lore got: what
is authored, what is derived, and who is allowed to change it.

## Next

1. **Lore learning in the implicit case.** A scenario where the topic is discussed without being
   named, which is what play produces. The current 14/14 measures a shape that does not occur.
2. **`Item`.** Two independent lines of evidence now.
3. **`source` on facts.** Four sightings; still four unattributed contradictory claims in this
   session.
4. **The player sheet**, as a design pass.
5. **The reconciliation pass**, for `relationship_changed` — zero in 102 turns is conclusive.
