# 2026-09-04 — Opening a session stops being the console's business

Closes the last open item in `design/CANON_OWNERSHIP.md`. `StorySession` owned canon once it
existed; **getting one** was still 160 lines of the console's private business, of which only
four were rendering.

## A new project, because nothing could host it

Opening needs Storage (the lock, the pack, the repository, the save origin), Llm (prompts, the
provider client, narrator, extractor) and Core. `Core` references nothing, and Llm and Storage
are siblings that cannot see each other.

**There was no project that could hold the sequence** — which is precisely why it never left the
console. `StoryWeaver.App` is the composition layer: it knows how to assemble a playable session
out of the three libraries, renders nothing, and asks nothing.

The dangerous item it now owns is the engine wiring. Get one argument wrong in a second client
and the pack silently loses its voice, which reads as the model being worse rather than as a
dropped parameter.

## Two phases, because one step is not a sequence

Twelve steps are pure. One is not: a pack shipping no `player.md` has to ask who you are.

Rather than take a callback — which inverts control and makes a window block inside a load —
opening stops and returns a `PendingPlayer` holding everything already loaded, with the save lock
already held. The client asks however suits it and completes; completing costs one write, not a
second open.

The lock being held across the question is the point, and it is also the obligation: an abandoned
question has to give the save back, so `PendingPlayer` is `IDisposable`.

## The seed fallback is gone

```csharp
WorldState world = loaded ?? pack.Seed ?? WorldSeeds.Marrow();
```

Every pack ships a seed, and `WorldSeeds` is otherwise used **only by eval scenarios** —
instrumentation, the category the design says never migrates inward. It was the one thing tying
session-opening to a CLI fixture. A pack with no seed is now refused at open rather than silently
starting somebody in Marrow, and the banner line that hedged about it is gone too.

## Two bugs, and the second is the interesting one

**`ashfall` hung forever on closed stdin.** Character creation looped on a blank name with no way
to give up, spinning the prompt while holding the save lock. Logged during the lock work in
August and never fixed, because the loop lived in the console and looked like console business.
Two-phase makes giving up ordinary: end of input returns null, dispose hands the save back.

I nearly shipped a half-fix. The first version printed *"the question was abandoned, the save was
given back"* and **never disposed the pending state** — the comment was true about the design and
false about the code, and the run left a stale lock behind. Caught by looking at the directory
instead of at the message.

**`SaveLock` could not see a second session in the same process.** It carried an exemption:

> *Our own id is not a conflict. A session that somehow re-acquires its own lock is taking back
> something it already owns.*

Sound when one process meant one playthrough. **`StorySession` ended that** — sessions are
objects now and several can exist at once, which is exactly why the statics were removed — so one
process opening the same save twice became real, and the file cannot tell it apart from a session
re-acquiring its own lock. Both look like our own process id. In-process holders are tracked in
memory now, where the answer is exact; `--force` still breaks it, with its own check so the new
set cannot become a lock nothing can break.

**The pattern worth keeping:** enabling several sessions per process silently invalidated an
assumption written down in another file. The comment stating that assumption is what made it
findable — and it was found by a test that could not have existed before the feature that broke
it.

## Measurements

`dotnet build` clean, 0 warnings. Self-tests **123 pass, 0 fail** — up from 116, with eight new
ones on the opener. They touch a real temporary filesystem, because most of what opening does
*is* filesystem behaviour: a fake repository would test none of it. No API calls: opening
constructs the provider client and never uses it, which is what keeps the suite free.

By hand: `ashfall` asked, was answered, persisted, and resumed without asking; `marrow` announced
its authored player and did not ask; one real turn; a clean exit with the lock released. One API
call pair.
