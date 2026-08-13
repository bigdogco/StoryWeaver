# Design — dice-resolved checks

**Status:** design, no code. Raised 2026-07-23 as thinking-out-loud; moved out of
`TODO_FUTURE_WORK.md` 2026-08-13 because the reasoning outgrew a backlog entry.

Resolve uncertain actions with a roll the narrator is **told about**, rather than letting it
decide.

**Where this sits now.** Under the base/plugin split in `PROJECT.md` §2, dice are not part of
the base game — they are the archetypal plugin, and probably the first one designed from
scratch rather than extracted. So this document describes a *system*, and Phase 3 decides how
a system is expressed at all. Both halves have to exist before either ships.

---

## 1. Why it fits the architecture

Canon is the source of truth and prose is a rendering of it. **A die roll is canon** — a fact
the model cannot argue with. The loop becomes:

```
1. roll      code. deterministic, auditable, no API call
2. narrate   the LLM renders the verdict as prose
3. extract   as today
```

**The rule the whole thing hinges on:** the roll happens *before* narration, and the narrator
is told the outcome, never asked to decide it. The moment the model decides who wins, the
answer is whatever it felt like — which is the chat-log-as-state failure this project exists
to avoid.

It also settles a tension already in the code. `LlmNarrator.SystemPrompt` carries "do not
resolve the encounter for them", a rule added because the narrator kept deciding outcomes.
Today the answer to "what happens?" is the model's taste; dice make it a fact.

**Cost is ~zero** — no extra call, just another line in the narration prompt.

## 2. Build it as a *check*, not a combat system

The mechanic is really "an uncertain action with an outcome": picking a lock, lying to a
guard, crossing the bog at night, persuading Hald. Combat is one case. A general check gets
far more for the same work and avoids a combat subsystem sitting awkwardly beside everything
else. Opposed rolls work for all of it.

## 3. Prerequisites and hazards

- **No stats exist.** `Character` has description, location, status, mood, knows,
  relationship — nothing to roll against. Lightest version: one number per character, or a
  per-check difficulty the world author sets.
- **`Status` is the only mechanical hook**, and `"wounded"` is already a natural consequence.
  Probably enough for v1. HP is a bigger commitment, easy to add later.
- **Double-counting is the real hazard.** If code applies "player is wounded" *and* extraction
  reads the prose and emits `StatusChanged(wounded)`, two sources are writing one fact —
  precisely the disagreement the canon store exists to prevent. Roll consequences must be
  applied by code as deltas, with extraction told not to re-derive them.

## 4. The upside hiding in it

**"Did the narration contradict the dice?" is objectively checkable** — the first property of
*narration* that could be evaluated. Prose quality is taste and unscoreable, which is why
reroll is currently its only quality control. A verdict gives a hook.

Worth reading alongside `NARRATION_EVAL.md`, which reaches the same wall from the other side.

## 5. Open questions, to settle before any code

1. **What happens when you lose?** Death, capture, a wound that persists? Combat without
   stakes is prose with extra steps, and the answer shapes the domain model more than the dice
   do.
2. **Who sets the difficulty** — the world author, or the LLM proposing a target number that
   code then rolls against? The second is more flexible and much less predictable.
3. **Does a world ship its own rules?** If checks are data — what is rolled, against what,
   what the outcomes are — a pack could carry them. That sits with "narration style belongs to
   the world author" and "prompts as editable files": all three are the same move of pulling
   authored content out of `const string`s and into world data.

   Worth resisting the urge to design a rules *language* early. The likely path is a small
   declarative block covering a handful of check types, generalised only once several real
   worlds want something it cannot express.

## 6. Sequencing

After a long session, and after Phase 3 says what a plugin is. Not because it is risky, but
because playing is what tells you how a check should feel in this game, and it touches the
domain model — the one place where guessing is expensive.
