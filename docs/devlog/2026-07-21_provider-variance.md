# Devlog — the model was never the variable

**Date:** 2026-07-21
**Scope:** provider variance, an invalidated baseline, and a new one that can be reproduced

---

## How it started

A question about whether a place the player mentions ("I came from Astaria") ever becomes
canon. Four diagnostic scenarios answered it cleanly: **mention never creates an entity**, and
authorship is irrelevant — the narrator naming an unknown place in passing is dropped exactly
like the player doing it. 0/7 on all three mention cases. The dividing line is *presence*, not
who spoke.

The fourth diagnostic, `player-arrival`, was supposed to confirm the other side of that line.
Instead it looked like a serious bug: moving to a new location failed 0/7, with the
destination emitted as a `character_introduced` under `characterId: "player"`.

## Three wrong conclusions in a row

1. Wrote a prompt rule (`introduce the place first, then the move`). `player-arrival` improved
   2/14 → 9/14 — but `movement`, previously 7/7, read 0/7. Concluded I had caused a
   regression.
2. Wrote a second rule targeting the confusion directly. Worse: `player-arrival` fell back to
   2/14. Concluded the first rule was better.
3. Reverted to the **committed, untouched** prompt as a control — and `movement` still read
   **0/7**.

That last one is the only reason this did not become a day of prompt tuning. Twenty minutes
later, the same untouched prompt scored `movement` **7/7**.

Nothing in the repository had changed. The variable was which upstream OpenRouter routed to.

## What was actually wrong

`require_parameters: true` solves providers that *cannot* honour a parameter. It does nothing
about a provider that honours the schema and reasons badly inside it.

Confirmed by enumerating `deepseek-v3.2`'s providers (free endpoint, no tokens): 14 total, 4
lacking `structured_outputs` and correctly excluded. AtlasCloud **supports** it, returns
**schema-valid** JSON, and picks the wrong branch — a building emitted as a character. No
schema and no request parameter can catch a valid-but-wrong branch choice.

A/B on the full scored set, same commit, same prompt, n=7, each pinned:

| scenario | via AtlasCloud | via Baidu |
|---|---|---|
| `revelation` | **0/21** | 18/21 |
| `movement` | 1/7, forbidden 7 | **6/6** |
| `hostility` | 3/12 | **12/12** |
| `new-character` | 7/7 | 6/6 |
| clean runs | 28/55, forbidden 0.35/run | 43/52, forbidden **0.00**/run |

**0/21 on `revelation`** — AtlasCloud never once established a fact or recorded who learned
it, the single most important thing extraction does. That one provider's share of the routing
mix explains every "regression" and "improvement" measured that afternoon.

## The cost

Every extraction number recorded before today, including **"100% required, 0 forbidden, 0
rejects across three independent n=7 sweeps"**, measured *deepseek-v3.2 as routed that
afternoon*. It was not wrong when recorded. It was a property of a mix we did not choose and
cannot reproduce, and it should never have been written down as a property of the model.

## What changed

- The eval **records the serving provider on every run** and prints a per-provider breakdown.
  Any future "the model got worse" is now checkable rather than plausible.
- `--providers a,b` pins each upstream in turn (`provider.order` + `allow_fallbacks: false`)
  so providers can be sampled deliberately. **Test instrument only, never the play path** —
  pinned Baidu also threw four HTTP 429s, which is its own argument.
- `providerIgnore` on a role. Deliberately an exclude list rather than a pin: the other nine
  providers stay in play, and a proxy that ignores the parameter degrades to unfiltered
  routing instead of failing. This is what makes the no-pinning constraint survivable.

## The new baseline

Routed normally, AtlasCloud excluded, 8 scenarios, n=7, 56 calls:

```
required 86%   forbidden 0.00   rejects 0.00   107 tokens

deflection      forbidden 0     revelation     19/21
movement        7/7             hostility      14/14
new-character   7/7             redescription  forbidden 0
atmosphere      forbidden 0     player-arrival 7/14

StreamLake  48 run(s), clean 39/48   Baidu  8 run(s), clean 8/8
```

**86% is not a regression from 100%.** Different denominator — eight scenarios including a
deliberately hard new one — and, more to the point, a number that can be reproduced.

The remaining 14% is two systematic things, not noise:

1. **The missing move, fully deterministic.** `player-arrival` decomposes exactly: "the mill
   recorded as a location" **7/7**, "the player moved to the mill" **0/7**. The model
   introduces the new place correctly every time and never moves the player into it.
2. **Speaker-learns**, `revelation` missing "Hald knows it" 2/7.

Those account for StreamLake's non-clean runs precisely (7 + 2 = 9 against 39/48). **No
provider variance remains in the data** — every failure left is systematic.

## The lesson, which outlives the bug

*Re-measure the baseline before attributing any change to your own edit.* Three confident
conclusions came from provider noise, and only a control run caught it. This is the second
time on this project that a conclusion failed to survive a repeat; the first cost a two-call
redesign, this one nearly cost a day.

The corollary: a quality number for a hosted model is meaningless without recording what
served it.

## Next

Fix the missing move — now worth doing, because a deterministic 0/7 is easy to measure and the
routing noise that made tuning impossible is controllable. The discarded "attempt 1" rule
deserves a fair test; it was thrown away for a regression it did not cause.

Automating all of this away — enumerate a model's providers, score each, propose the ignore
list — is specced in TODO_FUTURE_WORK as provider calibration. It is the user's idea and the
right one: nobody can be expected to know which of fourteen upstreams are good.
