# Devlog — a topic nobody names

**Date:** 2026-08-04
**Scope:** the lore eval was measuring a shape play does not produce

---

## The false pass

`lore-learned` scored 14/14. A 51-turn session in which the Cult of the Blind was the entire
plot taught it to **nobody**.

The eval has Hald say *"There's an old faith out in the fen — the Blind, folk call them"* — the
topic named and taught. Play never does that. Characters spoke of the Drowned Father, the
weeping woman, the capstone, a century of drowned men. Every recognisable feature of the topic,
and never once its label, because everyone in the scene already knew what they were discussing.

A scenario written to provoke a behaviour will provoke it, and that says nothing about whether
the behaviour occurs.

## Why it could not have worked

The extractor is shown lore as:

```
  - (cult-of-the-blind) The Cult of the Blind
```

Id and title, nothing else. Bodies are withheld on purpose — several paragraphs of reference
prose invite exactly the invention the extraction prompt spends most of its length suppressing.

But the entry's `keys` were withheld too, and keys are precisely the *"what does this topic
sound like"* signal: `cult, shurus, drowned father, weeping woman, medallion`. Without them,
"the Drowned Father took his tithe" and `cult-of-the-blind` are unrelated strings. **The model
was not failing to make the connection; it had no evidence the connection existed.**

## The reproduction

`lore-learned-implicit` — the same subject matter, the words "cult" and "the Blind" appearing
nowhere. Hald explains the capstone, the weeping woman, and the hundred years of tithe.

**0/14.** Every run produced the same thing real play did: five facts paraphrasing the entry's
own content, and no link to it.

```
fact_established  water-owed:    The water under Marrow Square is owed a tithe to the Drowned Father.
fact_established  tithe-payment: For a hundred years, the tithe was paid with men who went into the fen...
fact_established  capstone-purpose: The capstone was the lid sealing the well and the tithe.
```

The lore entry already says all of that. It was being re-derived from scratch every time,
because as far as the extractor could tell it was new.

## The fix, in two halves

**Keys in the extraction context.** Short authored strings, unlike a body:

```
  - (cult-of-the-blind) The Cult of the Blind — also: cult, shurus, drowned father
```

**0/14 → 8/14.** Real movement and not enough. The evidence was now present; nothing told the
model what to do with it.

**A prompt rule naming the situation.**

> A scene is usually about a topic without ever naming it. People speak of the thing itself —
> its god, its sign, its practices, what it is owed — and almost never say its title out loud,
> because everyone present already knows what they are discussing... If someone is being told
> the substance of a listed topic, they have learned that topic, whether or not its name was
> spoken.

**8/14 → 14/14.**

Worth separating those two results. The keys alone were not sufficient, and the rule alone
would have been useless — it would have told the model to match on substance it could not see.
Neither half is the fix; the pair is.

## Results

```
lore-learned-implicit   0/14  ->  14/14   (StreamLake)
                                  12/12   (Baidu, second provider)
lore-learned                      10/10   unchanged
lore-not-established              forbidden 0.00, unchanged
full scored set, 9                100% required, forbidden 0.00, rejects 0.00
```

Verified on two independent providers before being believed, per the rule in `CHALLENGES.md`.
The intended provider control was DeepInfra — the baseline's own — but it rate-limited every
run, so Baidu stood in. Second-best, and stated rather than glossed.

## What generalises

- **A green score is a place to look, not a reason to stop looking.** This is the first
  measured case of a scenario passing on a shape real play never takes. The habit worth keeping
  is to ask of anything scoring full marks: *does play actually look like this?*
- **The eval was written from the same intuition as the feature.** Both assumed lore gets
  taught by being named, because that is how you would explain it to somebody who had never
  heard of it — which is exactly what characters inside the world never need to do. A scenario
  written by the same person who wrote the feature inherits its blind spots. Play is the only
  thing that does not.
- **Withholding information has a cost that is invisible from inside.** Keys were not
  deliberately excluded; bodies were, and keys were simply never considered. The reasoning for
  the exclusion was sound and its scope was never checked.

## Next

The redundant facts remain: even at 14/14 the model still establishes some content the entry
already covers. That is the §9 fact-store pressure, unchanged, and the last piece of the lore
feature that does not work.
