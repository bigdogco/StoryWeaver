# 2026-08-12 — three fixes from a human session

The first play session against sheets, kebab ids and `Location.Status`. Fifty-one turns. It
validated two features and found three failures, all now fixed and measured.

TODO: [`TODO_PLAY_51_FIXES.md`](../todo/TODO_PLAY_51_FIXES.md). Session: `saves/marrow/`.

---

## What the session validated

**`Location.Status`, shipped hours earlier, works.** Five of seven locations carry one, and
**zero facts in the session are misfiled location state** — the category that was six of nine
last time.

**Character sheets hold up over fifty turns.** An authored companion followed the player the
whole way and her narrated voice stays recognisably her sheet's `Manner`.

**Fact quality 14/16**, against a 55% baseline. Both problems are near-duplicates, which is the
deduplication gap already logged as out of scope.

**Seven rejections in 136 applied deltas — and all seven were one bug.**

## 1. An object could not become a person

`tarp-covered-shape` was introduced as an item on turn 12, correctly — it was a shape under a
tarp. Then it proved to be a living man, and extraction tried four times to say so:

```
t13  status_changed   "breathing, with a hand moving"       REJECTED
t15  status_changed   "sitting up, black sludge weeping"    REJECTED
t16  fact_established sourceId: tarp-covered-shape          REJECTED
t18  status_changed   "burning, thrashing in agony"         REJECTED
```

The t16 rejection took three `fact_learned` with it. The man's revelation — *"the weeping
silver was given by the deep mud to keep the debt"* — **never entered canon**. It happened in
the prose and the world does not know it.

The model was right every time and the schema had no way to let it be right.

`ItemRevealedAsCharacter` keeps the id and moves the entity between namespaces, which works
because ids are already unique across all of them. **6/15 → 15/15, rejects 1.80 → 0.00.**

**The load-bearing detail is the tier.** It sits in tier 1 beside `CharacterIntroduced`,
because the turn a thing proves to be a person is the turn it speaks, and the fact quoting it
is judged in tier 2. In the default tier this delta would have reproduced the exact failure it
exists to fix. There is a self-test that emits the fact *before* the promotion, so emission
order cannot silently start mattering.

**The scenario needed tightening before it was honest.** The first version scored 3/5 by a
route that is not actually correct — introduce a new character, leave the item lying there,
which puts a man and a shape-under-a-tarp in the same room, both real. Adding "the shape is no
longer an item" took it to the 0/5 it deserved.

## 2. Movement recorded from an intention

Turn 21: the player said "we need another place to hide", the companion proposed the salt-house
and turned toward it, and extraction moved everyone there. Turn 22, where the player actually
walks there, produced **no deltas at all** — canon already had them inside.

Prompt-level, and **the wording had to avoid undoing the rule beside it**. `two-stage-entry`
exists because reporting only the first hop of a real journey leaves the player behind; "report
where they finish" is correct and is not the problem. A turn where nobody sets off has no
finish to report.

**forbidden 5/5 → 0**, with `player-arrival` 10/10 and `two-stage-entry` 8/10 after, both
unchanged.

Reproduced in a different costume than play: the scenario's model kept the *player* put
correctly and moved the companion instead. Same error, other victim — which is a reminder that
a reproduction confirms the class, not the instance.

## 3. Two identical objects became one

Turn 40: the player lifts a second medallion off a shrine, twenty turns after taking the first
off a body. Extraction emitted `item_renamed` on the **first** one, described as "an exact
match" for the new one.

**5/10 → 10/10, forbidden 0** — but only on the fourth attempt, and the three failures are the
useful part:

| version | second-identical | object-examined |
|---|---|---|
| v1 "match by where it came from…" | 6/10, **forbidden 4** | 10/10 |
| v2 + "leave the known item alone" | 10/10 | 7/10 |
| v3 scoped "not the thing being handled" | 10/10 | 6/10 |
| **v4, describes only the new object** | **10/10** | **9/10, then 10/10** |

**Every version that told the model what not to do to the *known* item broke
`object-examined`** — the legitimate case of revising a known item's description after looking
closely. v1 was worse than no rule at all: it pushed the model from a harmless no-op into
actively rewriting.

The rule that works names the new object, gives an example id, and says nothing whatever about
the old one. **A prohibition aimed at one case leaks into every case that resembles it.**

## A tell, noted and not acted on

The `item_moved` into the player's hand on turn 40 was a **no-op** — that medallion was already
held. *A pickup that changes nothing means the wrong id was chosen*, and the validator can see
that.

Not built. The validator could only reject, and rejecting loses the pickup entirely; it cannot
invent the right delta. Worth keeping in mind as a detector if this recurs.

## Found while working: `movement` is failing, and not because of today

`movement` — the plainest scenario in the scored set — came back **1/5**.

Checked rather than assumed, because the item-2 prompt edit is exactly the kind of change that
could cause it. **HEAD in a worktree, same provider, same n: 0/3 with two timeouts.** The
current build is if anything marginally better.

Left unfixed deliberately. It needs its own reproduction and its own before/after; folding it
into three other fixes is how a fourth failure gets attributed to the wrong cause.

## Verified

- `dotnet build` clean, `--selftest` all four suites pass, including the new promotion test
- Every scenario measured before and after, provider pinned, error count read before the score
- No collateral damage on `wrong-object-acted-on`, `object-examined`, `object-described`,
  `two-objects`, `player-arrival`, `two-stage-entry`

## Still owed

- **The full scored set has no clean run**, carried over from `Location.Status` and now doubly
  owed. Blocked on a provider not timing out on a third of calls
- **`movement`**, above
- Nobody has played any of this
