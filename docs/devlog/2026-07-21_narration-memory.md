# Devlog — the narrator had no memory

**Date:** 2026-07-21
**Scope:** recent-turn window for narration, found while verifying §6 persistence

---

## How it surfaced

Reported symptom: played six turns, `/quit`, `--play` again, "it just showed first message".

Checked the disk first rather than guessing. `saves/marrow/canon.json` had `turnNumber: 6`,
six lines in `history.jsonl`, player still in `marrow-tavern`. **Persistence was working
exactly as designed** — the session had resumed, and the tavern description appeared because
that is where the character was standing.

So not a storage bug. But looking at why the resumed session felt empty turned up something
much worse than a missing transcript.

## The finding

The entire message list sent for narration was:

```
System(SystemPrompt)
User("World state:\n\n{context}\n\nThe player: {playerInput}")
```

**No prior narration, ever.** Not on resume — on *every turn*. Turn 6 was written by a model
that had never seen a word of the story so far. Its only memory was canon: moods, statuses,
locations, who knows what.

Grepping the docs found no record of this as a deliberate decision. It was an unexamined gap,
not a choice — the extreme reading of "canon is the source of truth". True that canon should
be the *long-term* memory, but it cannot hold what an NPC actually just said, the thread of a
conversation in progress, or what has already been described. Canon knew Hald was `guarded`;
it did not know he had just said something specific and was waiting for an answer.

Resuming did not cause this. It only made an always-present gap visible, which is the useful
thing a resume feature did on its first day.

## The fix

Canon = long-term structured memory (prevents 50-turn drift). A recent window = short-term
prose memory (keeps the immediate scene coherent). Neither alone is enough.

Narration now receives the last N turns, replayed as **real alternating user/assistant
messages** rather than a transcript pasted into one blob:

```
System(prompt)
User(beat 1 player input)  /  Assistant(beat 1 narration)
...
User("World state: {context}\n\nThe player: {input}")   <- current turn only
```

Two reasons for the message form over a blob:

1. It is the shape a chat model was trained on for multi-turn dialogue.
2. **Prompt caching.** The volatile part — world state, which changes every turn — sits in the
   *last* message, so the system prompt plus the entire history is a stable prefix. A blob
   would invalidate the prefix every single turn; this only breaks it when the window slides.

The replayed user messages carry the player's **raw input only**, deliberately not the
world-state block they originally shipped with — stale state sitting in the history would
compete with the current state below it.

`StoryBeat(PlayerInput, Narration)` is a new Core type rather than reusing `TurnRecord`: the
narrator has no business seeing deltas, rejections, or raw extraction output, and handing it
the whole record invites exactly the bookkeeping-while-storytelling the two-model split exists
to prevent.

## Extraction gets nothing, deliberately

Narration only. Extraction scores 100% on `--eval` reading a single turn, and showing it
earlier turns invites it to re-extract events that are already canon as though they were new.
Per our own rule, no extraction change without re-running the eval — so this change does not
touch it at all.

## Window size is configuration

`story.historyTurns`, default **10 turns**. Worth being explicit that **a turn is two
messages** (input + narration), so the message count and the token cost are double the turn
number — roughly 200-300 tokens per remembered turn against the narration role. It is a taste
call, tunable without a rebuild, and belongs with the prompt-externalization work as
world-author data eventually. `0` restores the old canon-only behaviour exactly.

## Verification

Offline again, no credits spent — 15 assertions against a capturing fake client and a fake
narrator (scratchpad, not committed):

- ordering: system first, beats as alternating user/assistant pairs, current turn last
- history replayed as *raw* player input, with no world-state block leaking into it
- world state and current input present only in the final message
- window is the tail, oldest first; a window larger than the history returns all of it
- `historyTurns: 0` produces exactly the old two-message call

Build clean, 0 warnings.

## Also

`PlaySession` now replays the same window to the *player* on resume, so the person and the
model do not have different ideas about what just happened. It replaces the opening scene on
resume rather than following it — a static room description after the story tail reads as a
reset.

Logged in TODO_FUTURE_WORK: the window re-reads the whole turn log every turn (O(n) against a
file that only grows). Fine now, and the honest fix is a `LoadRecentTurnsAsync(worldId, count)`
on the repository, which is also the one that survives the eventual SQLite move.

## Next

Manual play to tune `historyTurns` by feel, then §9 — the ~50-turn session. Running that
before this fix would have produced a frustrating session and bad evidence: we would have been
recording incoherence caused by a missing feature rather than learning anything about the
architecture.
