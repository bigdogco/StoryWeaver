# 2026-08-16 — The first thing you read

Second piece of Phase 1. Designed 2026-07-23 in `WORLD_PACKS.md` §4, never built, and
rediscovered because a new pack made the gap impossible to ignore.

## What argued for it

`the-last-lantern` — a 1940 detective world with five characters, three lore entries and a
scenario about a missing husband and a black ledger. A new player saw this, and nothing else:

```
A narrow private detective's office above the shuttered Orpheum cinema. Venetian blinds, a
scarred desk, a filing cabinet, and a window looking down on a blacked-out street. Rain ticks
against the glass.
```

The raw location description from the seed, printed between two dashed lines. That was the
entire opening path.

**Vivian Vale and Eddie Mercer are both standing in that office in the seed**, and neither is
mentioned. It is past midnight, a woman has walked in with a job, and the player is shown
furniture.

The sharp part is what shipped yesterday. The *scenario* is in the narrator's system prompt from
turn one, so the model knows about Vivian and the ledger. **The player was the only participant
who did not.** They had to type into a scene they could not see, and the narrator would answer
as though they knew.

Now:

> Rain ticks against the blinds, and below the window the street is a black trench — no lamps,
> no headlights, nothing but the wet shine of pavement where a gap in somebody's curtain lets
> the light out.
>
> The woman in the rain-darkened green coat sits across the desk and has not touched the
> chair's back since she arrived. She gave a name at the door, Vivian Vale, and she has been
> turning a silver cigarette case over in her gloved hands for a full minute without opening
> it. Whatever she came to say, she has not said it yet.

Every person named is in the seed. The cigarette case is a real item, held by Vivian in canon,
alongside the ledger.

## The decision the design had left open

§4.3 asked whether the opening is turn zero. **No.**

It becomes the oldest beat in the narration window, and never enters `history.jsonl`.

The argument that settles it: **the opening is content, and history is what happened.** Writing
it as a turn record would bake today's prose into every existing save, so editing `opening.md`
between sessions would leave the old text in old worlds — the pack/save split broken in the
easiest place to break it.

It also gets the lifetime right for free. The opening slides out of the window after ten turns
like any other prose, while the scenario persists in the system prompt. That is exactly the
distinction `SCENARIOS.md` was built around yesterday, and the two features now demonstrate it
rather than assert it.

Implementation falls straight out: a `StoryBeat` with an empty player input, and `LlmNarrator`
emits no user message for one. Both halves are pinned by a self-test — the narrator sees it on
turn one, and one turn still produces exactly one history record.

## Measuring a heuristic before keeping it

The design asked for a warning when an opening names somebody who does not exist — *"a militia
officer waits by the fire"* puts an officer in the story and nobody in canon, and then the
narrator keeps referring to them.

That check cannot be exact. Natural language does not surrender its entity list on demand: "a
heavyset man" is Hald and matches nothing, while `Venetian`, `October` and `Orpheum` are
capitalised and are nobody. So the risk is noise, and **a warning nobody trusts is a warning
nobody reads.**

Built it, then measured it before deciding to keep it:

- **Silent on all three real packs**, including an opening written in period detective prose.
- **Fires correctly** on a deliberately inserted `Captain Roy Halloran waits by the door`.

Restricted to multi-word capitalised runs, which is what keeps the false-positive rate at zero
on real content. If it ever starts crying wolf it should come out.

Worth noting as a pattern: this is the first time a heuristic in this project has been *tested
for noise before being trusted*, rather than shipped and later found annoying.

## Also gone

```csharp
here ??= world.Locations.GetValueOrDefault("marrow-tavern");
```

A pack-specific id sitting in the general startup path since bootstrap. Harmless only because
every pack happens to seat its player, and silently wrong for any world that did not contain a
tavern in a marsh.

## Measurements

`dotnet build` clean, 0 warnings. 76 self-tests pass.

Scored set, StreamLake n=5: **50/50 clean, forbidden 0.00, rejects 0.00.** Extraction untouched,
which is what the run was for.

## Carried forward

The opening for `the-last-lantern` is a draft written by me, and it is the author's to rewrite.
It is the single most important paragraph in the experience and it belongs to whoever owns the
world's voice.

Phase 1 has two pack pieces left — `world.json` and prompt overrides — plus narration eval,
which is still the part with no answer.
