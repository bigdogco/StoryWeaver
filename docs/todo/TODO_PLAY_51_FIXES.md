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
- [ ] The no-op-pickup tell is **not** acted on. A pickup that changes nothing means the wrong
      id was chosen, and the validator could see that — but it could only reject, not repair,
      and rejecting loses the pickup entirely. Logged, not built
- [ ] Still the mirror of the deduplication problem, and still out of scope

## Verify

- [x] `dotnet build` clean, self-tests pass
- [x] Each scenario measured before and after, provider pinned, error count checked first

## Found while working: `movement` is failing, and it is not from today

`movement` — the plainest scenario in the scored set, "the player walks to the square" — scored
**1/5**. It is in `All`, so this matters.

**Not caused by any of today's changes.** HEAD in a worktree, same provider, same n: **0/3**
with two timeouts. The current build is if anything marginally better. Checked because the
movement prompt rule for item 2 is exactly the kind of edit that could have caused it.

Left unfixed on purpose: it needs its own reproduction and its own before/after, and folding it
into three other fixes is how a fourth failure gets attributed to the wrong cause.

- [ ] **Reproduce and fix `movement`.** Suspect the same intent-vs-arrival boundary, from the
      other side — but that is a guess, and the seed is the lever, not the prose
- [ ] Full scored set re-run — **still owed from `Location.Status`**, and now doubly so.
      Blocked on a provider that is not timing out on a third of calls
