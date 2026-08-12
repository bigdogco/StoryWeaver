# TODO — three failures from the 51-turn play session

Found in a human session on 2026-08-12, the first against character sheets, kebab ids and
`Location.Status`. Session: `saves/marrow/` at turn 51.

**Reproduce first.** Every one of these gets a scenario that fails before anything is built.
The well case established that the lever is the *seed*, not the prose.

---

## What that session already validated

- [x] **Character sheets hold up in play.** Mona followed for 51 turns and her narrated voice
      is recognisably her sheet's `Manner`
- [x] **`Location.Status` works.** Five of seven locations carry one, and **zero** facts in the
      session are misfiled location state — that category was six of nine last time
- [x] **Fact quality 14/16**, against a 55% baseline. Both problems are near-duplicates
      (`hut-hiding-breathing-man` / `hut-occupant-hiding-man`, and `well-body-medallion` /
      `well-body-medallion-confirmed`), which is the deduplication gap already logged
- [x] 7 rejections in 136 applied deltas — and all seven trace to item 1 below

## 1. An item cannot become a character — and it cost canon

**The most expensive of the three, and the only one that silently lost information.**

`tarp-covered-shape` was introduced as an item on turn 12, correctly: it was a shape under a
tarp. It then turned out to be a living man. The extractor tried **four times** to treat it as
a character and was refused every time:

```
t13  status_changed   "breathing, with a hand moving"       REJECTED
t15  status_changed   "sitting up, black sludge weeping"    REJECTED
t16  fact_established sourceId: tarp-covered-shape          REJECTED
t18  status_changed   "burning, thrashing in agony"         REJECTED
```

The t16 rejection took three `fact_learned` down with it, so the man's revelation — *"the
weeping silver was given by the deep mud to keep the debt"* — **is not in canon**. It happened
in the prose and the world does not know it.

The model was right every time and the schema had no way to let it be right.

Same shape as `character_renamed` — a thing whose nature is revealed later — but across entity
types, and worse, because a rename is recoverable and this is not.

**Fixed. `object-proves-alive` 6/15 → 15/15, rejects 1.80 → 0.00, 5/5 runs clean.**

- [x] Scenario first — `object-proves-alive`, scored on the outcome, so it could not pass
      before the feature existed. The first draft scored 3/5 by a route that is not correct
      (introduce a new character, leave the item lying there); tightened with "the shape is no
      longer an item", which then missed 5/5 as it should
- [x] `ItemRevealedAsCharacter`, keeping the id
- [x] Placement at the item's location, or the holder's location if it was being carried
- [x] **Tier 1, with `CharacterIntroduced`** — the turn a thing proves to be a person is the
      turn it speaks, and the fact quoting it is judged in tier 2. The default tier would have
      rejected that fact and every `fact_learned` behind it, reproducing the exact failure the
      delta exists to fix
- [x] Prompt rule: promoted, not re-introduced
- [x] Self-test with the fact emitted *before* the promotion, so emission order cannot matter

## 2. Movement extracted from an intention

Turn 21. Input was *"We need another place to hide"* — no movement. Mona proposes the
salt-house and turns toward it, and the extractor emitted `location_introduced` +
`player_moved` + `character_moved`.

Turn 22, where the player actually walks there, produced **no deltas at all**: already there.

Not the two-stage-entry bug, which is multi-hop and correct. This is the inverse — arrival
recorded from someone starting to move.

**Fixed. `move-proposed` forbidden 5/5 → 0.**

- [x] Scenario `move-proposed`. It reproduced in a different costume than play: the model kept
      the *player* put correctly and moved the companion instead. Same error, other victim
- [x] Prompt rule, worded not to undo the two-stage-entry rule it sits beside — that one exists
      because reporting only the first hop of a real journey leaves the player behind
- [x] `player-arrival` 10/10 and `two-stage-entry` 8/10 after, both unchanged

## 3. Two identical objects merged into one

Turn 40. The player lifts a second medallion off a shrine, twenty turns after taking the first
from the bloated man. Instead of `item_introduced`, the extractor emitted **`item_renamed` on
the first medallion**, with a description ending *"An exact match for…"*. It noticed they were
identical and collapsed them.

One item now exists where two were picked up, and the first one's description is overwritten.

**There is a detectable tell:** the `item_moved` into the player's hand that turn was a
**no-op**, because that medallion was already held. *A pickup that no-ops means the wrong id
was chosen.*

**Fixed, after three wrong prompt attempts. `second-identical-object` 5/10 → 10/10,
forbidden 0.**

- [x] Scenario `second-identical-object`, with the twin *remembered* rather than in the same
      paragraph — that distance is what makes the merge tempting, and is what distinguishes it
      from `two-objects`, which scores clean
- [x] Prompt rule, arrived at by measuring four versions:

      | version | second-identical | object-examined |
      |---|---|---|
      | v1 "match by where it came from…" | 6/10, **forbidden 4** | 10/10 |
      | v2 + "leave the known item alone" | 10/10 | 7/10 |
      | v3 scoped "not the thing being handled" | 10/10 | 6/10 |
      | **v4, only describes the new object** | **10/10** | **9/10, then 10/10** |

      **Every version that told the model what *not* to do to the known item broke
      `object-examined`**, which is the legitimate case of revising a known item's description
      after looking closely. The rule that works says only what the new object is and gives an
      example id, and says nothing about the old one
- [x] **The no-op-pickup tell — measured 2026-08-12, and it is too noisy to use.** All six
      saves swept: **7 no-op `item_moved` across 250+ turns, and exactly one was this bug.**
      The other six are the model harmlessly restating that a knife is still in Behn's hand or
      a coin still with Hald. **One in seven precision** — surfacing it would cry wolf six
      times out of seven, and the underlying merge is now fixed at 10/10 anyway
- [x] Its mirror, deduplication, was measured the same day and also declined — see
      `TODO_FACT_HYGIENE.md`. Seven of ten high-similarity fact pairs must stay separate

## Verify

- [x] `dotnet build` clean, self-tests pass
- [x] Each scenario measured before and after, provider pinned, error count checked first

## Found while working: `movement` is not failing — DeepInfra is

`movement` scored **1/5** and was about to be fixed with a prompt rule. It is not broken.

| build | provider | movement |
|---|---|---|
| current | DeepInfra | 0–1 of 5, 4–6 timeouts per 8 |
| `98896fb`, pre-`Location.Status` | DeepInfra | 1/2, 6 of 8 timed out |
| current | **StreamLake** | **8/8** |
| HEAD, without the prompt rule | **StreamLake** | **8/8** |

- [x] Reproduced, diagnosed, and **the fix reverted** — a rule written against mood-padding
      was deleted once HEAD scored 8/8 without it on a healthy provider. Committing an
      unmeasured prompt change to compensate for one sick upstream is the failure that was
      one step away
- [x] **Full scored set, StreamLake: 50/50 clean, required 100%, forbidden 0.00, rejects
      0.00.** The regression run owed since `Location.Status`. Today's four changes cost
      nothing
- [x] Sixth sighting of the provider hazard, logged in `CHALLENGES.md` — the worst so far,
      because the symptom was 0% versus 100% rather than quality drift

### A retraction, and something it reopens

`hostility` missing its standing rule 5/5 was explained here as long-standing, "consistent with
`relationship_changed` never having fired in 102 turns of play". **It scores 10/10 on
StreamLake.** That explanation was invented for an infrastructure symptom.

- [x] **Re-checked 2026-08-12: the claim survives, with better evidence.** Every saved session
      swept — **one firing in 253 turns across five sessions**, against the "zero in 102" the
      design cited. The capability is not the constraint: `hostility` scores 10/10 on a healthy
      provider. What does not occur is the trigger. Sharpest evidence is inside one session:
      after 51 turns every standing sat at its seeded value while mood moved constantly over
      the same prose. Written up in §3 of the character-sheets design

## Next

- [x] `providerIgnore: ["AtlasCloud", "DeepInfra"]` on the extraction role. An exclude list,
      so routing keeps every other upstream and no single host becomes a point of failure.
      The note beside it records the numbers and ends "re-test before removing — this is
      degradation, not a permanent property", which is the difference between this exclusion
      and AtlasCloud's, which is a capability judgement
- [x] **`TurnRecord.ExtractionProvider`**, set at all three construction sites (turn, retry,
      reroll) and shown on the turn header as `--- turn 12 · StreamLake ---`. Self-tested
      through `JsonWorldRepository` rather than a serializer, so it exercises the options the
      game really writes with — and the test's second half is that a turn saved *before* the
      field existed still loads, as provider-unknown

## Still open

- [x] **Re-checked — the claim survives.** See item above and §3 of the character-sheets
      design. My retraction cast doubt on the design conclusion; the doubt was misplaced, and
      only the *link* I drew between the eval failure and the play finding was wrong. They
      have different causes: one was a sick provider, the other is structural
- [ ] Narration's provider is still unrecorded. It needs `INarrator` to return more than a
      string, and prose has no score to attribute to anyone yet. Worth doing the day a
      narration eval exists, not before
- [ ] The 51-turn save, and every turn before today, is permanently provider-unknown
