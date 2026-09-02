# 2026-08-16 — A pack with a name

Third piece of Phase 1, and the smallest. Designed in `WORLD_PACKS.md` §2 and §6 on
2026-07-23, never built.

Before: a pack's entire identity was its folder name, and the banner printed a file path.

```
Pack       worlds\the-last-lantern
```

After:

```
Pack       The Last Lantern v1.0 by Pavel
           worlds\the-last-lantern
```

## Honest about what this is for

**Nothing today was blocked on it**, and the task doc says so at the top rather than dressing
it up. A display name and an author matter once something lists worlds for a player to pick
from, which is Phase 2. It was built because the pack design listed it and because it is small.

The version is the one field doing present work, and only in combination with a second piece:
without somewhere to record it, a version number is decorative. So `save.json` came along with
it — pack id, pack version, and when the playthrough began.

That serves §6's rule, *content may move; state degrades quietly and loudly*. An author editing
a world while somebody has a save in progress is normal rather than exceptional. Verified end
to end by bumping `the-last-lantern` to 1.1 and resuming:

```
  note  this save was started against The Last Lantern v1.0; the pack is now v1.1.
        content may have moved. Anything the pack no longer defines stays in your
        world; nothing is removed.
```

## Three decisions worth their reasoning

**A manifest whose id disagrees with its folder is refused.** The folder *is* the id by locked
decision, so the manifest only restates it — and a mismatch means somebody copied a pack and
renamed the directory without touching the file, leaving a world that answers to two names.
That is exactly the confusion opaque permanent ids exist to prevent, so it fails the load rather
than being quietly ignored.

**`save.json` is written once and never rewritten.** It records where a playthrough came *from*,
which is a fact about the past. Refreshing it on resume would quietly erase the only evidence
that the pack has moved since — the record would always agree with the pack, and the warning
could never fire. Pinned by a self-test that writes twice and expects the first value.

**Version is a free-form string.** Nothing compares versions for ordering; a save records what
it started on and a later session reports when it differs, which needs equality and nothing
more. Imposing semantic versioning would be a rule with no reader.

## The one thing that stays open

A mismatch **reports**; it does not act. Dropping references to content a pack no longer
defines is §6's real compatibility work and wants its own measurement — it is now in
`TODO_FUTURE_WORK.md` with the note that `save.json` finally carries the information needed to
make such a warning specific.

## Measurements

`dotnet build` clean, 0 warnings. 79 self-tests pass, three of them new.

**No scored set, deliberately.** Nothing here touches a prompt or the delta path. Recorded in
the task doc so the omission reads as a decision rather than something forgotten — this project
has a standing rule to re-run before trusting an extraction change, and the counterpart rule is
knowing when a change cannot possibly be one.
