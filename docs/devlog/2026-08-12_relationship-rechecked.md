# 2026-08-12 — re-checking a claim, and finding it stronger

A design decision rested on evidence gathered before measurements carried provider names. After
one upstream was caught scoring 0% on scenarios another scored 100% on, that evidence was worth
re-examining rather than defending.

Design: §3 of [`CHARACTER_SHEETS.md`](../design/CHARACTER_SHEETS.md).

---

## The claim

`relationship_changed` has never fired in real play, so character sheets take the *authoring*
half of relationships — the permanent why — and leave the moving number to canon.

Recorded as "zero across 102 turns and two sessions".

## The re-check

Every saved session swept, not just the two the claim cited:

| session | turns | `relationship_changed` |
|---|---|---|
| `marrow` (human, sheets) | 51 | 0 |
| `marrow-old` | 51 | 0 |
| `marrow-2` | 51 | 0 |
| `marrow-LLM-1` | 50 | **1** — turn 11, an innkeeper announcing hostility |
| `marrow-LLM-2` | 50 | 0 |

**One firing in 253 turns across five sessions.** The claim survives, with two and a half times
the evidence it had.

## What the re-check improved is the *reason*

"It never fires" invites the wrong explanation — that the model cannot do it. That is false, and
it was worth finding out:

**The `hostility` scenario scores 10/10 on a healthy provider.** Asked to read prose that states
outright that someone's regard has changed, the model emits the delta every time. The one play
firing is the same shape: hostility announced in as many words.

So the capability is not the constraint. **The trigger is.**

The sharpest evidence is inside a single session rather than across five. After 51 turns of the
human session — through a lie exposed, a cult uncovered, and a companion watching the player
burn a man alive — every standing sat at exactly its seeded value:

```
drinker-mabb      0    no strong feelings        (seed)
innkeeper-hald  -10    suspicious of strangers   (seed)
inspector-mona  100    likes and respects        (seed)
```

while `mood` moved constantly over the same turns: `terrified`, `enraged`, `shaken, relieved`.
Same prose, same extractor, same call, opposite outcome.

**Mood is visible in one scene. Standing is the integral of many.** Ordinary prose does not
announce that regard has shifted; it shows a moment. No prompt rule fixes a thing the input does
not contain, which is why this belongs to the reconciliation pass and not to prompt work.

## What cannot be tested, and why that is the same finding

The eval only covers the case where the prose says it outright — and it passes. There is no
scenario for accumulated resentment across scenes, and there cannot usefully be one, because a
scenario is a single turn by construction.

That is not a gap in the eval. It is the finding restated from the other side.

## The correction I owed

Earlier today I said `hostility` failing 5/5 was "consistent with `relationship_changed` never
firing", implying one cause. Two different causes: that failure was a degraded provider, and
this is structural. Linking them was wrong, and the doubt it cast on the design conclusion was
misplaced — only the link was.

Worth keeping as a pattern rather than an apology: **an explanation that fits the symptom is not
evidence, and offering one for a measurement with four timeouts in it is how folklore starts.**
The right move on unreliable data is to say the data is unreliable, not to reach for a story
that accommodates it.

## Changed

Documentation only. §3 of the character-sheets design rewritten with the mechanism, the same
correction propagated to `TODO_FUTURE_WORK.md` and `TODO_CHARACTER_SHEETS.md`, and the
re-check item in `TODO_PLAY_51_FIXES.md` closed.

`TODO_BOOTSTRAP.md`'s closure note is left as written. It is a dated record of what was true in
July and it points at `TODO_FUTURE_WORK.md`, which now carries the correction — rewriting the
history rather than the pointer would lose when we learned what.
