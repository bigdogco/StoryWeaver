# 2026-08-16 — Closing Phase 1, and stopping

Phase 1 is closed. The last item is deferred, deliberately, and the reason is worth more than
the item was.

## What shipped

Four pieces, all finishing the pack design written 2026-07-23 and left unbuilt:

| | |
|---|---|
| `scenario.md` | what the story is about — standing context in every prompt, never seen by extraction |
| `opening.md` | the first thing the player reads — a rendering of the seed, and not a turn |
| `world.json` + `save.json` | a pack has a name and a version; a save records what it began against |
| `prompts/*.md` | every engine prompt out of code, and a pack may bring its own voice |

The result that outlived its own feature: **scenario and opening are separated by lifetime, not
content.** An opening renders the seed and slides out of the narration window after ten turns; a
scenario is standing context forever. A premise written only into an opening works beautifully
for ten turns and is then forgotten — which is roughly where both long runs began to drift.

## What did not ship, and why that is the entry worth keeping

Phase 1's definition of done asked whether the prose is better, with evidence. **It is recorded
as unanswered rather than rewritten to match what was built.**

Two reasons. The audit in `design/NARRATION_EVAL.md` had already run every mechanically
checkable property over 51 turns and found everything passing — a rules-based eval would score
100% on day one. And the semantic half needs a judge model, which is a second model's unaudited
variance grading a first model's, in a project that misattributed provider noise to its own code
four times.

Then the player stopped the work, and was right:

> we are starting to go into a mode where we make features and not the game

**A narration eval is itself a feature built in a vacuum.** Nobody has complained about the
prose. The audit found nothing wrong. Building measurement for a problem no session has produced
is the same mistake as building features for one — one level up, and harder to see because
measurement feels virtuous.

Two rules went into `PROJECT.md` §3 as a result:

> **Build for observed failures, never for completeness.** A design doc listing six components
> is not six reasons to build.

> **Playing is how features get chosen.** When the queue and the play sessions disagree, the
> sessions win.

The second is not a principle, it is an observation about this repository. Every finding worth
having came from a long run — the world with no exits, two engines on one save, a place
introduced twice under two ids, the story with no direction. **None came from the backlog.**

## The lore check: measured, then dropped

Worth recording because the investigation was cheap and the finding survives the feature.

The audit had proposed a deterministic check: does a character reference a lore topic they have
not heard of? Match entry `keys` against quoted speech, scoped to speakers lacking the entry.

Run by hand across all eleven saves before writing anything, it does not work:

- **`keys` are retrieval keys** — broad by design, so an entry fires when relevant. Detection
  wants the opposite. Real hits included *"take the tube down"* (`tube`) and *"couriers don't
  stop to ask"* (`couriers`); a key of `blind` would fire on the Venetian blinds in a noir
  opening.
- **Attributing speech to a speaker is not a string match.**
- **The one clean hit was not a narrator bug at all.** Hald saying *"take Shurus from us"* is
  correct — he is a cult member — but canon never recorded him knowing the cult, because
  `Knows` tracks what a character *learned in play*, never what they always knew.

That last point is the durable finding: **a seed can under-declare what its characters know, and
nothing notices.** If this is ever revisited it is a content check, not a narration check.

An `--audit <save>` command was proposed the same day and declined under the same rule. The case
for it was real — seven throwaway analysis scripts in three days, all gone, and detection
recipes sitting in `CHALLENGES.md` that nothing implements. The case against won: tooling is not
game. It is in `TODO_FUTURE_WORK.md` with a trigger — revisit when analysing a save by hand
becomes the bottleneck for the third time.

## Where the project stands

The base is right and the CLI is not the problem. A 230-turn session stays coherent, canon does
not degrade with distance, extraction scores 50/50, and a world can now say what it is about,
open properly, and sound like itself.

**Phase 2 — the Avalonia UI — is next, and its framing has changed.** It was scheduled because
the pack format needed settling first. It is now the thing standing between here and the play
sessions that pick every feature after it: the CLI works, and it is not pleasant to play or to
author. Under the rules above, that makes it the only sensible next phase.

## Build

`dotnet build` clean, 0 warnings. 85 self-tests pass. Scored set 50/50 at the last measurement,
prompts `06f1e586`.
