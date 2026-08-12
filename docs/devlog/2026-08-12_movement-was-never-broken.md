# 2026-08-12 — movement was never broken

The one where the fix was deleted, and the deletion was the work.

TODO: [`TODO_PLAY_51_FIXES.md`](../todo/TODO_PLAY_51_FIXES.md).
Challenge: the sixth provider sighting, in [`CHALLENGES.md`](../CHALLENGES.md).

---

## What happened

`movement` — "the player finishes a drink and walks out to the square", the plainest scenario
in the scored set — scored **1/5**. It had been failing all day, through every sweep.

The obvious story was that a prompt rule added earlier for `move-proposed` had over-corrected:
teach a model that turning toward a door is not movement, and maybe it stops recording movement
at all. That story was wrong, and checking it took two worktrees.

| build | provider | movement |
|---|---|---|
| current | DeepInfra | 0–1 of 5, 4–6 timeouts per 8 |
| `98896fb`, before `Location.Status` | DeepInfra | 1/2, 6 of 8 timed out |
| current | **StreamLake** | **8/8**, 60 tokens a call |
| HEAD, without the new rule | **StreamLake** | **8/8** |

Same model id, same prompt, same scenario, same eval. One upstream at effectively zero, another
perfect.

## Reading deltas instead of scores is what found it

The score said "missing required delta". The deltas said something else entirely:

```
mood_changed  innkeeper-hald = guarded
mood_changed  drinker-mabb   = maudlin
mood_changed  player         = neutral
REJECTED mood_changed player = neutral      <- its own duplicate
```

Degenerate repetition. Moods sprayed at everyone in the room, values restated that canon
already holds, the model repeating itself until the validator rejects the copies — and the
movement never reported.

It also explains a pattern that had been visible all day and dismissed as noise: **the timeouts
clustered on this one scenario.** `movement` has the least to extract, so it has the most room
to pad, and padding long enough hits the 45s wall.

## The fix, and why it was deleted

A prompt rule was written against the padding — "never emit a whole turn's worth of moods for
everyone present… a turn whose real content is a move must report the move."

Then HEAD, *without* that rule, scored 8/8 on StreamLake.

So the rule fixed nothing that exists on a working provider. It was reverted. The run that
might have shown it helping DeepInfra was rate-limited away, so there is no evidence for it in
either direction — and a permanent instruction in the extraction prompt, added on the strength
of one sick upstream, is exactly the kind of thing that is never removed later because nobody
remembers why it is there.

**Deleting it is the actual output of this work.**

## The clean run, finally

Owed since `Location.Status` this afternoon and blocked all day on provider health:

```
deflection      n/a       revelation     15/15    movement        5/5
hostility      10/10      new-character   5/5     redescription   n/a
atmosphere      n/a       player-arrival 10/10    two-stage-entry 10/10
name-reveal    15/15

required 100%   forbidden 0.00   rejects 0.00   —   50/50 clean
```

**Today's four changes — `Location.Status`, `ItemRevealedAsCharacter`, and two prompt rules —
cost nothing.**

## A retraction

Earlier today, `hostility` missing its standing rule 5/5 was explained as long-standing and
"consistent with `relationship_changed` having fired zero times across 102 turns of play".

**It scores 10/10 on StreamLake.** That was an explanation invented to fit a symptom that was
infrastructure, and it was offered confidently on data that had four timeouts in it.

It reopens something load-bearing. The character-sheets design cites "`relationship_changed`
never fires" as the reason sheets take the authoring half of relationships and leave standing
to canon. That conclusion may well survive — a per-turn extractor genuinely cannot see
accumulation across scenes — but **the evidence for it was gathered without a provider name
attached**, and it should be re-checked before being cited again.

## What this cost, and what it did not

**Cost:** the better part of a day's measurements taken against a degraded upstream, one
near-miss prompt change, one wrong explanation offered as fact.

**Did not cost:** canon. Every run today, at its worst, scored `forbidden 0.00`. A degraded
provider produces *missing* deltas, not wrong ones, and the validator rejects the garbage. The
51-turn session played through this and canon held — 7 rejections in 136 deltas, all of them
one unrelated bug.

That is the architecture working exactly as intended, and it is worth saying plainly: the thing
that made a bad provider survivable is the thing that makes a bad *model* survivable.

## What follows

- **`providerIgnore: ["DeepInfra"]`** — an exclude list rather than a pin, so routing keeps
  every other upstream and no single host becomes a point of failure. The machinery already
  exists and was built after an earlier sighting
- **Record the provider on `TurnRecord`** and on recorded baselines. Its absence is precisely
  why "movement is broken" and "movement was always fine" were both true today, and why
  "when did it start?" has no answer
