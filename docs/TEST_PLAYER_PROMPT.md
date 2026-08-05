# The test-player prompt

For driving a long session with an LLM when a human play session is not available. Paste the
block below into ChatGPT (or similar), then feed it each turn's narration and send back its
reply.

**Read [`2026-08-04_llm-played-session.md`](devlog/2026-08-04_llm-played-session.md) first.** A
model-played session is a *coverage* tool, not a *discovery* tool: it tests what it was told to
do, where a human finds failures nobody thought to look for. The first unprompted run played
fifty cautious turns and never went down the well — 2 moves, 1 status change, 0 malformed
inputs, 42% of turns changing nothing.

This prompt exists to push it toward the areas that run missed. That makes it better coverage
and does **not** make it a substitute for playing.

---

## The prompt

```
You are playing a text RPG to stress-test it. Your goal is not to win or to tell a
good story — it is to exercise as much of the game's machinery as possible over
about 50 turns.

Format: write actions between *asterisks* and speech outside them.
  *I lean on the counter.* What do you know about the well?

Keep each turn short — one or two sentences. Long, careful, elaborate turns are
exactly what a real player does not write.

ACT MORE THAN YOU TALK. A previous test run spent fifty turns asking questions and
never went anywhere. Specifically, across the session:

- MOVE somewhere new at least every 4-5 turns. Go through doors. Leave the
  building. If a place is mentioned, go to it rather than asking about it.
- PICK THINGS UP, put them down, hand them to people, break them, use them on
  other things. Look closely at objects you are carrying.
- GET INTO TROUBLE. Start at least two fights or physical confrontations. Take
  damage. Break something. Threaten someone and follow through.
- CHANGE YOUR MIND. Be friendly to someone then turn on them, or the reverse.
- ASK TWO DIFFERENT PEOPLE THE SAME QUESTION, so they can contradict each other.
- DO SOMETHING STUPID at least twice — an obviously bad idea, carried out.

BE SLOPPY ON PURPOSE. Roughly one turn in ten, write the way a tired person
types: a typo or two, a missing asterisk, no capital letter, a sentence that
trails off. Do not clean it up. Example: "8I say to hald, wat about teh well"

Do not narrate outcomes. Write only what your character says and does; the game
decides what happens. Never write what another character says.

If asked a direct question, answer it in character and then do something.
```

---

## What to bring back

The save directory, and any turns that felt wrong at the time. The audit reads canon; it cannot
see a turn that read badly but recorded cleanly.

## What this still will not test

- **Genuine surprise.** Everything above is a list of things already known to be worth testing.
  A human doing something nobody anticipated is how the multi-stage movement bug, the
  two-object merge and the reasoning leak were all found.
- **Taste.** Whether the prose is any good, whether an NPC felt consistent, whether the story
  went somewhere interesting. Only a person reading it knows that, and it is half the product.

## Improving it

Treat this file as versioned like a prompt in the code. If a run comes back and a whole
category is still untouched, the fix is a line here rather than a note to remember next time.
