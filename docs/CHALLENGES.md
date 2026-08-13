# Challenges

Known risks, gotchas, and open problems. Add to this as they are identified — including
ones that turn out to be non-issues, with the resolution noted.

---

## Open

### Providers differ in semantic quality, not just parameter support

**Severity: High.** The one that invalidates measurements rather than breaking a call.

`require_parameters: true` solves providers that *cannot* honour a parameter. It does nothing
about a provider that honours the schema perfectly and then reasons badly inside it.

Measured 2026-07-21 on `deepseek-v3.2`. The `movement` scenario, which had scored **7/7 across
three independent sweeps**, scored **0/7** — same commit, same prompt, same scenario. Two
prompt "fixes" were written and measured against that, both apparently making things worse.
Then a control run with the *untouched* prompt reproduced 0/7, and a rerun twenty minutes
later scored **7/7 again**. Nothing in the repository had changed. Only the routing had:

```
Baidu        7 run(s), clean 7/7, forbidden/run 0.00
Friendli     6 run(s), clean 3/6, forbidden/run 0.17
AtlasCloud   1 run(s), clean 0/1, forbidden/run 1.00
```

**Second instance, 2026-07-23, milder and worth contrasting.** On `name-reveal-large`, Baidu
filed the revealed name as a fact on 4 of 7 runs where DeepInfra did so on 0 of 7 — same
model, same prompt, same scenario. But Baidu emitted the correct `character_renamed` *as
well*, scoring 21/21 on required. That is a provider weighting a prohibition in the prompt
more weakly, not one reasoning badly, and the result is a redundant fact rather than a corrupt
entity. Recorded rather than added to `providerIgnore` — the exclude list should be reserved
for providers that produce wrong canon, or it will quietly narrow routing to nothing.

Also seen the same day, twice: the provider that reports **no name** returned deltas shaped
`{type, id, name}` and `{type, id, content}` — `response_format` ignored outright. A provider
that ignores the schema and a provider that omits its own identity appear to be the same
provider, which makes "unreported" a useful signal in its own right.

**Third instance, 2026-07-24 — and now a rule rather than an anecdote.** Adding one lore entry
to a scenario's context appeared to drop `lore-learned` from 14/14 to 8/14, which reads exactly
like context crowding. Pinned, on identical input: **Baidu 14/14, Venice 10/14.** The earlier
measurement had routed entirely to Baidu; a provider new to the mix was the whole difference.

That is four times — AtlasCloud, the world-size hypothesis, the name-reveal work, and this —
that the by-provider table has been the only thing between a routing artefact and a written-up
finding about our own code. Stated as a working rule:

> **No single routed sweep is evidence about a change.** A before/after comparison is only
> meaningful with the provider mix held fixed, which means `--providers` on both halves.

The by-provider table is not diagnostics. It is the control.

**Postscript, 2026-07-24: AtlasCloud was partly right about something.** Its signature failure
here — emitting a *building* as a `character_introduced` — was written up as a provider
reasoning badly. The `object-described` scenario now reproduces exactly that shape **7/7 on
DeepInfra**, a provider with a clean record: an object with no `Item` type to hold it becomes a
character, standing in the room, with a name and a description.

The provider was worse at hiding a pressure that was always there. That does not retract the
finding — AtlasCloud scored 0/21 on revelation and its exclusion stands — but it is a useful
correction: a failure that looks like provider noise can also be a domain-model gap that a bad
provider surfaces first.

AtlasCloud's output was **fully schema-valid** — it emitted a building as a
`character_introduced` under `characterId: "player"`. No schema and no request parameter can
catch a valid-but-wrong branch choice.

**What this costs us:** every extraction quality number recorded before this date, including
"100% across three n=7 sweeps", measured *the model as routed that afternoon*. Those are
point-in-time observations of a mix we did not choose and cannot reproduce, not properties of
the model.

**Mitigations:**
1. **The eval now records the serving provider on every run** and prints a per-provider
   breakdown. Any future "the model got worse" claim is checkable rather than plausible.
2. `--providers a,b` pins each upstream in turn (`provider.order` + `allow_fallbacks: false`)
   so providers can be sampled deliberately instead of waiting for routing to land on them.
   **Test instrument only — never used in the play path.**
3. `providerIgnore` on a role: exclude measured-bad providers while keeping every other one.
   Weaker than pinning on purpose — a proxy that ignores the parameter degrades to today's
   behaviour rather than failing.

**The process lesson, which is the more valuable half:** three consecutive confident
conclusions were drawn from provider noise, and the only thing that caught it was running a
control with the original prompt. *Re-measure the baseline before attributing any change to
your own edit.* This is the second time on this project that a conclusion has failed to
survive a repeat, and the first time it nearly cost a day of prompt tuning.

Automating this away is logged in TODO_FUTURE_WORK as provider calibration.

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

### Extraction reliability

**Severity:** High — the architecture rests on it. **Substantially answered by §9;** see the
verdict at the end of this section. The history below is kept because how the answer was
reached matters as much as the answer.

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

**§9 verdict, 2026-07-23 — 51 turns of real play.** The question this section was opened to
answer. 209 deltas applied, 8 rejected (0.16/turn), 9 no-ops, no corruption, no history/canon
desync, every `player_moved` landing in the right room.

Exactly one rejection lost information: two `fact_learned` on turn 23 referenced a fact that
was never established. Note that the dependency-tier sort cannot help there — nothing existed
in the batch to sort ahead of. The other seven are the validator doing its job.

**Extraction is reliable enough. The architecture stands.** The failures §9 found are gaps in
what the delta set can *express*, not in the model's ability to fill it — see *The fact store
absorbs everything the delta set cannot express* above.

**But the omission problem is confirmed, and it is worse in play than in the eval.** Across
51 turns: **zero `relationship_changed`.** Not one — through Hald ordering an armed man to
attack the player, through the player killing that man, through Hald being coerced into
service as a guide, and through him ending the session back in his own tavern having survived
it. Every character's standing is still exactly what it was seeded at.

The 2026-07-20 finding that a stronger model emits `relationship_changed` reliably held on a
*scenario built to provoke it*, where the shift is the point of the passage. It does not
survive contact with a long session, where standing moves by accumulation across many turns
and a per-turn extractor sees one turn at a time. This is now the strongest evidence for the
periodic reconciliation pass floated above, rather than more prompt work.

The contrast is instructive: `mood_changed` fired 46 times in 51 turns — healthy, and the
earlier concern about it is closed. Mood is a per-turn observable and extraction gets it.
Standing is not, and extraction never will, because the evidence is not in the window.

---

### Reasoning tokens are billed against `max_tokens`, and running out is silent

**Severity:** High — no error, and the symptom points at the wrong cause.

**Update 2026-08-04: it is worse than silent, and it happened in live play.** Turn 26 of a
session printed **4,682 characters of the narrator's chain of thought into the story** —
"Thinking Process: 1. Analyze the Player's Input..." — ending mid-sentence, followed by
`applied: nothing`.

```
content: null           reasoning: 4682 chars
completion_tokens: 1202 reasoning_tokens: 1200   finish_reason: "length"
```

Three faults compounded:

1. **`OpenRouterResponse.Content` fell back to the reasoning field without asking why content
   was missing.** The fallback is correct for a provider that misreports the payload and
   catastrophic for a model truncated mid-thought. `FinishReason` distinguishes them, was
   already parsed, and was already documented in that very file as "the signature of a
   reasoning model that spent its whole budget thinking" — it simply was not consulted. Now the
   fallback refuses when `finish_reason` is `length`.
2. **The diagnostic written for exactly this case missed by two tokens.** `DescribeEmptyContent`
   required `reasoning >= completion`; the live values were 1200 and 1202. Now proportional.
   *An exact-equality guard on a number a provider controls will eventually be off by one.*
3. **The narration budget was never survivable.** `maxTokens: 1200` with no reasoning
   configuration — and startup validation only rejects a role that configures reasoning
   *without* `requireParameters`, so a role configuring none passes. A healthy verification turn
   then spent **1960 reasoning tokens**, so the old ceiling could not have completed whenever
   the model chose to think hard. Raised to 4000; a ceiling costs nothing unless used.

**The generalisable part: a failure mode logged as quiet deserves a second look at what happens
when it meets a recovery path.** This entry predicted silence. Silence plus a fallback produced
noise, in the story, in front of the player.

Guarded by four `--selftest` checks in `ResponseSelfTest`.

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

### The fact store absorbs everything the delta set cannot express

**Severity: High.** Found in §9 play, 2026-07-23. One finding wearing three costumes.

`fact_established` is the only open-ended slot in the schema — arbitrary text, no structure to
violate — so whenever the model meets a limit in the closed delta set, that is where the
overflow goes. Three instances in a single 51-turn session:

- **A name reveal.** ~~No delta changes a character's name~~ — **fixed 2026-07-23** by
  `character_renamed`; see the resolved entry below. The figure introduced anonymously on turn
  14 was `"Shivering figure"` in canon permanently, while her real name lived in a fact
  (`figure-name-nessa`). The narrator read correctly *from the fact*, which is why the prose
  looked fine and only a canon audit found it. Two further facts
  (`figure-is-young-woman`, `figure-in-cistern-location`) carry what are properly character
  attributes, and those remain.
- **A lie.** Facts have no truth value and no attribution, so a claim and a truth are stored
  identically. `hald-claims-roof-leaking` shows the model inventing its own workaround — it
  wrote "claims" into the id and text because there was nowhere else to put it.
  `hald-looking-for-stray`, the same character's other lie, got no such treatment and is now
  simply false canon.
- **Blow-by-blow.** `drowned-follower-wounded-again` and `...-again-2` recorded individual sword
  strikes as permanent world truth, on a creature that died two turns later. The `-2` suffix is
  the model resolving its own id collision. The `status_changed` chain already carried the real
  state correctly.

**Why it matters beyond tidiness.** Facts are replayed into context and compete for budget, so
sediment crowds out substance — which links this directly to *Silent lore drops* below. And
false canon stored as true will eventually be narrated as true.

**Design consequence:** this is load-bearing for the queued lore-entry work. Adding a fourth
entity type without relieving the pressure just gives the overflow a new place to pool. The
underlying fix is to give the delta set the expressiveness the model keeps reaching for —
rename/identity reveal, attribution and truth value on facts.

**Fourth instance, 2026-07-24, and the prediction above was right.** With lore entries in
place, a character explaining an authored topic produced the correct
`fact_learned player <- cult-of-the-blind` *and* two new facts paraphrasing what the entry
already says (`shurus-drowned-father`, `blind-faith-fen`). The prompt rule explicitly says not
to. Required behaviour was bought; tidiness was not.

So the pressure survives a new entity type, exactly as predicted — the overflow simply pools
next to it. Whatever fixes this has to change what `fact_established` is *for*, not add
somewhere else for content to go.

**Read the workarounds as a specification.** A model routing around the schema is a more
reliable signal than an outright failure, precisely because it looks like success.

---

### The lore eval measures a shape that play does not produce

**Fixed 2026-08-04, same day.** Kept in Open rather than Resolved because the *lesson* is about
how scenarios are written, and applies to every one of them.

`lore-learned-implicit` now reproduces the play shape and scores **14/14** (12/12 on a second
provider), up from 0/14. Two halves were needed and neither worked alone:

- **Keys in the extraction context.** The extractor saw `(cult-of-the-blind) The Cult of the
  Blind` and nothing else, so "the Drowned Father took his tithe" was an unrelated string. It
  was not failing to make the connection — it had no evidence one existed. Keys are short
  authored strings, unlike bodies, which are withheld deliberately. **0/14 → 8/14.**
- **A prompt rule naming the situation**: a scene is usually about a topic without naming it,
  because everyone present already knows what they are discussing. **8/14 → 14/14.**

The original finding follows, because the reason it happened is worth more than the fix.

---

**Severity: High** — not because the code is wrong, but because the measurement says it is
right. Found by auditing a 51-turn session, 2026-08-04.

`lore-learned` scores **14/14**. Across a full session in which the Cult of the Blind was the
entire plot, **not one character learned it.**

The eval has Hald say *"There's an old faith out in the fen — the Blind, folk call them"* — a
topic named and taught. Play never does that. Characters spoke of the Drowned Father, the
weeping woman, the capstone and the century of drowned men; nobody named the cult as a subject
somebody could be told about. Extraction recognises learning only when the prose hands it a
labelled topic.

The lore was not ignored — the narrator used it throughout, and it leaked back into canon as
three facts paraphrasing what the entry already said. So the feature half-worked in exactly the
way the eval could not see.

**The generalisable part: a scenario written to provoke a behaviour will provoke it, and that
says nothing about whether the behaviour occurs.** Every scored scenario here is hand-written
prose aimed at a delta. This is the first case where a 14/14 was measuring a shape real play
never takes, and it will not be the last.

Two things worth carrying to every future scenario:

- **The eval inherited the feature's blind spot**, because the same person wrote both from the
  same intuition: that lore gets taught by being named. That is how you would explain a topic
  to somebody who had never heard of it — and exactly what characters inside the world never
  need to do. Play is the only thing that does not share the author's assumptions.
- **Withholding information has a cost invisible from inside.** Keys were never deliberately
  excluded from the extractor; *bodies* were, for good reasons, and keys were simply never
  considered separately. The reasoning was sound and its scope was never re-checked.

---

### The story could overwrite the player's identity

**Found and fixed 2026-08-04**, from live play.

Turn 38 emitted `character_renamed` on the player, replacing the name `"You"` with the literal
id string `"player"` and the description *"A traveller, recently arrived in Marrow"* with
*"burned, with blistered and stained hand from contact with the black water"*.

Both halves destructive, neither recoverable. And the injury was **already correctly recorded
on `status`** by the mood/status fix shipped the day before — so one event was written twice,
once into the field built for it and once over the player's identity.

Three rules, six self-tests: the story cannot rename the player; the player still can via
`/rename`; a name equal to the id is refused, since that is the model echoing the key back
rather than writing a name.

**The design decision worth keeping.** `/rename` routes through the same validator, so
protecting the player from the story would have blocked the player from themselves. Routing
authoring around the validator was rejected — a second way for the world to change is how two
paths start disagreeing about ids and collisions — so `Validate` gained an `authored` flag.
*One gate, and it knows who is knocking.*

**What it exposes beyond the bug:** the player's identity was a mutable field with no owner.
Any turn could overwrite it and one did. Protection is now in place; giving it structure is the
character-sheet feature, and a separate piece of work.

---

### Item status absorbs description — and it is the third instance of one pattern

**Found and fixed 2026-08-04**, in the first session played with items. Kept here because the
*pattern* is what matters and it will recur on the next entity type.

A rusted mooring ring is examined and turns out to be carved with the weeping woman. Extraction
emitted:

```
item_status_changed  rusted-mooring-ring = "carved with a weeping woman symbol,
                                            groove coated in black residue and old blood"
```

Status is condition — intact, broken, burned, ground to powder. This is a **permanent property
discovered**, which is what `item_renamed`'s optional description is for: the same shape as
"Shivering figure" becoming Nessa, one level down. Canon now carries a description of the ring's
appearance in a field meant for what has happened to it, and the real description sits unchanged
beside it.

**The pattern, now three times:**

| entity | field that absorbed | field that should have taken it |
|---|---|---|
| character | `mood` = "injured" | `status` |
| fact | fact text = a description | the entity's description |
| item | `status` = a carving | `description`, via `item_renamed` |

Every one is *what has happened to a thing* colliding with *what a thing is*. The distinction is
obvious in the type system and not in the prose, so the model resolves it by whichever field the
prompt described more vividly.

**And the cause is the same asymmetry as the mood/status bug.** That fix worked by giving status
an equal voice — "Status is the body, mood is the feeling, and they are different deltas". No
equivalent sentence exists for items: the prompt tells the model what an item *is* and when to
introduce one, and never distinguishes its status from its description. One field was explained
and the other was not.

**The scenario found something better than the theory.** `object-examined` scored 7/14, and the
dominant failure was not status-absorption at all — that happened once in seven. Seven times in
seven the model recorded **nothing**, because `item_renamed` reads as "the name changes" and
here the name does not.

So the fix was not only the missing symmetry sentence. It was telling the schema and the prompt
that revising a description while the name stays identical is a normal, expected use of that
delta — plus the direct rule that status is condition and a discovered property is description.
**7/14 → 14/14, forbidden 0.00**, full set 50/50 clean pinned.

Worth carrying: **a delta named for its most obvious use will not be reached for in its less
obvious one.** `item_renamed` handles two things and is named after one of them. The same is
true of `character_renamed`, and nobody has checked whether it is silently under-used for
description revisions.

---

### Mood absorbs status

**Found and fixed 2026-07-24.** Kept here rather than moved to Resolved, because the *cause*
generalises to every rule in the extraction prompt.

**The cause was an asymmetry, not a missing rule.** The schema described `status_changed`
perfectly well — "Physical or situational condition changed... **Not for emotions**" — but the
system prompt mentioned mood three times and status **zero** times, and the mood schema branch
actively recruits: "Emit this whenever the prose shows a shift in how a character feels,
however brief."

One field was defined; the other was campaigned for. The model reached for the one it had been
told to reach for.

The mood rule was itself added to fix a real problem — moods were being missed — and it created
this one by being the only voice in the room. **A rule that fixes one field can bias every
field it does not mention.** Worth checking the next time a prompt rule is added: what does it
now outweigh?

Fixed by giving status an equal voice, ending with the consequence in canon ("a character
beaten senseless whose status still reads normal is recorded as unhurt"), which is the shape
every durable rule in this prompt has. **0/7 → 7/7**, verified on the baseline's own provider,
full set 99% with no regression.

**The original measurement, for context.** Measured by `blow-landed`, 7/7 failure.

`Status` and `Mood` are documented as distinct — physical or situational condition versus
emotional register — and separated deliberately, because mood turns over constantly and status
rarely does. The model does not reliably respect the split.

Hald is driven into his own counter, struck above the ear with a blade, and goes down bleeding.
`status_changed` **never fires**. What comes back every run is:

```
mood_changed  innkeeper-hald = injured
```

The §9 audit saw the edge of this without naming it: `guard-tomas` ended the session with mood
`startled` and status `terrified`, `drinker-mabb` with mood `terrified` and status `drunk`.
Terror is in both fields on different characters.

Consequences: a wounded character reads as merely upset, `status` stays at whatever it was
seeded with, and anything that later keys off physical condition — dice checks, death, healing
— has nothing to read.

Worth noting the same scenario scored `forbidden` 0.00, so the fact-store theory this was found
under was wrong about combat: the defect is the field, not the fact.

---

### Time of day, and other re-framings, spawn duplicate locations

**Severity:** Medium. Found in §9 play, 2026-07-23.

Turn 29 introduced `marrow-square-night` ("Marrow Square (night)") as a location distinct from
`marrow-square`, with a description carrying events rather than permanent character: *"the
aftermath of a fight lingers: the smoldering corpse of Hald's companion."* Turn 30 then moved
the player to `marrow-square`, correctly — the model caught itself, and canon is left with an
orphan location nobody ever entered.

Self-correction meant no visible damage this time. It will not always. Two entities for one
place splits characters, connections and facts across both.

Same underlying question as *buildings mentioned in prose are not locations* in
`TODO_FUTURE_WORK.md`: when does a described thing become an entity — and, the half not yet
asked, when is it the same entity in different clothes.

---

### Two delta kinds move the player

**Severity:** Low — asymmetric by construction, harmless so far.

Turn 47 moved the player with `character_moved` and `characterId: "player"` instead of
`player_moved`. It applied correctly: `world.Player` and `FindCharacter("player")` return the
same object, which is the payoff of treating the player as an ordinary character.

But the duplicate-detection keys differ — `moved:player:X` vs `player-moved:X` — so a batch
emitting both slips past the check, and any validator rule written against one kind is
bypassable through the other. Either normalise `character_moved` on the player id into
`PlayerMoved` before validation, or make the dedup key identical for both.

---

### Player-authored canon is unvalidated and permanent

**Severity:** Low. Found in §9 play, 2026-07-23.

`/fact`, `/place` and `/character` text is stored exactly as typed and reaches the narrator as
authoritative prose. From the session: *"Cult of the Blind is an encient cult worhiping an old
god Shurus. A violent and dakr god, aso is its members."*

No harm observed — the narrator read through it without trouble. Recorded because the input
path has no validation and the output is permanent world truth. An optional cleanup pass is the
obvious answer; the risk to weigh is a cleanup step quietly changing an author's meaning.

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

### The world model could only build dead ends

**Found 2026-08-13 in a 150-turn `ashfall` run; fixed the same day.**

Nothing in the delta set could connect two locations. `LocationIntroduced` carries id, name and
description, and no other kind touched `Location.Connections`, so **every location extraction
ever created was an orphan** — 33 of them across nine saves, in both worlds, human- and
model-played. The only places with exits were the two from a hand-written seed.

It survived that long because the symptom is invisible until a player walks somewhere they
cannot narratively wander back out of. Then it is total: the player reached a maintenance shaft
on turn 80 and the last 25 turns produced 4 deltas, 23 of 25 turns changing nothing.

**It reads as the narrator looping and is not.** `ContextAssembler` renders "Leads to:" from
that set, so the narrator was told the player stood in a sealed room and narrated exactly that,
correctly, for seventy turns. Canon was telling the truth about a broken world.

**The lesson is about where to look, not about connections.** A field that is written by a seed
and never by play looks populated in every hand-made test and is empty in every real world. The
general form: *any field only the seed writes is a field play cannot maintain* — worth checking
the rest of the domain model against.

Fixed by deriving edges from movement in `DeltaApplier.Connect`, adding no delta kind.

---

### Running the eval honestly — five rules paid for in wrong conclusions

Moved here from `TODO_FUTURE_WORK.md` 2026-08-13. They had sat as unchecked boxes for weeks,
which was the wrong shape: they are not things to do, they are things that are true every time
the eval runs, and they were filed among items nobody intended to build. Each one was learned
by getting it wrong first.

**Re-run before trusting any extraction change, and before changing the extraction model.**
Provider routing drifts under the same model id, so the eval measures the model *as actually
served*, and that can move between one sweep and the next.

**Always read the per-provider breakdown, not just the headline.** A verified sweep showed
`forbidden 0.02` — traceable to exactly one run served by Google, against StreamLake 53/53.
Without that line it is an unexplained rounding error and the prompt becomes the suspect.
Google is flagged, not excluded: n=1 is the evidence threshold that produced three wrong
conclusions in a single day.

**Sample size is per provider, not per sweep.** A 56-call sweep that lands 53 times on one
upstream has n=1 or 2 everywhere else, so the headline is really a measurement of whichever
provider won the routing.

**World size is a variable, and the scored set barely tests it.** Every hand-written scenario
ran against two locations and one fact until `WorldSeeds.Marrow_Late` existed. A real session
had seven locations, six characters, forty-four facts and a 10,000-character context.
Identical prose scored **14/14** in the small world and **2/14** in the large one. Any scenario
worth scoring is worth running at both sizes.

**n=7 was not enough to be safe.** A movement failure looked solid at n=7 and did not
reproduce. For anything close, prefer three independent sweeps over one larger one — the
cross-run spread is the signal, not a single average.

The related rules that are *architectural* rather than operational — score outcomes not routes,
a measurement without a provider name is not a measurement, a schema branch is not free — live
in `PROJECT.md` §3.

---

## Resolved

### A character could not be renamed

**Resolved 2026-07-23** by the `character_renamed` delta and the `/rename` command.

Found in §9: a character introduced anonymously kept that name forever, so the figure from
turn 14 was still `"Shivering figure"` at turn 51 while the prose had called her Nessa since
turn 15. The extractor had stored her real name as a *fact*, which is why narration read
correctly and only a canon audit found it.

**Ids are opaque, names are mutable.** The id never changes, so every existing reference
survives a reveal. `Entity.Name` was already `{ get; set; }`, documented "May change; the id
may not" — the domain model was built for this and only the delta set had not caught up.

`name-reveal` scores 21/21 required, 0 forbidden, clean 7/7, and the extractor emitted the
rename against the correct existing id on every run in every configuration. Full scored set
moves to **100% across 9 scenarios**.

**Two things worth keeping, both about measurement rather than the feature:**

- **The `two-stage-entry` scoring bug, written again.** The first forbidden rule flagged any
  fact mentioning the revealed name and fired 5/7 on `sera-knows-player: "Sera Voight knows
  who the player is"` — a legitimate fact that merely refers to her. A rule must target the
  workaround, not every sentence the right answer appears in. Having recorded the lesson eight
  days earlier did not prevent the repeat; reading the deltas behind a failing rule did.
- **World size was confounded with provider, again.** Routed, the large-world variant scored
  5/7 forbidden against the small world's 0/7 — which looks exactly like the world-size effect
  `two-stage-entry-large` genuinely has. Pinned to DeepInfra it is 0/7; pinned to Baidu, 4/7.
  The small run had gone entirely to DeepInfra. See the provider-variance entry above: the
  by-provider table printed on every run is the only reason this did not become a finding.

---

### Adding a field can change a delta's dependency tier

**Found and fixed 2026-08-04**, one day after `source` was added. A second instance of the
resolved ordering bug below, from a cause that entry did not anticipate.

`FactEstablished` sat in tier 0 — "depends on nothing else in the batch" — which was true until
`SourceId` gave it a reference to a character. `CharacterIntroduced` is tier 1, so a fact was
always judged before any character the same batch introduced:

```
character_introduced  older-man-square       applied
fact_established      well-sealed-air-smell  REJECTED: source is not a character
fact_learned x4                              REJECTED: cascade
```

A stranger walking in and saying something is the commonest scene in the game. **Sixteen of
twenty-three rejections in one session** were this single mis-tiering.

**The guard was a comment, and comments cannot fail a build.** Nothing connects "this delta
gained a field referencing another entity" to "its tier must move". The tiers are now written to
state what each level may reference, and a self-test covers the shape that broke.

**Check the tier whenever a delta gains a reference to another entity.** That is the rule; there
is no mechanism enforcing it.

Also worth noting how it was found: fifty turns of *directed* model play hit it within six
turns, while the scored eval never could — no scenario introduces a character and quotes them in
the same turn.

---

### The validator rejected correct deltas because of their order

**Resolved 2026-07-21.** Ours, not the model's — and it looked exactly like a model failure
for most of a day.

`DeltaValidator` checked each delta against canon plus everything accepted *earlier in the same
batch*, walking the batch in the order the model emitted it. That silently assumed the model
emits in dependency order. It does not:

```
REJECTED player_moved        -> old-mill
ok       location_introduced old-mill (Old mill)
```

Correct and complete output, in an order we did not expect. The move was rejected for naming a
location that "did not exist", which was then accepted one line later — so walking into a new
place recorded nothing at all. It measured 0/7 on `player-arrival`, and a prompt rule written
to make the model sort its own output did not fix it, because the ordering was never the
model's problem to solve.

**Fix:** sort into dependency tiers before validating — locations and facts, then characters
(which may be introduced into a new location), then everything referencing existing entities.
`OrderBy` is stable so within-tier order is preserved, and the cascade is unaffected: later
tiers see only what earlier ones *accepted*, so a rejected introduction still poisons
everything referencing it.

**Two lessons worth keeping:**

- **The tell was a metric kept for another purpose.** `rejects 0.33/run` over 21 runs was 7
  rejections against exactly 7 `player-arrival` runs. The required/forbidden score could never
  have surfaced this, because required is scored *after* validation and so only ever sees what
  survived. `--show-deltas`, printing the raw proposal beside the verdict, is what made it
  visible.
- **Do not ask a model to work around your own bug.** The instinct to add a prompt rule
  ("emit the introduction first") was treating a deterministic defect in our code as a
  behavioural quirk to be coaxed. It was also fragile in principle and ineffective in practice.

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

### A pack could define a character nothing could reach

**Found 2026-08-06**, by walking the load path aloud rather than by a failing session — the
same way the narration-history gap above was found, and worth noting twice.

`WorldPack.ApplySheets` created a character from a sheet with no `seed.json` entry, placing
them offstage (`LocationId = null`). Decision 4 of the sheets design, intended to let an author
write a cast before deciding where anyone stands.

Every route to that character was shut:

- the **narrator** never sees them — `AppendNpcs` filters on the player's location, and someone
  nowhere is in no location
- the **player** cannot summon them by name — *mention never creates or moves an entity*,
  measured 0/7 and consistent across 21 runs, and a deliberate rule for NPC speech
- `/character` cannot place them — it only introduces, and `AskId` refuses an id already in
  canon
- the **extractor** sees only the bare slug, because `AppendKnownIds` lists every character id
  with no name and no description attached

So the pack loaded, the character existed in canon, and they could never appear. A silent drop
with no symptom — exactly what `RequirePlayer` and `RejectUnresolvedReferences` exist to
prevent, in a case neither of them covered.

The lesson is narrower than "validate more". Both `/character` and a sheet can leave a
character nowhere, and only one of them is a bug: the player who invents a brother back home
remembers him, and an author who forgot a seat has no memory and no symptom. **A permissive
rule inherited from one authoring path was applied to another where nothing could recover
from it.** Reversed to a load-time refusal — see §9.1 of the character-sheets design.

Found alongside it: nothing enforces the kebab-case id convention, so `warrior_mike.md`
referenced as `warrior-mike` would produce exactly this failure with a one-glyph diff.

### A degraded provider corrupts a regression check — the fifth sighting

**2026-08-06.** The scored set was re-run after adding `Location.Status` and came back dirty:
`movement` 0/2, `new-character` 1/5, four calls timing out at 45s. It reads exactly like a
regression from the change.

It was not. Running the **same three scenarios against HEAD in a worktree** — the build without
the change — produced the same failures and *more* timeouts. Routed traffic landed on the same
upstream, so there was nowhere healthy to compare against.

The previous four sightings were all the same shape: OpenRouter routing one model id across
upstreams of differing quality, and a score moving because the mix moved. **This one is
different and the pin does not help.** A single pinned upstream degrading in *latency* turns
healthy runs into timeouts, and a timeout scores as every required delta missing. The failure
mode of the infrastructure imitates the failure mode of a bad change.

Two things caught it, and only the second was decisive:

- `forbidden` was **0.00 in every run of every build**. Missing deltas without any wrong ones
  is what a dead call looks like, not what a broken schema looks like
- **the baseline was re-run under the same conditions.** This is the only real answer. A
  before-number recorded on a different day is not a control, because the thing that changed
  in between may be the provider

So the rule gains a clause. *No single routed sweep is evidence about a change* becomes: pin
the provider, **check the error count before reading the score**, and when a sweep looks bad,
re-run the previous build now rather than trusting a number from last week.

Recorded as unfinished: the scored set still needs a clean run on a healthy provider, and the
50/50 baseline needs re-establishing with it.

### The same provider, 0% and 100% — the sixth sighting, and the worst

**2026-08-12.** `movement` — the plainest scenario in the scored set, "the player walks out to
the square" — scored **0–1 of 5** with timeouts on a third to three-quarters of calls. It was
about to be treated as a real extraction failure and fixed with a prompt rule.

It is not broken. Same model id, same prompt, same scenario:

| provider | movement |
|---|---|
| DeepInfra | 0–1 of 5, plus 4–6 timeouts per 8 |
| **StreamLake** | **8/8** at 60 tokens a call |

And the full scored set, which had never had a clean run all day: **50/50 on StreamLake,
required 100%, forbidden 0.00, rejects 0.00.** Every scenario that looked broken —
`new-character` 1/5, `hostility` 5/10, `two-stage-entry` 8/10 — was clean.

**What the bad provider actually does**, visible only by reading deltas rather than scores:

```
mood_changed  innkeeper-hald = guarded
mood_changed  drinker-mabb   = maudlin
mood_changed  player         = neutral
REJECTED mood_changed player = neutral      <- its own duplicate
```

Degenerate repetition. It sprays moods at everyone present, restates values canon already
holds, repeats until the validator rejects the copies, and never reports the movement. That
also explains why timeouts clustered on this one scenario all day: it has the least to extract,
so the most room to pad.

**Three things this cost, and they are the lesson:**

- **A prompt rule was written against the padding, then deleted.** HEAD scores 8/8 on a healthy
  provider without it. The rule fixed a problem that does not exist, and would have been
  committed as a permanent instruction on the strength of one sick upstream.
- **A wrong explanation was offered for `hostility`** — that its standing rule failing 5/5 was
  long-standing, consistent with `relationship_changed` never firing in 102 turns of play. It
  scores 10/10 on StreamLake. An explanation invented for a symptom that was infrastructure.
  The "relationship_changed never fires" note now needs re-checking before it is trusted again.
- **"When did it break?" is unanswerable.** The recorded 50/50 baseline does not say which
  provider produced it. It may have been failing on DeepInfra for weeks.

**The rule, restated:** *a measurement without a provider name attached is not a measurement.*
Both "movement is broken" and "movement was always fine" were true today, and only the missing
provider name made that possible.

**And the practical defence already exists** — `providerIgnore` in settings, deliberately an
exclude list rather than a pin, so routing keeps every remaining upstream and one bad host
cannot become a single point of failure.

**What the architecture got right:** none of this corrupted canon. Every run today, at its
worst, scored `forbidden 0.00`. A degraded provider produces *missing* deltas, not wrong ones,
and the validator rejects the garbage. The 51-turn session played on this and canon held.
