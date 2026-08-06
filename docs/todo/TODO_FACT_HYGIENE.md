# TODO — fact hygiene

Item #1 off the post-lore list. Opened as "facts need truth value and attribution"; the audit
found that is the fifth-largest problem, not the first.

Design: [`docs/design/FACT_HYGIENE.md`](../design/FACT_HYGIENE.md).

---

## Audit

- [x] Classify all 53 facts in the 51-turn save by what they actually are
- [x] Find why the extraction prompt's own test does not catch the two largest categories
- [x] Propose a test that explains all 53 rather than 40

**Result: fewer than one fact in five is a fact.** 12 momentary events, 11 descriptions,
10 correct, 7 lore, 5 claims, 4 who-knows-what, 2 names, 2 agreements.

## Decisions — settled 2026-07-24

- [x] Is "a fact is something one character can know and another not know" the right test? —
      yes, alongside the durability test rather than replacing it
- [x] `entity_described`: **replace**, with a revision mode logged as a future option
- [x] Source on facts, truth value, or both? — **source only**, and now built
- [x] One `entity_described` delta or two? — **two**, matching the `character_*` / `location_*`
      split

## Measurement — done first, and it changed the plan

Devlog: [`2026-07-24_fact-hygiene-measurement.md`](../devlog/2026-07-24_fact-hygiene-measurement.md).

- [x] Eval: description-shaped prose does not produce a fact — **forbidden 0.00, small and
      large world.** Would not fail
- [x] Eval: a completed action does not produce a fact — **forbidden 0.00.** Would not fail
- [x] Eval: a revelation does not produce a who-knows-what fact — **7/7 required, forbidden
      0.00.** Would not fail
- [x] Work out why: **8 of the 11 description-facts describe something with no entity at all.**
      The scenarios described a *room*, which has a `Location` to hold its description
- [x] Rewrite the scenarios in the shape that actually fails

**Result: the design was aimed one level too shallow.** "Descriptions land in facts" is mostly a
missing *entity type*, not a missing delta. `character_described` fixes 3 of 11, not the
largest category as claimed.

## What the reproductions found

- [x] `object-described` — **an item becomes a `character_introduced`, 7/7.** A knife standing
      in the tavern with a name and a location, because that is the only delta that can bring a
      thing into canon. Same shape as the AtlasCloud failure in `CHALLENGES.md`, reproduced on
      a good provider
- [x] `blow-landed` — **`status_changed` never fires, 0/7.** Hald is beaten unconscious and the
      model writes `mood_changed = injured`. **Mood is absorbing status.** Forbidden was 0.00,
      so the fact-store theory was wrong about combat too
- [x] `sub-space-described` — 1/7. Real in play, weakly provoked by this scenario
- [x] Third unprompted sighting of the attribution instinct: *"Mabb claims he found the ritual
      knife in the reeds"*

## Build — resequenced by evidence

- [x] **`status` vs `mood` in the extraction prompt** — **done 2026-07-24, 0/7 → 7/7.**
      The cause was an asymmetry rather than a missing rule: the prompt mentioned mood three
      times and status zero, and the mood schema branch actively recruits. Verified on the
      baseline's own provider; full set 99%, no regression. See
      [`2026-07-24_status-vs-mood.md`](../devlog/2026-07-24_status-vs-mood.md)
- [x] **`Item` as an entity type** — **done 2026-08-04.** Items-as-characters eliminated, and
      the false-canon merge fixed at 14/14. See `TODO_ITEMS.md`
- [x] **`source` on `FactEstablished`** — **done 2026-08-04, 14/14 first measurement.**
      Attribution rather than a truth value: a speaker is observable, honesty is not. Rival
      claims are kept apart by a duplicate key of `fact:{id}:{source}`.

      Unplanned bonus: the applier now derives that a fact's source knows it, which removed the
      intermittent speaker-learns miss that had scored 0–2/7 on every sweep since it was found.
      A prompt rule carrying a known weakness for weeks became unnecessary. See
      [`2026-08-04_fact-source.md`](../devlog/2026-08-04_fact-source.md)
- [ ] **`character_described` / `location_described`.** Still worth having, correctly sized at
      3 of 11

## Not building — no measured failure to fix

- [x] ~~Prompt line: a completed action is not a fact~~ — `event-not-fact` cannot reproduce it
- [x] ~~Prompt line: never write a fact about who knows something~~ — `knowledge-not-fact`
      scores clean

---

## The momentary-events residue, characterised 2026-08-04

The last misfiled category, and it is **not** a grab-bag. A 50-turn directed session produced
nine misfiled facts, and **six were one location's changing state**:

```
well-sound-changed  well-fluid  well-boards-straining
well-fluid-stopped  well-sound-churning  well-sound-faded
```

**Characters have `Status`. Items have `Status`. Locations do not.** A well that is filling,
straining, then falling silent has nowhere to record what it is *doing*, so the fact store takes
it. The remaining three are chapel descriptions, which `location_described` covers.

That also explains why `event-not-fact` scored clean: it has a *character* do something trivial,
where the real case is a *place* changing.

- [x] **`Location.Status` — reproduces. The blocker is gone.**

      Measured 2026-08-06, `deepseek/deepseek-v3.2` pinned to DeepInfra, n=7 each:

      | scenario | seed | forbidden | rejects |
      |---|---|---|---|
      | `place-changing` | base Marrow | **7/7** | 0.43 |
      | `place-changing-late` | `Marrow_WellSignificant` | **6/7** | 0.00 |

      Every run files the well's condition as facts — `well-fluid-stopped`,
      `well-sound-changed`, `bronze-provokes-shaft`. Exactly the shape the 50-turn session
      produced six of.

      **The premise of the block was wrong.** `place-changing` reproduces on the *base* seed,
      which the note below recorded as scoring 0.00 twice. Why it differs is not established —
      items, `source` and the tier fix all landed after that measurement, and the provider was
      not recorded — so this is a fresh result rather than a refutation of an old one. Worth
      remembering as a case where "cannot reproduce" was allowed to persist as a fact about the
      world rather than as a dated measurement.

      `Marrow_WellSignificant` is still worth having: the base seed makes the model invent or
      move items that do not exist (rejects 0.43, measuring the validator), where the loaded
      seed has real objects to act on and rejects nothing.

      **Ready to build, and the measurement to beat is forbidden 7/7 → 0.**

      - [ ] Add `Location.Status` and a `location_status_changed` delta
      - [ ] Score on the outcome — the well's condition ends up in status and not in facts,
            whichever route the model takes there. A rule naming a specific delta is the
            mistake made four times already
      - [ ] Re-run both scenarios pinned, plus the full scored set to check for regression

- [x] **`EstablishedTurn` was off by one** — found while tracing which turn produced those
      facts. Deltas were applied before `world.TurnNumber++`, so a fact accepted on turn 7
      recorded turn 6, while `LastSeenTurn` was set after the increment and was right. The two
      disagreed about when "now" was. Fixed and self-tested

- [ ] The knowledge-worthiness test — **decided, and deliberately not written yet.** It is a
      good test and there is currently no scenario that fails without it. Revisit if the
      category reappears in a fresh play session

## Still true, still logged

- [ ] Re-run the fact audit against a fresh **human** play session and compare the category
      split. This is the measurement that matters.

      A model-played session on the current build scored ~68% correct against the human 55%,
      and **that comparison is not valid** — conversation produces knowledge-worthy facts while
      action produces events, so the play style flatters the metric. See
      [`2026-08-04_llm-played-session.md`](../devlog/2026-08-04_llm-played-session.md)

## Out of scope, logged

- [ ] **Deduplication.** Three facts describe the cult's location, two describe Shurus
      preserving followers, two describe the same creature being wounded. The extractor sees
      no semantic overlap and the validator catches only exact id collisions. This is what
      makes a fact store degrade slowly rather than visibly
- [ ] **Agreements as commitments.** `hald-agrees-to-guide` is durable and knowledge-worthy,
      but a promise is the kind of thing that gets broken, and canon cannot record that
