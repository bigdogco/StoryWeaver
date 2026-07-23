# Devlog — a character can finally be renamed

**Date:** 2026-07-23
**Scope:** `character_renamed`, `/rename`, and world size cleared of a crime it did not commit

---

## The gap

§9 found it: a character introduced anonymously kept that name forever. Nessa was named in
the prose on turn 15 and was still `"Shivering figure"` in canon at turn 51. The narration
read correctly the whole time, because the extractor had stored her real name as a *fact* —
which is why nothing looked wrong until canon was audited on disk.

Anonymous-stranger-becomes-named-person is not an edge case. It is one of the most common
moves in the medium, and the closed delta set could not express it.

## The decision: ids are opaque, names are mutable

The id does not change. `figure-in-cistern` stays her id for the life of the world.

This costs nothing, and the codebase already said so. `Entity.Name` is `{ get; set; }`,
documented *"May change; the id may not"* — the domain model was built for this from the
start and only the delta set never caught up. `ContextAssembler.ForNarration` sends names
only, so the narrator never sees a stale id; `ForExtraction` sends
`Nessa (id: figure-in-cistern)`, which is exactly right — the current name to match against
the prose, the stable id to emit deltas against.

That split exists because the very first play session had the narrator write `marrow-tavern`
into the prose. The fix for that old bug is what makes stale ids safe now.

Rewriting the id instead would mean rewriting every reference in `Knows`, in history, and in
anything lore adds later — to buy readability in a file the player is not meant to read.

## The change

`CharacterRenamed(CharacterId, Name, Description?)`. Description is nullable because a reveal
usually revises both — "a shivering figure in rags" is no longer who she is once she has a
name — but a bare name reveal must not be forced to invent one.

The validator rejects a rename of a character that does not exist and a blank name, and
treats it as a no-op only when *nothing* would change, description included. There is
deliberately **no uniqueness check on the name**: two guards may both be "Guard", and
identity lives in the id regardless.

The prompt rule names the workaround explicitly, because the model invented it unprompted:

> ...do not record the name as a fact. A name is not a world truth, it is who somebody is.

`/rename` joins `/place`, `/character` and `/fact`. It lists the cast, shows the id, and
states that the id is staying — clearer than hiding a field that cannot be edited.

## Results

`name-reveal`: a hooded drinker in the tavern gives her name. Scored on the **outcome**
(`StateRule`) rather than the delta sequence, since a bare rename and a rename-plus-revised-
description both reach the right world.

```
name-reveal (small)              21/21 required, 0 forbidden, clean 7/7
name-reveal-large (pinned)       18/18 required, 0 forbidden, clean 6/6
full scored set, 9 scenarios     100% required, forbidden 0.00, rejects 0.00
```

The extractor emitted `character_renamed hooded-drinker -> Sera Voight` on **every single
run** across every configuration, always against the existing id. No new baseline compromise:
the scored set moves from 98% across 8 scenarios to **100% across 9**.

## Two scoring lessons, one of them a repeat

**I wrote the two-stage-entry bug again.** The first version of the forbidden rule flagged any
fact mentioning "Sera", and fired 5/7 on this:

```
fact_established  sera-knows-player: Sera Voight knows who the player is.
```

That is a legitimate fact, taken straight from the prose — it merely refers to her by name.
The rule was supposed to catch a fact whose *content is the naming*, and instead caught every
sentence the right answer appears in. Narrowed to match the assertion (`is named`, `is
called`, an id containing `name`) it drops to 0/7 on the same runs.

The devlog for `two-stage-entry` recorded this exact lesson eight days ago and it did not
prevent the repeat. Writing it down is evidently not sufficient; the check that would have
caught it is reading the *deltas* on a failing rule before believing the rule.

**World size was innocent — again.** Routed, the large-world variant scored 5/7 forbidden
against 0/7 for the small one, which looks exactly like the world-size effect that
`two-stage-entry-large` really does have. It is not:

| | forbidden |
|---|---|
| large, routed (Baidu ×4, StreamLake ×2, SiliconFlow ×1) | 5/7 |
| large, pinned to DeepInfra | **0/7** |
| large, pinned to Baidu | 4/7 |

The small-world run had gone entirely to DeepInfra. **Provider and world size were confounded
by the routing**, and the difference is entirely the provider. This is the third time a
conclusion about our own code has been within reach of a routing artefact, and the only
reason it did not land is that the by-provider table is printed on every run.

Worth being precise about Baidu's failure: it emits the rename **correctly and additionally**
files the name as a fact. Required is 21/21 there too. That is redundancy, not corruption —
categorically milder than AtlasCloud emitting a building as a `character_introduced` — so it
is recorded rather than added to `providerIgnore`. It reads as a provider weighting a
prohibition in the prompt more weakly, not one reasoning badly.

## Also seen

Two calls today failed with schema-shaped nonsense:

```
Delta has no 'kind' property. Properties present: type, id, name.
Delta has no 'kind' property. Properties present: type, id, content.
```

Both came from the `(unreported)` provider — the one that returns no provider name. A
provider that ignores `response_format` and a provider that omits its own identity appear to
be the same provider, which is a useful correlation: the runs we cannot attribute are the
runs most likely to be junk. Caught loudly by `StateDeltaConverter` rather than silently
deserialized, which is the payoff for throwing on an unknown shape.

## Next

Nessa is still `"Shivering figure"` in the existing save — `/rename` fixes it in place, and
that is the first thing to do on resuming. Then the remaining §9 queue: fact truth value and
attribution, location identity, and the `character_moved`-on-player seam.
