# TODO: Narration memory (recent-turn window)

**Status:** DONE (pending manual play verification by user)
**Created:** 2026-07-21

---

## The problem

The narrator has **no memory of any previous turn** — not on resume, not mid-session. The
whole message list sent for narration was:

```
System(SystemPrompt)
User("World state:\n\n{context}\n\nThe player: {playerInput}")
```

Its only memory was canon: moods, statuses, locations, who knows what. So an NPC's exact
words last turn were gone, a thread of dialogue could not continue, and the room was
re-described from scratch every turn.

Found while diagnosing "resume shows nothing" after the §6 storage work. Persistence was
working correctly (6 turns, `turnNumber: 6`); resuming just made an *always-present* gap
visible. It was never recorded as a deliberate decision anywhere in the docs — an unexamined
gap, not a choice.

## The design

**Canon = long-term structured memory** (prevents 50-turn drift).
**Recent window = short-term prose memory** (keeps the immediate scene coherent).
That combination is what makes long-form work; either alone is not enough.

### History goes in as real alternating messages, not a text blob

```
System(SystemPrompt)
User(beat 1 player input)        \
Assistant(beat 1 narration)       |  the window, oldest first
...                              /
User("World state: {context}\n\nThe player: {input}")   <- current turn only
```

Two reasons this beats pasting a transcript into one user message:

1. It is the shape a chat model was trained on for multi-turn dialogue.
2. **Prompt caching.** The volatile part (world state, which changes every turn) sits in the
   *last* message, so the system prompt plus the whole history is a stable prefix. A
   transcript blob would invalidate the prefix every single turn. The prefix only breaks when
   the window slides, not on every turn.

Note the replayed user messages carry the player's **raw input only** — not the world-state
block they originally shipped with. Stale state in history would compete with current state.

### Extraction gets nothing

Deliberate and firm. Extraction is at 100% on `--eval`, and feeding it prior turns invites it
to re-extract old events as new deltas. Narration only. Per our own rule, no extraction change
without re-running the eval.

### Window size is configuration, not a constant

`story.historyTurns`, default **10 turns (= 20 messages)**. A *turn* is a player input plus
its narration, so the message count is double the turn count — worth being explicit about,
since it is a 2x difference in cost. Tunable without a rebuild so it can be judged by feel;
belongs with the prompt-externalization work as world-author data eventually.

Rough cost: ~200-300 tokens per remembered turn against the narration role.

## Tasks

- [x] `StoryBeat(PlayerInput, Narration)` in Core — the narrator-facing view of a past turn,
      carrying no extraction detail.
- [x] `INarrator.NarrateAsync` takes `IReadOnlyList<StoryBeat> recent`.
- [x] `LlmNarrator` builds the alternating message list described above.
- [x] `TurnEngine` loads recent history from the repository and maps it to beats.
- [x] `StorySettings.HistoryTurns` (`story.historyTurns`), default 10; wired into
      `PlaySession` and added to `settings.example.json`.
- [x] `PlaySession` prints the last turns on resume so the *player* has context too, not just
      the model. Replaces the opening scene on resume rather than following it.
- [x] `dotnet build` clean.
- [x] **Offline check** (throwaway, scratchpad): 15 assertions — message ordering, history
      replayed as raw input with no world-state leak, state only in the final message, tail
      slicing oldest-first, window larger than history, and `0` reverting to the old
      two-message call. All pass.
- [ ] Manual verify by user: resume a world, confirm the narrator continues the scene rather
      than restarting it. Tune `historyTurns` by feel.

## Known cost, accepted for now

`TurnEngine` reads the whole `history.jsonl` each turn to take the last N — O(n) per turn.
Fine at bootstrap scale (50 turns). Logged in TODO_FUTURE_WORK rather than optimized now; the
fix when it stops being fine is a `LoadRecentTurnsAsync(worldId, count)` on the repository, or
holding the window in memory across a session. (Originally written as "move the turn log to
SQLite" — that plan was dropped 2026-08-13; storage stays JSON.)
