# Devlog — a door the extraction rule does not apply to

**Date:** 2026-07-21
**Scope:** `/place`, `/character`, `/fact`; and where an organisation is supposed to live

---

## The gap

Extraction never records a merely *mentioned* entity. Measured: `player-place` 0/7,
`player-absent-character` 0/7, `narrator-mention` 0/7. Say "I came from Astaria" and Astaria
does not exist. The dividing line is **presence, not authorship** — walk somewhere new and it
is recorded (`player-arrival` 14/14), name it and it is not, and the narrator naming a place in
passing fares no better than the player doing it.

**That behaviour is correct and was not changed.** A character *saying* something is not the
same as it being true; players lie, misremember and boast, and NPC speech in particular must
not become world truth. Six models refused the original `player-claim` rule near-unanimously,
which is why that scenario is retired rather than fixed.

The answer is not to weaken the rule but to give **the world's author** a door it does not
apply to.

Urgency came from an interaction with the narration window: say Astaria on turn 3 and the
narrator uses it correctly for ten turns — because it is sitting in the message window, not
because anything recorded it. Then it slides out and Astaria ceases to exist, and the narrator
can contradict the player's own backstory around turn 14. It looks like it works right up until
it doesn't.

## What was built

Prompted rather than positional — it is easier to get right and invites better descriptions,
which is the actual point of authoring.

- `/place` — name, id, description
- `/character` — name, id, description, location (**blank = offstage**)
- `/fact` — text, id, and *"does your character know this?"*

Three decisions worth recording:

**Offstage by default.** `Character.LocationId` is nullable precisely for this. A brother back
home exists without being anywhere; he gets placed when he actually turns up.

**Knowing is asked, not assumed.** Establishing a fact says nothing about who knows it — that
separation is the whole knowledge model — and an author may well write down a truth their own
character has not discovered. "There is a second body still in the well" is a good thing to
author and a bad thing to already know.

**No `TurnRecord` appended.** The turn log feeds the narrator's prose memory window, and an
authoring action has no prose. The options were to fabricate a narration line or to exclude
authoring records with a new field on `TurnRecord`; both were unnecessary, because **canon
already carries the change** — once Astaria is a `Location`, `ContextAssembler.ForNarration`
shows it to the narrator every turn regardless. A proper UI will log authoring separately.

Everything routes through `DeltaValidator` + `DeltaApplier` + save rather than writing to canon
directly. Less code, and more importantly one path: id uniqueness, cross-namespace collisions
and the atomic write are already solved there, and a second writer is how two paths start
disagreeing about what was persisted.

## Tested in an isolated save directory

`saves/` resolves against the working directory, so the test ran from a temp folder rather than
the repo root — otherwise it would have written into a real play session. Covered: apostrophes,
id collision and re-prompt, offstage character, cancel, and the saved JSON.

One real bug found: `The King's Investigators` slugged to `the-king-s-investigators`, the
apostrophe having been treated as a word separator. Apostrophes sit *inside* words and fantasy
names are full of them, so they are now dropped rather than split on.

## Where an organisation lives — the question this raised

`/fact` prompted a good question: what *is* a fact, and where does "the King's Investigators
exist" go?

A fact is a **proposition** — one sentence, true or not, that each character separately knows
or does not. "Bill stole the grain." An organisation is not a proposition, and the domain model
has exactly three concepts, none of which fit.

Splitting the question was what made it tractable:

- **"People know about it"** — already solved. `Fact` + `Character.Knows` handles it exactly.
- **"It exists as a thing"** — nowhere to put it.

The first shape considered was a `Faction` with a standing toward the player. The user's own
framing was better: *a lore item, like something you would find in a lorebook for DnD*. That is
reference material, not a simulated actor, and it sidesteps generalising
`RelationshipToPlayer` beyond `Character` entirely.

Logged as a **fourth entity type**, with per-character knowledge decided, and with the catch
recorded: `ContextAssembler` currently dumps the whole world into every prompt, so lore is the
point at which keyword-triggered retrieval stops being optional.

This is the third domain gap play has surfaced, after items/inventory and buildings-as-locations
— which is section 5 working as designed: *"deliberately small, expand only when a turn actually
needs it."*

## Note

`/place`, `/character` and `/fact` were agreed as a definite feature earlier in the day and then
dropped when the provider investigation took over — never built, and never written down either.
Caught only because the user asked. Worth a note: an agreement made mid-conversation is not a
record.
