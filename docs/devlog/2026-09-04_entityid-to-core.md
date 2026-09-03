# 2026-09-04 — EntityId moves, and the check finally runs on real saves

Third and smallest piece of Phase 2. Still no Avalonia.

## Deferred twice, on purpose, with a trigger

`Authoring.Slug` (Core) makes ids. `EntityId.IsWellFormed` (Storage) judges them. `Core`
references nothing, so the producer could not see the checker — two halves of one convention in
two projects, bridged only by a self-test.

It was deferred on 2026-09-02 with a stated trigger: **revisit when a second Core caller
needs to validate an id.** `CanonRefresh` became that caller two days later — a save hand-edited
to `Warrior_Mike` is exactly what a reload should catch, and it could not.

Waiting was still right. Moving it inside the boundary refactor would have buried a structural
change in a task about something else, and until `CanonRefresh` existed the case was
hypothetical. **A trigger that fires within two days is a trigger that worked**, not one set so far out
that it never fires.

## Measured before building, and the measurement answered an older question

`EntityId`'s own docs left something open since July:

> Whether ids *proposed by extraction* should be held to the same shape is a separate question
> with a different cost.

That matters here, because if extraction had ever emitted an id the checker rejects, the new
warning would fire on every reload of every real save — and a check that cries wolf gets
ignored, which is worse than no check. That trap has already been paid for once, in row 4 of the
narration audit.

**549 ids across all 11 saves. Zero malformed.**

So the warning is silent on real play, and the July question has an answer with evidence behind
it rather than a guess: extraction already produces well-formed ids, over eleven playthroughs,
without ever having been told to. Recorded in `EntityId`'s own docs, where the question was
asked.

## Refused at load, reported in canon

The same rule now has two postures, and the split is the interesting part.

**Pack loading throws.** A pack is content being brought in, a mistyped filename is a mistyped
id, and it is refused before a session starts.

**A reload warns.** Canon is the player's own file. Refusing to load someone's edit because an
id looks wrong would be the validator's suspicion of a cheap model pointed at a person, which
§3 already says is the wrong posture.

Same check, opposite consequences, decided by whose file it is.

## The result that was not asked for

Verifying this needed the real code path rather than a script, so three real saves were copied
and reloaded through `/reload` itself: `marrow` at 230 turns, `marrow-old` at 51, `ashfall` at
250. **229 entities, zero CHECK lines.**

That is the first time any of the seven invariants have run against real long-run canon rather
than hand-built fixtures — they were written yesterday and tested only against worlds built to
break them. Nothing fires: no dangling locations, no items in two places, no keys disagreeing
with their ids, no malformed ids, across three of the longest sessions this project has.

**Two hundred and fifty turns of a cheap model writing structured deltas leaves canon
structurally clean.** That is worth more than the refactor that surfaced it, and it is the sort
of thing the audit command was declined for not being able to prove cheaply.

## Measurements

`dotnet build` clean, 0 warnings. Self-tests **108 pass, 0 fail**, up from 107. Zero API calls —
nothing here touches a model.
