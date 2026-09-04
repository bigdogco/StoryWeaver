# Design — can narration be measured?

**Status:** audit, no design committed. Written 2026-07-24.
**Decisions answered 2026-08-16 — see §7.** Deferred out of Phase 1, deliberately.

Every measurement this project has is about extraction. The half of the system that writes the
story has **no automated quality control at all** — reroll is the only lever, and it is manual.

The lore feature just added a rule nothing verifies: *"a character may only refer to a topic
they have heard of."* That is the immediate prompt for asking whether narration can be checked
at all.

---

## 1. The audit

Every mechanically checkable property, run against all 51 turns of real play.

| # | property | result |
|---|---|---|
| 1 | an internal id leaked into the prose | **0** |
| 2 | narration length | 982–1823 chars, mean 1445 — stable |
| 3 | verbatim sentence repeated across turns | **0** |
| 4 | a character named before canon knew the name | 0 real (5 false positives, see below) |
| 5 | a fact established without the player learning it | **1**, and it is correct |

**Everything mechanically checkable already passes.** That is the finding, and it is not the
one expected.

Notes on the two non-zero rows:

- **Row 4** matched name *prefixes* — "Hald's companion" fired on "Hald's", "Drowned follower"
  on "Drowned". Zero real violations. Recorded because a naive version of this check would
  ship five false alarms, and a check that cries wolf gets ignored, which is worse than no
  check.
- **Row 5** is `player-is-investigator`, established on turn 10 without a `fact_learned` for the
  player. Correct: the player asserted it about themselves, and the *turn* they said it is not
  the turn they learned it. A rule flagging this would be wrong.

Row 1 deserves emphasis. The id leak is the bug that produced *"the heavy oak door of the
marrow-tavern flies outward"* and caused the `ForNarration`/`ForExtraction` split. **Fifty-one
turns, zero recurrences.** The fix holds, and this is the first time that has been measured
rather than assumed.

## 2. What this means

The cheap checks are all green, so a narration eval built from rules would score 100% on day
one and tell us nothing. **The properties actually worth checking are all semantic:**

- Did a character reference lore they have not heard of? *(the lore premise, unverified)*
- Did the prose reveal a fact the player has not learned?
- Did the narrator contradict canon — a dead character speaking, a wounded one unhurt?
- Did the narrator speak or act for the player?
- Did it restate the world-state block instead of continuing the story?

None of these are string matches. All of them need a model to judge, and that is a
categorically bigger thing than the extraction eval, which is deterministic rule-matching over
structured output.

## 3. The problem with a judge

**A judge model is a second model whose variance we do not understand, grading a first model
whose variance we barely do.**

This project has been wrong four times this year by attributing provider noise to its own code,
and the by-provider table is now the only reason those did not become findings. A judge adds a
second, unaudited source of exactly that noise — and unlike extraction, its output is a verdict
with no schema to constrain it and no validator behind it.

That does not make it a bad idea. It makes it a thing that needs the same treatment everything
else got: a fixed set of cases, N runs, provider pinned, and a measured baseline before it is
trusted to say anything about narration.

**Specifically it needs its own control: hand-labelled narration.** A judge that has never been
scored against known-good and known-bad prose is an opinion generator. Producing that labelled
set is most of the work, and it is the part that cannot be automated.

## 4. The cheaper thing that might be worth more

One property from §2 *is* mechanically checkable, and it is the one the lore feature just made
load-bearing:

> **Did a character reference a lore topic they have not heard of?**

Lore entries have titles and `keys` — authored strings. If Mabb has not heard of the cult and
the narration puts "Shurus" or "the Drowned Father" in Mabb's dialogue, that is a string match
against `keys`, scoped to quoted speech, with no judge involved.

It is narrow, it will miss paraphrase, and it cannot tell the difference between Mabb saying it
and Mabb being told it. But it is deterministic, it costs nothing per turn, and it tests the
one rule this codebase added without any way to check it.

**Proposal: build this before any judge.** If it fires in real play, we have a bug and a
measurable one. If it never fires across a session, that is weak evidence the narrator respects
knowledge boundaries — and weak evidence acquired for free beats strong evidence that needs a
labelled corpus.

## 5. Honest assessment of priority

The reason to build a narration eval is that half the product is unmeasured. The reason not to
is that the audit found nothing wrong.

Both are true. What tips it is what a judge would *unlock* rather than what it would catch
today: the dice-checks idea in `TODO_FUTURE_WORK.md` rests on "did the narration contradict the
roll?", which is a judge question, and it was logged as the first objectively checkable
property of prose. It is not checkable without this.

So: **the §4 lore check is worth building now. A judge is worth designing before it is worth
building**, and it should be sequenced against something that needs it — dice, or a real
complaint from play — rather than built on the general principle that prose ought to be
measured.

## 6. Decisions needed

1. **Build the lore-knowledge check now?** (§4 argues yes — it is cheap and tests an unverified
   rule.)
2. **Does a judge get designed now or deferred until dice?** (§5 leans defer.)
3. **If a judge happens: who produces the labelled narration?** It cannot be generated, and it
   is the bulk of the work.

---

## 7. The decisions, answered 2026-08-16

**1. Build the lore-knowledge check?** **No.** Run by hand across all eleven saves before
building anything, and §4's proposal does not survive contact:

- `keys` are *retrieval* keys — broad on purpose so an entry fires when relevant. Detection
  wants precision. Real hits: *"take the tube down"* (`tube`), *"couriers don't stop to ask"*
  (`couriers`). A key of `blind` would fire on the Venetian blinds in a noir opening.
- Attributing quoted speech to a speaker is not a string match, and without the speaker there is
  no knowledge to check against.
- **The cleanest hit was not a narrator bug.** Hald saying *"take Shurus from us"* on marrow-old
  t25 is correct — he is a cult member — but canon never recorded him knowing the cult, because
  `Knows` tracks what a character *learned in play*, never what they always knew.

That last point is the finding worth keeping: **a seed can under-declare what its characters
know and nothing notices.** If revisited, this is a content-authoring check, not a narration
check.

Note that §1 predicted this shape exactly — its row 4 produced five false positives from name
prefixes, and warned that *"a check that cries wolf gets ignored, which is worse than no
check."* The same trap, one feature later.

**2. Judge now or deferred?** **Deferred**, as §5 leaned. Sequence it against dice, where *"did
the narration contradict the roll?"* is the first objectively checkable property of prose.

**3. Who produces the labelled narration?** Moot while deferred.

## 8. The reason above all three

§5 called this honestly: *the reason to build a narration eval is that half the product is
unmeasured; the reason not to is that the audit found nothing wrong.*

What settled it was noticing that **a narration eval is a feature built in a vacuum.** Nobody
has complained about the prose. Building measurement for a problem no session has produced is
the same mistake as building features for one — and harder to see, because measurement feels
virtuous.

`PROJECT.md` §3 now carries that as a rule: *build for observed failures, never for
completeness.*
