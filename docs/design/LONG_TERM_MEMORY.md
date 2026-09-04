# Design — long-term memory

**Status:** notes, no design committed. Written 2026-07-19, moved out of `TODO_FUTURE_WORK.md`
2026-08-13 because four options are reasoning, not four tasks.

Lorebooks handle *world* facts. They do not handle *what happened*, which is harder.

> **The measurement came back, 2026-08-14, and it was negative.** A 230-turn `marrow` run
> showed no decay at all: rejection rate flat across every 50-turn block, no drift, no
> corruption, narration still coherent and in character at t228.
>
> **This whole document rested on the assumption that canon degrades over distance. It does
> not.** Nothing below is scheduled, and none of it should be built on the old reasoning; it
> needs a fresh justification, which as of today does not exist.
>
> What the long run actually surfaced was the opposite shape — canon staying *correct* while
> becoming harder to narrate from, because names collide and nothing ever leaves inventory.
> That is a relevance and rendering problem. See `NAMES_ARE_NOT_UNIQUE.md` and the lorebook
> retrieval item, not this.

The notes below are kept as the record of what was considered.

---

## The options, roughly in order of appeal

**Structured state.** Explicit JSON world state updated by a second model call, injected as a
compact block. Deterministic, inspectable, user-editable, cheap in tokens. More work to build,
needs a genre-fitting schema — which an RPG gives you largely for free.

This is the direction with actual leverage, and it is worth noticing that **StoryWeaver
already is this.** Canon *is* the structured state; extraction *is* the second model call.
What is missing is not the mechanism but its application to *events* rather than entities — a
record of what happened, at the same fidelity the entity store gives to what exists.

**Scene-indexed retrieval.** Summarize per *scene* rather than per N messages, keep summaries
addressable, retrieve whole scenes. Best fidelity-per-token of the options, and the least
common in the wild. Fits the architecture: a scene boundary is closer to how canon already
thinks than an arbitrary message count.

**Rolling summarization.** Cheap and lossy; errors compound into permanent canon. Probably a
component, not the answer — and it is the failure mode this project exists to avoid, so it
needs the same suspicion the extractor gets.

**Vector recall over history.** Catches what keywords miss, but retrieves
semantically-similar-but-irrelevant chunks constantly, and returns fragments without temporal
context. Mediocre in practice.

---

## What to do first

Nothing. The long run did not fail at memory, so none of these has a problem to solve. Revisit
only if a future run shows canon actually losing or corrupting what it holds — which two runs
past 150 turns have now failed to produce.
