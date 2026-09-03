# Design — who owns canon, and how it is allowed to change

**Status:** §4 and §5 **built 2026-09-04** — see `todo/TODO_STORY_SESSION.md`. §5's
second-tier delta proposal is still undecided; §6's session-lifecycle gap is still open.
**Written:** 2026-09-04, entering the UI half of Phase 2.

Started as a discussion about UI separation and turned into one subject with three layers. The
question *"can we swap the UI without touching the engine?"* leads to *"who owns `WorldState`?"*,
and the answer to that turns out to also answer *"what is allowed to change canon?"*

---

## 1. The framing that started it

> The CLI is one UI, and we will have another windowed UI made.

That is a better framing than `PROJECT.md` had, and it is the one to design against: **the
console is client one, the window is client two, and neither is privileged.**

It holds for `PlaySession`. It does **not** hold for the CLI *project*, which is roughly
two-thirds eval scaffolding and self-tests — instrumentation, not a client, and it should never
migrate toward Core. Worth stating plainly so nobody later reads "the CLI is a client" and starts
relocating `EvalScenarios`.

---

## 2. Who owned `WorldState` — the finding this all came from

**A local variable in the console** — as it stood on 2026-09-04, before §4 was built:

```csharp
WorldState world = loaded ?? pack.Seed ?? WorldSeeds.Marrow();
```

Nothing else holds one:

| | holds a `WorldState`? |
|---|---|
| `JsonWorldRepository` | **No.** Deserializes fresh on every load, writes on every save. Stateless. |
| `TurnEngine` | **No.** Holds narrator, extractor, repository, lore, sheets, scenario — takes the world *per call*. |
| `DeltaApplier`, `DeltaValidator`, `CanonRefresh` | No. Static, world passed in. |
| `InMemoryWorldRepository` | Yes, and deliberately hands back the same graph rather than a copy — a test double. |

So the most important object in the domain is owned by a local in the outermost method of a UI
client. **That contradicts the rule locked on 2026-09-02** — *a UI is a thin layer, never a
driver* — on the object rather than on the rules. The window would have to own canon its own way,
and then two clients own it differently.

---

## 3. Why that is also a correctness problem

A turn looks like this:

```
1. read world  → build narration + extraction context     (instant)
2. await       load recent history                        (I/O)
3. await       narrate                                    (10–40s)
4. await       extract                                    (5–20s)
5.             validate against world
6. MUTATE      TurnNumber++, Apply(deltas), TouchPresent
7. await       save canon, append turn record
```

**Between reading canon at step 1 and mutating it at step 6 there is 20–60 seconds of network.**

In the console that window is unreachable: `Console.ReadLine` is not running, so nothing else can
happen. In an event-driven window it is simply time, with buttons on screen.

**The concrete race, using the feature shipped 2026-09-02.** The player edits `canon.json` and
presses Update State while narration is streaming. `/reload` returns a *new* `WorldState` and the
session swaps its reference — but the in-flight turn captured the **old** object at step 1,
mutates it at step 6, and saves it at step 7. The reload is discarded and pre-edit canon is
written back. That is the bug Update State exists to fix, reintroduced through a different door,
and Update State cannot fix it because Update State caused it.

Siblings: two turns submitted at once (both validated against pre-first-turn canon), a panel
editing a character mid-turn (the model never saw the edit), reroll fired during a turn (two
writers to history).

**The console structurally cannot reveal any of this.** It arrives all at once with client two
and looks like "the UI is flaky."

### The race has nowhere to live, which is the real finding

There is no object whose job is *canon for this session*. So there is nowhere to put a guard:
`/reload` swaps a reference, a turn mutates the object that reference used to point at, and the
only thing connecting them is a local in a method neither knows about.

**You cannot enforce one-writer-at-a-time without something that owns the thing being written.**
The concurrency question and the ownership question are the same question.

---

## 4. `StorySession` — **built 2026-09-04**

Lives in Core. Owns canon, and is the only thing that can change it. The sketch below is what
was built, with the three open questions answered underneath it.

```csharp
public sealed class StorySession : IDisposable
{
    private readonly SemaphoreSlim _oneWriter = new(1, 1);
    private WorldState _world;          // the local that used to live in PlaySession

    public string SaveId { get; }
    public string PackId { get; }
    public WorldState World => _world;  // reads: /state, /prose, a UI panel
    public bool IsBusy { get; }

    Task<SessionResult<TurnOutcome>>       TakeTurnAsync(string input, ct);
    Task<SessionResult<TurnOutcome>>       ReExtractLastAsync(ct);
    Task<SessionResult<TurnOutcome>>       RerollLastAsync(ct);
    Task<SessionResult<RefreshReport>>     UpdateStateAsync(ct);   // swaps _world inside the guard
    Task<SessionResult<ValidationOutcome>> AuthorAsync(deltas, ct);
    Task<SessionResult<EditReport>>        EditAsync(Action<WorldState> edit, ct);
}
```

Six mutating operations, one door, one guard. The race disappears because `UpdateStateAsync`
swaps `_world` **inside the same guard a turn holds** — a reload can no longer land mid-turn.

**Refuse rather than queue**, with precedent already in the codebase: `RerollOutcome.Refused(reason)`
existed for "you asked for something that is not valid right now." A busy session returns *"a turn
is in progress"* rather than throwing, or silently queueing a click the player has forgotten
making.

**One refusal concept.** `SessionResult<T>` carries a value or a reason. Reroll's own refusals and
"there are no turns yet" — previously a `RerollOutcome` field and a caller-side history check —
both fold into it, so a caller has one kind of no rather than three.

**Two operations lost their arguments.** `ReExtractLastAsync` and `RerollLastAsync` take no turn:
*the last turn* is a session concept, and both clients were loading history themselves to find
it. It is now read inside the guard, because "the last turn" is only stable while nothing else
can append one.

`PlaySession` dropped to rendering and prompting and owns nothing. The window gets the same six
methods plus an `IsBusy` to bind a spinner to.

### The open questions, answered when it was built

1. **Does the session own the save lock? — Yes.** *"This save is mine for now"* and *"I hold
   canon for this save"* are one lifetime, and splitting them was avoidance rather than design.
   `SaveLock` stays in Storage; the session takes an acquired `IDisposable`, so Core gains the
   ownership without learning that the mechanism is a file. The console keeps its own `using` as
   well, because pack and prompt loading sit between acquiring the lock and constructing the
   session and both throw on malformed content — `SaveLock.Dispose` is documented idempotent, so
   the second call is a no-op rather than a bug.
2. **May a client mutate `World` directly? — Yes, and it is labelled rather than prevented.**
   Reads go through `World`; writes go through `AuthorAsync` or `EditAsync`. That is convention,
   not types. An immutable projection contradicts `WorldState`'s "mutable by design" rationale
   and is its own decision.
3. **One session per process, or several? — Several.** The two `static` fields in `PlaySession`
   are gone, which is what had made multiple saves per pack awkward.

### What the guard does not do

Found by the test written to prove it worked, which failed on its last assertion — and the
assertion was wrong, not the code.

**The guard stops canon being half-updated. It does not preserve an external edit made while a
turn is running.** The turn saves the session's canon at the end and overwrites the file the edit
was in, so the edit is gone before any later update can read it — the same consequence as editing
without asking for an update at all.

Still better than what it replaced, where the update appeared to succeed and was then silently
discarded. But it is a limit rather than a fix, it is asserted in the self-test so it stays
known, and closing it properly means the turn noticing the file changed underneath it, which is
the file-watching family §5 and §3 both reject.

---

## 5. What is allowed to change canon — **decided**

> **Deltas are the way canon changes.** Direct authoring exists for what the delta set cannot
> express, carries a warning, and is the player's call.

Decided by the player, 2026-09-04. It is not a symmetric two-path design: one path is the norm
and the other is a labelled escape hatch.

In practice it goes window by window. A *New Character* window emits the same deltas `/character`
does. Fixing the wording of a description goes through authoring, because no delta says that.

### Why an escape hatch is needed at all

The delta set is **17 kinds**, and it describes *things that happen in a story*. Authoring is not
a story event, so the set has holes exactly where an editor needs them:

| you want to | delta for it? |
|---|---|
| fix a location's description | **none** — `LocationIntroduced` only creates |
| fix a character's description | only by also renaming them (`CharacterRenamed` carries it) |
| fix a fact's wording | **none** — `FactEstablished` only creates |
| delete a character added by mistake | **none** |
| make someone *un*-know something | **none** |

Not an oversight. "I typed the description wrong" is not an event in the world.

### The hatch is also the instrument that measures its own replacement

Every reach for direct editing is **evidence about a missing delta.** If descriptions are always
hand-edited, the answer is not a better warning — it is that description editing should have been
a delta. Same shape as *playing is how features get chosen*, pointed at the delta set.

### The warning must not become wallpaper

A warning on every edit is clicked through by the third one, and then it is worse than nothing
because the player has trained themselves to dismiss it. Open: once per session, first use per
window, or only on the genuinely dangerous edits.

The truthful warning is also narrower than "this can break your session." Editing a description
corrupts nothing. **The risk is concentrated in ids and references** — change an id and every
reference to it orphans; delete a character and items are held by nobody. That is precisely what
`CanonRefresh.Check` reports, and a warning naming the real risk gets read.

### Proposed: a second tier of delta kinds — **not decided**

The closed delta set exists **for the model's benefit, not the applier's**. §3's rationale is
entirely about a cheap model writing `character.mood.current` and having it land as a silent
no-op. That argument does not apply to a window with a text box.

So a delta kind could exist that the model never sees: present in `DeltaApplier` and
`DeltaValidator`, absent from `DeltaSchema`. `CharacterDescriptionChanged`,
`LocationDescriptionChanged`, `FactRewritten`, `CharacterRemoved`. Authoring emits them,
extraction cannot, and **the cost in model attention is zero** — which is the thing the closed set
actually protects.

That would move most of the "editing wording" case back onto the delta path — validated, applied
and persisted the ordinary way — leaving the raw hatch genuinely exotic.

**It needs one guard:** extraction must reject a kind absent from the schema, so an off-schema
kind cannot arrive from a provider that ignores the structured-output constraint. Cheap, and
worth having regardless.

Two tiers — *kinds that exist* and *kinds the model may propose* — sounds like it weakens the
closed-set decision. It does not: the closure was always about what the model is offered.

---

## 6. What this leaves open

- **Session lifecycle.** `RunAsync` is ~160 lines and mostly decisions, not rendering: take the
  lock, load pack and prompts, resume vs. fresh, does the pack author the player, write the first
  save, write `save.json`, compare pack versions, opening scene vs. recent turns. The window needs
  every one. This is the next gap and it is bigger than the authoring one was.
- Whether the two-tier delta set happens, and which kinds.
- When the authoring warning appears.
- Everything in §4's open questions.

**Nothing here is built.** The decision in §5 is real; §4 is endorsed in principle and unspecified
in detail.
