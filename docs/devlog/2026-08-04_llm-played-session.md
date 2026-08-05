# Devlog — a session played by a model

**Date:** 2026-08-04
**Scope:** auditing 50 turns played by ChatGPT rather than a person, and what that is good for

---

## Why

Play sessions cost an evening and are the scarce resource. The question was whether an LLM can
drive one when the player cannot, to keep measurement moving.

The answer is **yes for one half of what play tests, and no for the other**, and the split is
measurable rather than a matter of taste.

## What it validated — real evidence the day's work landed

**`source` works.** 15 of 28 facts carry a speaker, and the attribution is *correct*: Mabb's
rumours to Mabb, Hald's warnings to Hald, narrated events left unattributed.

```
t11  drinker-mabb    tomas-dragged-down    Tomas leaned over the well, and the water rose...
t21  innkeeper-hald  well-looks-back       If you look down into the well's black water, it will look back.
t35  -               well-water-dropped    The water in the well dropped a full foot...
```

**Items work.** The mooring ring has a life across five turns and three moves: introduced in the
square, picked up by the player, put down, status changed, moved to the miller's cottage. Object
identity survived, which was impossible the day before.

**`relationship_changed` fired.** Hald to −20, "openly hostile and threatening". **First time in
152 turns across three sessions.** n=1 and not a trend, but it is the first evidence the delta
can fire in play at all.

## What it does not test, measurably

| | ChatGPT | human-1 | human-2 |
|---|---:|---:|---:|
| deltas applied | 92 | 209 | 172 |
| turns changing nothing | **42%** | 24% | 27% |
| `player_moved` | **2** | 6 | 9 |
| locations discovered | **3** | 8 | 5 |
| `status_changed` | **1** | 6 | 10 |
| malformed input | **0** | 1 | 1 |
| median input length | 160 | 164 | 209 |

It played cautiously and conversationally. **Fifty turns and it never went down the well** — it
asked about the well, prepared to descend, organised a rescue party, secured a rope, and called
into it. Every input is immaculately formed: `*action* speech *action*`, 160 characters, no
typos, no unbalanced asterisks.

Human sessions produced `"Move the board aisde"` and `"8I say"`. Malformed input has found real
bugs.

The 42% of turns changing nothing is the signature: a model player asks questions, and a
question is the deflection case, which correctly records nothing.

## The trap in the headline number

Fact quality reads **~68% correct**, against 55% in the last human session. That looks like the
day's work paying off and **it is not comparable**.

Conversation produces knowledge-worthy facts; action produces events. A session that talks for
fifty turns will score better on a metric that rewards durable truths, regardless of whether
extraction improved. The play style flatters the measurement.

Recorded as a number not to trust until a human session confirms it. This is the same trap as
the false 14/14 on lore — a measurement taken on a shape the target does not have.

## What it found anyway

`item_status_changed` absorbing a description: a mooring ring discovered to be carved with the
weeping woman had that carving written into its *status* rather than its description. Third
instance of one pattern — mood absorbing status, facts absorbing descriptions, item status
absorbing description. See `CHALLENGES.md`.

Worth noting the LLM session found this precisely *because* it was patient: it examined an
object closely rather than using it, which is the behaviour a goal-directed human player skips.

## The rule

**Use a model-played session for bookkeeping under load** — attribution, knowledge tracking,
object identity over many turns, drift, long-run stability. It is genuinely good at that and it
produced useful signal here.

**Do not use it for movement, combat, risk-taking or messy input.** Those are where the
multi-stage movement bug, the mood/status defect and the two-object merge were found, and a
model player systematically avoids all four.

And **never compare fact-quality percentages across play styles.** The denominators are
different games.
