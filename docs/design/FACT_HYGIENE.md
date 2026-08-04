# Design — what a fact is for

**Status:** design, no code. Written 2026-07-24, from an audit of the 51-turn save.

The fact store has absorbed something it should not four separate times — a name reveal, a
lie, combat blow-by-blow, and paraphrases of lore. The working theory was that facts need a
truth value and an attribution.

**The audit says that theory is mostly wrong.** Truth and attribution matter, but they are the
fifth-largest problem, not the first.

---

## 1. The audit

Every fact in `saves/marrow/canon.json` after 51 turns of real play, classified by what it
actually is:

| count | what it really is | where it belongs |
|---:|---|---|
| 12 | a momentary event, over and done | nowhere — history already has it |
| 11 | a description | on the entity |
| **10** | **a durable world truth** | **a `Fact` — correct** |
| 7 | reference material about a topic | a lore entry |
| 5 | a claim or a lie | a fact *with a speaker* |
| 4 | who-knows-what | `Character.Knows`, which already models it |
| 2 | a name | `CharacterRenamed`, which now exists |
| 2 | an agreement | probably a fact; see §5 |
| 53 | | |

**Fewer than one fact in five is a fact.** That is the finding, and it is much bigger than the
one this document was opened to address.

## 2. Why the prompt's own test does not catch it

The extraction prompt defines a fact well, and the definition is not the problem:

> A fact is a durable truth about the world. The test: **would it still be true if nobody had
> ever mentioned it?**

That test correctly excludes questions, deflections, greetings, moods — the things it was
written against, which is why *those* categories are absent from the audit. It works.

It does not exclude the two largest categories, because they pass it:

- *"The drowned follower was struck deeply in the torso"* — would still be true if nobody had
  mentioned it. **Passes.** Useless: the creature died two turns later.
- *"The altar is a massive sculpture of pale, waterlogged wood"* — would still be true if
  nobody had mentioned it. **Passes.** It is the altar's description.

The test is necessary and not sufficient. It filters conversation out of canon and does nothing
about *durable-but-misplaced*.

## 3. The missing test

What actually distinguishes the 10 correct facts is not durability. It is this:

> **A fact is something one character can know and another not know.**

Facts exist to hang off `Character.Knows`. That is their entire purpose — per-character
knowledge is the premise the whole project is built on. So the test that matters is whether
knowledge of it can meaningfully differ between two people.

Run it against the audit:

- *"The well was boarded after something was found in it"* — Hald knows, the player does not.
  **A fact.**
- *"The altar is huge and made of pale wood"* — anyone standing there sees it; nobody can
  usefully *not* know it while present. **A description.**
- *"The follower was struck in the torso"* — nobody needs to know this, then or later.
  **An event.**
- *"Hald claims the roof is leaking"* — the player knows Hald said it; whether it is *true* is
  a separate question. **A fact, and the case for attribution.**

This is a better test, it is short enough for a prompt, and it explains all 53 classifications
rather than 40 of them.

## 4. What each category needs

### 4.1 Descriptions (11) — the largest fixable category

The prose describes a place or a person and extraction has nowhere to put it, because
`LocationIntroduced` and `CharacterIntroduced` set a description **once, at introduction**, and
nothing can revise it afterwards.

That is the same gap the rename work just closed for names. A description is discovered
gradually — you enter a room and see more of it on the second visit — and canon cannot
represent that.

**Proposal: `location_described` and `character_described`**, or one `entity_described`
carrying an id and replacement text. Cheap, and it drains the largest misfiled category into
the right place. Note `CharacterRenamed` already carries an optional description, so the
character half is half-built.

Open question: replace or append? Replacing loses detail; appending grows without bound. A
described-again delta that hands the model the current text and asks for a revision is a third
option and the most expensive.

### 4.2 Events (12) — probably nothing

An event is already recorded. `history.jsonl` holds every turn, and the narration window
replays the recent ones. An event that has lasting consequence *becomes* something else: the
follower being struck matters as `status_changed -> wounded`, and that delta fired correctly
every time.

**Proposal: prompt only.** Say that a completed action is not a fact unless someone could
later act on knowing it. Measure whether that alone drains the category — it is the cheapest
possible intervention and the audit suggests it should work, since the model is clearly
applying *a* rule, just not this one.

### 4.3 Claims and lies (5) — the original question

This is where truth value and attribution belong, and the model is already improvising a
workaround: it wrote "claims" into both the id and the text of `hald-claims-roof-leaking` and
did not do so for the same character's other lie, which is now simply false canon.

**Proposal: a `source` on the fact** — who asserted it, or null for narrated world truth.
Not a boolean truth flag. A boolean asks the extractor to adjudicate honesty, which it cannot
do and should not try; a speaker is an observable. "Hald said X" is checkable from the prose.
Whether X is true is a thing the *story* resolves later, and the player is the right arbiter.

This also composes with per-character knowledge in a way a truth flag does not: a character
believing something false is a feature, and the interesting model is not "this fact is false"
but "these three people believe it and this one knows better."

**Fourth sighting, and the sharpest, from live play 2026-07-24 (fresh save, turn 6).** The
player asks where the thing from the well was taken. Two characters answer differently in the
same turn:

```
fact  blocks-taken-to-quarry: The heavy thing pulled from the well was taken to the old quarry.
fact  blocks-taken-to-bog:    The heavy thing pulled from the well was taken to the deep bog.
hald   learned quarry     (his own claim)
mabb   learned bog        (his own claim)
player learned both       (heard both)
```

**Canon now asserts both as settled world truth**, with no record that they are rival claims —
Hald's cover story ("carted to the quarry, it's a death trap") and Mabb's drunk contradiction
("ain't no quarry — gave it back to the water"). They cannot both be true and the flat fact
model cannot say which, or that either is contested.

Two things this establishes that the earlier sightings did not:

- **The knowledge graph is already perfect.** Hald knows only his claim, Mabb only his, the
  player both. Per-character knowledge did exactly the right thing with no help. So the missing
  piece is *only* the source, not the belief tracking — a `source` field slots into a model
  that is otherwise already correct here.
- **Two contradictory claims in one turn cannot be represented without corrupting canon.** The
  single-lie sightings degraded slowly; this one is immediate. With `source`, the turn is
  clean and the contradiction becomes the *content*: quarry (says Hald), bog (says Mabb), truth
  unresolved — which is precisely the mystery the scene is dangling.

**Downstream consequence to watch for in this same session:** both facts replay to the narrator
as true, so Hald or Mabb may later state the other's version as settled. If that happens it is
the corruption made visible, not a new bug.

### 4.4 Who-knows-what (4) — prompt only

`hald-knows-cult-of-the-blind` is a fact asserting a knowledge relationship that
`Character.Knows` already models — and, ironically, `fact_learned` had already recorded it
correctly on the same turn. The fact is a duplicate of a delta that fired.

**Proposal: one prompt line.** "Never write a fact about who knows something; emit
`fact_learned`."

### 4.5 Lore (7) — already fixed, partially

Seven facts are reference material that would now be lore entries; two of them the *player*
authored through `/fact` before lore existed. The category should shrink on its own.

The residue is the known open gap: the model still establishes paraphrases of an entry it has
just correctly emitted `fact_learned` for.

## 5. Deliberately unresolved

**Agreements** (`hald-agrees-to-guide`, `nessa-agrees-to-accompany`). Durable, knowledge-worthy,
and they pass the §3 test — but they are really *commitments*, which are the kind of thing that
gets broken, and canon has no way to record a promise being broken. Left as facts for now; a
world that models obligations would want more.

**Deduplication.** Three facts describe the cult's location, two describe Shurus preserving
followers, and two describe the same creature being wounded. The extractor has no view of
semantic overlap and the validator only catches exact id collisions. Out of scope here, and
worth its own thought — it is the failure mode that makes a fact store degrade slowly rather
than visibly.

## 6. Sequencing

Cheapest and most measurable first, since three of five categories are prompt-only:

1. **The §3 test in the extraction prompt**, replacing nothing — added alongside the existing
   durability test. Measure against the four categories it should drain.
2. **Prompt lines for events and who-knows-what.** Same pass, same measurement.
3. **`entity_described`**, if §4.1's open question resolves cleanly. This is the only new delta
   kind proposed, and it drains the largest fixable category.
4. **`source` on facts.** The original request, and genuinely fifth in line.

Each step is measurable against the audit's own categories, which is the point of having done
the audit before writing any prompt text. Every prompt rule written this year without a
control was wrong.

## 7. Decisions — settled 2026-07-24

1. **The §3 knowledge-worthiness test is right**, and is added *alongside* the durability test
   rather than replacing it. Durability is doing real work — no question, greeting or mood
   appears anywhere in the 53 audited facts — and dropping it would reopen a closed category.
2. **`character_described` and `location_described`**, two deltas, matching the existing
   `character_*` / `location_*` split and letting the validator give a precise error.
3. **Replace, for now.** The delta carries the full new text and overwrites. The model already
   sees the current description in context, so it can rewrite rather than truncate.
4. **Source only on facts.** Who asserted it, or null for narrated world truth.

### 7.1 Replace, and the revision option left open

Replacing is the cheap choice and it has a real failure mode: a careless model drops detail it
should have kept, and nothing recovers it — the old text is gone.

**Logged as a future option rather than a fix:** a revision pass that hands a model the old
description alongside the new prose and asks for a merged one. Strictly better output for an
extra call per description change, which is a trade worth *offering* rather than imposing —
some worlds are description-heavy, and some players will not want to pay per turn for prose
polish.

It should therefore arrive as a setting (`story.describeMode: replace | revise`) rather than a
migration. That is also the honest way to find out whether replacement loses anything: run both
and compare descriptions after fifty turns.

Two things make this cheap to defer. **The delta shape does not change** — a revision pass
emits the same `*_described` delta, it just computes better text. And the failure is visible in
canon rather than silent: if replacement is degrading descriptions, `/state` shows it.
