# Devlog — lore entries, and the assumption that was wrong

**Date:** 2026-07-24
**Scope:** the fourth entity type, the first world pack, and a design saved by measuring first

---

## What shipped

`LoreEntry` and `LoreBook` in Core, a strict markdown reader in Storage, a lore block in both
context renderings, one validator rule, `/lore` and `/knows` in the harness, ten self-test
checks, and two eval scenarios. The first pack lives at `worlds/marrow/lore/` with two entries.

The design decisions were settled in `docs/design/LORE_ENTRIES.md` before any code, and only
one of them turned out to be wrong.

## The assumption that was wrong

The cheapest decision in the design was that lore ids and fact ids share one namespace, so
**learning a lore entry needs no new delta kind** — the extractor emits the existing
`fact_learned` against a lore id. That saving was the reason the design looked small.

It was also unmeasured, and the task list said so: *"measure before assuming — being wrong
here costs a `lore_learned` delta kind."*

`lore-learned` scored **0/14**.

Hald explains the cult to a player who has never heard of it. Every run, the model shredded
the speech into new facts and taught the player those instead:

```
fact_established  shurus-drowned-father: The weeping woman sign is Shurus, the Drowned Father.
fact_learned      player <- shurus-drowned-father
fact_established  blind-faith-fen: There is an old faith in the fen called the Blind...
fact_learned      player <- blind-faith-fen
```

Not unreasonable — it is exactly what the fact prompt asks for. But the player ends the turn
still not having heard of the entry that already says all of this, and the fact store gains two
paraphrases of authored content.

**The tell that this was a prompt gap rather than a design flaw** was in the same output: three
runs emitted `fact_learned innkeeper-hald <- cult-of-the-blind`. The model *can* reference a
lore id and will do it unprompted for the speaker. It simply never considered it for the
listener, because nothing in the extraction prompt mentioned lore at all.

One rule:

> The world lore list holds authored topics. When someone is told about a topic that is already
> listed there — the order, the cult, the war — emit fact_learned for the listener against THAT
> topic's id. Do not establish new facts restating what the topic already covers, and never
> establish the topic itself.

**0/14 → 14/14.** The shared namespace holds and no `lore_learned` delta is needed.

## The other half

`lore-not-established` checks the opposite: the player produces a King's Investigator's seal in
a room where the order is known lore. The tempting wrong answer is to establish the topic as a
fact — the §9 behaviour of routing everything unrepresentable through `fact_established`.

**Forbidden 0.00 across 7 runs.** The model never tried it. Worth noting the validator would
have rejected it anyway, which is why forbidden rules are scored on raw output: this measures
the model respecting the boundary, not the net catching it.

An unrequested result in the same runs, every time:

```
mood_changed   innkeeper-hald = resigned
fact_learned   innkeeper-hald <- kings-investigators
```

Hald sees the seal, so Hald now knows the order is real and standing in his tavern. Nothing
asked for that. It is the feature working exactly as intended.

## What is still wrong

The model **still establishes redundant facts** alongside the correct `fact_learned` —
`shurus-drowned-father` and `blind-faith-fen` restate what the entry already says, despite the
rule telling it not to. Required passes; the tidiness does not.

This is the §9 finding again in a new costume: the fact store accumulating what belongs
elsewhere. The rule bought the behaviour that matters and not the one that keeps canon clean.
Left as a known gap rather than chased with a second rule, because the first four eval
scenarios written without a control this year were all wrong, and "add another sentence and
see" is how that starts.

## Common knowledge

Raised while reviewing `/knows`: most lore is the kind everybody knows. The kingdom they live
in, its king, the war that ended last spring. Seeding that per character does not scale past
three NPCs, and without it the feature deadlocks — the lore exists, nobody has heard of it, so
no NPC can raise the subject, so the scene that would establish it cannot happen.

`common: true` on an entry. Two decisions inside it were worth getting right.

**It is a second flag, not a reuse of `always`.** They are different axes: `always` answers
"is this in context at all" and is about retrieval; `common` answers "who may refer to it" and
is about knowledge. A kingdom is both. A secret cult may well be `always` — the narrator needs
it for tone — and must not be `common`, because who knows about it is the plot. One field
could not express that.

**It is derived, never written to `Character.Knows`.** The obvious implementation is a pass on
load that copies common ids into everyone's knowledge. That copies pack content into save
state, and the two can then disagree: set the flag false a month later and every save keeps
entries indistinguishable from things a character genuinely learned in play. Resolving it at
read time instead keeps canon meaning "what this character learned", leaves the pack
authoritative for "what everyone knows", and is less code. Two self-tests hold the line —
one that common lore reaches an untold character, one that reading it does not write anything.

## The provider trap, fourth instance

Adding the common entry took `lore-learned` from 14/14 to **8/14**, which reads exactly like
the new entry crowding the context. It is not:

| | required |
|---|---|
| routed (Venice ×8, Baidu ×5, SiliconFlow ×1) | 8/14 |
| pinned to Baidu | **14/14** |
| pinned to Venice | 10/14 |

The 14/14 measurement had gone entirely to Baidu. A provider new to the mix — Venice — is the
whole difference, on identical input. Same confound as AtlasCloud, as the world-size
hypothesis, and as the name-reveal work three days ago.

Four times now the by-provider table has been the only thing standing between a routing
artefact and a written-up finding about our own code. It is worth stating plainly: **on this
setup, no single sweep is evidence about a change unless the provider mix is held fixed.**

## Notes from the build

- **`Entity.Name` was already mutable and `DeltaValidator.Taken` already enforced global id
  uniqueness.** Two earlier decisions, made for unrelated reasons, are what let lore join the
  id namespace in one line. This is the second time this week an old decision has quietly paid
  for a new feature — the other being `ForNarration`/`ForExtraction` making stale ids harmless
  during the rename work.
- **The caching hazard did not apply.** `CHALLENGES.md` warns that injecting lore mid-prompt
  destroys the cacheable prefix. Narration already keeps volatile state in the last message, so
  lore joins that block and nothing above it moves.
- **Bodies for the narrator, titles for the extractor.** The extractor gets ids and titles only.
  Handing it several paragraphs of reference prose would invite exactly the invention the
  extraction prompt spends most of its length suppressing.
- **`StoryWeaverException` lives inside `LlmNarrator.cs`**, in the Llm project, so Storage
  cannot use it — the markdown reader throws `InvalidDataException` instead. Logged as a
  separate cleanup rather than restructured mid-task.
- **`/knows` was not in the plan.** A seeded world starts with nobody having heard of anything,
  which is correct and unusable: an author needs to say the innkeeper knows what the cult is
  without staging a scene for it. It emits `fact_learned` against a lore id — the same property
  the extractor uses.

## Results

```
lore-learned          0/14  ->  14/14 required, 0 forbidden   (one prompt rule, pinned)
lore-not-established        forbidden 0.00, 7/7 clean
full scored set, 9          100% required, forbidden 0.00, rejects 0.00
self-test                   10 delta checks + 13 lore checks, all passing
```

`revelation` scored a full 21/21 this sweep — the intermittent speaker-learns miss did not fire.
No regression from the new prompt rule.

## Next

Keyed retrieval and budgeting stay deferred until a world is large enough to need them, and the
cut reporting must be built with the budget rather than after it. Before that: the fact-store
pressure this feature just demonstrated again from a fourth direction.
