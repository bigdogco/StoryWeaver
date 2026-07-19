# Challenges

Known risks, gotchas, and open problems. Add to this as they are identified — including
ones that turn out to be non-issues, with the resolution noted.

---

## Open

### OpenRouter silently drops unsupported parameters

**Severity:** High — silent, intermittent, near-unreproducible if hit cold.

OpenRouter load-balances a single model ID across multiple upstream providers, weighted
by price (a cheaper provider receives substantially more traffic). By default, a provider
that does not support a request parameter **ignores it rather than erroring**.

Consequence: an extraction call that specifies `response_format` can come back as prose
instead of JSON, at a rate that tracks the provider price distribution, with no error, no
warning, and no way to tell from the response which provider served it.

**Mitigations (both, not either):**
1. `provider: { require_parameters: true }` on any role depending on `response_format` —
   restricts routing to providers that support everything sent.
2. The validator + repair loop ported from AI-Lord — re-asks with a corrective
   instruction when the response fails validation. Covers the case where routing is
   constrained but the model still emits malformed JSON.

**Caveat on mitigation 1:** if the chosen model does not support `json_schema` at all,
`require_parameters: true` may produce a hard failure or an empty provider set rather
than degrading gracefully. `requireParameters` and `responseFormat` must be changed
together.

---

### Extraction reliability is unproven

**Severity:** High — the architecture rests on it.

The entire canon-vs-narration design assumes a cheap model can reliably read creative
prose and emit correct structured state deltas. This is unvalidated. Small models may be
inconsistent at structured output over free-form fiction, and cost pressure pushes toward
exactly those models.

This is the question the bootstrap phase exists to answer. If extraction is not reliable
enough, the architecture needs rethinking — better to learn that in a console harness
than after an Avalonia UI is built on top.

---

### Compounding canon errors

**Severity:** Medium — deferred, but design-relevant now.

A wrong fact committed to canon becomes permanent world truth and is replayed into every
subsequent turn. Summarization compounds this: an error in a summary is indistinguishable
from a fact.

Defence is extraction-time validation — check asserted facts against the entity record
before committing. For v1, log and surface conflicts rather than auto-resolving; we need
to see how often and how badly it goes wrong first. Letting the player arbitrate
("canon says X, the story said Y — which is true?") is a legitimate answer, not a cop-out.

---

### Silent lore drops

**Severity:** Medium — not yet reached (no budgeting layer exists).

Once context budgeting exists, entries that do not fit are dropped with no signal. This is
the single biggest source of "why did the AI forget the Duke exists?" in existing systems:
a user writes 30 lorebook entries, a scene mentions eight, four fit the budget, four
vanish silently.

Surfacing which entries fired and which were cut is a genuine differentiator — almost
nobody does it.

---

### Cache invalidation vs. dynamic injection

**Severity:** Low for now — relevant once cost matters.

Prompt caching keys on exact prefix match. Injecting lore into the middle of the prompt
rewrites the prefix and invalidates the cache for everything below it. Depth-based
injection (near the end) is friendlier — it only invalidates the tail.

Unsolved in general; an argument for putting dynamic lore at depth rather than in the
system block.

---

### Cost per turn

**Severity:** Low — needs measurement, not mitigation.

Two-plus model calls per turn. Estimated shape: narration dominates, extraction ~5–10% of
turn cost on a cheap model. This is an estimate from the design, not a measurement. Needs
real numbers early, since it constrains model choice for the whole project.

---

## Resolved

*(nothing yet)*
