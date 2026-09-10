# Player discovery — extraction prompt v6–v8 and three code fixes — 2026-09-10

Follows `2026-09-10_player-discovery-implementation.md`, which recorded the v5 results.
That entry attributed the regression-suite drop to model limitations. It was a prompt
regression introduced by the discovery work, and this entry corrects it.

## The regression, and its cause

The v5 discovery prompt rewrote the opening paragraph of `prompts/extraction.md`,
replacing the rule that made the player's own actions authoritative:

> ~~"The player's input is authoritative … If the player did something the narration does
> not restate — handing over an object, moving somewhere, revealing information — it
> still happened and must be reported."~~

with a single restraint covering everything the input contains:

> "Use input as action context, not proof that a requested outcome happened."

That is right for discovery — a player asserting they found something must not create
knowledge — and wrong for movement, which the pre-existing suite depends on. The
pre-existing regression suite fell from its 30/30 baseline to 26/30: `player-arrival`
reported the waypoint and stopped, 4/6; `two-stage-entry` lost the final hop 1/3; and
`deflection` established a fact 1/3, the discovery section's "first establish an
attributed fact" competing with the standing rule that a deflection reveals nothing.

## Prompt versions

Each version was scored on both suites, `deepseek/deepseek-v3.2` via StreamLake, three
runs per scenario, so the numbers are comparable with the earlier entry's.

| prompt | regression req | regression clean | discovery req | discovery clean | discovery forbidden |
|---|---|---|---|---|---|
| baseline `330d29db` | 100% | 30/30 | — | — | — |
| v5 `dd3cd7b1` | 93% | 26/30 | 98% | 71/78 | 0.04 |
| v6 `ee23b532` | 100% | 30/30 | 98% | 70/78 | 0.05 |
| v7 `61e094c1` | 100% | 30/30 | 97% | 67/78 | 0.08 |
| **v8 `c157d908`** | 98% / 100% | 29/30, 28/30 | 98% | 70/78 | 0.05 |

**v6** split the opening rule in two: what the player DID is authoritative, including
every stage of a journey and the destination reached; what the player LEARNED or FOUND
is not. It also scoped the fact rules so a deflection yields neither fact nor reported
aspect. This restored the regression suite completely.

**v7** added two corrections. The movement rule's negative case — a player who did not
change location gets no `player_moved`, and naming the room they already stand in is not
a move — fixed an invented move v6 had introduced in `discovery-stale-memory`, 1/3 to
0/3, and is kept. The second, a general encouragement that "what the protagonist plainly
sees is always worth recording, even when it is mundane", fixed `discovery-closed-container`
(4/6 to 6/6) and broke three other scenarios: the model attached a speaker's visible
appearance to observations of them in 2/3 briefing runs, and padded descriptions until
the evidence check rejected whole observations, costing `discovery-unnamed` its alias
3/3. Attributed by mechanism from the raw proposals in the JSON reports, not by score.

**v8** keeps the movement fix and replaces the general encouragement with a narrow one:
a closed, sealed or empty container still has a visible exterior, record the outside and
nothing of the inside. `discovery-closed-container` holds at 6/6, `discovery-found`
reaches 12/12 (its best across every version), and the v7 collateral damage is gone.

## Measurement limits

Two runs of the identical regression suite against v8 scored 29/30 and 28/30 with
different scenarios failing each time — `two-stage-entry` in one, `hostility` in the
other. At three runs per scenario the noise floor is roughly plus or minus two clean
runs even at temperature 0, so single-scenario deltas of one or two runs carry no
information about a prompt change. Only the findings above, which reproduced across
runs and have an identifiable mechanism in the raw proposals, are attributed. Future
prompt comparisons should use more runs. Recorded in CHALLENGES.

Still unfixed and unattributable to any prompt version: `discovery-unnamed` fabricates
evidence in 3/3 runs, rejected by the validator every time so nothing reaches the world;
`discovery-lore-topic` misses topic-learning at a rate the harness cannot pin down
between 6/9 and 8/9. Both are model limitations, and this time that is measured rather
than assumed.

## Code fixes

Three defects found reviewing the uncommitted discovery implementation.

`DiscoveryEngine.SafeName` resolved the player's label for an entity by recency across
both provenances, so a later report overwrote an earlier direct sighting. Design says
the opposite: preserve the latest sighting separately from the latest report and present
both, rather than letting a rumour overwrite a witnessed encounter. An NPC lying about a
name on a later turn silently relabelled someone the player had already seen. Both
values were always stored; only the label resolution was wrong, and it now prefers the
sighting, a report filling in when the name was never directly witnessed.

`EntityReferences.ResolveForPlayer` tried each kind's discovery memories in turn for an
id that carries no kind, letting a character and a location sharing an id borrow each
other's disclosed label. It now settles the kind against canon first, in the same
precedence `Resolve` uses. It also routed `{{player}}` through the memories and rendered
the protagonist as "not yet identified" to themselves; the player's own name is not a
discovery and is now returned directly.

`ContextAssembler` compared an item's `LocationId` to the current location with `==`
while the holder test on the same line used `OrdinalIgnoreCase`, as does the rest of the
codebase. An item whose stored id differed only in case dropped out of the narrator's
presentation rules, so its discovery rule never reached the prompt.

The discovery suite was re-run after these fixes, since the evals assert on `SafeName`:
98% required, 68/78 clean, forbidden 0.06. `discovery-false-report` and
`discovery-identification` — the two scenarios that would catch a label-resolution
change — are 12/12 and 9/9. The difference from the prompt-only v8 measurement (70/78)
is two clean runs in mixed directions, inside the noise floor recorded above, and no
scenario failed in a way traceable to the fixes. This is the state of the committed code.

## Validation

`dotnet build StoryWeaver.sln -c Release`: zero warnings, zero errors.
`git diff --check`: no whitespace errors. No automated UI or unit tests exist; manual
desktop acceptance remains outstanding, with steps in the Desktop README.
