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
together. Startup validation now enforces this coupling, so the mistake is caught before
the first API call rather than showing up as intermittent bad output.

**Status 2026-07-19:** verified working for the chosen models — `deepseek-v4-flash`
returned schema-conformant JSON on the first attempt with `require_parameters: true`.
Kept open rather than resolved: this was one call on one day. The hazard is *routing*,
which varies by model, provider mix, and time, so it will need re-checking whenever a
role's model changes.

---

### Response shape beyond the schema is not guaranteed

**Severity:** High as a class, though the known instance is fixed.

JSON schema pins which fields exist and what type they are. It says **nothing** about
property order, key casing, or whitespace — and because OpenRouter routes the same model id
across upstream providers, those can differ between two otherwise identical calls.

Hit on 2026-07-19. Extraction failed on every turn that produced a delta:

```
Deserialization of types without a parameterless constructor ...
Type 'StoryWeaver.Core.StateDelta'.
```

The model had emitted properties alphabetically, putting `kind` last. System.Text.Json's
built-in polymorphism requires the type discriminator to be the **first** property. The
schema was honoured perfectly; both orderings are valid JSON. The dependency on ordering was
ours. The earlier probe passed only because a different provider happened to emit
`kind` first — the failing response came from DigitalOcean.

Fixed by `StateDeltaConverter`, which finds `kind` wherever it appears and dispatches on it.
Unknown or missing kinds throw rather than returning null, because a null is
indistinguishable from the model reporting no changes.

**The general rule matters more than the fix: never depend on the shape of a response beyond
what the schema guarantees.** Anything that does is a latent version of this bug, and it will
present as an intermittent model failure rather than as our own.

Covered by `--selftest`, which is offline and free to run.

**Second instance, 2026-07-20: the payload arrived in the wrong field.** MiniMax M3 served by
Parasail returned `content: null` with well-formed delta JSON in `reasoning`, on 20 of 21
calls. The client read only `message.content`, so twenty correct answers were recorded as
empty responses and the model scored near zero on an eval it was actually passing.

Fixed by falling back to `reasoning` when `content` is empty, and logging when that happens.
The fallback cannot smuggle thinking into a normal response because it only applies when
there is no content at all.

Three instances now — silently dropped parameters, property order, response field. This is
the defining hazard of the integration, not a run of bad luck.

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

**Status 2026-07-19:** the *format* half is looking good — one narration passed to
`deepseek-v4-flash` came back schema-conformant first try. The *semantic* half is
untouched. That test asked for three flat fields from two sentences of prose; real
extraction means diffing deltas against existing canon over hundreds of turns, deciding
what changed versus what was merely restated, and resisting the pull to invent detail the
prose only implied. Nothing here has been tested against that yet. §9's ~50-turn manual
session is the real answer.

One thing already visible: the extraction reply was correct but *lossy* in a telling way
— it recorded `"characters": ["patrons"]`, flattening a crowd into a single unnamed
entity. Harmless in a smoke test; exactly the kind of thing that silently degrades an
entity graph if unnoticed over a long session.

**Update 2026-07-19, delta schema probe.** One realistic paragraph, nine delta kinds
available, `strict: true`. The *format* was flawless — five well-formed deltas, correctly
discriminated, clean round-trip into Core types. The *semantics* were not:

| Problem | What happened |
|---|---|
| **Dangling reference** | Emitted `fact_learned` for `cellar-poisoning` with **no `fact_established` for it**. Someone learned a fact that does not exist. |
| **Re-introduced known entity** | Emitted `location_introduced` for `marrow-cellar`, which the prompt explicitly listed as already known. |
| **Garbage field content** | That location's `description` was set to the poisoning fact — a *description* field filled with an event. |
| **Missed obvious change** | No `mood_changed` for Hald, despite "his easy grin curdling into something guarded" being the clearest state change in the passage. |
| **Invented an id** | Used `"player"` as a `characterId`, a thing the domain model does not have. |

This is the reliability question landing squarely, and the answer is nuanced: **schema
compliance is solved, semantic correctness is not.** Every failure above is one a
validation pass can catch (dangling id, duplicate introduction, unknown character id) —
which is the argument for making §7's validation strict and loud rather than trusting the
extractor. The missed mood change is the hardest, because nothing detects an omission.

Notable that the schema is doing real work in a direction it was not designed for: because
`fact_learned` cannot carry fact *text*, the model had to invent an id, and the dangling
reference is **visible**. A generic property patch would have absorbed it silently.

**Update 2026-07-19, first four-turn play session.** The central premise survived its first
real test: asked about a fact only Hald knew, Mabb did not know it, and the narration turned
that into a character beat rather than a lookup failure. Per-character knowledge works.

Four problems, all now fixed, recorded because two were self-inflicted and the pattern is
worth remembering:

1. **The narrator wrote an internal id into the prose** — "the heavy oak door of the
   marrow-tavern flies outward". `ContextAssembler` listed connections as bare ids, which
   the narrator read as names. Ids had been added to context to help the *extractor*; that
   the narrator would echo them was never considered. Fixed by splitting into
   `ForNarration` (names only) and `ForExtraction` (ids), since the two roles want opposite
   things from the same state.
2. **A character did not know the fact he had just disclosed.** The extractor prompt said
   "the speaker usually already knew it, so they need no fact_learned" — true about the
   fiction, wrong about the bookkeeping, because canon contains only what gets written
   down. Hald stated his own secret and was recorded as not knowing it. Fixed by requiring
   `fact_learned` for the speaker too.
3. **Padding.** One batch contained the *same* `location_introduced` three times with
   different evidence quotes, plus re-establishment of two known facts. Now deduplicated on
   semantic identity (ignoring evidence) before validation.
4. **No-ops counted as successes.** "player learned well-boarded" was reported as applied
   on a turn where the player already knew it. Validation now returns three categories
   rather than two, so restatements of existing canon cannot inflate a quality measure over
   a long session.

**Update 2026-07-19, second session (five turns, roleplay input convention).** The previous
fixes held: no id reached the prose, the narrator never echoed the player's dialogue back,
no-op detection fired correctly, and movement tracked cleanly. Typos and malformed asterisk
markup (`8I say`) were handled without trouble, which matters — real play is full of them.

**One new failure, caused by the previous fix.** Extraction was told "anything the player
told a character is a fact that character now knows", to close the hole where player-stated
actions never reached canon. The model applied it to *questions*:

```
fact player-asked-about-well-rumor: The player asked Hald and Mabb
     if they have heard a rumor about the well.
innkeeper-hald learned player-asked-about-well-rumor
drinker-mabb learned player-asked-about-well-rumor
```

A conversational event promoted to permanent world truth, with two characters now "knowing"
it. Over a long session this mints junk facts at conversation rate, each replayed into
context forever, crowding out real ones — and `Knows` is precisely the field that is
supposed to make NPCs feel simulated.

Fixed in the prompt by defining what a fact *is*, with a usable test: **would it still be
true if nobody had ever mentioned it?** Plus an explicit never-list (questions, refusals,
greetings, purchases, moods, "a conversation happened") and permission to establish nothing,
since most turns legitimately establish no facts.

Deliberately not fixed with a validator rule. Pattern-matching text like "The player asked"
is brittle and would miss the general case; this is a definition problem, not a syntax one.

**Lesson worth keeping:** the fix for a silent omission created a silent over-production.
Both directions need watching whenever an extraction rule is loosened.

**Still unsolved: omissions.** No `mood_changed` for Mabb through an obvious slide into
maudlin self-pity, and no `relationship_changed` for Hald across two turns of escalating
hostility — he ended the exchange by shutting the subject down, still at standing −10.
Nothing detects a delta that was never emitted, and no validator can. Candidate approaches
if this proves systematic: a periodic reconciliation pass asking a model to compare canon
against recent narration, or making a small number of high-value fields (mood, standing)
*required* per turn so the model must state them even when unchanged.

**Resolved as model-dependent, 2026-07-20.** `qwen/qwen3.7-plus` scored 14/14 on the
hostility scenario at n=7, including `relationship_changed` every single time, using the
*existing* schema and prompt. deepseek-v3.2 and minimax-m3 missed standing on 5–6 runs of 7.

So the earlier conclusion below was wrong: this was never a schema or prompt failure. Six
models failing the same way looked like a design problem and was a capability problem.

Recorded rather than deleted, because the reasoning that produced the wrong conclusion was
sound given the evidence available — the mistake was drawing it from models that shared a
weakness, without a single counter-example.

**The original observation, kept for context.** Across nine turns over two sessions:
**zero** `relationship_changed`, through an innkeeper who was consistently cold, turned his
back, and twice closed a subject down. He remains at his seeded −10. Mood is reported but
patchily — a character going from oblivious to "pale eyes fixing on you" produced nothing.

Relationships appear to be the worst case, plausibly because they are the least concrete:
a move or a revealed secret is a discrete event, while standing shifts by accumulation
across a scene, and a per-turn extractor sees one turn. If so, relationship drift may
belong in a periodic reconciliation pass looking at several turns at once, rather than in
the per-turn extraction at all.

---

### Reasoning tokens are billed against `max_tokens`, and running out is silent

**Severity:** High — no error, and the symptom points at the wrong cause.

A reasoning model spends its thinking budget from the same `max_tokens` allowance as its
answer. Set the budget to fit the expected *output* and the model can think until the
budget is gone and return **empty content with no error** — HTTP 200, a bland
`finish_reason: "length"`, and `completion_tokens == reasoning_tokens`.

Hit for real on 2026-07-19: extraction was capped at 800 tokens, `deepseek-v4-flash` spent
all 800 reasoning, and the probe reported "no message content". The natural reading was
that the schema had been rejected — the investigation went that way and was wrong. The
reasoning trace (recoverable from the session log) showed the model working through the
schema correctly; it simply never reached the point of writing output.

**Mitigations:**
1. Extraction budget raised 800 → 4000. Any role on a reasoning model needs headroom for
   thinking on top of the answer.
2. `OpenRouterClient.DescribeEmptyContent` now names this case explicitly rather than
   reporting a generic empty response, so the next occurrence diagnoses itself.

The general lesson is worth more than the fix: **an empty response has several very
different causes that are indistinguishable at the call site.** Any of them will look like
whatever you were most recently worried about.

**Related — reasoning is controllable, and that control is itself droppable.** OpenRouter
exposes a `reasoning` parameter: `effort` (max/xhigh/high/medium/low/minimal/none),
`max_tokens` for an explicit budget, and `exclude` to strip reasoning from the response.
Now configurable per role via `RoleSettings.Reasoning`.

Two traps in it:

- **`exclude: true` saves nothing.** It removes reasoning from the *response*, not from the
  bill or the token budget. `effort` is the cost control; `exclude` is cosmetic.
- **`reasoning` is a parameter, so it can be silently dropped like any other.** Sent
  without `require_parameters: true`, a provider that does not support it ignores it and
  you get full-effort reasoning at full cost while believing effort was "low". This is
  *worse* than a dropped `response_format`, because the output still looks correct — there
  is no symptom to prompt an investigation. Startup validation now rejects a role that
  configures reasoning without `requireParameters`, and rejects a `reasoning.maxTokens`
  that leaves no room under the role's `maxTokens`.

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

**First data point 2026-07-19 (smoke test):** narration 1522 tokens, extraction 821. Note
the ratio — extraction was ~35% of the turn's *tokens*, not the 5–10% assumed. Both models
are reasoners, so much of that is thinking tokens rather than output. Cost-wise the gap is
wider than the token counts suggest since the two models are priced differently, but the
assumption that extraction is a rounding error does not survive contact. Worth measuring
properly, in currency rather than tokens, once turns are real.

---

## Resolved

### The narrator had no memory of any previous turn

**Resolved 2026-07-21.** Found while diagnosing what looked like a resume bug after §6.

Narration was sent exactly two messages — the system prompt, and the current world state plus
player input. **No prior prose was ever included**, on any turn. Every turn was written by a
model that had not seen a word of the story, with canon as its only memory.

Canon is the right *long-term* memory, but it cannot hold what an NPC just said, the thread of
a conversation, or what has already been described. The result is a narrator that re-describes
the scene from scratch and cannot continue a dialogue.

**Two things worth remembering from how this was found.** It had never been recorded as a
decision anywhere — it was an unexamined gap that looked like an architectural principle, and
"canon is the source of truth" quietly justified it. And the bug report was for something
else entirely: persistence was working perfectly (verified on disk: `turnNumber: 6`, six
history lines), and resuming merely made an always-present gap *visible*. Checking the disk
before theorising is what separated the two.

Fixed with a configurable window (`story.historyTurns`, default 10) of recent turns replayed
as real alternating user/assistant messages, keeping volatile world state in the last message
so the prefix stays cacheable. Extraction deliberately gets none of it — it scores 100% on the
eval reading a single turn, and prior turns invite re-extracting settled events as new deltas.
See the 2026-07-21 devlog.

### `anyOf` in strict JSON schema — works

**Resolved 2026-07-19** by `--probe-schema`.

The `StateDelta` set is nine variants discriminated by `kind`, which in JSON schema is an
`anyOf`. `strict: true` support for `anyOf` is less universally implemented than for a flat
object, and the earlier smoke test was weak evidence — it exercised a three-field flat
object, the easy case.

`deepseek-v4-flash` handled the nine-branch union correctly: well-formed deltas, right
branch per change, every `required` field present, clean deserialization into the Core
types via `[JsonPolymorphic]`. The flat-object fallback is not needed.

Strict mode requires each branch to set `additionalProperties: false` and list *every*
property in `required` — optionality is expressed as a nullable type, not by omission.

Re-run `--probe-schema` if the extraction model changes; this is a per-model property, and
the routing hazard above means it is not even stable per model ID.
