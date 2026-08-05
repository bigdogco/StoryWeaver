# Devlog — who said it

**Date:** 2026-08-04
**Scope:** `source` on facts, and a prompt rule that became unnecessary

---

## The failure

Turn 5 of a live session. The player asks where the stone from the well went; Hald says the
quarry, Mabb contradicts him and says the deep bog. Canon recorded:

```
fact  blocks-taken-to-quarry: The heavy thing pulled from the well was taken to the quarry.
fact  blocks-taken-to-bog:    The heavy thing pulled from the well was taken to the deep bog.
```

Both as settled world truth. They cannot both be true, and the flat model could not say which
was contested — or that either was a claim at all. The model had been improvising around the
gap for weeks, writing "claims" into fact text unprompted; this was the fourth sighting and the
first where two rival claims collided in a single turn.

## The design, unchanged from the audit

**A source, not a truth value.** A boolean asks the extractor to adjudicate honesty from one
turn, which it cannot do; a speaker is an observable — whether Hald said it is checkable from
the prose, and whether he was lying is something the story resolves later, with the player as
the right arbiter.

It composes with per-character knowledge in a way a truth flag does not. The interesting model
is not "this fact is false" but "these three believe it and this one knows better".

`FactEstablished` gained `SourceId`, `Fact` gained it as an immutable field, and the duplicate
key became `fact:{id}:{source}` — two characters asserting the same thing are two claims, and
keying on the id alone would silently drop the second half of a disagreement.

**14/14, forbidden 0.00, first measurement.**

```
fact_established  stone-quarry: The stone was taken to the old quarry.  [said by innkeeper-hald]
fact_established  stone-bog:    The stone was taken to the deep bog.    [said by drinker-mabb]
```

Both recorded, neither asserted, the disagreement preserved as content rather than corruption.

## The regression, and the better fix

The pinned sweep then showed `revelation` at 10/15 — a rule that had been solid for weeks. The
speaker-learns requirement: a character who states a secret must be recorded as knowing it, or
canon says they do not know their own secret.

The cause was immediate on reading the deltas. **Naming the speaker in `sourceId` made emitting
`fact_learned` for them feel redundant**, and the model started dropping it about half the time.

The obvious response was another prompt rule insisting on both. The better one:

> A character who asserted something knows it.

That is entailment, not judgement, so it belongs in code. `DeltaApplier` now adds the source to
their `Knows` — the same reasoning as `TouchPresentCharacters`, whose comment already reads
*"bookkeeping the extractor should not be asked to do... every question delegated to the model
is another thing it can get wrong"*.

**`revelation` returned to 21/21.** And more than recovered: the intermittent speaker-learns
miss that had scored 0–2/7 on *every* sweep since it was found is **gone**, because the failure
mode no longer exists rather than being argued down. A prompt rule that had been carrying a
known weakness for weeks is now unnecessary.

## The scoring mistake, for the third time this week

Fixing it meant `revelation`'s rule was wrong: it demanded the *delta* `fact_learned hald`,
which the better route no longer emits. Moved to an outcome rule — Hald ends the turn knowing
what he said, by either path.

Two-stage-entry movement, the Sera fact rule, the ground-chunks rule, and now this. **A rule
must target the world the turn produces, not the route taken to it.** Four times, written down
each time. What actually catches it is reading the deltas behind a failing rule before
believing the rule — the habit, not the note.

## Watch item, recorded rather than claimed clean

`two-stage-entry` scored 8/10 on the sweep and 13/14 on a focused re-run, against 10/10 earlier
the same day on the same pinned provider. One miss in seven.

Not conclusive at that sample size, and not dismissed either: **the extraction prompt is now
considerably longer than when that movement rule was tuned**, and prompt growth diluting
attention on older rules is a plausible mechanism with no evidence yet. Worth a larger sample
before either concluding a regression or assuming noise.

## Results

```
contradictory-claims   14/14 required, forbidden 0.00   (first measurement)
revelation             10/15 -> 21/21, and the long-standing intermittent miss is gone
full scored set, 9     48/50 clean, pinned
--selftest             2 new checks: source must be a real character; rival claims are not duplicates
```

## Next

Four of the five categories from the fact audit are now addressed — names by
`character_renamed`, who-knows-what by the existing delta, objects by `Item`, claims by
`source`. What remains is the residue the audit called momentary events, and a third play
session to see whether the 55% correct-fact share moves again.
