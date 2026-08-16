# 2026-08-15 — What the story is about

First piece of Phase 1. A pack could describe a world; nothing could describe a **story in
it**. Now `scenario.md` can.

Design: `design/SCENARIOS.md`. Task: `todo/TODO_SCENARIOS.md`.

## The gap, and how narrow it turned out to be

Two long model-played runs ended the same way, and the player said why in its own inputs:

```
t91   *I keep going until we hit another new location.*
t226  *I follow the next obvious sign if the story offers one.*
```

53% of all deltas were movement; the world grew as 31 new locations against 2 new characters.
Nothing told the player what it was there to do, so the only legible objective was *see more*.

The useful part of the design was working out how **small** the answer is. A scenario file
downloaded from another site — a Fallout-inspired one, with a structured `rules` block — maps
almost entirely onto things this project already has:

| their field | home here |
|---|---|
| `rules.world` | lore |
| `rules.characters` | character sheet |
| `rules.player` | `player.md` |
| `rules.narrative` | narration prompt override (Phase 1) |
| `description`, `tags` | `world.json` manifest (Phase 1) |
| `rules.goals` | **nothing** |

Five of six already have somewhere to live. Those formats bundle everything into one blob
because a character card has no entity store; here, only the central conflict is homeless, and
that is all a scenario needs to be.

The same test on two long "scenarios" from those sites — a fantasy world with guilds and
half-breeds, a modern setting with policing and class — finds both are almost entirely
*setting*, which is lore. **Long scenarios are mostly lore wearing a scenario's name.**

## Scenario and opening are separated by lifetime, not content

`WORLD_PACKS.md` already settled that an opening is a rendering of the seed. The scenario sits
beside it:

| | lifetime |
|---|---|
| **opening** | read once; gone from the narration window after ~10 turns |
| **scenario** | in every prompt, forever |

**This is why a premise cannot just live in the opening**, and it is the mistake anyone would
make first. The failure is already on record as the Astaria case: *say Astaria on turn 3 and
the narrator references it correctly for ten turns — because it is sitting in the message
window, not because anything recorded it.* A premise written only into an opening works
beautifully for ten turns and then the story quietly forgets what it is about. Which is roughly
where both long runs began to drift.

## Two decisions worth their reasoning

**Extraction never sees it.** Direct precedent: the narration window was withheld from
extraction because *"feeding it prior turns invites it to re-extract old events as new
deltas."* Hand the extractor "a child has gone missing and you were sent to investigate" and it
has every reason to emit that premise as a `fact_established` on turn one, and again whenever
the prose brushes near it. The fact store already absorbs everything the delta set cannot
express.

Not asserted in a comment — a self-test runs a real turn through `TurnEngine` with recording
fakes and checks the string is absent from the extraction context.

**It rides in the system message, not the world-state block.** Both are prompt text, but the
narration-memory design puts volatile state in the *last* message so everything above it is a
stable cacheable prefix. A scenario is identical for the life of the save; putting it below
would break the prefix every turn to resend the same paragraph.

## Two bugs, both mine

**`{{player}}` reached the narrator unresolved.** The loader checks that every `{{ }}` *points
at something*; nothing turned it into words. So the narrator was handed a literal
`{{player}}` in every prompt — the token-in-the-prose failure that the narration/extraction
split exists to prevent, arriving through a door nobody had closed.

Caught by eyeballing `/prose`. **That is the second time in a week that command has found
something no count would have.** It was built to check that ids do not leak into narration, and
it keeps being the instrument that shows what the model is actually handed. Fixed by resolving
per turn in `TurnEngine` rather than at load — a character renamed on turn 40 has to read
correctly on turn 41 — and covered by a test now.

**A test whose sentinel collided with the world under test.** The first version of the
"extraction never sees it" check searched the extraction context for the word *marsh*. Marrow's
seed contains it already: Mabb is an old marsh-hand. It failed, and for a moment looked like a
real leak. Sentinels are now strings that cannot occur naturally.

## Measurements

`dotnet build` clean, 0 warnings. 74 self-tests pass.

Scored set, pinned to StreamLake, n=5: **50/50 clean, forbidden 0.00, rejects 0.00.**
Extraction untouched, which was the point of running it — "extraction is unaffected" is exactly
the kind of claim that has been wrong here before.

`two-stage-entry` scored 10/10 against 9/10 yesterday. **Not an improvement from this change.**
Nothing in the extraction path moved, and that scenario has swung 8/10–10/10 historically; it
landed at the top of its own range. Recorded because reading it as a win is the error the
per-provider table exists to prevent.

## What this does not settle

**Whether a scenario makes the story better.** It is standing text in a prompt; whether the
narrator holds a story to it across 200 turns is a *narration* question, and narration still
has no automated quality control.

The honest proxies, computable from any save with no API calls, both extreme in the goalless
run: **movement share of all deltas (53%)** and **new locations to new characters (31 : 2)**.
Neither measures quality. They measure *aimlessness*, which is the specific thing a scenario is
for.

And the next session should be **human-played**. Every session so far has been model-played,
and a model with no goal is precisely the failure a scenario is meant to fix — which makes a
model the worst available judge of whether it did.
