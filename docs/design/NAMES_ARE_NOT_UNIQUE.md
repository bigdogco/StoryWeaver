# Design — names are not unique, and the narrator only sees names

**Status:** finding, no design committed. Written 2026-08-14 from a 230-turn `marrow` run.

A tension that is invisible in a small world and unavoidable in a large one. Not a bug in
anything: two correct decisions meet and produce an unusable prompt.

---

## The two decisions

**Ids are unique and permanent; names are mutable.** Locked in bootstrap. A rename must not
orphan a reference, so identity lives in the id and the name is free to change — and free,
therefore, to be the same as another name.

**The narrator is never shown ids.** `ContextAssembler.ForNarration` strips them, and
`ForExtraction` keeps them. That split exists because a model handed an id will eventually
write one into the prose; it was added after exactly that happened, and `/prose` exists so a
human can eyeball the narrator's view and confirm no id leaked.

Both are right. Together they mean **the narrator's entire view of the world is a set of
strings that are not guaranteed to be distinct.**

## What it looks like at 230 turns

Real context, verbatim:

```
Leads to: boardwalk shrine, dilapidated stilt-hut, drowned chapel, rotting footbridge,
          ruined tollhouse, shrine yard, skiff cabin, dilapidated stilt-hut, storage wall
          passage, submerged slipway, sunken reliquary, tollhouse landing

Carrying: waterlogged leather map, ..., heavy iron key, ..., rusted iron skeleton key, ...,
          stamped lead token, ..., corroded iron key, ..., stamped lead token, ...,
          tarnished copper key, ..., barnacle-encrusted iron key, ..., stamped lead token, ...
```

Thirty-one carried items including **six different keys and four lead tokens**. An exits list
naming the same destination twice.

**Canon is correct.** `chapel-lead-token`, `crypt-stairs-lead-token` and `slipway-lead-token`
are three genuinely different tokens found in three different places — precisely the case the
deduplication audit said must stay separate, where 7 of 10 high-similarity pairs were
contradictions, before-and-afters, or identifications rather than duplicates.

The extractor did its job. The store is right. The *rendering* is unusable: the narrator
cannot write about an object it cannot distinguish from three others, and cannot describe an
exit that appears twice.

## Why this is the interesting failure

Every previous problem in this project has been canon being **wrong**. This is canon being
**right and unnarratable** — the first failure that lives entirely in the rendering layer, and
the first that gets worse purely as a function of length.

It also explains a felt quality problem that no delta count would have shown. The 230-turn run
scored well on every mechanical measure — flat rejection rate, no drift, stable narration
length — and still read badly.

## Directions, none chosen

- **Disambiguate on render.** When two entities in the same view share a name, qualify them
  from something canon already holds — where it was found, who gave it. Cheap, and the
  qualifier has to come from somewhere; description text is the obvious source and is not
  written for this.
- **Give the narrator a stable handle that is not an id.** A short label distinct from the
  name. Trades one leak risk for another.
- **Do not render what does not matter.** Thirty-one carried items are in the prompt because
  nothing ever leaves inventory. Most are irrelevant to the scene. This is really a
  relevance/budget problem wearing a naming costume, and it points at the same retrieval work
  the lorebook layer needs.
- **Let names be unique per type.** Rejected on sight: it would forbid two guards both called
  "a guard", which is a real thing a story does.

The third is the most promising and the least about names.

## What this is not

**Not deduplication.** Merging those tokens would be wrong; they are different objects.
Measured and declined already.

**Not the duplicate-place bug.** A single place introduced twice under two ids is canon being
wrong, and is recorded separately in `CHALLENGES.md`. It shows up in the same exits list, which
is why the two are easy to confuse.

## Where it sits

Phase 2 touches it — a UI showing 31 items has the same problem and better tools. The
relevance angle belongs with the lorebook retrieval layer. Nothing to build until the story
layer exists, because a scenario with a point would also make "which of these matters" answerable.
