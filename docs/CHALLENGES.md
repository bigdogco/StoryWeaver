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
