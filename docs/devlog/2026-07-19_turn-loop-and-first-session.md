# Devlog — turn loop, first play session, and four fixes

**Date:** 2026-07-19
**Scope:** TODO_BOOTSTRAP §7, most of §8

---

## The turn loop (§7)

Assemble context → narrate → extract → validate → commit → record.

Built before §6 (storage) on purpose. Storage has no unknowns in it; the turn loop does,
and building it first meant the save format would not freeze around a domain model the turn
loop was still reshaping. That paid off immediately — the player-as-`Character` change and
the context split both landed after §5 was "done".

`InMemoryWorldRepository` was written *before* the JSON one so that "the interface leaks no
storage detail" is verified by a second implementation rather than self-certified. Writing
the JSON one first would have shaped the interface around it.

The loop lives in `Core` and reaches models through `INarrator` / `IStateExtractor`, which
`Core` defines and `Llm` implements. `Core` still references nothing.

**Narration is shown regardless of what extraction does.** Extraction failing is a canon
problem, not a storytelling one; discarding good prose because a second model could not
parse it converts a silent bookkeeping error into a visibly broken game. `TurnOutcome`
keeps "extraction died" distinct from "extraction ran and its output was rejected".

**Rejections cascade.** Validation walks the batch in order against canon plus what has
been accepted so far, so a batch may introduce a character then move them — but if the
introduction is rejected, the move goes too. Otherwise rejecting a `fact_established` would
strand the `fact_learned` depending on it, which is the exact failure being prevented.

## First play session — four turns

**The premise held.** Asked about a fact only Hald knew, Mabb did not know it, and the
narration made that a character beat: "his addled mind offers no answer". Hald, who does
know it, delivered it. Per-character knowledge is doing what it exists to do.

Four problems. Two were mine.

### 1. The narrator wrote an internal id into the prose

> "The heavy oak door of the marrow-tavern flies outward."

`ContextAssembler` listed exits as bare ids, and the narrator read them as names. Ids were
added to context to help the *extractor*; that the narrator would echo them never came up.

Fixed by splitting the assembler in two: `ForNarration` (names only, exits resolved to
names, no ids anywhere) and `ForExtraction` (ids beside every name, plus the known-id
roster). The two roles want opposite things from the same state and one rendering cannot
serve both. Verified offline via a new `/prose` command — no API call needed to check that
a view contains no ids.

### 2. A character did not know the fact he had just disclosed

The extractor prompt said: *"The speaker usually already knew it, so they need no
fact_learned."* True about the fiction, wrong about the bookkeeping. Canon contains only
what gets written down, so Hald stated his own secret and was recorded as not knowing it —
free to contradict himself later.

Now requires `fact_learned` for the speaker too, with the reasoning spelled out in the
prompt so it does not get "simplified" back later.

### 3. Padding

One batch contained the same `location_introduced` **three times**, each with a different
evidence quote, plus re-establishment of two known facts. Now deduplicated on semantic
identity — ignoring evidence, since the differing quotes mean record equality would not
have caught it. Duplicates surface as rejections rather than disappearing.

### 4. No-ops counted as successes

"player learned well-boarded" reported as applied on a turn where the player already knew
it. `ValidationOutcome` now returns three categories instead of two. This one is about
measurement: merging "changed something" with "restated something" would inflate every
quality number taken over a long session by the model repeating itself.

## Still unsolved: omissions

No `mood_changed` for Mabb through an obvious slide into maudlin self-pity. No
`relationship_changed` for Hald across two turns of escalating hostility — he shut the
subject down and remains at standing −10.

Nothing detects a delta that was never emitted, and no validator can. Candidates if it
proves systematic: a periodic reconciliation pass comparing canon against recent narration,
or making a few high-value fields required per turn so the model must state them even when
unchanged. Both cost tokens. Not worth choosing on a four-turn sample.

## Next

§6 — JSON storage, atomic writes, and load/save wired into the harness. The domain model
has now been through a real session, which is the condition I wanted before freezing a
save format.
