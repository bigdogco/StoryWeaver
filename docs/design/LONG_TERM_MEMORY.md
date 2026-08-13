# Design — long-term memory

**Status:** notes, no design committed. Written 2026-07-19, moved out of `TODO_FUTURE_WORK.md`
2026-08-13 because four options are reasoning, not four tasks.

Lorebooks handle *world* facts. They do not handle *what happened*, which is harder.

**Gated on a measurement, not on a decision.** `PROJECT.md` carries an open question — does
canon survive 200 turns? — and this whole line of work rests on the assumption that it does
not. Bootstrap proved coherence at 51 turns against a 10-turn narration window; nothing has
run further. If canon holds, most of what follows is unnecessary, and building it first would
be solving a problem we have never observed.

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

Nothing, until the 200-turn measurement exists. Then the question is not "which of these
four" but "what did the long run actually fail at" — and the answer decides between them, or
shows that none is needed yet.
