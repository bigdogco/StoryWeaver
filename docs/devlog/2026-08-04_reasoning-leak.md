# Devlog — the narrator showed its work

**Date:** 2026-08-04
**Scope:** a chain of thought printed into a live story, and the two bugs that let it happen

---

## From play

Turn 26 of the new save. Instead of prose, the player got this:

```
Thinking Process:

1.  **Analyze the Player's Input:**
    *   Intent: De-escalate Hald using the authority of the King's Investigators...
2.  **Review World State & Character Constraints:**
...
6.  **Writing the prose:**
    Hald's jaw works silently for a moment...
```

Four thousand characters of the narrator's internal reasoning, ending in a half-written scene
cut off mid-sentence. Then `applied: nothing`.

The response body says exactly what happened:

```
model: qwen/qwen3.7-plus   provider: Alibaba
content: null             reasoning: 4682 chars
completion_tokens: 1202   reasoning_tokens: 1200
finish_reason: "length"
```

**The narrator spent its entire 1200-token budget thinking and produced no prose at all.**

## Bug 1: a fallback that could not tell two failures apart

`OpenRouterResponse.Content` fell back to the `reasoning` field whenever `content` was empty.
That fallback exists for a real, observed reason — some providers put the answer in the wrong
field — and its comment claimed it was safe because "a provider that fills both is unaffected."

True, and it misses the case where **neither** field holds an answer. Empty content plus
reasoning has two causes:

| cause | reasoning field holds | right response |
|---|---|---|
| provider misreported | the answer | recover it |
| model ran out of tokens thinking | half a thought | **fail the turn** |

The discriminator was already parsed, already documented, and simply not consulted:
`FinishReason`, whose own summary in the same file reads *"`length` alongside empty content is
the signature of a reasoning model that spent its whole budget thinking."* The knowledge was
there; the branch was not.

Fixed: the fallback refuses when `finish_reason` is `length`. A hard failure the turn loop
already knows how to report and `/retry` already knows how to fix is strictly better than prose
that is not prose.

## Bug 2: the diagnostic that missed by two tokens

`DescribeEmptyContent` exists precisely to name this failure instead of letting it look like a
schema rejection. Its condition:

```csharp
if (parsed.FinishReason == "length" && reasoning > 0 && reasoning >= completion)
```

Live values: `reasoning = 1200`, `completion = 1202`. The model emitted two tokens of nothing
before the ceiling, so `1200 >= 1202` was false and the good message was skipped in favour of
the bland one it was written to replace.

Fixed with a proportional test (`reasoning * 10 >= completion * 9`). Worth recording as a
category: **an exact-equality guard on a number a provider controls will eventually be off by
one.**

## Bug 3, arguably the root: the budget was never survivable

The narration role had `maxTokens: 1200` and no reasoning configuration at all. Startup
validation rejects a role that configures reasoning *without* `requireParameters` — narration
configures none, so it passed the check and reasoned by default.

Measured against real play: narration output is ~500 tokens at p90, 520 at maximum. So 1200
left roughly 680 for thinking.

Then the fix was verified with a live turn, which produced the number that matters:

```
reasoning_tokens: 1960   content: 872 chars   finish_reason: stop
```

**1960 reasoning tokens.** The old ceiling was 1200. This was not an edge case that occasionally
tripped — it was *unsurvivable* whenever the model chose to think hard, which is exactly what a
tense multi-character scene provokes. Turn 26 was a complex negotiation; that is why it broke
there and not at turn 3.

Raised to 4000. `maxTokens` is a ceiling rather than a spend, so headroom costs nothing unless
used, and 2176 of 3000 on the verification turn was a thinner margin than it looked.

## The third consequence

`applied: nothing` was not a fourth bug. Extraction was handed 4,682 characters of
meta-commentary as "the narration", read it faithfully, and correctly found no world changes in
a description of how to write a scene. The bookkeeper did its job on garbage input.

## What generalises

- **A fallback needs to know what it is falling back *from*.** This one recovered a value
  without asking why the primary was missing, and the two reasons wanted opposite handling.
- **`CHALLENGES.md` predicted this and predicted it too gently.** The existing entry says
  running out of reasoning tokens is *silent*. It is worse than silent: combined with the
  fallback it was *loud and wrong*, printing thinking into the story. A failure mode logged as
  quiet deserves a second look at what happens when it meets a recovery path.
- **The verification run is where the real number came from.** Fixing it against 1200 would have
  suggested 2000 was plenty. Watching a healthy turn spend 1960 on reasoning is what showed the
  ceiling needed to be far higher than the failure implied.

## Results

```
--selftest        4 new response-parsing checks, all passing
                  (ordinary, misreported-recovered, truncated-refused, content-wins)
live narration    clean prose, finish_reason: stop, 1960 reasoning tokens inside a 4000 ceiling
```
