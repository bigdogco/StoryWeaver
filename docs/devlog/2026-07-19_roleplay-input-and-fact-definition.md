# Devlog — roleplay input convention and what counts as a fact

**Date:** 2026-07-19
**Scope:** player input handling, extraction quality, findings from a second play session

---

## Roleplay input convention

Adopted the `*action*` / speech convention for **player input**:

```
*I lean on the counter.* What do you know about the well?
```

The initial discussion was about narrator *output* formatting, and the answer there is
"later" — because narration is a rendering of canon rather than the state itself, the
convention can change at any point without breaking anything. That is not true of
chat-log-as-state tools, where changing it leaves history permanently mixed and the model
reads that history back.

Input is a different question and belongs now. It changes what both models receive, and
adopting it after the section 9 validation session would have made that session evidence
for a style we were no longer using.

Why it is better roleplay: "ask Hald about the well" is an instruction. The convention
supplies intent, body language, and exact words, so the narrator does not have to invent
the player's personality — which is where these systems usually go wrong.

Narrator prompt changes:

- Asterisks are actions, everything else is speech, both authoritative and already happened.
- Never rewrite, paraphrase, or echo the player's dialogue back at them.
- Never invent words, actions, thoughts, or feelings for the player.
- They write "I"; the narrator answers with "you".
- Plain instructions still work.

## The hole this closed

Extraction previously received only the narration. When the player writes an action the
narrator does not restate — handing over an object, revealing something to an NPC — it
happened, but canon never heard about it. Information is the worst case: telling an NPC a
secret is a fact that NPC now knows, silently lost if the prose only shows them reacting.

`IStateExtractor` now takes the player input as a separate authoritative source.

## Narration length: changed, then reverted

Cut to "one or two short paragraphs" on the theory that long narration steals the player's
turn. Reverted — too thin in practice.

The agency rules that arrived alongside it were kept, and they are the part that actually
works: stop where the player would naturally act, leave the scene open, never resolve the
encounter, never ask "what do you do?". A long paragraph ending mid-moment leaves more room
to roleplay than a short one that wraps everything up.

The deeper point, now in TODO_FUTURE_WORK: length is a taste call belonging to whoever
builds the world, and the narrator prompt should eventually be **data, not a `const
string`**. Tone, register, point of view, and verbosity are all world-authoring parameters.
Left hardcoded for bootstrap because there is no world-authoring format to hang it off yet,
and inventing one to hold a single setting would be the wrong order.

## Second session: the fix that caused a new failure

Five turns. The earlier fixes held — no id in the prose, no echoed dialogue, no-op
detection firing correctly, clean movement, and typos and malformed markup (`8I say`)
handled without trouble.

Then this:

```
fact player-asked-about-well-rumor: The player asked Hald and Mabb
     if they have heard a rumor about the well.
innkeeper-hald learned player-asked-about-well-rumor
drinker-mabb learned player-asked-about-well-rumor
```

A conversation promoted to permanent world truth, with two characters now knowing it.
Caused directly by the previous fix: extraction had been told "anything the player told a
character is a fact that character now knows", and it applied that to questions.

Over a long session this mints junk facts at conversation rate, each replayed into context
forever, crowding out real ones — in the one field that is supposed to make NPCs feel
simulated.

Fixed by defining what a fact *is*, with a test the model can actually apply: **would it
still be true if nobody had ever mentioned it?** Plus an explicit never-list (questions,
refusals, greetings, purchases, moods, "a conversation happened") and explicit permission
to establish nothing, since a model that feels obliged to report something is how this
happens.

Not fixed with a validator rule. Matching text like "The player asked" is brittle and would
miss the general case; this is a definition problem, not a syntax one.

**The lesson is the pattern, not the instance:** the fix for a silent omission produced a
silent over-production. Loosening an extraction rule needs watching in both directions.

## Omissions now look systematic

Nine turns across two sessions, **zero** `relationship_changed` — through an innkeeper who
was cold throughout, turned his back, and twice shut a subject down. Still sitting at his
seeded −10. Mood is reported, but patchily.

Hypothesis: relationships are the worst case because they are the least discrete. A move or
a revealed secret is an event; standing shifts by accumulation across a scene, and a
per-turn extractor only ever sees one turn. If that holds, relationship drift may belong in
a periodic reconciliation pass over several turns rather than in per-turn extraction at all.

## Domain gaps found by playing

Logged, not built — adding them now would mean tuning two things at once with no way to
attribute cause:

- **Items and inventory do not exist.** Buying a beer correctly produced no deltas, because
  there is nothing to report against. A missing concept, not an extraction failure.
- **Mentioned buildings are not locations.** A stranger kicked open a door that has no id
  and cannot be entered. The general form is "when does a mentioned thing become a real
  entity", which is the lazy-expansion question.

## Next

Another session to see whether the fact definition took, then section 6 (JSON storage).
