# Devlog — the body and the feeling

**Date:** 2026-07-24
**Scope:** `status_changed` never firing, and an asymmetry in the prompt that caused it

---

## The failure

`blow-landed`, written the same day to reproduce a real play failure: Hald is driven into his
own counter, struck above the ear with the flat of a blade, and goes down on one knee bleeding
through his fingers.

```
mood_changed  innkeeper-hald = injured
```

**0/7.** `status_changed` never fired. "Injured" went into the *mood* field.

The §9 audit had seen the edge of this without naming it — `guard-tomas` ended the session with
mood `startled` and status `terrified`, `drinker-mabb` with mood `terrified` and status `drunk`.
Terror was living in both fields on different characters and it read as untidiness rather than
a defect.

## The cause was an asymmetry, not a missing rule

The schema is not vague. `status_changed` is documented as *"Physical or situational condition
changed: wounded, asleep, imprisoned. **Not for emotions.**"* That is about as clear as a field
description gets.

The problem is what sits either side of it. Counting mentions in the extraction system prompt:

| | mentions |
|---|---|
| mood | 3 |
| status | **0** |

And the mood schema branch does not merely define itself, it *recruits*: "Emit this whenever
the prose shows a shift in how a character feels, however brief." The system prompt says the
same thing again — "these are easy to miss and matter."

So one field was defined and the other was campaigned for. The model reached for the one it had
been told to reach for. **Nothing in the prompt was wrong; the imbalance was.**

Worth keeping: a rule added to fix one problem (moods being missed, which was real) quietly
created another by being the only voice in the room. The fix is not to weaken it but to give
the other field an equal one.

## The fix

```
Status is the body, mood is the feeling, and they are different deltas. Wounded, bleeding,
unconscious, bound, poisoned, drunk, dead — all status_changed. "Injured" is not a mood. When
someone is physically harmed, restrained, or incapacitated you must emit status_changed; add
mood_changed as well only if how they FEEL also changed. A character beaten senseless whose
status still reads "normal" is recorded as unhurt, and everything downstream will treat them
as unhurt.
```

The last sentence is doing deliberate work. Every rule in this prompt that has held up explains
the *consequence in canon* rather than just stating the rule — the speaker-learns rule works
because it says a character who states a secret will contradict themselves later.

## Results

```
blow-landed, routed        0/7  ->  7/7 required, forbidden 0.00
blow-landed, DeepInfra     0/7  ->  7/7   (same provider as the baseline)
full scored set, 9         99% required, forbidden 0.00, rejects 0.00
```

The single miss on the full sweep is the known intermittent `revelation` speaker-learns, which
has been 0–2/7 on every sweep since it was found. No regression.

The output is now what the model was reaching for all along, correctly split:

```
status_changed  innkeeper-hald = injured
mood_changed    innkeeper-hald = stunned
mood_changed    drinker-mabb  = terrified
```

Both fields, each with the right kind of thing in it, plus a bystander's reaction the scenario
never asked about.

## The provider control, applied before drawing a conclusion

The first measurement of the fix ran on StreamLake; the 0/7 baseline had run on DeepInfra.
That is exactly the confound that has produced four wrong conclusions this year, and
`CHALLENGES.md` now states the rule as *"no single routed sweep is evidence about a change
unless the provider mix is held fixed."*

So the fix was re-measured pinned to **DeepInfra, the baseline's own provider: 7/7.**

This is the first time that rule has been followed proactively rather than discovered after the
fact, and it cost one extra eval run.

## Next

Unchanged from the measurement pass, minus this one:

1. ~~`status` vs `mood`~~ — done
2. **`Item`** — 7/7 measured failure, an object with nowhere to live becomes a character
3. **`source` on facts** — three unprompted sightings of the model wanting it
4. **`character_described` / `location_described`** — 3 of 11, correctly sized
